-- =====================================================
-- Create Initial Partitions
-- Run after 01_create_schema.sql
-- =====================================================

-- Create partitions for the last 24 months and next 6 months
DO $$
DECLARE
    current_month DATE;
    i INTEGER;
    partition_date DATE;
BEGIN
    -- Start from 24 months ago
    current_month := DATE_TRUNC('month', CURRENT_DATE) - INTERVAL '24 months';
    
    -- Create 30 partitions (24 past + 6 future)
    FOR i IN 0..29 LOOP
        partition_date := (current_month + (i || ' months')::INTERVAL)::DATE;
        PERFORM rpa_data.recon_create_worldline_partition(partition_date);
    END LOOP;
    
    RAISE NOTICE 'Created 30 partitions from % to %', 
        current_month, 
        current_month + INTERVAL '29 months';
END $$;
