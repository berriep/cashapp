-- Debug script to investigate rabo_booking_datetime precision issues
-- Run this to diagnose the problem

-- 1. Check if the column precision was actually changed
SELECT 
    column_name, 
    data_type, 
    datetime_precision,
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name = 'rabo_booking_datetime';

-- Expected result after fix: datetime_precision should be 6

-- 2. Check recent inserts vs older data
SELECT 
    'Recent Data (should have microseconds)' as data_type,
    rabo_booking_datetime,
    rabo_booking_datetime::TEXT as full_text,
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US TZ') as formatted,
    created_at,
    audit_id
FROM rpa_data.bai_rabobank_transactions 
WHERE created_at > NOW() - INTERVAL '2 hours'  -- Recent data
ORDER BY created_at DESC 
LIMIT 5;

-- 3. Check older data (should still be .000000)
SELECT 
    'Old Data (will still be .000000)' as data_type,
    rabo_booking_datetime,
    rabo_booking_datetime::TEXT as full_text,
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US TZ') as formatted,
    created_at,
    audit_id
FROM rpa_data.bai_rabobank_transactions 
WHERE created_at < NOW() - INTERVAL '1 day'  -- Older data
ORDER BY created_at DESC 
LIMIT 5;

-- 4. Check the most recent audit_id to see raw insert
SELECT 
    audit_id,
    COUNT(*) as transaction_count,
    MIN(rabo_booking_datetime) as earliest_rabo_time,
    MAX(rabo_booking_datetime) as latest_rabo_time,
    MAX(created_at) as inserted_at
FROM rpa_data.bai_rabobank_transactions 
GROUP BY audit_id 
ORDER BY MAX(created_at) DESC 
LIMIT 3;

-- 5. Look for specific microsecond patterns in recent data
SELECT 
    rabo_booking_datetime,
    EXTRACT(MICROSECONDS FROM rabo_booking_datetime) as microseconds_part,
    created_at
FROM rpa_data.bai_rabobank_transactions 
WHERE created_at > NOW() - INTERVAL '4 hours'
  AND EXTRACT(MICROSECONDS FROM rabo_booking_datetime) > 0  -- Only show non-zero microseconds
LIMIT 10;

-- If this returns no results, microseconds are still being lost somewhere