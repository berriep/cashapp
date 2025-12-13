-- Live Test: Verify raboBookingDateTime precision after database fix
-- Purpose: Test if microseconds are now correctly stored with timestamp(6) column

-- Test 1: Check current column definition
SELECT 
    table_name,
    column_name,
    data_type,
    numeric_precision,
    numeric_scale,
    datetime_precision,
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'bai_rabobank_transactions' 
AND column_name = 'rabo_booking_datetime';

-- Test 2: Check latest entries for microsecond precision
SELECT 
    entry_reference,
    rabo_booking_datetime,
    EXTRACT(MICROSECONDS FROM rabo_booking_datetime) as microseconds_extracted,
    created_at,
    EXTRACT(MICROSECONDS FROM created_at) as created_microseconds
FROM bai_rabobank_transactions 
ORDER BY created_at DESC 
LIMIT 10;

-- Test 3: Look for recent entries that might have microseconds
-- (entries inserted after the database fix)
SELECT 
    entry_reference,
    rabo_booking_datetime,
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US') as formatted_with_microseconds,
    created_at
FROM bai_rabobank_transactions 
WHERE created_at >= '2025-11-03 14:00:00'  -- Today after we made the fix
ORDER BY created_at DESC 
LIMIT 5;

-- Test 4: Manual insert test with exact API data to verify precision works
INSERT INTO bai_rabobank_transactions 
(
    audit_id, 
    attempt_nr,
    iban, 
    currency,
    booking_date,
    entry_reference,
    transaction_amount,
    transaction_currency,
    bank_transaction_code,
    rabo_booking_datetime,
    rabo_detailed_transaction_type,
    source_system
)
VALUES 
(
    'PRECISION_TEST_' || EXTRACT(EPOCH FROM NOW()),
    1,
    'NL70RABO0300002432',
    'EUR',
    '2025-08-04',
    'TEST_PRECISION_' || EXTRACT(EPOCH FROM NOW()),
    100.00,
    'EUR',
    'PMNT-ICCN-ICCT',
    '2025-08-04T15:00:36.584509Z',  -- Exact value from API response
    '625',
    'PRECISION_TEST'
);

-- Test 5: Verify the test insert has correct microseconds
SELECT 
    entry_reference,
    rabo_booking_datetime,
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US') as formatted_microseconds,
    EXTRACT(MICROSECONDS FROM rabo_booking_datetime) as extracted_microseconds
FROM bai_rabobank_transactions 
WHERE source_system = 'PRECISION_TEST'
ORDER BY created_at DESC 
LIMIT 1;

-- Test 6: Compare with manual precision test
-- Expected result: 584509 microseconds (from .584509Z)
-- If we see 0 or 584000, then precision is still lost somewhere