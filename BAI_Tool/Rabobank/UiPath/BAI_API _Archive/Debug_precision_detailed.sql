-- DETAILED MICROSECOND PRECISION TROUBLESHOOTING
-- Run this to investigate why microseconds are still not being stored

-- 1. Check exact column definition in database
SELECT 
    column_name,
    data_type,
    numeric_precision,
    numeric_scale,
    datetime_precision,
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'transactions'
  AND column_name = 'rabo_booking_datetime';

-- 2. Check PostgreSQL version (affects timestamp precision support)
SELECT version();

-- 3. Show exact data as stored vs. as text for recent records
SELECT 
    external_audit_id,
    rabo_booking_datetime,                                              -- Default output format
    rabo_booking_datetime::TEXT AS exact_stored_value,                  -- Exact string representation
    EXTRACT(EPOCH FROM rabo_booking_datetime) AS epoch_with_decimals,   -- Unix timestamp with microseconds
    LENGTH(SPLIT_PART(rabo_booking_datetime::TEXT, '.', 2)) - 1 AS decimal_places_count,  -- Count decimal places
    created_at
FROM rpa_data.transactions 
WHERE rabo_booking_datetime IS NOT NULL
ORDER BY created_at DESC 
LIMIT 5;

-- 4. Test inserting a microsecond value manually to check if column accepts it
-- (This will only work if you have INSERT permissions)
/*
INSERT INTO rpa_data.transactions 
(external_audit_id, rabo_booking_datetime) 
VALUES 
('TEST_PRECISION_' || EXTRACT(EPOCH FROM NOW()), '2025-01-15T10:30:45.123456+00:00'::TIMESTAMP(6) WITH TIME ZONE);
*/

-- 5. Check if there are any triggers or functions that might modify the data
SELECT 
    trigger_name,
    event_manipulation,
    action_timing,
    action_statement
FROM information_schema.triggers 
WHERE event_object_schema = 'rpa_data' 
  AND event_object_table = 'transactions';

-- 6. Show the actual SQL being used for the last few inserts from application logs
-- (This would require checking application logs or enabling query logging)

-- 7. Compare string values vs. timestamp values for discrepancies
SELECT 
    external_audit_id,
    '2025-10-06T08:12:49.282952Z' AS original_api_value,
    '2025-10-06T08:12:49.282952Z'::TIMESTAMP(6) WITH TIME ZONE AS parsed_timestamp,
    rabo_booking_datetime,
    CASE 
        WHEN rabo_booking_datetime::TEXT LIKE '%.______%' THEN 'Has 6 decimals'
        WHEN rabo_booking_datetime::TEXT LIKE '%.____%' THEN 'Has 4-5 decimals'  
        WHEN rabo_booking_datetime::TEXT LIKE '%.__%' THEN 'Has 2-3 decimals'
        WHEN rabo_booking_datetime::TEXT LIKE '%.%' THEN 'Has 1 decimal'
        ELSE 'No decimals (seconds only)'
    END AS precision_analysis
FROM rpa_data.transactions 
WHERE rabo_booking_datetime IS NOT NULL
ORDER BY created_at DESC 
LIMIT 10;