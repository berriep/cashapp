-- Fix the rabo_booking_datetime column precision (second attempt)
-- The previous ALTER might not have worked correctly

-- First, check current precision again
SELECT 
    column_name,
    data_type,
    datetime_precision,
    is_nullable
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name = 'rabo_booking_datetime';

-- If datetime_precision shows 6 but microseconds are still lost,
-- the issue might be with existing data or the ALTER wasn't applied correctly

-- Try explicit ALTER with USING clause to force conversion
ALTER TABLE rpa_data.bai_rabobank_transactions 
ALTER COLUMN rabo_booking_datetime 
TYPE timestamp(6) with time zone 
USING rabo_booking_datetime::timestamp(6) with time zone;

-- COMMIT the transaction to make changes permanent
COMMIT;

-- Verify the change worked
SELECT 
    column_name,
    data_type,
    datetime_precision,
    is_nullable
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name = 'rabo_booking_datetime';