use anyhow::Result;
use tokio::time::{sleep, Duration};
use tracing::info;

#[derive(Debug, Clone)]
pub struct ReceiverConfig {
    pub device_name: String,
    pub server_endpoint: String,
}

impl Default for ReceiverConfig {
    fn default() -> Self {
        Self {
            device_name: "office-receiver".to_string(),
            server_endpoint: "https://127.0.0.1:8443".to_string(),
        }
    }
}

pub async fn run(config: ReceiverConfig) -> Result<()> {
    info!("receiver connecting to {}", config.server_endpoint);
    info!("heartbeat loop scaffold active");
    sleep(Duration::from_millis(50)).await;
    Ok(())
}
