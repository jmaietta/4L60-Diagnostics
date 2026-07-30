[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'app'
$programsRoot = Join-Path $env:LOCALAPPDATA 'Programs'
$installRoot = Join-Path $programsRoot '4L60 Diagnostics'
$executable = Join-Path $installRoot '4L60-Diagnostics.exe'

if (-not (Test-Path -LiteralPath (Join-Path $source '4L60-Diagnostics.exe'))) {
    throw 'The application files are missing. Extract the complete ZIP before running Install.'
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $installRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $installRoot -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.cmd') -Destination $installRoot -Force

$shell = New-Object -ComObject WScript.Shell
$desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) '4L60 Diagnostics.lnk'))
$desktopShortcut.TargetPath = $executable
$desktopShortcut.WorkingDirectory = $installRoot
$desktopShortcut.Description = '4L60 Diagnostics for the 1994 Buick Roadmaster'
$desktopShortcut.Save()

$startMenuDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) '4L60 Diagnostics'
New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null
$startShortcut = $shell.CreateShortcut((Join-Path $startMenuDirectory '4L60 Diagnostics.lnk'))
$startShortcut.TargetPath = $executable
$startShortcut.WorkingDirectory = $installRoot
$startShortcut.Description = '4L60 Diagnostics for the 1994 Buick Roadmaster'
$startShortcut.Save()
$uninstallShortcut = $shell.CreateShortcut((Join-Path $startMenuDirectory 'Uninstall 4L60 Diagnostics.lnk'))
$uninstallShortcut.TargetPath = (Join-Path $installRoot 'Uninstall.cmd')
$uninstallShortcut.WorkingDirectory = $installRoot
$uninstallShortcut.Save()

Start-Process -FilePath $executable -WorkingDirectory $installRoot
