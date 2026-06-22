const params = new URLSearchParams(window.location.search);
const roomInput = document.querySelector("#roomInput");
const codeInput = document.querySelector("#codeInput");
const startButton = document.querySelector("#startButton");
const stopButton = document.querySelector("#stopButton");
const statusText = document.querySelector("#statusText");
const statusPill = document.querySelector("#statusPill");
const viewerCount = document.querySelector("#viewerCount");
const previewVideo = document.querySelector("#previewVideo");
const emptyPreview = document.querySelector("#emptyPreview");
const secureWarning = document.querySelector("#secureWarning");

const iceServers = [{ urls: "stun:stun.l.google.com:19302" }];
const peerConnections = new Map();
const activeViewers = new Set();

let ws;
let localStream;

roomInput.value = params.get("room") || "head-office";
codeInput.value = params.get("code") || "";

if (!window.isSecureContext) {
  secureWarning.classList.remove("hidden");
}

function setStatus(text, state = "warn") {
  statusText.textContent = text;
  statusPill.textContent = text;
  statusPill.classList.toggle("pill-warn", state !== "ok");
}

function signalUrl() {
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  const room = encodeURIComponent(roomInput.value.trim() || "head-office");
  const code = encodeURIComponent(codeInput.value);
  return `${protocol}//${window.location.host}/signal?role=sharer&room=${room}&code=${code}`;
}

function send(message) {
  if (ws?.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(message));
  }
}

function updateViewerCount() {
  viewerCount.textContent = String(activeViewers.size);
}

function closePeer(viewerId) {
  const pc = peerConnections.get(viewerId);
  if (pc) {
    pc.close();
    peerConnections.delete(viewerId);
  }

  activeViewers.delete(viewerId);
  updateViewerCount();
}

function createPeer(viewerId) {
  closePeer(viewerId);

  const pc = new RTCPeerConnection({ iceServers });
  peerConnections.set(viewerId, pc);

  for (const track of localStream.getTracks()) {
    pc.addTrack(track, localStream);
  }

  pc.onicecandidate = (event) => {
    if (event.candidate) {
      send({
        type: "ice",
        target: viewerId,
        candidate: event.candidate,
      });
    }
  };

  pc.onconnectionstatechange = () => {
    if (["connected", "completed"].includes(pc.connectionState)) {
      activeViewers.add(viewerId);
      updateViewerCount();
      setStatus("Sharing", "ok");
    }

    if (["closed", "failed", "disconnected"].includes(pc.connectionState)) {
      closePeer(viewerId);
      if (activeViewers.size === 0 && localStream) {
        setStatus("Waiting for viewer");
      }
    }
  };

  return pc;
}

async function startOffer(viewerId) {
  if (!localStream) return;

  const pc = createPeer(viewerId);
  const offer = await pc.createOffer();
  await pc.setLocalDescription(offer);

  send({
    type: "offer",
    target: viewerId,
    description: pc.localDescription,
  });
}

function connectSignaling() {
  ws = new WebSocket(signalUrl());

  ws.onopen = () => setStatus("Waiting for viewer");

  ws.onmessage = async (event) => {
    const message = JSON.parse(event.data);

    if (message.type === "joined") {
      setStatus("Waiting for viewer");
      return;
    }

    if (message.type === "viewer-ready") {
      await startOffer(message.from);
      return;
    }

    if (message.type === "answer") {
      const pc = peerConnections.get(message.from);
      if (pc) {
        await pc.setRemoteDescription(message.description);
      }
      return;
    }

    if (message.type === "ice") {
      const pc = peerConnections.get(message.from);
      if (pc && message.candidate) {
        await pc.addIceCandidate(message.candidate);
      }
      return;
    }

    if (message.type === "peer-left") {
      closePeer(message.id);
    }
  };

  ws.onclose = () => {
    if (localStream) {
      setStatus("Disconnected");
    }
  };

  ws.onerror = () => setStatus("Connection failed");
}

async function startSharing() {
  try {
    localStream = await navigator.mediaDevices.getDisplayMedia({
      video: {
        frameRate: 30,
      },
      audio: false,
    });

    previewVideo.srcObject = localStream;
    emptyPreview.classList.add("hidden");
    startButton.disabled = true;
    stopButton.disabled = false;

    for (const track of localStream.getVideoTracks()) {
      track.addEventListener("ended", stopSharing);
    }

    connectSignaling();
  } catch (error) {
    setStatus(error?.message || "Screen sharing cancelled");
  }
}

function stopSharing() {
  for (const pc of peerConnections.values()) {
    pc.close();
  }

  peerConnections.clear();
  activeViewers.clear();
  updateViewerCount();

  if (ws) {
    ws.close();
    ws = null;
  }

  if (localStream) {
    for (const track of localStream.getTracks()) {
      track.stop();
    }
    localStream = null;
  }

  previewVideo.srcObject = null;
  emptyPreview.classList.remove("hidden");
  startButton.disabled = false;
  stopButton.disabled = true;
  setStatus("Not sharing");
}

startButton.addEventListener("click", startSharing);
stopButton.addEventListener("click", stopSharing);
