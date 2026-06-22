# HRAS Architecture

## High-Level Design

```text
+-----------------+
| Controller App  |
| Home PC/Laptop  |
+--------+--------+
         |
         | TLS Encrypted
         v
+-----------------+
| Office Receiver |
| Public IP / NAT |
| Windows / Linux |
+--------+--------+
         |
         v
+-----------------+
| Coordination    |
| Server          |
| Auth + Logs     |
+-----------------+
```

## Responsibilities

### Receiver Agent

- Capture the display at target 30 FPS
- Encode frames with H.264
- Send media to the controller
- Accept keyboard and mouse commands
- Execute local input safely
- Support file upload and download
- Run headlessly as a service

### Controller Application

- Authenticate to HRAS
- Discover or address a receiver
- Display the live desktop
- Send mouse and keyboard input
- Transfer files
- Show session state and security events

### Server

- Authenticate users and devices
- Issue session tokens
- Maintain device registry
- Store audit logs
- Enforce whitelist, timeout, and lockout policies

## Recommended Transport Split

- WebRTC: video/audio/data paths that benefit from low latency and NAT traversal
- HTTPS or TCP/TLS: authentication, metadata, device registration, audit events

## Security Baseline

- TLS 1.3 everywhere
- Password hashing with Argon2
- Short-lived session tokens
- Optional TOTP-based 2FA
- Device trust records
- Rate limiting and login lockout
- Full audit trail
