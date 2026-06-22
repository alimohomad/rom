use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Device {
    pub id: String,
    pub hostname: String,
    pub platform: Platform,
    pub online: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum Platform {
    Windows,
    Linux,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Session {
    pub id: String,
    pub controller_id: String,
    pub receiver_id: String,
    pub status: SessionStatus,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum SessionStatus {
    Pending,
    Active,
    Closed,
}
