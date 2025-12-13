-- =============================================================================
-- CashApp Database Rollback Script
-- Rollback Fase 2: Verwijder module permission kolommen
-- Datum: 2025-12-11
-- =============================================================================

-- Stap 1: Verwijder de permission kolommen
ALTER TABLE rpa_data.cashapp_users 
DROP COLUMN IF EXISTS has_bai_access,
DROP COLUMN IF EXISTS has_recon_access;

-- Stap 2: Verifieer de kolommen zijn verwijderd
SELECT 
    column_name
FROM information_schema.columns
WHERE table_schema = 'rpa_data' 
AND table_name = 'cashapp_users'
ORDER BY ordinal_position;

COMMIT;
