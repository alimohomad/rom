# HRAS Office Browser Share

This folder is a browser-viewer screen sharing prototype with a native Windows sender.

## What It Does

- The office machine runs `server.js` on a public IP or forwarded port.
- The office viewer opens `office.html` in a browser.
- The remote person runs `HRAS-Receiver-Agent.exe`, which starts without a command prompt.
- The office viewer sees the remote person's screen.

The browser-based `share.html` page still exists for testing, but the intended sender is now the Windows `.exe` in `receiver-agent/`.

## Run

```powershell
cd C:\Users\Acer\Documents\joo\office
npm install
$env:HRAS_ACCESS_CODE="change-this-secret"
npm start
```

The server listens on port `8080` by default.

## If You See 401 Unauthorized

The access code is different between the server and client.

These three values must be exactly the same:

- `HRAS_ACCESS_CODE` on the office server
- `code=...` in the office browser URL
- the embedded code in the Windows receiver `.exe`

## URLs

Office viewer:

```text
http://OFFICE_PUBLIC_IP:8080/office.html?room=head-office&code=change-this-secret
```

Remote sharer:

```text
HRAS-Receiver-Agent.exe
```

## Multiple Employees

Multiple Windows employees can run the same `HRAS-Receiver-Agent.exe` at the same time.

- They must use the same room and access code.
- The office browser shows an `Employee screen` dropdown.
- The dropdown names come from each employee's Windows machine name.
- The office viewer receives frames only from the selected employee.
- Switching employees does not require restarting the office server or receiver apps.

For real office use, replace the shared `change-me` code with a strong private code. A later production version should add per-employee identity, device approval, and audit logs.

## Employee And Manager Controls

The Windows receiver runs without a command prompt. Management can create desktop controls with `receiver-agent/install-desktop-controls.ps1`.

- `Start HRAS Sharing` starts the receiver and prints active status.
- `Status HRAS Sharing` shows whether it is running, PID, start time, uptime, and path.
- `Stop HRAS Sharing` stops the receiver.
- Task Manager can also stop it by ending `HRAS-Receiver-Agent.exe`.

The tray icon is kept as a transparency indicator because this app captures the employee's screen.

## Important HTTPS Note

Screen sharing works on `localhost` over HTTP, but for a public IP most browsers require HTTPS before `share.html` can capture the screen.
The native `.exe` sender can connect over plain `ws://`, but use `wss://` for real Internet deployment so the stream is encrypted.

For real Internet use, put this server behind HTTPS/WSS with one of these:

- A domain name plus Let's Encrypt
- Caddy or Nginx as a TLS reverse proxy
- Cloudflare Tunnel or ngrok for quick testing

Optional built-in HTTPS can be enabled with PEM files:

```powershell
$env:HRAS_TLS_CERT="C:\path\to\cert.pem"
$env:HRAS_TLS_KEY="C:\path\to\key.pem"
npm start
```

Then use:

```text
https://OFFICE_PUBLIC_IP:8080/office.html?room=head-office&code=change-this-secret
https://OFFICE_PUBLIC_IP:8080/share.html?room=head-office&code=change-this-secret
```
