param(
    [string]$RuntimeIdentifier = "win-x64",
    [bool]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$projectDir = Join-Path $scriptDir "RemoteDesktopClient"
$envReleasePath = Join-Path $projectDir ".env.release"
$appSettingsFactoryPath = Join-Path $projectDir "Core\Configuration\AppSettingsFactory.cs"
$slnxPath = Join-Path $scriptDir "RemoteDesktopClient.slnx"
$generatorProjectPath = Join-Path $scriptDir "tools\ReleaseConfigGenerator"

if (-not (Test-Path $envReleasePath)) {
    throw "Missing $envReleasePath. Copy RemoteDesktopClient\.env.release.example to RemoteDesktopClient\.env.release and fill in the real release target first."
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$originalAppSettingsFactoryContent = [System.IO.File]::ReadAllText($appSettingsFactoryPath, $utf8NoBom)

try {
    Write-Output "Generating release AppSettingsFactory from .env.release..."
    & dotnet run --project $generatorProjectPath -- $envReleasePath $appSettingsFactoryPath
    if ($LASTEXITCODE -ne 0) {
        throw "Release configuration generation failed (see errors above) - nothing was published."
    }

    & dotnet publish $slnxPath -c Release -r $RuntimeIdentifier --self-contained $SelfContained.ToString().ToLower()
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    Write-Output "Published to RemoteDesktopClient\bin\Release\net10.0\$RuntimeIdentifier\publish\"
}
finally {
    [System.IO.File]::WriteAllText($appSettingsFactoryPath, $originalAppSettingsFactoryContent, $utf8NoBom)
    Write-Output "Restored AppSettingsFactory.cs to its development form."
}
