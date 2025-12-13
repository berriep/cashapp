-- Compare column precision between working and non-working timestamp columns
SELECT 
    column_name,
    data_type,
    datetime_precision,
    CASE 
        WHEN datetime_precision = 6 THEN 'CORRECT - supports microseconds'
        WHEN datetime_precision = 0 THEN 'PROBLEM - only supports seconds'
        ELSE 'UNKNOWN precision: ' || datetime_precision::text
    END as precision_status
FROM information_schema.columns 
WHERE table_schema = 'rpa_data' 
  AND table_name = 'bai_rabobank_transactions' 
  AND column_name IN ('rabo_booking_datetime', 'created_at', 'updated_at', 'retrieved_at')
ORDER BY column_name;

-- This will show if rabo_booking_datetime has different precision than created_at