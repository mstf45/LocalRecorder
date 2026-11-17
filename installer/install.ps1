$ErrorActionPreference = 'Stop'

$destination = Join-Path $env:LOCALAPPDATA 'Programs\LocalRecorder'
New-Item -ItemType Directory -Force -Path $destination | Out-Null

Get-Process LocalRecorder -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

$source = Split-Path -Parent $PSCommandPath
Copy-Item (Join-Path $source 'LocalRecorder.exe') (Join-Path $destination 'LocalRecorder.exe') -Force
Copy-Item (Join-Path $source 'ScreenRecorderLib.dll') (Join-Path $destination 'ScreenRecorderLib.dll') -Force

$shell = New-Object -ComObject WScript.Shell

function New-AppShortcut {
    param([string]$ShortcutPath)

    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = Join-Path $destination 'LocalRecorder.exe'
    $shortcut.WorkingDirectory = $destination
    $shortcut.IconLocation = "$(Join-Path $destination 'LocalRecorder.exe'),0"
    $shortcut.Description = 'LocalRecorder'
    $shortcut.Save()
}

$desktop = [Environment]::GetFolderPath('Desktop')
$programs = [Environment]::GetFolderPath('Programs')
New-AppShortcut (Join-Path $desktop 'LocalRecorder.lnk')
New-AppShortcut (Join-Path $programs 'LocalRecorder.lnk')

Start-Process (Join-Path $destination 'LocalRecorder.exe')
