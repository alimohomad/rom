$ErrorActionPreference = "Stop"

$publishDir = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-x64\publish"
$exePath = Join-Path $publishDir "services.exe"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Receiver executable was not found at: $exePath"
}

$startupDir = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupDir "HRAS Receiver Agent.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $publishDir
$shortcut.WindowStyle = 7
$shortcut.Description = "Starts HRAS Receiver Agent when this Windows user logs in."
$shortcut.Save()

Write-Host "Installed startup shortcut:"
Write-Host $shortcutPath





