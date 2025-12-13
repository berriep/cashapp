-- Verify exact precision for entry_reference 474318
SELECT 
    entry_reference,
    rabo_booking_datetime,
    rabo_booking_datetime::TEXT as exact_text,
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US TZ') as formatted_with_microseconds,
    EXTRACT(EPOCH FROM rabo_booking_datetime) as epoch_seconds_with_decimals,
    EXTRACT(MICROSECONDS FROM rabo_booking_datetime) as total_microseconds,
    EXTRACT(MICROSECONDS FROM rabo_booking_datetime) % 1000000 as true_fractional_microseconds,
    created_at
FROM rpa_data.bai_rabobank_transactions 
WHERE entry_reference = '474318'
ORDER BY created_at DESC 
LIMIT 1;

-- Expected: 
-- API had: 2025-08-04T21:58:04.283480Z
-- true_fractional_microseconds should be: 283480
-- If it shows 0 or wrong value, the precision is still being lost somewhere