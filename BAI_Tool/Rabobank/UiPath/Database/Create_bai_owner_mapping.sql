-- ==============================================================================
-- BAI Multi-Bank Owner Mapping Table Creation Script
-- ==============================================================================
-- Purpose: Create mapping table for customized owner names used in reporting (all banks)
-- Created: November 18, 2025
-- ==============================================================================

-- Drop table if exists (for clean recreation)
DROP TABLE IF EXISTS bai_owner_mapping CASCADE;

-- Create the owner mapping table
CREATE TABLE bai_owner_mapping (
    id SERIAL PRIMARY KEY,
    bank VARCHAR(50) NOT NULL,
    iban VARCHAR(34) NOT NULL,
    owner_name VARCHAR(255) NOT NULL,
    team VARCHAR(100),
    purpose VARCHAR(500),
    is_active BOOLEAN DEFAULT TRUE,
    CONSTRAINT uk_bank_iban UNIQUE (bank, iban)
);

-- Create indexes for performance
CREATE INDEX idx_owner_mapping_bank ON bai_owner_mapping(bank);
CREATE INDEX idx_owner_mapping_iban ON bai_owner_mapping(iban);
CREATE INDEX idx_owner_mapping_bank_iban ON bai_owner_mapping(bank, iban);
CREATE INDEX idx_owner_mapping_team ON bai_owner_mapping(team);
CREATE INDEX idx_owner_mapping_active ON bai_owner_mapping(is_active);

-- Add comments for documentation
COMMENT ON TABLE bai_owner_mapping IS 'Custom owner name mapping for all bank accounts used in reporting';
COMMENT ON COLUMN bai_owner_mapping.id IS 'Primary key auto-increment';
COMMENT ON COLUMN bai_owner_mapping.bank IS 'Bank identifier (RABOBANK, BNP, ING, etc.)';
COMMENT ON COLUMN bai_owner_mapping.iban IS 'IBAN account number';
COMMENT ON COLUMN bai_owner_mapping.owner_name IS 'Custom display name for reporting';
COMMENT ON COLUMN bai_owner_mapping.team IS 'Team or department responsible for this account';
COMMENT ON COLUMN bai_owner_mapping.purpose IS 'Description of account purpose or usage';
COMMENT ON COLUMN bai_owner_mapping.is_active IS 'Flag to enable/disable mapping without deletion';



-- ==============================================================================
-- SAMPLE DATA INSERT (for testing purposes)
-- ==============================================================================

-- Insert actual Center Parcs account mappings
INSERT INTO bai_owner_mapping (bank, iban, owner_name, team, purpose) VALUES
('RABOBANK', 'NL90RABO0100925189', 'Center Parcs NL Holding BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL50RABO0101000502', 'Center Parcs Germany Holding BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL30RABO0100929567', 'Center Parcs Development BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL94RABO0123735637', 'CP Participations BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL31RABO0300087233', 'Center Parcs Europe BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL48RABO0300002343', 'Center Parcs Europe BV inz Verhuur', 'Treasury', 'Reporting'),
('RABOBANK', 'NL98RABO0300031319', 'Center Parcs Europe BV inz Accounts Receivable', 'Treasury', 'Reporting'),
('RABOBANK', 'NL95RABO0320289338', 'Center Parcs Europe BV inz Accounting', 'Treasury', 'Reporting'),
('RABOBANK', 'NL06RABO0376883650', 'Center Parcs Europe B.V. inz Cash Collateral IPP', 'Treasury', 'Reporting'),
('RABOBANK', 'NL11RABO0369111435', 'Center Parcs Europe BV inz Collateral', 'Treasury', 'Reporting'),
('RABOBANK', 'NL12RABO0330208888', 'Center Parcs Europe BV inz App BNG', 'Treasury', 'Reporting'),
('RABOBANK', 'NL18RABO0144437678', 'Sunparks BV inz Verhuur', 'Treasury', 'Reporting'),
('RABOBANK', 'NL59RABO0144437813', 'Sunparks BV inz Business to Business', 'Treasury', 'Reporting'),
('RABOBANK', 'NL35RABO0100924530', 'Center Parcs Netherlands BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL51RABO0300002386', 'Center Parcs Netherlands BV inz Meerdal', 'Treasury', 'Reporting'),
('RABOBANK', 'NL29RABO0300002394', 'Center Parcs Netherlands BV inz Huttenheugte', 'Treasury', 'Reporting'),
('RABOBANK', 'NL39RABO0300002408', 'Center Parcs Netherlands BV inz Kempervennen', 'Treasury', 'Reporting'),
('RABOBANK', 'NL70RABO0300002432', 'Center Parcs Netherlands BV inz Heijderbos', 'Treasury', 'Reporting'),
('RABOBANK', 'NL92RABO0300002424', 'Center Parcs Netherlands BV inz Eemhof', 'Treasury', 'Reporting'),
('RABOBANK', 'NL06RABO0100902936', 'Center Parcs Netherlands BV inz Port Zelande', 'Treasury', 'Reporting'),
('RABOBANK', 'NL84RABO0100902987', 'Center Parcs Netherlands BV inz Park Zandvoort', 'Treasury', 'Reporting'),
('RABOBANK', 'NL37RABO0100902960', 'Center Parcs Netherlands BV inz Limburgse Peel', 'Treasury', 'Reporting'),
('RABOBANK', 'NL08RABO0100929575', 'Pierre & Vacances Center Parcs Vastgoed BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL65RABO0306817063', 'PV-CP China Holding BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL49RABO0101012063', 'PV-CP Distribution SA', 'Treasury', 'Reporting'),
('RABOBANK', 'NL14RABO0118337572', 'Sandur Vastgoed BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL19RABO0364594292', 'Sandur Exploitatie BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL44RABO3461512648', 'Sandur Vastgoed BV', 'Treasury', 'Reporting'),
('RABOBANK', 'NL32RABO0189753498', 'Vacansoleil Maeva BV', 'Treasury', 'Reporting');

-- ==============================================================================
-- USEFUL QUERIES FOR MAINTENANCE
-- ==============================================================================

-- View to join with existing account info (Rabobank example)
CREATE OR REPLACE VIEW v_enhanced_rabobank_account_info AS
SELECT 
    brai.iban,
    'RABOBANK' AS bank,
    COALESCE(bom.owner_name, brai.owner_name) AS display_owner_name,
    brai.owner_name AS original_owner_name,
    bom.team,
    bom.purpose,
    brai.currency,
    brai.bic,
    brai.account_name,
    bom.is_active AS has_custom_mapping
FROM bai_rabobank_account_info brai
LEFT JOIN bai_owner_mapping bom 
    ON brai.iban = bom.iban 
    AND bom.bank = 'RABOBANK'
    AND bom.is_active = TRUE;

COMMENT ON VIEW v_enhanced_rabobank_account_info IS 'Enhanced view combining Rabobank account info with custom owner mappings';

-- ==============================================================================
-- MAINTENANCE PROCEDURES
-- ==============================================================================

-- Function to add or update owner mapping
CREATE OR REPLACE FUNCTION upsert_owner_mapping(
    p_bank VARCHAR(50),
    p_iban VARCHAR(34),
    p_owner_name VARCHAR(255),
    p_team VARCHAR(100) DEFAULT NULL,
    p_purpose VARCHAR(500) DEFAULT NULL
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO bai_owner_mapping (bank, iban, owner_name, team, purpose)
    VALUES (p_bank, p_iban, p_owner_name, p_team, p_purpose)
    ON CONFLICT (bank, iban) 
    DO UPDATE SET 
        owner_name = EXCLUDED.owner_name,
        team = EXCLUDED.team,
        purpose = EXCLUDED.purpose,
        is_active = TRUE;
END;
$$ LANGUAGE plpgsql;

-- Function to deactivate mapping (soft delete)
CREATE OR REPLACE FUNCTION deactivate_owner_mapping(
    p_bank VARCHAR(50),
    p_iban VARCHAR(34)
)
RETURNS BOOLEAN AS $$
DECLARE
    rows_affected INTEGER;
BEGIN
    UPDATE bai_owner_mapping 
    SET is_active = FALSE
    WHERE bank = p_bank AND iban = p_iban;
    
    GET DIAGNOSTICS rows_affected = ROW_COUNT;
    RETURN rows_affected > 0;
END;
$$ LANGUAGE plpgsql;

-- ==============================================================================
-- EXAMPLE USAGE
-- ==============================================================================

-- Add or update a mapping
-- SELECT upsert_owner_mapping('RABOBANK', 'NL31RABO0300087233', 'Custom Name', 'Finance', 'Main account');
-- SELECT upsert_owner_mapping('BNP', 'FR7630004123459876543219870', 'BNP Custom Name', 'Finance', 'BNP account');

-- Deactivate a mapping
-- SELECT deactivate_owner_mapping('RABOBANK', 'NL31RABO0300087233');
-- SELECT deactivate_owner_mapping('BNP', 'FR7630004123459876543219870');

-- Query with enhanced names for reporting
-- SELECT * FROM v_enhanced_rabobank_account_info WHERE team = 'Finance Team';
-- SELECT * FROM bai_owner_mapping WHERE bank = 'BNP' AND is_active = TRUE;

-- ==============================================================================
-- GRANTS (adjust based on your user permissions)
-- ==============================================================================

-- Grant permissions (uncomment and adjust usernames as needed)
-- GRANT SELECT, INSERT, UPDATE, DELETE ON bai_owner_mapping TO your_app_user;
-- GRANT USAGE, SELECT ON SEQUENCE bai_owner_mapping_id_seq TO your_app_user;
-- GRANT SELECT ON v_enhanced_rabobank_account_info TO your_reporting_user;

PRINT 'BAI Multi-Bank Owner Mapping table created successfully!';