-- ============================================================================
-- Migration Script: Partition bai_rabobank_transactions by booking_date
-- 
-- Purpose: Convert existing table to monthly partitioned table
-- Test on: ACCEPT environment first
-- Target: Production (after successful accept test)
--
-- Estimated downtime: ~30 seconds (for table rename)
-- Rollback: See rollback section at bottom
-- ============================================================================

-- Set schema
SET search_path TO rpa_data;

-- ============================================================================
-- STEP 1: BACKUP EXISTING TABLE
-- ============================================================================
BEGIN;

-- Create backup of existing table
CREATE TABLE bai_rabobank_transactions_backup_20251223 AS 
SELECT * FROM bai_rabobank_transactions;

COMMENT ON TABLE bai_rabobank_transactions_backup_20251223 IS 
'Backup before partitioning migration - Safe to drop after verification';

COMMIT;

SELECT 
    'Backup created: ' || COUNT(*) || ' rows backed up' as status
FROM bai_rabobank_transactions_backup_20251223;


-- ============================================================================
-- STEP 2: GET CURRENT TABLE STRUCTURE
-- ============================================================================

-- First, let's see the current table structure
-- RUN THIS MANUALLY TO VERIFY:
\d rpa_data.bai_rabobank_transactions

-- Get indexes
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'rpa_data' 
  AND tablename = 'bai_rabobank_transactions';

-- Get constraints
SELECT 
    conname,
    pg_get_constraintdef(oid) as definition
FROM pg_constraint
WHERE conrelid = 'rpa_data.bai_rabobank_transactions'::regclass;


-- ============================================================================
-- STEP 3: CREATE PARTITIONED TABLE
-- ============================================================================

BEGIN;

-- Rename existing table
ALTER TABLE bai_rabobank_transactions RENAME TO bai_rabobank_transactions_old;

-- Create new partitioned table based on actual table structure
CREATE TABLE bai_rabobank_transactions (
    id SERIAL,
    external_audit_id VARCHAR(255),
    audit_id VARCHAR(255) NOT NULL,
    attempt_nr INTEGER DEFAULT 1,
    iban VARCHAR(34) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    booking_date DATE NOT NULL,
    entry_reference VARCHAR(255) NOT NULL,
    transaction_amount NUMERIC(15,2) NOT NULL,
    transaction_currency VARCHAR(3) NOT NULL,
    bank_transaction_code VARCHAR(50) NOT NULL,
    value_date DATE,
    end_to_end_id VARCHAR(255),
    batch_entry_reference VARCHAR(255),
    acctsvcr_ref VARCHAR(255),
    instruction_id VARCHAR(255),
    interbank_settlement_date DATE,
    debtor_iban VARCHAR(34),
    debtor_name VARCHAR(255),
    debtor_agent_bic VARCHAR(11),
    creditor_iban VARCHAR(34),
    creditor_name VARCHAR(255),
    creditor_agent_bic VARCHAR(11),
    creditor_currency VARCHAR(3),
    creditor_id VARCHAR(255),
    ultimate_debtor VARCHAR(255),
    ultimate_creditor VARCHAR(255),
    initiating_party_name VARCHAR(255),
    mandate_id VARCHAR(255),
    remittance_information_unstructured TEXT,
    remittance_information_structured TEXT,
    purpose_code VARCHAR(10),
    reason_code VARCHAR(10),
    payment_information_identification VARCHAR(255),
    number_of_transactions INTEGER,
    currency_exchange_rate NUMERIC(10,6),
    currency_exchange_source_currency VARCHAR(3),
    currency_exchange_target_currency VARCHAR(3),
    instructed_amount NUMERIC(15,2),
    instructed_amount_currency VARCHAR(3),
    rabo_booking_datetime TIMESTAMP(6) WITH TIME ZONE NOT NULL,
    rabo_detailed_transaction_type VARCHAR(10) NOT NULL,
    rabo_transaction_type_name VARCHAR(10),
    balance_after_booking_amount NUMERIC(15,2),
    balance_after_booking_currency VARCHAR(3),
    balance_after_booking_type VARCHAR(50),
    source_system VARCHAR(50) DEFAULT 'BAI_API',
    created_at TIMESTAMP(6) WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP(6) WITH TIME ZONE DEFAULT NOW(),
    retrieved_at TIMESTAMP(6) WITH TIME ZONE DEFAULT NOW(),
    
    PRIMARY KEY (id, booking_date)  -- Partition key MUST be in PK
) PARTITION BY RANGE (booking_date);

COMMENT ON TABLE bai_rabobank_transactions IS 
'Rabobank transactions - Partitioned by booking_date (monthly)';

COMMIT;


-- ============================================================================
-- STEP 4: CREATE PARTITIONS FOR EXISTING DATA
-- ============================================================================

-- Get date range of existing data
SELECT 
    MIN(booking_date) as min_date,
    MAX(booking_date) as max_date,
    DATE_TRUNC('month', MIN(booking_date)) as first_partition,
    DATE_TRUNC('month', MAX(booking_date)) + INTERVAL '1 month' as last_partition
FROM bai_rabobank_transactions_old;

-- Create partitions (adjust dates based on your data!)
-- Current date: December 2025

BEGIN;

-- November 2025
CREATE TABLE IF NOT EXISTS bai_rabobank_transactions_2025_11 
PARTITION OF bai_rabobank_transactions
FOR VALUES FROM ('2025-11-01') TO ('2025-12-01');

-- December 2025
CREATE TABLE IF NOT EXISTS bai_rabobank_transactions_2025_12 
PARTITION OF bai_rabobank_transactions
FOR VALUES FROM ('2025-12-01') TO ('2026-01-01');

-- January 2026 (future)
CREATE TABLE IF NOT EXISTS bai_rabobank_transactions_2026_01 
PARTITION OF bai_rabobank_transactions
FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');

-- February 2026 (future)
CREATE TABLE IF NOT EXISTS bai_rabobank_transactions_2026_02 
PARTITION OF bai_rabobank_transactions
FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');

-- March 2026 (future)
CREATE TABLE IF NOT EXISTS bai_rabobank_transactions_2026_03 
PARTITION OF bai_rabobank_transactions
FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');

COMMIT;

-- TODO: Add more partitions based on your actual date range


-- ============================================================================
-- STEP 5: MIGRATE DATA
-- ============================================================================

BEGIN;

-- Copy data from old table to new partitioned table
-- This will automatically route to correct partitions
INSERT INTO bai_rabobank_transactions 
SELECT * FROM bai_rabobank_transactions_old;

COMMIT;

-- Verify data count
SELECT 
    'Old table: ' || COUNT(*) as old_count
FROM bai_rabobank_transactions_old
UNION ALL
SELECT 
    'New table: ' || COUNT(*) as new_count
FROM bai_rabobank_transactions;


-- ============================================================================
-- STEP 6: RECREATE INDEXES
-- ============================================================================

-- Based on your pg_indexes output, recreate indexes
-- Adjusted for actual table structure

BEGIN;

-- Index on IBAN (most common query for account-specific transactions)
CREATE INDEX IF NOT EXISTS idx_bai_transactions_iban 
ON bai_rabobank_transactions(iban);

-- Index on audit_id (NOT NULL field, likely used for tracking)
CREATE INDEX IF NOT EXISTS idx_bai_transactions_audit_id 
ON bai_rabobank_transactions(audit_id);

-- Index on entry_reference (NOT NULL field, unique transaction reference)
CREATE INDEX IF NOT EXISTS idx_bai_transactions_entry_reference 
ON bai_rabobank_transactions(entry_reference);

-- Index on end_to_end_id (used for transaction reconciliation)
CREATE INDEX IF NOT EXISTS idx_bai_transactions_end_to_end_id 
ON bai_rabobank_transactions(end_to_end_id);

-- Index on rabo_booking_datetime (for timestamp-based queries)
CREATE INDEX IF NOT EXISTS idx_bai_transactions_rabo_booking_datetime 
ON bai_rabobank_transactions(rabo_booking_datetime);

-- Index on created_at (for recent data queries)
CREATE INDEX IF NOT EXISTS idx_bai_transactions_created_at 
ON bai_rabobank_transactions(created_at);

-- Composite index for common IBAN + date queries
CREATE INDEX IF NOT EXISTS idx_bai_transactions_iban_booking_date 
ON bai_rabobank_transactions(iban, booking_date);

-- Composite index for debtor/creditor searches
CREATE INDEX IF NOT EXISTS idx_bai_transactions_debtor_creditor 
ON bai_rabobank_transactions(debtor_iban, creditor_iban);

COMMIT;


-- ============================================================================
-- STEP 7: RECREATE CONSTRAINTS & TRIGGERS
-- ============================================================================

BEGIN;

-- Add any foreign keys, unique constraints, check constraints
-- Based on output from STEP 2

-- Example:
-- ALTER TABLE bai_rabobank_transactions 
-- ADD CONSTRAINT unique_transaction_id UNIQUE (transaction_id);

-- Recreate any triggers if they exist

COMMIT;


-- ============================================================================
-- STEP 8: UPDATE PERMISSIONS
-- ============================================================================

-- Grant same permissions as old table
-- First check which roles have access to the old table:
SELECT 
    grantee,
    privilege_type
FROM information_schema.role_table_grants
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions_old';

-- Then grant same permissions to new table
-- Example (adjust based on output above):
-- GRANT SELECT, INSERT, UPDATE, DELETE ON bai_rabobank_transactions TO your_app_user;
-- GRANT SELECT ON bai_rabobank_transactions TO your_readonly_user;


-- ============================================================================
-- STEP 9: VERIFY MIGRATION
-- ============================================================================

-- Check row counts per partition
SELECT 
    tableoid::regclass AS partition_name,
    COUNT(*) as row_count,
    MIN(booking_date) as min_date,
    MAX(booking_date) as max_date
FROM bai_rabobank_transactions
GROUP BY tableoid
ORDER BY partition_name;

-- Compare total counts
SELECT 
    'Old' as table_type, COUNT(*) as total_rows FROM bai_rabobank_transactions_old
UNION ALL
SELECT 
    'New' as table_type, COUNT(*) as total_rows FROM bai_rabobank_transactions;

-- Test query performance
EXPLAIN ANALYZE
SELECT * FROM bai_rabobank_transactions 
WHERE booking_date = CURRENT_DATE - INTERVAL '1 day'
LIMIT 10;

-- Verify indexes exist
SELECT 
    schemaname,
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'rpa_data' 
  AND tablename LIKE 'bai_rabobank_transactions%'
ORDER BY tablename, indexname;


-- ============================================================================
-- STEP 10: CLEANUP (After verification!)
-- ============================================================================

-- DO NOT RUN IMMEDIATELY!
-- Wait 24-48 hours and verify everything works

-- DROP TABLE rpa_data.bai_rabobank_transactions_old;
-- DROP TABLE rpa_data.bai_rabobank_transactions_backup_20251223;


-- ============================================================================
-- BONUS: AUTO-CREATE FUTURE PARTITIONS
-- ============================================================================

-- Function to automatically create next month partition
CREATE OR REPLACE FUNCTION create_next_partition()
RETURNS void AS $$
DECLARE
    next_month DATE;
    partition_name TEXT;
    start_date DATE;
    end_date DATE;
BEGIN
    -- Get first day of next month
    next_month := DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '2 months';
    start_date := next_month;
    end_date := next_month + INTERVAL '1 month';
    
    -- Generate partition name
    partition_name := 'bai_rabobank_transactions_' || TO_CHAR(next_month, 'YYYY_MM');
    
    -- Create partition if not exists
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS rpa_data.%I 
         PARTITION OF rpa_data.bai_rabobank_transactions
         FOR VALUES FROM (%L) TO (%L)',
        partition_name, start_date, end_date
    );
    
    RAISE NOTICE 'Created partition: %', partition_name;
END;
$$ LANGUAGE plpgsql;

-- Schedule this function to run monthly (via cron or pg_cron)
-- Or call it manually before start of new month


-- ============================================================================
-- ROLLBACK PROCEDURE (if something goes wrong)
-- ============================================================================

/*
-- EMERGENCY ROLLBACK:

BEGIN;

-- Drop new partitioned table
DROP TABLE IF EXISTS rpa_data.bai_rabobank_transactions CASCADE;

-- Restore old table
ALTER TABLE rpa_data.bai_rabobank_transactions_old 
RENAME TO bai_rabobank_transactions;

COMMIT;

-- Verify
SELECT COUNT(*) FROM rpa_data.bai_rabobank_transactions;

*/


-- ============================================================================
-- POST-MIGRATION MONITORING
-- ============================================================================

-- Add to OPS dashboard: Check partition fill status
SELECT 
    tableoid::regclass AS partition_name,
    COUNT(*) as row_count,
    pg_size_pretty(pg_total_relation_size(tableoid)) as size
FROM rpa_data.bai_rabobank_transactions
GROUP BY tableoid
ORDER BY partition_name;

-- Check if new partition needed
SELECT 
    MAX(booking_date) as latest_date,
    DATE_TRUNC('month', MAX(booking_date)) + INTERVAL '1 month' as next_partition_needed
FROM rpa_data.bai_rabobank_transactions;


-- ============================================================================
-- NOTES
-- ============================================================================

/*
BEFORE RUNNING ON PRODUCTION:

1. ✅ Test complete script on ACCEPT first
2. ✅ Verify all queries work in app/UiPath after migration
3. ✅ Check performance of common queries
4. ✅ Schedule during low-traffic window
5. ✅ Notify stakeholders of brief downtime
6. ✅ Have rollback plan ready

ESTIMATED TIMELINE:
- Backup: 5 seconds
- Create partitions: 10 seconds  
- Data migration: 20-60 seconds (depends on row count)
- Index creation: 30-60 seconds
- Verification: 10 seconds

TOTAL DOWNTIME: ~2 minutes (most operations can run without blocking reads)

MONITORING AFTER:
- Watch for missing partitions (new months)
- Monitor query performance
- Check autovacuum runs per partition
- Verify backups include all partitions
*/
