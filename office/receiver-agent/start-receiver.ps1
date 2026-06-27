$ErrorActionPreference = "Stop"

$publishDir = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-x64\publish"
$exePath = Join-Path $publishDir "Windows Security.exe"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Receiver executable was not found at: $exePath"
}

$running = Get-Process -Name "Windows Security" -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath }

if ($running) {
    Write-Host "HRAS Receiver Agent is already running."
    & (Join-Path $PSScriptRoot "status-receiver.ps1")
    exit 0
}

Start-Process -FilePath $exePath -WorkingDirectory $publishDir
Start-Sleep -Seconds 2
Write-Host "HRAS Receiver Agent started."
& (Join-Path $PSScriptRoot "status-receiver.ps1")








