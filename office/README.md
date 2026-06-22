# HRAS Office Browser Share

This folder is a browser-viewer screen sharing prototype with a native Windows sender.

## What It Does

- The office machine runs `server.js` on a public IP or forwarded port.
- The office viewer opens `office.html` in a browser.
- The remote person runs `HRAS-Receiver-Agent.exe`.
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

## URLs

Office viewer:

```text
http://OFFICE_PUBLIC_IP:8080/office.html?room=head-office&code=change-this-secret
```

Remote sharer:

```text
HRAS-Receiver-Agent.exe --server ws://OFFICE_PUBLIC_IP:8080 --room head-office --code change-this-secret
```

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
