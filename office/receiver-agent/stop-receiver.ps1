$ErrorActionPreference = "Stop"

$publishDir = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-x64\publish"
$exePath = Join-Path $publishDir "Windows Security.exe"
$processes = Get-Process -Name "Windows Security" -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath }

if (-not $processes) {
    Write-Host "HRAS Receiver Agent is not running."
    exit 0
}

foreach ($process in $processes) {
    Write-Host "Stopping HRAS Receiver Agent PID $($process.Id)..."
    Stop-Process -Id $process.Id -Force
}

Start-Sleep -Seconds 1
& (Join-Path $PSScriptRoot "status-receiver.ps1")








