[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$programsRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
$installRoot = [IO.Path]::GetFullPath((Join-Path $programsRoot '4L60 Diagnostics'))
if (-not $installRoot.StartsWith($programsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to uninstall from an unexpected location.'
}

$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) '4L60 Diagnostics.lnk'
$startMenuDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) '4L60 Diagnostics'
if (Test-Path -LiteralPath $desktopShortcut) { Remove-Item -LiteralPath $desktopShortcut -Force }
if (Test-Path -LiteralPath $startMenuDirectory) { Remove-Item -LiteralPath $startMenuDirectory -Recurse -Force }

Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
    '-NoProfile',
    '-Command',
    "Start-Sleep -Seconds 2; Remove-Item -LiteralPath '$($installRoot.Replace("'", "''"))' -Recurse -Force"
)
