mod decoder;
mod input;
mod ui;

use anyhow::Result;
use tracing::info;

#[tokio::main]
async fn main() -> Result<()> {
    tracing_subscriber::fmt().with_env_filter("info").init();

    info!("starting HRAS controller");
    info!("decoder planned: {}", decoder::codec_name());
    info!("input client planned: {}", input::mode_name());
    info!("ui mode planned: {}", ui::shell_name());

    Ok(())
}
