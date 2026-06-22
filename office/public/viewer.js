const params = new URLSearchParams(window.location.search);
const roomInput = document.querySelector("#roomInput");
const codeInput = document.querySelector("#codeInput");
const connectButton = document.querySelector("#connectButton");
const disconnectButton = document.querySelector("#disconnectButton");
const statusText = document.querySelector("#statusText");
const statusPill = document.querySelector("#statusPill");
const roomLabel = document.querySelector("#roomLabel");
const remoteFrame = document.querySelector("#remoteFrame");
const emptyPreview = document.querySelector("#emptyPreview");

let ws;
let lastFrameUrl;

roomInput.value = params.get("room") || "head-office";
codeInput.value = params.get("code") || "";
roomLabel.textContent = roomInput.value;

function setStatus(text, state = "warn") {
  statusText.textContent = text;
  statusPill.textContent = text;
  statusPill.classList.toggle("pill-warn", state !== "ok");
}

function frameUrl() {
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  const room = encodeURIComponent(roomInput.value.trim() || "head-office");
  const code = encodeURIComponent(codeInput.value);
  return `${protocol}//${window.location.host}/frames?role=viewer&room=${room}&code=${code}`;
}

function showFrame(blob) {
  const nextUrl = URL.createObjectURL(blob);
  remoteFrame.onload = () => {
    if (lastFrameUrl) {
      URL.revokeObjectURL(lastFrameUrl);
    }
    lastFrameUrl = nextUrl;
  };
  remoteFrame.src = nextUrl;
  emptyPreview.classList.add("hidden");
  setStatus("Watching", "ok");
}

function connect() {
  disconnect();

  roomLabel.textContent = roomInput.value.trim() || "head-office";
  ws = new WebSocket(frameUrl());
  ws.binaryType = "blob";

  connectButton.disabled = true;
  disconnectButton.disabled = false;
  setStatus("Connecting");

  ws.onopen = () => setStatus("Waiting");

  ws.onmessage = async (event) => {
    if (event.data instanceof Blob) {
      showFrame(event.data);
      return;
    }

    const message = JSON.parse(event.data);

    if (message.type === "room-status") {
      if (message.agents > 0) {
        setStatus("Receiver online", "ok");
      } else {
        setStatus("Waiting");
      }
    }
  };

  ws.onclose = () => {
    connectButton.disabled = false;
    disconnectButton.disabled = true;
    if (!remoteFrame.src) {
      setStatus("Disconnected");
    }
  };

  ws.onerror = () => setStatus("Connection failed");
}

function disconnect() {
  if (ws) {
    ws.close();
    ws = null;
  }

  if (lastFrameUrl) {
    URL.revokeObjectURL(lastFrameUrl);
    lastFrameUrl = null;
  }

  remoteFrame.removeAttribute("src");
  emptyPreview.classList.remove("hidden");
  connectButton.disabled = false;
  disconnectButton.disabled = true;
}

connectButton.addEventListener("click", connect);
disconnectButton.addEventListener("click", disconnect);

if (codeInput.value) {
  connect();
}
