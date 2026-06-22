const params = new URLSearchParams(window.location.search);
const roomInput = document.querySelector("#roomInput");
const codeInput = document.querySelector("#codeInput");
const employeeSelect = document.querySelector("#employeeSelect");
const connectButton = document.querySelector("#connectButton");
const disconnectButton = document.querySelector("#disconnectButton");
const statusText = document.querySelector("#statusText");
const statusPill = document.querySelector("#statusPill");
const roomLabel = document.querySelector("#roomLabel");
const receiverCount = document.querySelector("#receiverCount");
const frameCount = document.querySelector("#frameCount");
const serverLabel = document.querySelector("#serverLabel");
const remoteFrame = document.querySelector("#remoteFrame");
const emptyPreview = document.querySelector("#emptyPreview");

let ws;
let lastFrameUrl;
let framesSeen = 0;
let selectedAgentId = null;

roomInput.value = params.get("room") || "head-office";
codeInput.value = params.get("code") || "";
roomLabel.textContent = roomInput.value;
serverLabel.textContent = window.location.host;

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
  framesSeen += 1;
  frameCount.textContent = String(framesSeen);
  emptyPreview.classList.add("hidden");
  setStatus("Watching", "ok");
}

function clearFrame() {
  if (lastFrameUrl) {
    URL.revokeObjectURL(lastFrameUrl);
    lastFrameUrl = null;
  }

  remoteFrame.removeAttribute("src");
  emptyPreview.classList.remove("hidden");
  framesSeen = 0;
  frameCount.textContent = "0";
}

function updateEmployeeSelect(agentList, nextSelectedAgentId) {
  const previousSelectedAgentId = selectedAgentId;
  const agents = Array.isArray(agentList) ? agentList : [];

  employeeSelect.innerHTML = "";

  if (agents.length === 0) {
    const option = document.createElement("option");
    option.value = "";
    option.textContent = "No receiver connected";
    employeeSelect.append(option);
    employeeSelect.disabled = true;
    selectedAgentId = null;
    clearFrame();
    return;
  }

  employeeSelect.disabled = false;
  selectedAgentId = nextSelectedAgentId || agents[0].id;

  for (const agent of agents) {
    const option = document.createElement("option");
    option.value = agent.id;
    option.textContent = `${agent.name} (${agent.frames || 0} frames)`;
    employeeSelect.append(option);
  }

  employeeSelect.value = selectedAgentId;

  if (previousSelectedAgentId && previousSelectedAgentId !== selectedAgentId) {
    clearFrame();
  }
}

function connect() {
  disconnect();

  roomLabel.textContent = roomInput.value.trim() || "head-office";
  serverLabel.textContent = window.location.host;
  framesSeen = 0;
  frameCount.textContent = "0";
  receiverCount.textContent = "0";
  selectedAgentId = null;
  employeeSelect.disabled = true;
  employeeSelect.innerHTML = '<option value="">No receiver connected</option>';
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
      receiverCount.textContent = String(message.agents || 0);
      updateEmployeeSelect(message.agentList, message.selectedAgentId);

      if ((message.frames || 0) > framesSeen) {
        frameCount.textContent = String(message.frames);
      }

      if (message.agents > 0) {
        setStatus("Receiver online", "ok");
      } else {
        setStatus("Waiting: no receiver");
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

  ws.onerror = () => setStatus("Check code/server");
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
  receiverCount.textContent = "0";
  frameCount.textContent = "0";
  selectedAgentId = null;
  employeeSelect.disabled = true;
  employeeSelect.innerHTML = '<option value="">No receiver connected</option>';
  connectButton.disabled = false;
  disconnectButton.disabled = true;
}

connectButton.addEventListener("click", connect);
disconnectButton.addEventListener("click", disconnect);
employeeSelect.addEventListener("change", () => {
  selectedAgentId = employeeSelect.value || null;
  clearFrame();

  if (ws?.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify({
      type: "select-agent",
      agentId: selectedAgentId,
    }));
  }
});

if (codeInput.value) {
  connect();
}
