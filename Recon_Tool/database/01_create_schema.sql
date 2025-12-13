-- =====================================================
-- Reconciliation Tool - Database Schema
-- PostgreSQL with Table Partitioning
-- =====================================================

-- Use existing rpa_data schema
-- Tables will be prefixed with recon_

-- =====================================================
-- 1. WORLDLINE PAYMENTS TABLE (Partitioned)
-- =====================================================

-- Main partitioned table
CREATE TABLE IF NOT EXISTS rpa_data.recon_worldline_payments (
    id VARCHAR(50),
    ref VARCHAR(100),
    "order" VARCHAR(50),
    status VARCHAR(10),
    lib VARCHAR(100),
    accept VARCHAR(50),
    ncid VARCHAR(50),
    ncster VARCHAR(50),
    paydate DATE NOT NULL,  -- Partition key
    cie VARCHAR(50),
    facname1 VARCHAR(200),
    country VARCHAR(10),
    total NUMERIC(18,2),
    cur VARCHAR(10),
    method VARCHAR(50),
    brand VARCHAR(50),
    card VARCHAR(50),
    expdate VARCHAR(10),
    uid VARCHAR(50),
    struct VARCHAR(50),
    fileid VARCHAR(50),
    action VARCHAR(50),
    ticket TEXT,
    "desc" TEXT,
    ship NUMERIC(18,2),
    tax NUMERIC(18,2),
    userid VARCHAR(100),
    merchref VARCHAR(100),
    refid VARCHAR(100),
    refkind VARCHAR(50),
    eci VARCHAR(10),
    cccty VARCHAR(10),
    ipcty VARCHAR(10),
    cvccheck VARCHAR(10),
    aavcheck VARCHAR(10),
    vc VARCHAR(10),
    batchref VARCHAR(100),
    owner VARCHAR(200),
    alias VARCHAR(200),
    fraud_type VARCHAR(50),
    bincard VARCHAR(20),
    rec_ipaddr VARCHAR(50),
    paydatetime TIMESTAMP,
    orderdatetime TIMESTAMP,
    subbrand VARCHAR(50),
    
    -- Metadata
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    source_file VARCHAR(255),
    
    -- Composite primary key including partition key
    PRIMARY KEY (id, paydate)
) PARTITION BY RANGE (paydate);

-- Create indexes on the partitioned table
CREATE INDEX IF NOT EXISTS idx_recon_worldline_paydate ON rpa_data.recon_worldline_payments(paydate);
CREATE INDEX IF NOT EXISTS idx_recon_worldline_ref ON rpa_data.recon_worldline_payments(ref);
CREATE INDEX IF NOT EXISTS idx_recon_worldline_order ON rpa_data.recon_worldline_payments("order");
CREATE INDEX IF NOT EXISTS idx_recon_worldline_batchref ON rpa_data.recon_worldline_payments(batchref);
CREATE INDEX IF NOT EXISTS idx_recon_worldline_brand ON rpa_data.recon_worldline_payments(brand);
CREATE INDEX IF NOT EXISTS idx_recon_worldline_merchref ON rpa_data.recon_worldline_payments(merchref);
CREATE INDEX IF NOT EXISTS idx_recon_worldline_total ON rpa_data.recon_worldline_payments(total);
CREATE INDEX IF NOT EXISTS idx_recon_worldline_status ON rpa_data.recon_worldline_payments(status);

-- Comment
COMMENT ON TABLE rpa_data.recon_worldline_payments IS 'Worldline payment transactions, partitioned by payment date (PAYDATE) for optimal performance with 18+ months of data';

-- =====================================================
-- 2. WORLDLINE PAYMENTS ARCHIVE TABLE
-- =====================================================

CREATE TABLE IF NOT EXISTS rpa_data.recon_worldline_payments_archive (
    LIKE rpa_data.recon_worldline_payments INCLUDING ALL
);

COMMENT ON TABLE rpa_data.recon_worldline_payments_archive IS 'Archive table for Worldline payments older than retention period';

-- =====================================================
-- 3. DATA SOURCES TABLE
-- =====================================================

CREATE TABLE IF NOT EXISTS rpa_data.recon_data_sources (
    source_id SERIAL PRIMARY KEY,
    source_name VARCHAR(100) UNIQUE NOT NULL,
    source_type VARCHAR(50) NOT NULL,  -- 'WORLDLINE', 'BANK', 'RESERVATION', 'SAP'
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

COMMENT ON TABLE rpa_data.recon_data_sources IS 'Registry of all data sources used in reconciliation';

-- Insert initial data sources
INSERT INTO rpa_data.recon_data_sources (source_name, source_type, description) VALUES
    ('Worldline', 'WORLDLINE', 'Worldline payment gateway transactions'),
    ('Bank Statements', 'BANK', 'Bank account statements for reconciliation'),
    ('Reservation System', 'RESERVATION', 'Booking and reservation data'),
    ('SAP', 'SAP', 'SAP ERP financial data')
ON CONFLICT (source_name) DO NOTHING;

-- =====================================================
-- 4. RECONCILIATION RULES TABLE
-- =====================================================

CREATE TABLE IF NOT EXISTS rpa_data.recon_reconciliation_rules (
    rule_id SERIAL PRIMARY KEY,
    rule_name VARCHAR(100) UNIQUE NOT NULL,
    source_a_id INTEGER REFERENCES rpa_data.recon_data_sources(source_id),
    source_b_id INTEGER REFERENCES rpa_data.recon_data_sources(source_id),
    matching_criteria JSONB NOT NULL,  -- Store matching logic as JSON
    tolerance_amount NUMERIC(18,2) DEFAULT 0.00,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

COMMENT ON TABLE rpa_data.recon_reconciliation_rules IS 'Configurable rules for matching transactions across different data sources';
COMMENT ON COLUMN rpa_data.recon_reconciliation_rules.matching_criteria IS 'JSON structure defining matching fields: e.g., {"ref": true, "paydate": true, "total": true, "brand": true}';

-- Insert default Worldline matching rule
INSERT INTO rpa_data.recon_reconciliation_rules (rule_name, source_a_id, source_b_id, matching_criteria, tolerance_amount) VALUES
    ('Worldline-Bank Basic Match', 
     (SELECT source_id FROM rpa_data.recon_data_sources WHERE source_name = 'Worldline'),
     (SELECT source_id FROM rpa_data.recon_data_sources WHERE source_name = 'Bank Statements'),
     '{"fields": ["ref", "paydate", "brand", "merchref"], "match_type": "exact", "tolerance_days": 1}'::jsonb,
     0.01)
ON CONFLICT (rule_name) DO NOTHING;

-- =====================================================
-- 5. RECONCILIATION MATCHES TABLE
-- =====================================================

CREATE TABLE IF NOT EXISTS rpa_data.recon_reconciliation_matches (
    match_id SERIAL PRIMARY KEY,
    rule_id INTEGER REFERENCES rpa_data.recon_reconciliation_rules(rule_id),
    source_a_id INTEGER REFERENCES rpa_data.recon_data_sources(source_id),
    source_b_id INTEGER REFERENCES rpa_data.recon_data_sources(source_id),
    source_a_record_id VARCHAR(200),  -- ID from source A (e.g., worldline_payments.id)
    source_b_record_id VARCHAR(200),  -- ID from source B
    match_confidence NUMERIC(5,2),    -- Percentage (0-100)
    match_type VARCHAR(50),           -- 'EXACT', 'PARTIAL', 'MANUAL'
    matched_fields JSONB,             -- Which fields matched
    amount_difference NUMERIC(18,2),
    matched_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    matched_by VARCHAR(100),          -- Username who confirmed match
    notes TEXT
);

CREATE INDEX IF NOT EXISTS idx_recon_matches_source_a ON rpa_data.recon_reconciliation_matches(source_a_id, source_a_record_id);
CREATE INDEX IF NOT EXISTS idx_recon_matches_source_b ON rpa_data.recon_reconciliation_matches(source_b_id, source_b_record_id);
CREATE INDEX IF NOT EXISTS idx_recon_matches_rule ON rpa_data.recon_reconciliation_matches(rule_id);
CREATE INDEX IF NOT EXISTS idx_recon_matches_confidence ON rpa_data.recon_reconciliation_matches(match_confidence);

COMMENT ON TABLE rpa_data.recon_reconciliation_matches IS 'Successfully matched transactions across different data sources';

-- =====================================================
-- 6. RECONCILIATION EXCEPTIONS TABLE
-- =====================================================

CREATE TABLE IF NOT EXISTS rpa_data.recon_reconciliation_exceptions (
    exception_id SERIAL PRIMARY KEY,
    source_id INTEGER REFERENCES rpa_data.recon_data_sources(source_id),
    record_id VARCHAR(200),
    exception_type VARCHAR(50),       -- 'UNMATCHED', 'DUPLICATE', 'AMOUNT_MISMATCH', 'DATE_MISMATCH'
    exception_date DATE,
    amount NUMERIC(18,2),
    currency VARCHAR(10),
    description TEXT,
    status VARCHAR(50) DEFAULT 'OPEN',  -- 'OPEN', 'INVESTIGATING', 'RESOLVED', 'ACCEPTED'
    assigned_to VARCHAR(100),
    resolved_at TIMESTAMP,
    resolved_by VARCHAR(100),
    resolution_notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_recon_exceptions_source ON rpa_data.recon_reconciliation_exceptions(source_id, record_id);
CREATE INDEX IF NOT EXISTS idx_recon_exceptions_type ON rpa_data.recon_reconciliation_exceptions(exception_type);
CREATE INDEX IF NOT EXISTS idx_recon_exceptions_status ON rpa_data.recon_reconciliation_exceptions(status);
CREATE INDEX IF NOT EXISTS idx_recon_exceptions_date ON rpa_data.recon_reconciliation_exceptions(exception_date);

COMMENT ON TABLE rpa_data.recon_reconciliation_exceptions IS 'Unmatched transactions and reconciliation discrepancies';

-- =====================================================
-- 7. FILE IMPORT LOG TABLE
-- =====================================================

CREATE TABLE IF NOT EXISTS rpa_data.recon_file_import_log (
    import_id SERIAL PRIMARY KEY,
    source_id INTEGER REFERENCES rpa_data.recon_data_sources(source_id),
    filename VARCHAR(255),
    file_size_bytes BIGINT,
    records_total INTEGER,
    records_imported INTEGER,
    records_failed INTEGER,
    records_duplicate INTEGER,
    import_status VARCHAR(50),  -- 'SUCCESS', 'PARTIAL', 'FAILED'
    error_message TEXT,
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP,
    imported_by VARCHAR(100)
);

CREATE INDEX IF NOT EXISTS idx_recon_import_log_source ON rpa_data.recon_file_import_log(source_id);
CREATE INDEX IF NOT EXISTS idx_recon_import_log_status ON rpa_data.recon_file_import_log(import_status);

COMMENT ON TABLE rpa_data.recon_file_import_log IS 'Audit trail for CSV file imports';

-- =====================================================
-- 8. UTILITY FUNCTIONS
-- =====================================================

-- Function to create monthly partitions
CREATE OR REPLACE FUNCTION rpa_data.recon_create_worldline_partition(partition_date DATE)
RETURNS TEXT AS $$
DECLARE
    partition_name TEXT;
    start_date DATE;
    end_date DATE;
BEGIN
    -- Calculate partition boundaries (monthly)
    start_date := DATE_TRUNC('month', partition_date);
    end_date := start_date + INTERVAL '1 month';
    
    -- Generate partition name
    partition_name := 'recon_worldline_payments_' || TO_CHAR(start_date, 'YYYY_MM');
    
    -- Create partition if it doesn't exist
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS rpa_data.%I PARTITION OF rpa_data.recon_worldline_payments
         FOR VALUES FROM (%L) TO (%L)',
        partition_name, start_date, end_date
    );
    
    RETURN partition_name;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION rpa_data.recon_create_worldline_partition IS 'Creates a monthly partition for recon_worldline_payments table';

-- Function to archive old partitions
CREATE OR REPLACE FUNCTION rpa_data.recon_archive_old_partitions(retention_months INTEGER DEFAULT 18)
RETURNS TABLE(partition_name TEXT, records_archived BIGINT) AS $$
DECLARE
    partition_record RECORD;
    cutoff_date DATE;
    records_count BIGINT;
BEGIN
    cutoff_date := DATE_TRUNC('month', CURRENT_DATE) - (retention_months || ' months')::INTERVAL;
    
    FOR partition_record IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'rpa_data'
          AND tablename LIKE 'recon_worldline_payments_%'
          AND tablename != 'recon_worldline_payments_archive'
    LOOP
        -- Extract date from partition name (format: recon_worldline_payments_YYYY_MM)
        DECLARE
            partition_date DATE;
            year_part TEXT;
            month_part TEXT;
        BEGIN
            year_part := SUBSTRING(partition_record.tablename FROM 'recon_worldline_payments_(\d{4})_\d{2}');
            month_part := SUBSTRING(partition_record.tablename FROM 'recon_worldline_payments_\d{4}_(\d{2})');
            
            IF year_part IS NOT NULL AND month_part IS NOT NULL THEN
                partition_date := TO_DATE(year_part || '-' || month_part || '-01', 'YYYY-MM-DD');
                
                IF partition_date < cutoff_date THEN
                    -- Archive data
                    EXECUTE format(
                        'INSERT INTO rpa_data.recon_worldline_payments_archive SELECT * FROM rpa_data.%I',
                        partition_record.tablename
                    );
                    
                    GET DIAGNOSTICS records_count = ROW_COUNT;
                    
                    -- Drop partition
                    EXECUTE format('DROP TABLE IF EXISTS rpa_data.%I', partition_record.tablename);
                    
                    partition_name := partition_record.tablename;
                    records_archived := records_count;
                    RETURN NEXT;
                END IF;
            END IF;
        END;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION rpa_data.recon_archive_old_partitions IS 'Archives partitions older than specified retention period';
