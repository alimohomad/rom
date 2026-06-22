use anyhow::Result;
use tokio::time::{sleep, Duration};
use tracing::info;

#[derive(Debug, Clone)]
pub struct ServerConfig {
    pub bind_addr: String,
}

impl Default for ServerConfig {
    fn default() -> Self {
        Self {
            bind_addr: "127.0.0.1:8443".to_string(),
        }
    }
}

pub async fn run(config: ServerConfig) -> Result<()> {
    info!("server bootstrap scaffold listening at {}", config.bind_addr);
    sleep(Duration::from_millis(50)).await;
    Ok(())
}
