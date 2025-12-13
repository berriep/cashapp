-- Fix rabo_booking_datetime precision to support microseconds (6 decimal places)
-- This will preserve the full precision from Rabobank API data

-- STEP 1: Check current precision
SELECT column_name, data_type, datetime_precision 
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name = 'rabo_booking_datetime';

-- STEP 2: Alter column to add microseconds precision
-- WARNING: This requires table lock and could take time on large tables
-- IMPORTANT: Existing data will be preserved, but microseconds will remain 0
--           Only NEW data inserted after this change will have microseconds precision

-- Check table size first (estimate time needed)
SELECT 
    schemaname,
    tablename,
    n_tup_ins as inserts,
    n_tup_upd as updates,
    n_tup_del as deletes,
    n_live_tup as live_rows,
    n_dead_tup as dead_rows,
    pg_size_pretty(pg_total_relation_size('rpa_data.bai_rabobank_transactions')) as total_size
FROM pg_stat_user_tables 
WHERE schemaname = 'rpa_data' AND tablename = 'bai_rabobank_transactions';

-- Execute the ALTER (this will lock the table temporarily)
-- Time estimate: ~1 second per 100k rows (varies by hardware)
ALTER TABLE rpa_data.bai_rabobank_transactions 
ALTER COLUMN rabo_booking_datetime TYPE timestamp(6) with time zone;

-- STEP 3: Verify the change
SELECT column_name, data_type, datetime_precision 
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name = 'rabo_booking_datetime';

-- STEP 4: Test with sample data to verify microseconds are preserved
-- NOTE: Existing data will still show .000000 (no microseconds)
--       Only NEW data inserted after the ALTER will have microseconds
SELECT 
    rabo_booking_datetime,
    rabo_booking_datetime::TEXT AS rabo_booking_datetime_text,
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US TZ') AS rabo_booking_datetime_formatted,
    created_at  -- Check when this record was inserted
FROM rpa_data.bai_rabobank_transactions 
ORDER BY rabo_booking_datetime DESC 
LIMIT 5;

-- Expected output after fix:
-- EXISTING data: rabo_booking_datetime: 2025-10-06 08:12:49.000000+00 (still .000000)
-- NEW data:      rabo_booking_datetime: 2025-10-06 08:12:49.257686+00 (with microseconds)

-- STEP 5: Optional - Update existing data if you have the original source with microseconds
-- WARNING: Only do this if you have access to the original microsecond data
-- Example update (DO NOT RUN without proper data source):
/*
UPDATE rpa_data.bai_rabobank_transactions 
SET rabo_booking_datetime = 'ORIGINAL_TIMESTAMP_WITH_MICROSECONDS'::timestamp(6) with time zone
WHERE conditions_to_identify_specific_records;
*/