param(
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$projectDir = Join-Path $scriptDir "RemoteDesktopClient"
$projectPath = Join-Path $projectDir "RemoteDesktopClient.csproj"
$envPublishPath = Join-Path $projectDir ".env.publish"
$appSettingsFactoryPath = Join-Path $projectDir "Core\Configuration\AppSettingsFactory.cs"
$generatorProjectPath = Join-Path $scriptDir "tools\ReleaseConfigGenerator"
$updaterProjectPath = Join-Path $scriptDir "tools\RemoteConnect.Updater\RemoteConnect.Updater.csproj"

if (-not (Test-Path $envPublishPath)) {
    throw "Missing $envPublishPath. Copy RemoteDesktopClient\.env.publish.example to RemoteDesktopClient\.env.publish and fill in the real production target first."
}

Write-Output "Publishing the update helper (tools/RemoteConnect.Updater)..."
& dotnet publish $updaterProjectPath -c Release -r $RuntimeIdentifier --self-contained true `
    -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=embedded
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the update helper failed with exit code $LASTEXITCODE - nothing else was published."
}
$updaterExePath = Join-Path $scriptDir "tools\RemoteConnect.Updater\bin\Release\net10.0\$RuntimeIdentifier\publish\RemoteConnect.Updater.exe"
if (-not (Test-Path $updaterExePath)) {
    throw "Update helper publish reported success but $updaterExePath is missing."
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$originalAppSettingsFactoryContent = [System.IO.File]::ReadAllText($appSettingsFactoryPath, $utf8NoBom)

try {
    Write-Output "Generating publish AppSettingsFactory from .env.publish..."
    & dotnet run --project $generatorProjectPath -- $envPublishPath $appSettingsFactoryPath
    if ($LASTEXITCODE -ne 0) {
        throw "Publish configuration generation failed (see errors above) - nothing was published."
    }

    $publishDirPreClean = Join-Path $projectDir "bin\Release\net10.0\$RuntimeIdentifier\publish"
    if (Test-Path $publishDirPreClean) {
        Remove-Item -Path $publishDirPreClean -Recurse -Force
    }

    & dotnet publish $projectPath -c Release -r $RuntimeIdentifier --self-contained true `
        -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=embedded
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $publishDir = Join-Path $projectDir "bin\Release\net10.0\$RuntimeIdentifier\publish"
    $exePath = Join-Path $publishDir "RemoteConnect.exe"

    if (-not (Test-Path $exePath)) {
        throw "Publish reported success but $exePath is missing - not a usable portable build."
    }

    Get-ChildItem -Path $publishDir -Filter "*.pdb" -File -ErrorAction SilentlyContinue | Remove-Item -Force

    $sha256Hex = (Get-FileHash -Algorithm SHA256 -Path $exePath).Hash.ToLower()
    $sha256Path = "$exePath.sha256"
    [System.IO.File]::WriteAllText($sha256Path, $sha256Hex, $utf8NoBom)

    $leftoverFiles = Get-ChildItem -Path $publishDir -File
    Write-Output "Published portable build to $publishDir"
    Write-Output "  Files in that folder: $($leftoverFiles.Name -join ', ')"
    Write-Output "IMPORTANT: unlike before, the bundled FFmpeg DLLs are no longer loose next to the exe, so this script can no longer statically verify they made it in - the only real check is running RemoteConnect.exe and confirming it starts with no FFmpeg-init failure in its log (see %AppData%\RemoteConnect\logs). Do this before distributing a build."
    Write-Output "To publish a real update: tag a GitHub release vMAJOR.MINOR.PATCH matching this project's <Version> on mark-anthony-libres/remote-connect-client (or whatever UPDATE_GITHUB_REPO is set to), and upload BOTH RemoteConnect.exe and RemoteConnect.exe.sha256 as release assets - the in-app updater looks for both by exact name."
}
finally {
    [System.IO.File]::WriteAllText($appSettingsFactoryPath, $originalAppSettingsFactoryContent, $utf8NoBom)
    Write-Output "Restored AppSettingsFactory.cs to its development form."
}
