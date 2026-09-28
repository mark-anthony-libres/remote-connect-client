param(
    [Parameter(Mandatory)]
    [string]$VmName,

    [PSCredential]$Credential,

    [string]$DestinationPath
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$clientDir = Split-Path $scriptDir -Parent

if (-not $DestinationPath) {
    $DestinationPath = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "RemoteConnect-Diagnostics"
}

if (-not (Get-Command Get-VM -ErrorAction SilentlyContinue)) {
    throw "Get-VM isn't available. This needs the Hyper-V PowerShell module (Hyper-V must be installed/enabled) and an elevated (Administrator) PowerShell session."
}

try {
    $vm = Get-VM -Name $VmName -ErrorAction Stop
}
catch [Microsoft.HyperV.PowerShell.VirtualizationException] {
    throw "Couldn't query Hyper-V VMs ($($_.Exception.Message)) - re-run this script from an elevated (Administrator) PowerShell session."
}
if (-not $vm) {
    throw "No VM named '$VmName' found. Check the exact name with Get-VM or Hyper-V Manager."
}
if ($vm.State -ne "Running") {
    throw "VM '$VmName' is not running (state: $($vm.State)) - PowerShell Direct needs it running."
}

if (-not $Credential) {
    $Credential = Get-Credential -Message "Local account inside VM '$VmName' (used to read its logs back)"
}

New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null

$appDataLogsPath = Join-Path $env:APPDATA "RemoteConnect\logs"
$deviceIdentityPath = Join-Path $env:APPDATA "RemoteConnect\device.json"

if (-not (Test-Path $deviceIdentityPath)) {
    throw "No install_key found at $deviceIdentityPath - this computer has never connected as a device yet."
}
$installKey = (Get-Content $deviceIdentityPath -Raw | ConvertFrom-Json).install_key
if (-not $installKey) {
    throw "$deviceIdentityPath exists but has no install_key field."
}

$envFilePath = Join-Path $clientDir "RemoteDesktopClient\.env"
if (-not (Test-Path $envFilePath)) {
    throw "Missing $envFilePath - can't determine DEVICE_WS_URL to reach the backend."
}
$deviceWsUrl = $null
foreach ($rawLine in Get-Content $envFilePath) {
    $line = $rawLine.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
    $separatorIndex = $line.IndexOf('=')
    if ($separatorIndex -le 0) { continue }
    $key = $line.Substring(0, $separatorIndex).Trim()
    if ($key -eq "DEVICE_WS_URL") {
        $deviceWsUrl = $line.Substring($separatorIndex + 1).Trim().Trim('"')
        break
    }
}
if (-not $deviceWsUrl) {
    throw "DEVICE_WS_URL not found in $envFilePath."
}

$wsUri = [Uri]$deviceWsUrl
$wsSuffix = "/ws/device"
$apiPrefixPath = if ($wsUri.AbsolutePath.EndsWith($wsSuffix)) { $wsUri.AbsolutePath.Substring(0, $wsUri.AbsolutePath.Length - $wsSuffix.Length) } else { $wsUri.AbsolutePath }
$scheme = if ($wsUri.Scheme -eq "wss") { "https" } else { "http" }
$restBase = "${scheme}://$($wsUri.Authority)$($apiPrefixPath.TrimEnd('/'))"

Write-Output "Asking $restBase/devices/latest-connection-request for this computer's latest request..."
$response = Invoke-RestMethod -Uri "$restBase/devices/latest-connection-request" -Headers @{ "X-Install-Key" = $installKey }
$requestId = $response.request_id
if (-not $requestId) {
    throw "This computer has no connection requests yet (as requester) - nothing to collect for the remote-session logs. Run a session first."
}
Write-Output "Latest request_id: $requestId"

if (Test-Path $DestinationPath) {
    Get-ChildItem -Path $DestinationPath -File | Remove-Item -Force
}

function Copy-LatestClientLogLocal {
    param([string]$LogsPath, [string]$DestinationFilePrefix)

    $latest = Get-ChildItem -Path $LogsPath -Filter "client-*-*.log" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $latest) {
        Write-Warning "No client-*.log files found in $LogsPath - skipping."
        return
    }
    $destination = Join-Path $DestinationPath "$DestinationFilePrefix-$($latest.Name)"
    Copy-Item -Path $latest.FullName -Destination $destination -Force
    Write-Output "Copied $($latest.FullName) -> $destination"
}

function Copy-RemoteSessionLogLocal {
    param([string]$LogsPath, [string]$RequestId, [string]$DestinationFilePrefix)

    $sourcePath = Join-Path $LogsPath "remote-sessions\$RequestId.txt"
    if (-not (Test-Path $sourcePath)) {
        Write-Warning "No remote-session log at $sourcePath - skipping (this session may never have reached ACTIVE here)."
        return
    }
    $destination = Join-Path $DestinationPath "$DestinationFilePrefix-$RequestId.log"
    Copy-Item -Path $sourcePath -Destination $destination -Force
    Write-Output "Copied $sourcePath -> $destination"
}

Copy-LatestClientLogLocal -LogsPath $appDataLogsPath -DestinationFilePrefix "requester"
Copy-RemoteSessionLogLocal -LogsPath $appDataLogsPath -RequestId $requestId -DestinationFilePrefix "requester"

Write-Output "STOP: checking for a running RemoteConnect.exe on '$VmName' (so its client log file isn't still held open)..."
try {
    Invoke-Command -VMName $VmName -Credential $Credential -ScriptBlock {
        $ErrorActionPreference = "Stop"

        $logsPath = Join-Path $env:APPDATA "RemoteConnect\logs"
        $latestClientLog = Get-ChildItem -Path $logsPath -Filter "client-*-*.log" -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1

        $proc = Get-Process -Name "RemoteConnect" -ErrorAction SilentlyContinue
        if (-not $proc) {
            Write-Output "  No running RemoteConnect.exe found."
        }
        else {
            $originalIds = @($proc.Id)
            Write-Output "  Found RemoteConnect.exe running as PID(s): $($originalIds -join ', ')"

            $maxAttempts = 3
            $waitPerAttemptSeconds = 30
            for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
                $proc | Stop-Process -Force -ErrorAction SilentlyContinue
                Write-Output "  Stop-Process -Force issued for PID(s): $($originalIds -join ', ') (attempt $attempt of $maxAttempts)"
                $proc | Wait-Process -Timeout $waitPerAttemptSeconds -ErrorAction SilentlyContinue
                $proc = Get-Process -Id $originalIds -ErrorAction SilentlyContinue
                if (-not $proc) {
                    Write-Output "  Confirmed PID(s) $($originalIds -join ', ') are gone after attempt $attempt."
                    break
                }
            }
        }

        if ($latestClientLog) {
            try {
                ([System.IO.File]::Open($latestClientLog.FullName, 'Open', 'ReadWrite', 'None')).Close()
                Write-Output "  $($latestClientLog.Name) is not locked - proceeding."
            }
            catch {
                Write-Warning "  $($latestClientLog.Name) is still locked: $($_.Exception.Message)"
            }
        }
    }
}
catch {
    Write-Warning "STOP failed on '$VmName' - continuing anyway, but the client log read below may fail if the app is still running: $($_.Exception.Message)"
}
Write-Output "STOP: done."

Write-Output "Reading the VM's latest client log over PowerShell Direct..."
try {
    $vmClientLog = Invoke-Command -VMName $VmName -Credential $Credential -ScriptBlock {
        $logsPath = Join-Path $env:APPDATA "RemoteConnect\logs"
        $latest = Get-ChildItem -Path $logsPath -Filter "client-*-*.log" -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $latest) { return $null }
        $bytes = $null
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try { $bytes = [System.IO.File]::ReadAllBytes($latest.FullName); break }
            catch [System.IO.IOException] {
                if ($attempt -eq 5) { throw }
                Start-Sleep -Milliseconds 300
            }
        }
        [PSCustomObject]@{
            Name  = $latest.Name
            Bytes = $bytes
        }
    }
    if ($vmClientLog) {
        $destination = Join-Path $DestinationPath "target-$($vmClientLog.Name)"
        [System.IO.File]::WriteAllBytes($destination, $vmClientLog.Bytes)
        Write-Output "Copied '$VmName':$($vmClientLog.Name) -> $destination"
    }
    else {
        Write-Warning "No client-*.log files found on '$VmName' - skipping."
    }
}
catch {
    Write-Warning "Failed to read the VM's client log: $($_.Exception.Message)"
}

Write-Output "Reading the VM's remote-session log for $requestId over PowerShell Direct..."
try {
    $vmSessionLogBytes = Invoke-Command -VMName $VmName -Credential $Credential -ScriptBlock {
        param($RequestId)
        $sourcePath = Join-Path $env:APPDATA "RemoteConnect\logs\remote-sessions\$RequestId.txt"
        if (-not (Test-Path $sourcePath)) { return $null }
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try { return [System.IO.File]::ReadAllBytes($sourcePath) }
            catch [System.IO.IOException] {
                if ($attempt -eq 5) { throw }
                Start-Sleep -Milliseconds 300
            }
        }
    } -ArgumentList $requestId
    if ($vmSessionLogBytes) {
        $destination = Join-Path $DestinationPath "target-$requestId.log"
        [System.IO.File]::WriteAllBytes($destination, $vmSessionLogBytes)
        Write-Output "Copied '$VmName':logs\remote-sessions\$requestId.txt -> $destination"
    }
    else {
        Write-Warning "No remote-session log for $requestId on '$VmName' - skipping (this session may never have reached ACTIVE there)."
    }
}
catch {
    Write-Warning "Failed to read the VM's remote-session log: $($_.Exception.Message)"
}

Write-Output "Done. Collected logs are in $DestinationPath."
