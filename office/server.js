import { createServer as createHttpServer } from "node:http";
import { createServer as createHttpsServer } from "node:https";
import { randomUUID } from "node:crypto";
import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { extname, join, normalize, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { WebSocketServer, WebSocket } from "ws";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const publicDir = join(__dirname, "public");

const port = Number(process.env.PORT || 8080);
const accessCode = process.env.HRAS_ACCESS_CODE || "change-me";
const tlsCertPath = process.env.HRAS_TLS_CERT;
const tlsKeyPath = process.env.HRAS_TLS_KEY;

const rooms = new Map();
const frameRooms = new Map();

const contentTypes = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
};

function send(ws, message) {
  if (ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(message));
  }
}

function getRoom(roomName) {
  if (!rooms.has(roomName)) {
    rooms.set(roomName, new Map());
  }

  return rooms.get(roomName);
}

function broadcast(roomName, message, exceptId) {
  const room = rooms.get(roomName);
  if (!room) return;

  for (const [clientId, client] of room.entries()) {
    if (clientId !== exceptId) {
      send(client, message);
    }
  }
}

function removeClient(ws) {
  const room = rooms.get(ws.roomName);
  if (!room) return;

  room.delete(ws.clientId);

  if (room.size === 0) {
    rooms.delete(ws.roomName);
    return;
  }

  broadcast(ws.roomName, {
    type: "peer-left",
    id: ws.clientId,
    role: ws.role,
  });
}

function getFrameRoom(roomName) {
  if (!frameRooms.has(roomName)) {
    frameRooms.set(roomName, {
      agents: new Map(),
      viewers: new Map(),
    });
  }

  return frameRooms.get(roomName);
}

function sendFrameRoomStatus(roomName) {
  const room = frameRooms.get(roomName);
  if (!room) return;

  const message = JSON.stringify({
    type: "room-status",
    agents: room.agents.size,
    viewers: room.viewers.size,
  });

  for (const client of [...room.agents.values(), ...room.viewers.values()]) {
    if (client.readyState === WebSocket.OPEN) {
      client.send(message);
    }
  }
}

function removeFrameClient(ws) {
  const room = frameRooms.get(ws.roomName);
  if (!room) return;

  room.agents.delete(ws.clientId);
  room.viewers.delete(ws.clientId);

  if (room.agents.size === 0 && room.viewers.size === 0) {
    frameRooms.delete(ws.roomName);
    return;
  }

  sendFrameRoomStatus(ws.roomName);
}

async function serveStatic(req, res) {
  const requestUrl = new URL(req.url, `http://${req.headers.host}`);
  const pathname = decodeURIComponent(requestUrl.pathname);
  const requestedPath = pathname === "/" ? "/index.html" : pathname;
  const filePath = normalize(join(publicDir, requestedPath));
  const rel = relative(publicDir, filePath);

  if (rel.startsWith("..") || rel === "" || rel.includes("..\\")) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }

  if (!existsSync(filePath)) {
    res.writeHead(404);
    res.end("Not found");
    return;
  }

  const body = await readFile(filePath);
  res.writeHead(200, {
    "Content-Type": contentTypes[extname(filePath)] || "application/octet-stream",
    "Cache-Control": "no-store",
  });
  res.end(body);
}

function routeSignalMessage(ws, raw) {
  let message;

  try {
    message = JSON.parse(raw.toString());
  } catch {
    send(ws, { type: "error", message: "Invalid JSON message." });
    return;
  }

  const room = rooms.get(ws.roomName);
  if (!room) return;

  if (message.type === "viewer-ready") {
    for (const client of room.values()) {
      if (client.role === "sharer") {
        send(client, {
          type: "viewer-ready",
          from: ws.clientId,
        });
      }
    }
    return;
  }

  if (!message.target || !room.has(message.target)) {
    return;
  }

  send(room.get(message.target), {
    ...message,
    from: ws.clientId,
  });
}

function attachSignaling(server) {
  const wss = new WebSocketServer({ noServer: true });
  const frameWss = new WebSocketServer({ noServer: true });

  server.on("upgrade", (req, socket, head) => {
    const requestUrl = new URL(req.url, `http://${req.headers.host}`);
    const roomName = requestUrl.searchParams.get("room") || "head-office";
    const role = requestUrl.searchParams.get("role");
    const code = requestUrl.searchParams.get("code") || "";

    if (requestUrl.pathname === "/frames") {
      if (!["viewer", "agent"].includes(role) || code !== accessCode) {
        socket.write("HTTP/1.1 401 Unauthorized\r\n\r\n");
        socket.destroy();
        return;
      }

      frameWss.handleUpgrade(req, socket, head, (ws) => {
        frameWss.emit("connection", ws, req, { roomName, role });
      });
      return;
    }

    if (requestUrl.pathname !== "/signal") {
      socket.destroy();
      return;
    }

    if (!["viewer", "sharer"].includes(role) || code !== accessCode) {
      socket.write("HTTP/1.1 401 Unauthorized\r\n\r\n");
      socket.destroy();
      return;
    }

    wss.handleUpgrade(req, socket, head, (ws) => {
      wss.emit("connection", ws, req, { roomName, role });
    });
  });

  wss.on("connection", (ws, _req, meta) => {
    ws.clientId = randomUUID();
    ws.roomName = meta.roomName;
    ws.role = meta.role;

    const room = getRoom(ws.roomName);
    room.set(ws.clientId, ws);

    send(ws, {
      type: "joined",
      id: ws.clientId,
      role: ws.role,
      room: ws.roomName,
      peers: Array.from(room.values())
        .filter((client) => client.clientId !== ws.clientId)
        .map((client) => ({ id: client.clientId, role: client.role })),
    });

    broadcast(
      ws.roomName,
      {
        type: "peer-joined",
        id: ws.clientId,
        role: ws.role,
      },
      ws.clientId,
    );

    if (ws.role === "viewer") {
      routeSignalMessage(ws, JSON.stringify({ type: "viewer-ready" }));
    }

    if (ws.role === "sharer") {
      for (const client of room.values()) {
        if (client.role === "viewer") {
          send(ws, { type: "viewer-ready", from: client.clientId });
        }
      }
    }

    ws.on("message", (raw) => routeSignalMessage(ws, raw));
    ws.on("close", () => removeClient(ws));
    ws.on("error", () => removeClient(ws));
  });

  frameWss.on("connection", (ws, _req, meta) => {
    ws.clientId = randomUUID();
    ws.roomName = meta.roomName;
    ws.role = meta.role;

    const room = getFrameRoom(ws.roomName);
    const group = ws.role === "agent" ? room.agents : room.viewers;
    group.set(ws.clientId, ws);

    send(ws, {
      type: "joined",
      id: ws.clientId,
      role: ws.role,
      room: ws.roomName,
    });
    sendFrameRoomStatus(ws.roomName);

    ws.on("message", (raw, isBinary) => {
      const currentRoom = frameRooms.get(ws.roomName);
      if (!currentRoom || ws.role !== "agent") return;

      for (const viewer of currentRoom.viewers.values()) {
        if (viewer.readyState === WebSocket.OPEN) {
          viewer.send(raw, { binary: isBinary });
        }
      }
    });

    ws.on("close", () => removeFrameClient(ws));
    ws.on("error", () => removeFrameClient(ws));
  });
}

async function createServer() {
  if (tlsCertPath && tlsKeyPath) {
    const [cert, key] = await Promise.all([readFile(tlsCertPath), readFile(tlsKeyPath)]);
    return createHttpsServer({ cert, key }, serveStatic);
  }

  return createHttpServer(serveStatic);
}

const server = await createServer();
attachSignaling(server);

server.listen(port, "0.0.0.0", () => {
  const protocol = tlsCertPath && tlsKeyPath ? "https" : "http";
  console.log(`HRAS office server running at ${protocol}://0.0.0.0:${port}`);

  if (accessCode === "change-me") {
    console.warn("Set HRAS_ACCESS_CODE before exposing this server to the Internet.");
  }

  if (protocol === "http") {
    console.warn("Public screen sharing usually requires HTTPS in the browser.");
  }
});
