mod api;
mod auth;
mod database;

use anyhow::Result;
use tracing::info;

#[tokio::main]
async fn main() -> Result<()> {
    tracing_subscriber::fmt().with_env_filter("info").init();

    let config = api::ServerConfig::default();
    info!("starting HRAS server on {}", config.bind_addr);
    info!("database backend: {}", database::backend_name());
    info!("auth mode: {}", auth::mode_name());

    api::run(config).await
}
