-- =============================================================================
-- CashApp Database Migration Script
-- Fase 2: Voeg Module Permissions toe
-- Datum: 2025-12-11
-- =============================================================================

-- Stap 1: Voeg permission kolommen toe aan cashapp_users
ALTER TABLE rpa_data.cashapp_users 
ADD COLUMN IF NOT EXISTS has_bai_access BOOLEAN DEFAULT TRUE,
ADD COLUMN IF NOT EXISTS has_recon_access BOOLEAN DEFAULT FALSE;

-- Stap 2: Geef bestaande users standaard BAI toegang (backward compatibility)
UPDATE rpa_data.cashapp_users 
SET has_bai_access = TRUE
WHERE has_bai_access IS NULL;

-- Stap 3: Admin users krijgen toegang tot alles
UPDATE rpa_data.cashapp_users 
SET has_bai_access = TRUE,
    has_recon_access = TRUE
WHERE is_admin = TRUE;

-- Stap 4: Verifieer de nieuwe kolommen
SELECT 
    column_name, 
    data_type, 
    column_default
FROM information_schema.columns
WHERE table_schema = 'rpa_data' 
AND table_name = 'cashapp_users'
AND column_name IN ('has_bai_access', 'has_recon_access')
ORDER BY ordinal_position;

-- Stap 5: Toon user permissions overzicht
SELECT 
    id,
    username,
    is_admin,
    has_bai_access,
    has_recon_access,
    is_active
FROM rpa_data.cashapp_users
ORDER BY username;

COMMIT;
