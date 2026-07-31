[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'app'
$programsRoot = Join-Path $env:LOCALAPPDATA 'Programs'
$installRoot = Join-Path $programsRoot 'Maietta Diagnostics'
$executable = Join-Path $installRoot '4L60-Diagnostics.exe'

if (-not (Test-Path -LiteralPath (Join-Path $source '4L60-Diagnostics.exe'))) {
    throw 'The application files are missing. Extract the complete ZIP before running Install.'
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $installRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $installRoot -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.cmd') -Destination $installRoot -Force

$shell = New-Object -ComObject WScript.Shell
$desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Maietta Diagnostics.lnk'))
$desktopShortcut.TargetPath = $executable
$desktopShortcut.WorkingDirectory = $installRoot
$desktopShortcut.Description = 'Maietta Diagnostics — GM 4L60E'
$desktopShortcut.Save()

$startMenuDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) 'Maietta Diagnostics'
New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null
$startShortcut = $shell.CreateShortcut((Join-Path $startMenuDirectory 'Maietta Diagnostics.lnk'))
$startShortcut.TargetPath = $executable
$startShortcut.WorkingDirectory = $installRoot
$startShortcut.Description = 'Maietta Diagnostics — GM 4L60E'
$startShortcut.Save()
$uninstallShortcut = $shell.CreateShortcut((Join-Path $startMenuDirectory 'Uninstall Maietta Diagnostics.lnk'))
$uninstallShortcut.TargetPath = (Join-Path $installRoot 'Uninstall.cmd')
$uninstallShortcut.WorkingDirectory = $installRoot
$uninstallShortcut.Save()

Start-Process -FilePath $executable -WorkingDirectory $installRoot
