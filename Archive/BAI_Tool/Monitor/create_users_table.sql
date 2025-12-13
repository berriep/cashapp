-- BAI Monitor - Users Table
-- Run this SQL in pgAdmin to create the users table

-- Create users table in rpa_data schema
CREATE TABLE IF NOT EXISTS rpa_data.bai_monitor_users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(100),
    is_active BOOLEAN DEFAULT TRUE,
    is_admin BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP WITH TIME ZONE
);

-- Create index on username for faster lookups
CREATE INDEX IF NOT EXISTS idx_bai_monitor_users_username ON rpa_data.bai_monitor_users(username);
CREATE INDEX IF NOT EXISTS idx_bai_monitor_users_email ON rpa_data.bai_monitor_users(email);

-- Insert default admin user (password: admin123!)
-- Password hash is bcrypt hash of 'admin123!'
INSERT INTO rpa_data.bai_monitor_users (username, email, password_hash, full_name, is_admin)
VALUES (
    'admin',
    'admin@example.com',
    '$2b$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewY5NU7x/DJc.PQu',  -- admin123!
    'Administrator',
    TRUE
)
ON CONFLICT (username) DO NOTHING;

-- Grant permissions to rpa_pvcp_acc user
GRANT ALL PRIVILEGES ON TABLE rpa_data.bai_monitor_users TO rpa_pvcp_acc;
GRANT USAGE, SELECT ON SEQUENCE rpa_data.bai_monitor_users_id_seq TO rpa_pvcp_acc;

-- Verify the table was created
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_monitor_users'
ORDER BY ordinal_position;

-- Show the admin user
SELECT id, username, email, full_name, is_admin, is_active, created_at
FROM rpa_data.bai_monitor_users;
