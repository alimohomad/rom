# Project: HeadOffice Remote Access System (HRAS)

HRAS is a custom remote desktop platform for securely viewing and controlling an office workstation from a remote controller device over the Internet.

## Objective

Build a self-managed remote access stack with:

- A **Receiver Agent** running on the office machine
- A **Controller Application** running on the remote device
- A **Server** for authentication, device registry, session coordination, and audit logging

## Workspace Layout

```text
headoffice-remote/
|-- receiver/     # Office-side capture, input execution, and network agent
|-- controller/   # Remote-side viewer and control client
|-- common/       # Shared protocol, crypto, and data models
|-- server/       # Auth, device management, session APIs, and persistence
`-- docs/         # Architecture, roadmap, protocol, and deployment notes
```

## Recommended Stack

- Language: Rust
- Streaming: WebRTC for media, TCP/TLS or HTTPS/WebSocket for control paths
- Video codec: H.264
- Database: SQLite
- Security: TLS 1.3, device whitelist, login lockout, audit logs, session timeout

## Initial Build Plan

1. Implement server-side authentication and device registration
2. Bring up a receiver heartbeat connection
3. Add a controller login and session creation flow
4. Introduce screen capture and streaming
5. Add mouse, keyboard, and file-transfer commands
6. Harden with TLS, 2FA, logging, and service mode

## Quick Start

```bash
cargo run -p server
cargo run -p receiver
cargo run -p controller
```

Current binaries are scaffolds with protocol models and startup logging so the project structure is ready for implementation.

## Office Browser Viewer + Receiver EXE

The `office/` folder contains a working screen sharing prototype:

- `office/public/office.html` is opened by the office viewer in a browser.
- `office/receiver-agent` builds the Windows `.exe` that shares the remote computer screen.
- `office/server.js` serves the pages and relays screen frames over WebSocket.

See `office/README.md` for run commands and public IP/HTTPS notes.
"# rom" 
