-- =============================================================================
-- CashApp Database Rollback Script
-- Rollback Fase 1: Hernoem cashapp_users terug naar bai_monitor_users
-- Datum: 2025-12-11
-- =============================================================================

-- Stap 1: Hernoem de tabel terug
ALTER TABLE rpa_data.cashapp_users 
RENAME TO bai_monitor_users;

-- Stap 2: Verifieer de tabel bestaat
SELECT 
    tablename, 
    schemaname 
FROM pg_tables 
WHERE schemaname = 'rpa_data' 
AND tablename = 'bai_monitor_users';

COMMIT;
