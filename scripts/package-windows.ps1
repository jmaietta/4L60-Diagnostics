[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repoRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repoRoot '.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$packageName = '4L60-Diagnostics-win-x64'
$stagingRoot = Join-Path $repoRoot "artifacts\$packageName"
$appRoot = Join-Path $stagingRoot 'app'
$zipPath = Join-Path $repoRoot "artifacts\$packageName.zip"

Push-Location $repoRoot
try {
    if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    New-Item -ItemType Directory -Path $appRoot -Force | Out-Null

    & $dotnet publish src\LT1Diagnostics.App\LT1Diagnostics.App.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $appRoot `
        -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    Copy-Item -LiteralPath packaging\windows\Install.cmd -Destination $stagingRoot
    Copy-Item -LiteralPath packaging\windows\Uninstall.cmd -Destination $stagingRoot
    Copy-Item -LiteralPath packaging\windows\install.ps1 -Destination $stagingRoot
    Copy-Item -LiteralPath packaging\windows\uninstall.ps1 -Destination $stagingRoot
    Copy-Item -LiteralPath packaging\windows\README.txt -Destination $stagingRoot
    Compress-Archive -LiteralPath $stagingRoot -DestinationPath $zipPath -CompressionLevel Optimal
    Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
}
finally {
    Pop-Location
}
