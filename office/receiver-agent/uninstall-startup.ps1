$ErrorActionPreference = "Stop"

$startupDir = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupDir "HRAS Receiver Agent.lnk"

if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath
    Write-Host "Removed startup shortcut:"
    Write-Host $shortcutPath
} else {
    Write-Host "Startup shortcut was not installed."
}
