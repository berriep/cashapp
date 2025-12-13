-- CLEAN SLATE APPROACH: Backup data and recreate table with proper precision
-- This ensures the table structure is correct from the ground up

-- Step 1: Create backup table with current data
CREATE TABLE rpa_data.bai_rabobank_transactions_backup AS 
SELECT * FROM rpa_data.bai_rabobank_transactions;

-- Step 2: Verify backup was created
SELECT COUNT(*) as backup_record_count 
FROM rpa_data.bai_rabobank_transactions_backup;

-- Step 3: Drop the original table completely
DROP TABLE rpa_data.bai_rabobank_transactions;

-- Step 4: Recreate table with proper timestamp precision from the start
CREATE TABLE rpa_data.bai_rabobank_transactions (
    id SERIAL PRIMARY KEY,
    external_audit_id VARCHAR(255),
    audit_id VARCHAR(255) NOT NULL,
    attempt_nr INTEGER DEFAULT 1,
    
    -- Account info
    iban VARCHAR(34) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    
    -- Transaction basic data
    booking_date DATE NOT NULL,
    entry_reference VARCHAR(255) NOT NULL,
    transaction_amount DECIMAL(15,2) NOT NULL,
    transaction_currency VARCHAR(3) NOT NULL,
    bank_transaction_code VARCHAR(50) NOT NULL,
    
    -- Optional basic fields
    value_date DATE,
    end_to_end_id VARCHAR(255),
    batch_entry_reference VARCHAR(255),
    acctsvcr_ref VARCHAR(255),
    instruction_id VARCHAR(255),
    interbank_settlement_date DATE,
    
    -- Debtor information
    debtor_iban VARCHAR(34),
    debtor_name VARCHAR(255),
    debtor_agent_bic VARCHAR(11),
    
    -- Creditor information
    creditor_iban VARCHAR(34),
    creditor_name VARCHAR(255),
    creditor_agent_bic VARCHAR(11),
    creditor_currency VARCHAR(3),
    creditor_id VARCHAR(255),
    
    -- Ultimate parties
    ultimate_debtor VARCHAR(255),
    ultimate_creditor VARCHAR(255),
    initiating_party_name VARCHAR(255),
    
    -- SEPA fields
    mandate_id VARCHAR(255),
    
    -- Payment information
    remittance_information_unstructured TEXT,
    remittance_information_structured TEXT,
    purpose_code VARCHAR(10),
    reason_code VARCHAR(10),
    
    -- Batch information
    payment_information_identification VARCHAR(255),
    number_of_transactions INTEGER,
    
    -- Currency exchange
    currency_exchange_rate DECIMAL(10,6),
    currency_exchange_source_currency VARCHAR(3),
    currency_exchange_target_currency VARCHAR(3),
    
    -- Instructed amount
    instructed_amount DECIMAL(15,2),
    instructed_amount_currency VARCHAR(3),
    
    -- Rabobank specific - CRITICAL: timestamp(6) with time zone for microseconds!
    rabo_booking_datetime timestamp(6) with time zone NOT NULL,
    rabo_detailed_transaction_type VARCHAR(10) NOT NULL,
    rabo_transaction_type_name VARCHAR(10),
    
    -- Balance after booking
    balance_after_booking_amount DECIMAL(15,2),
    balance_after_booking_currency VARCHAR(3),
    balance_after_booking_type VARCHAR(50),
    
    -- System metadata - all with microsecond precision
    source_system VARCHAR(50) DEFAULT 'BAI_API',
    created_at timestamp(6) with time zone DEFAULT NOW(),
    updated_at timestamp(6) with time zone DEFAULT NOW(),
    retrieved_at timestamp(6) with time zone DEFAULT NOW()
);

-- Step 5: Create indexes for performance
CREATE INDEX idx_bai_transactions_audit_id ON rpa_data.bai_rabobank_transactions(audit_id);
CREATE INDEX idx_bai_transactions_entry_ref ON rpa_data.bai_rabobank_transactions(entry_reference);
CREATE INDEX idx_bai_transactions_booking_date ON rpa_data.bai_rabobank_transactions(booking_date);
CREATE INDEX idx_bai_transactions_rabo_datetime ON rpa_data.bai_rabobank_transactions(rabo_booking_datetime);

-- Step 6: COMMIT the new table structure
COMMIT;

-- Step 7: Verify the new table has correct precision
SELECT 
    column_name,
    data_type,
    datetime_precision,
    is_nullable
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name LIKE '%datetime%'
ORDER BY column_name;

-- Step 8: Test insert with microseconds
INSERT INTO rpa_data.bai_rabobank_transactions 
(audit_id, iban, currency, booking_date, entry_reference, transaction_amount, 
 transaction_currency, bank_transaction_code, rabo_booking_datetime, rabo_detailed_transaction_type)
VALUES 
('TEST_PRECISION', 'NL12RABO0330208888', 'EUR', '2025-08-04', '999999', 
 100.00, 'EUR', 'PMNT-RCDT-ESCT', '2025-08-04T21:58:04.283480Z', '100');

-- Step 9: Verify microseconds are preserved
SELECT 
    entry_reference,
    rabo_booking_datetime,
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US TZ') as formatted_microseconds,
    EXTRACT(MICROSECONDS FROM rabo_booking_datetime) % 1000000 as true_microseconds
FROM rpa_data.bai_rabobank_transactions 
WHERE entry_reference = '999999';

-- Expected result: true_microseconds should show 283480