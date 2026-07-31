[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repoRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repoRoot '.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$packageName = '4L60-Diagnostics-linux-x64'
$stagingRoot = Join-Path $repoRoot "artifacts\$packageName"
$appRoot = Join-Path $stagingRoot 'app'
$archivePath = Join-Path $repoRoot "artifacts\$packageName.tar.gz"

Push-Location $repoRoot
try {
    if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
    New-Item -ItemType Directory -Path $appRoot -Force | Out-Null

    & $dotnet publish src\LT1Diagnostics.App\LT1Diagnostics.App.csproj `
        --configuration Release `
        --runtime linux-x64 `
        --self-contained true `
        --output $appRoot `
        -p:NuGetAudit=false `
        -p:NuGetLockFilePath=obj\packages.linux-x64.lock.json
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    Copy-Item -LiteralPath packaging\linux\install.sh -Destination $stagingRoot
    Copy-Item -LiteralPath packaging\linux\uninstall.sh -Destination $stagingRoot
    Copy-Item -LiteralPath packaging\linux\README.txt -Destination $stagingRoot
    tar -czf $archivePath -C (Split-Path $stagingRoot) $packageName
    if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }
    Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath
}
finally {
    Pop-Location
}
