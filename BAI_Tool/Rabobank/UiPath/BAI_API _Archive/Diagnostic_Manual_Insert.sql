-- DIAGNOSTIC: Test what our application code is actually sending to PostgreSQL

-- Test 1: What happens when we manually insert the exact API value?
INSERT INTO rpa_data.bai_rabobank_transactions 
(audit_id, iban, currency, booking_date, entry_reference, transaction_amount, 
 transaction_currency, bank_transaction_code, rabo_booking_datetime, rabo_detailed_transaction_type)
VALUES 
('MANUAL_TEST_1', 'NL12RABO0330208888', 'EUR', '2025-08-04', 'MANUAL_1', 
 100.00, 'EUR', 'PMNT-RCDT-ESCT', '2025-08-04T16:15:57.123456Z', '100');

-- Test 2: What happens with different precision formats?
INSERT INTO rpa_data.bai_rabobank_transactions 
(audit_id, iban, currency, booking_date, entry_reference, transaction_amount, 
 transaction_currency, bank_transaction_code, rabo_booking_datetime, rabo_detailed_transaction_type)
VALUES 
('MANUAL_TEST_2', 'NL12RABO0330208888', 'EUR', '2025-08-04', 'MANUAL_2', 
 100.00, 'EUR', 'PMNT-RCDT-ESCT', '2025-08-04 16:15:57.123456+00', '100'),
('MANUAL_TEST_3', 'NL12RABO0330208888', 'EUR', '2025-08-04', 'MANUAL_3', 
 100.00, 'EUR', 'PMNT-RCDT-ESCT', '2025-08-04T16:15:57.123456+00:00', '100');

-- Verify what we get
SELECT 
    entry_reference,
    rabo_booking_datetime,
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US TZ') as formatted_microseconds,
    EXTRACT(MICROSECONDS FROM rabo_booking_datetime) % 1000000 as true_microseconds,
    LENGTH(TO_CHAR(rabo_booking_datetime, 'US')) as microsecond_length
FROM rpa_data.bai_rabobank_transactions 
WHERE entry_reference IN ('MANUAL_1', 'MANUAL_2', 'MANUAL_3')
ORDER BY entry_reference;

-- Expected: All should show 123456 microseconds if database precision works