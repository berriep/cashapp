-- Admin Users Table
CREATE TABLE IF NOT EXISTS rpa_data.bai_monitor_users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(100),
    full_name VARCHAR(100),
    is_active BOOLEAN DEFAULT TRUE,
    is_admin BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP
);

-- Grant permissions
GRANT ALL PRIVILEGES ON TABLE rpa_data.bai_monitor_users TO rpa_pvcp_acc;
GRANT USAGE, SELECT ON SEQUENCE rpa_data.bai_monitor_users_id_seq TO rpa_pvcp_acc;

-- Insert default admin user (password: admin123!)
-- Password hash for 'admin123!' using bcrypt
INSERT INTO rpa_data.bai_monitor_users (username, password_hash, email, full_name, is_admin)
VALUES ('admin', '$2b$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewY5GyYqGUi8d6sK', 'admin@example.com', 'Administrator', TRUE)
ON CONFLICT (username) DO NOTHING;

SELECT 'User table created and admin user added!' AS result;
