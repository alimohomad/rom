# HRAS Receiver Agent

This is the Windows `.exe` sender. Run it on the computer whose screen should be visible to the office.

## Double-Click Mode

Open `HRAS-Receiver-Agent.exe`. The office server, room, access code, and stream settings are embedded in the app. It runs without a command prompt and shows the custom HRAS tray icon without a right-click menu.

```text
server: ws://198.105.113.144:8080
room: head-office
code: change-me
fps: 5
quality: 55
maxWidth: 1280
monitor: 0
```

## Run From Source

```powershell
dotnet run --project C:\Users\Acer\Documents\joo\office\receiver-agent -- --server ws://OFFICE_PUBLIC_IP:8080 --room head-office --code change-me
```

## Build EXE

```powershell
dotnet publish C:\Users\Acer\Documents\joo\office\receiver-agent -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
```

The published file is:

```text
C:\Users\Acer\Documents\joo\office\receiver-agent\bin\Release\net10.0-windows\win-x64\publish\HRAS-Receiver-Agent.exe
```

The publish folder only needs `HRAS-Receiver-Agent.exe`.

## Start Automatically At Login

Run this once on the employee Windows account:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\Acer\Documents\joo\office\receiver-agent\install-startup.ps1
```

Remove auto-start:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\Acer\Documents\joo\office\receiver-agent\uninstall-startup.ps1
```

This uses the user's Windows Startup folder because a real Windows Service cannot reliably capture the logged-in desktop session.

## Options

- `--server`: optional override for the embedded office server URL
- `--room`: room name, default `head-office`
- `--code`: optional override for the embedded access code, must match `HRAS_ACCESS_CODE` on the office server
- `--fps`: frame rate from `1` to `15`, default `5`
- `--quality`: JPEG quality from `20` to `90`, default `55`
- `--max-width`: resize width, default `1280`
- `--monitor`: monitor number, default `0`

The app shows a visible custom tray icon while sharing. Use the desktop stop control or Task Manager to stop it.

## Desktop Start/Stop Controls

Create desktop shortcuts for employees:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\Acer\Documents\joo\office\receiver-agent\install-desktop-controls.ps1
```

This creates:

- `Start HRAS Sharing`
- `Status HRAS Sharing`
- `Stop HRAS Sharing`

The start and status scripts show whether the receiver is active, including PID, start time, uptime, and executable path. The receiver can also be stopped from Task Manager by ending `HRAS-Receiver-Agent.exe`.

Run status manually:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\Acer\Documents\joo\office\receiver-agent\status-receiver.ps1
```

## Multiple Employees

Several employees can run the same `.exe`. The office browser will list each connected Windows machine and let the head select which screen to view.
