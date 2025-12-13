-- =============================================================================
-- CashApp Database Migration Script
-- Fase 1: Hernoem bai_monitor_users naar cashapp_users
-- Datum: 2025-12-11
-- =============================================================================

-- Stap 1: Hernoem de tabel
ALTER TABLE rpa_data.bai_monitor_users 
RENAME TO cashapp_users;

-- Stap 2: Verifieer de tabel bestaat
SELECT 
    tablename, 
    schemaname 
FROM pg_tables 
WHERE schemaname = 'rpa_data' 
AND tablename = 'cashapp_users';

-- Stap 3: Toon huidige kolommen
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'rpa_data' 
AND table_name = 'cashapp_users'
ORDER BY ordinal_position;

COMMIT;
