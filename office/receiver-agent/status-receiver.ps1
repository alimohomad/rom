$ErrorActionPreference = "Stop"

$processes = Get-Process -Name "HRAS-Receiver-Agent" -ErrorAction SilentlyContinue

Write-Host "HRAS Receiver Agent"
Write-Host "-------------------"

if (-not $processes) {
    Write-Host "Status    : Not running"
    exit 0
}

foreach ($process in $processes) {
    $started = "Unavailable"
    $uptime = "Unavailable"
    $path = "Unavailable"

    try {
        $started = $process.StartTime
        $uptime = (Get-Date) - $process.StartTime
    } catch {
        $started = "Unavailable"
        $uptime = "Unavailable"
    }

    try {
        $path = $process.Path
    } catch {
        $path = "Unavailable"
    }

    Write-Host "Status    : Active"
    Write-Host "PID       : $($process.Id)"
    Write-Host "Started   : $started"
    Write-Host "Uptime    : $uptime"
    Write-Host "Path      : $path"
}
