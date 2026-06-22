mod capture;
mod encoder;
mod input;
mod network;

use anyhow::Result;
use tracing::info;

#[tokio::main]
async fn main() -> Result<()> {
    tracing_subscriber::fmt().with_env_filter("info").init();

    let config = network::ReceiverConfig::default();
    info!("starting HRAS receiver for {}", config.device_name);
    info!("capture backend planned: {}", capture::backend_name());
    info!("encoder planned: {}", encoder::codec_name());
    info!("input backend planned: {}", input::backend_name());

    network::run(config).await
}
