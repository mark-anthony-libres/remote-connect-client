param(
    [string]$SourcePath,

    [string]$DestinationPath = (Join-Path $env:LOCALAPPDATA "Programs\RemoteConnect"),

    [switch]$Force
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$clientDir = Split-Path $scriptDir -Parent
$exeName = "RemoteConnect"

if (-not $SourcePath) {
    $SourcePath = Join-Path $clientDir "RemoteDesktopClient\bin\Release\net10.0\win-x64\publish"
}

$minValidExeBytes = 50MB
function Test-PortableBuild {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Label)

    if (-not (Test-Path $Path)) {
        Write-Host "  $Label ($Path) does not exist."
        return $false
    }
    $exePath = Join-Path $Path "$exeName.exe"
    $exeFile = Get-Item -Path $exePath -ErrorAction SilentlyContinue
    if (-not $exeFile) {
        Write-Host "  $Label is missing $exeName.exe."
        return $false
    }
    if ($exeFile.Length -lt $minValidExeBytes) {
        Write-Host "  $Label's $exeName.exe is only $([math]::Round($exeFile.Length / 1MB, 1)) MB - too small to be a real self-contained build (expected 200+ MB with FFmpeg bundled in)."
        return $false
    }
    Write-Host "  $Label OK: $exeName.exe present ($([math]::Round($exeFile.Length / 1MB, 1)) MB)."
    return $true
}

Write-Output "VALIDATE: checking source build at $SourcePath ..."
if (-not (Test-PortableBuild -Path $SourcePath -Label "Source")) {
    throw "Source build at $SourcePath is not valid - nothing was touched. Run ..\publish.ps1 first."
}

if (-not (Test-Path $DestinationPath)) {
    Write-Output "INSTALL: $DestinationPath does not exist yet - performing a fresh install."
    New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
    Copy-Item -Path (Join-Path $SourcePath "*") -Destination $DestinationPath -Recurse -Force
    Write-Output "INSTALL: done. Launch $DestinationPath\$exeName.exe yourself when ready - this script does not start it."
    return
}

$destExePath = Join-Path $DestinationPath "$exeName.exe"
if (-not (Test-Path $destExePath)) {
    throw "$DestinationPath exists but does not look like a RemoteConnect installation (no $exeName.exe there) - not touching it. Pick an empty or different -DestinationPath."
}

Write-Output "STOP: checking for a running $exeName.exe at $destExePath ..."
$targetProc = Get-Process -Name $exeName -ErrorAction SilentlyContinue | Where-Object {
    try { $_.MainModule.FileName -eq $destExePath } catch { $false }
}

if ($targetProc) {
    Write-Output "  Found $exeName.exe running as PID $($targetProc.Id) at the install path."
    Write-Output "  Requesting graceful shutdown (CloseMainWindow)..."
    $null = $targetProc.CloseMainWindow()
    Wait-Process -Id $targetProc.Id -Timeout 15 -ErrorAction SilentlyContinue
    $targetProc = Get-Process -Id $targetProc.Id -ErrorAction SilentlyContinue

    if ($targetProc) {
        Write-Output "  $exeName.exe (PID $($targetProc.Id)) did not exit within 15s of a graceful close request."
        if (-not $Force) {
            $answer = Read-Host "  Force-terminate it? This may interrupt an active remote session. Type 'yes' to continue"
            if ($answer -ne "yes") {
                throw "STOP aborted by user - leaving the existing installation untouched (nothing was replaced)."
            }
        }

        $maxAttempts = 3
        $waitPerAttemptSeconds = 10
        for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
            $targetProc | Stop-Process -Force -ErrorAction SilentlyContinue
            Write-Output "  Stop-Process -Force issued (attempt $attempt of $maxAttempts)..."
            Wait-Process -Id $targetProc.Id -Timeout $waitPerAttemptSeconds -ErrorAction SilentlyContinue
            $targetProc = Get-Process -Id $targetProc.Id -ErrorAction SilentlyContinue
            if (-not $targetProc) {
                Write-Output "  Confirmed process is gone after attempt $attempt."
                break
            }
        }
    }
}
else {
    Write-Output "  No running $exeName.exe found at the install path."
}

try {
    ([System.IO.File]::Open($destExePath, 'Open', 'ReadWrite', 'None')).Close()
}
catch {
    throw "STOP failed - $destExePath is still locked, not replacing anything: $($_.Exception.Message)"
}
Write-Output "STOP: done ($destExePath is not locked)."

$backupPath = "$DestinationPath.old"
Write-Output "BACKUP: preserving current install at $backupPath ..."
if (Test-Path $backupPath) {
    Remove-Item -Path $backupPath -Recurse -Force
}
Rename-Item -Path $DestinationPath -NewName (Split-Path $backupPath -Leaf)

try {
    Write-Output "REPLACE: copying new build into $DestinationPath ..."
    New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
    Copy-Item -Path (Join-Path $SourcePath "*") -Destination $DestinationPath -Recurse -Force

    Write-Output "VERIFY: checking the replaced install..."
    if (-not (Test-PortableBuild -Path $DestinationPath -Label "Replaced install")) {
        throw "Replaced install at $DestinationPath failed verification after copying."
    }
}
catch {
    Write-Output "REPLACE/VERIFY failed - rolling back to the previous install: $($_.Exception.Message)"
    if (Test-Path $DestinationPath) {
        Remove-Item -Path $DestinationPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    Rename-Item -Path $backupPath -NewName (Split-Path $DestinationPath -Leaf)
    throw "Deployment failed and was rolled back - $DestinationPath is unchanged from before this run. $($_.Exception.Message)"
}

Write-Output "DEPLOY: done. $DestinationPath now has the new build."
Write-Output "The previous install is kept at $backupPath as a rollback safety net - delete it once you're confident, or restore it by renaming it back."
Write-Output "Not starting the app - launch $destExePath yourself when ready."
