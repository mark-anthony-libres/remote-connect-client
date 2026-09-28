param(
    [Parameter(Mandatory)]
    [string]$VmName,

    [string]$RuntimeIdentifier = "win-x64",

    [string]$DestinationPath = "C:\RemoteConnect",

    [PSCredential]$Credential
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command Copy-VMFile -ErrorAction SilentlyContinue)) {
    throw "Copy-VMFile isn't available. This needs the Hyper-V PowerShell module (Hyper-V must be installed/enabled) and an elevated (Administrator) PowerShell session."
}

$scriptDir = $PSScriptRoot
$clientDir = Split-Path $scriptDir -Parent
$sourcePath = Join-Path $clientDir "RemoteDesktopClient\bin\Release\net10.0\$RuntimeIdentifier\publish"

if (-not (Test-Path $sourcePath)) {
    throw "Missing $sourcePath. Run ..\release.ps1 -RuntimeIdentifier $RuntimeIdentifier first to produce it."
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
    throw "VM '$VmName' is not running (state: $($vm.State)). Start it before deploying - every stage below needs it running."
}

if (-not $Credential) {
    $Credential = Get-Credential -Message "Local account inside VM '$VmName' (used to stop/start the app there)"
}

$exeName = "RemoteConnect"
$exePathInVm = "$DestinationPath\$exeName.exe"

Write-Output "STOP: checking for a running $exeName.exe on '$VmName'..."
try {
    Invoke-Command -VMName $VmName -Credential $Credential -ScriptBlock {
        param($ProcessName, $ExePath)
        $ErrorActionPreference = "Stop"

        $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
        if (-not $proc) {
            Write-Output "  No running $ProcessName.exe found."
            return
        }
        $originalIds = @($proc.Id)
        Write-Output "  Found $ProcessName.exe running as PID(s): $($originalIds -join ', ')"

        $maxAttempts = 3
        $waitPerAttemptSeconds = 30
        for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
            $proc | Stop-Process -Force -ErrorAction SilentlyContinue
            Write-Output "  Stop-Process -Force issued for PID(s): $($originalIds -join ', ') (attempt $attempt of $maxAttempts)"

            $proc | Wait-Process -Timeout $waitPerAttemptSeconds -ErrorAction SilentlyContinue

            $proc = Get-Process -Id $originalIds -ErrorAction SilentlyContinue
            if (-not $proc) {
                Write-Output "  Confirmed PID(s) $($originalIds -join ', ') are gone after attempt $attempt."
                return
            }
        }

        $totalWaitSeconds = $maxAttempts * $waitPerAttemptSeconds
        Write-Output "  Get-Process still lists PID(s) $($originalIds -join ', ') ${totalWaitSeconds}s later across $maxAttempts attempts; checking whether $ExePath is actually still locked..."
        if (Test-Path $ExePath) {
            try {
                ([System.IO.File]::Open($ExePath, 'Open', 'ReadWrite', 'None')).Close()
            }
            catch {
                throw "$ProcessName.exe (PID(s) $($originalIds -join ', ')) is still running and $ExePath is still locked: $($_.Exception.Message)"
            }
        }
        Write-Output "  $ExePath is not locked - proceeding (a lingering process-table entry, not an actual block)."
    } -ArgumentList $exeName, $exePathInVm
}
catch {
    throw "STOP failed - leaving '$VmName' untouched (no clear, no deploy): $($_.Exception.Message)"
}
Write-Output "STOP: done (was running and was stopped, or was not running)."

Write-Output "CLEAR: removing old contents of '$VmName':$DestinationPath ..."
try {
    Invoke-Command -VMName $VmName -Credential $Credential -ScriptBlock {
        param($Path)
        $ErrorActionPreference = "Stop"
        if (Test-Path $Path) {
            Remove-Item -Path $Path -Recurse -Force
        }
        New-Item -Path $Path -ItemType Directory -Force | Out-Null
    } -ArgumentList $DestinationPath
}
catch {
    throw "CLEAR failed - not deploying: $($_.Exception.Message)"
}
Write-Output "CLEAR: done."

$files = Get-ChildItem -Path $sourcePath -File -Recurse
Write-Output "DEPLOY: copying $($files.Count) files from $sourcePath"
Write-Output "        to '$VmName':$DestinationPath ..."

$copied = 0
try {
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($sourcePath.Length).TrimStart('\')
        $destinationFilePath = "$DestinationPath\$relativePath"
        Copy-VMFile $VmName -SourcePath $file.FullName -DestinationPath $destinationFilePath -FileSource Host -CreateFullPath -Force
        $copied++
        Write-Progress -Activity "Copying to VM '$VmName'" -Status "$copied / $($files.Count): $relativePath" -PercentComplete (($copied / $files.Count) * 100)
    }
}
catch {
    Write-Progress -Activity "Copying to VM '$VmName'" -Completed
    throw "DEPLOY failed after copying $copied of $($files.Count) files: $($_.Exception.Message)"
}
Write-Progress -Activity "Copying to VM '$VmName'" -Completed
Write-Output "DEPLOY: done ($copied files)."
Write-Output "Not starting the app (see script header) - launch $exePathInVm yourself from an interactive session on '$VmName' (RDP or the VM console)."
