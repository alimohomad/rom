$ErrorActionPreference = "Stop"

$desktopDir = [Environment]::GetFolderPath("Desktop")
$publishDir = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-x64\publish"
$exePath = Join-Path $publishDir "HRAS-Receiver-Agent.exe"
$startScript = Join-Path $PSScriptRoot "start-receiver.ps1"
$statusScript = Join-Path $PSScriptRoot "status-receiver.ps1"
$stopScript = Join-Path $PSScriptRoot "stop-receiver.ps1"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Receiver executable was not found at: $exePath"
}

$shell = New-Object -ComObject WScript.Shell

$startShortcut = $shell.CreateShortcut((Join-Path $desktopDir "Start HRAS Sharing.lnk"))
$startShortcut.TargetPath = "powershell.exe"
$startShortcut.Arguments = "-ExecutionPolicy Bypass -File `"$startScript`""
$startShortcut.WorkingDirectory = $PSScriptRoot
$startShortcut.IconLocation = "$exePath,0"
$startShortcut.Description = "Start HRAS screen sharing."
$startShortcut.Save()

$statusShortcut = $shell.CreateShortcut((Join-Path $desktopDir "Status HRAS Sharing.lnk"))
$statusShortcut.TargetPath = "powershell.exe"
$statusShortcut.Arguments = "-ExecutionPolicy Bypass -File `"$statusScript`""
$statusShortcut.WorkingDirectory = $PSScriptRoot
$statusShortcut.IconLocation = "shell32.dll,23"
$statusShortcut.Description = "Show HRAS screen sharing status."
$statusShortcut.Save()

$stopShortcut = $shell.CreateShortcut((Join-Path $desktopDir "Stop HRAS Sharing.lnk"))
$stopShortcut.TargetPath = "powershell.exe"
$stopShortcut.Arguments = "-ExecutionPolicy Bypass -File `"$stopScript`""
$stopShortcut.WorkingDirectory = $PSScriptRoot
$stopShortcut.IconLocation = "shell32.dll,131"
$stopShortcut.Description = "Stop HRAS screen sharing."
$stopShortcut.Save()

Write-Host "Desktop controls installed:"
Write-Host (Join-Path $desktopDir "Start HRAS Sharing.lnk")
Write-Host (Join-Path $desktopDir "Status HRAS Sharing.lnk")
Write-Host (Join-Path $desktopDir "Stop HRAS Sharing.lnk")
