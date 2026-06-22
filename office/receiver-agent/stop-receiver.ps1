$ErrorActionPreference = "Stop"

$processes = Get-Process -Name "HRAS-Receiver-Agent" -ErrorAction SilentlyContinue

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
