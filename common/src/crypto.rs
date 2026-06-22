#[derive(Debug, Clone)]
pub struct SecurityProfile {
    pub tls_enabled: bool,
    pub device_whitelist: bool,
    pub totp_enabled: bool,
}

impl Default for SecurityProfile {
    fn default() -> Self {
        Self {
            tls_enabled: true,
            device_whitelist: true,
            totp_enabled: false,
        }
    }
}
