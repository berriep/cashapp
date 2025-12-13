-- =====================================================
-- Users Table for Authentication (matching BAI_Tool)
-- =====================================================

CREATE TABLE IF NOT EXISTS rpa_data.recon_monitor_users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(255),
    full_name VARCHAR(200),
    is_admin BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_recon_monitor_users_username ON rpa_data.recon_monitor_users(username);
CREATE INDEX IF NOT EXISTS idx_recon_monitor_users_active ON rpa_data.recon_monitor_users(is_active);

COMMENT ON TABLE rpa_data.recon_monitor_users IS 'User authentication table for Reconciliation Tool web interface';
