use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum ClientMessage {
    Login {
        username: String,
        password: String,
        device_token: String,
        totp_code: Option<String>,
    },
    Mouse {
        x: u32,
        y: u32,
        button: Option<MouseButton>,
        action: MouseAction,
    },
    Key {
        key: String,
        state: KeyState,
    },
    FileUpload {
        filename: String,
        size: u64,
    },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum ServerMessage {
    Challenge {
        nonce: String,
    },
    Authenticated {
        session_token: String,
    },
    SessionCreated {
        session_id: String,
    },
    Error {
        code: String,
        message: String,
    },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum MouseButton {
    Left,
    Right,
    Middle,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum MouseAction {
    Move,
    Down,
    Up,
    Click,
    DoubleClick,
    Scroll,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum KeyState {
    Down,
    Up,
}
