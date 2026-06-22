# Protocol Outline

## Authentication Flow

1. Controller connects to server
2. Server returns challenge metadata
3. Controller submits username, password, device token, and optional TOTP
4. Server issues a session token
5. Controller requests a connection to a receiver

## Message Families

- `auth/*`: login, challenge, token refresh
- `session/*`: create, accept, terminate, heartbeat
- `input/*`: mouse and keyboard actions
- `file/*`: upload, download, rename, delete
- `audit/*`: security and activity logs
- `stream/*`: stream negotiation and quality control

## Example Input Messages

```json
{
  "type": "mouse",
  "x": 1200,
  "y": 600,
  "button": "left",
  "action": "click"
}
```

```json
{
  "type": "key",
  "key": "A",
  "state": "down"
}
```
