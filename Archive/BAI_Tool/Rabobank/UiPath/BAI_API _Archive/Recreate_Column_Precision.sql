-- DRASTIC FIX: Complete column recreation with proper precision
-- Sometimes PostgreSQL doesn't properly convert existing columns

-- Step 1: Check what we're working with
SELECT 
    column_name,
    data_type,
    datetime_precision,
    column_default,
    is_nullable
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name = 'rabo_booking_datetime';

-- Step 2: Add a NEW column with correct precision
ALTER TABLE rpa_data.bai_rabobank_transactions 
ADD COLUMN rabo_booking_datetime_new timestamp(6) with time zone;

-- Step 3: Copy data from old column to new column (will truncate to precision 0)
UPDATE rpa_data.bai_rabobank_transactions 
SET rabo_booking_datetime_new = rabo_booking_datetime;

-- Step 4: Drop the old column
ALTER TABLE rpa_data.bai_rabobank_transactions 
DROP COLUMN rabo_booking_datetime;

-- Step 5: Rename new column to original name
ALTER TABLE rpa_data.bai_rabobank_transactions 
RENAME COLUMN rabo_booking_datetime_new TO rabo_booking_datetime;

-- Step 6: Add NOT NULL constraint if needed
ALTER TABLE rpa_data.bai_rabobank_transactions 
ALTER COLUMN rabo_booking_datetime SET NOT NULL;

-- COMMIT everything
COMMIT;

-- Final verification
SELECT 
    column_name,
    data_type,
    datetime_precision,
    is_nullable
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name = 'rabo_booking_datetime';

-- Test with a direct INSERT to see if new precision works
-- INSERT INTO rpa_data.bai_rabobank_transactions (rabo_booking_datetime) 
-- VALUES ('2025-08-04T21:58:04.283480Z'::timestamp(6) with time zone);