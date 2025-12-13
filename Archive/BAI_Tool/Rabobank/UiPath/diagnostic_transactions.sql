-- Diagnostische query om te controleren welke kolommen en data er zijn
-- Run deze query eerst om te zien wat er beschikbaar is

-- 1. Controleer kolom structuur
SELECT column_name, data_type, is_nullable
FROM information_schema.columns 
WHERE table_name = 'bai_rabobank_transactions_payload'
ORDER BY ordinal_position;

-- 2. Bekijk sample data
SELECT TOP 5 *
FROM bai_rabobank_transactions_payload
WHERE value_date >= '2025-10-01'
AND value_date <= '2025-10-07'
AND iban = 'NL48RABO0300002343';

-- 3. Check welke bedrag kolommen er zijn
SELECT 
    COUNT(*) as total_rows,
    COUNT(CASE WHEN transaction_amount IS NOT NULL AND transaction_amount != 0 THEN 1 END) as non_zero_transaction_amount,
    COUNT(CASE WHEN amount IS NOT NULL AND amount != 0 THEN 1 END) as non_zero_amount,
    AVG(transaction_amount) as avg_transaction_amount,
    AVG(amount) as avg_amount,
    MIN(value_date) as min_value_date,
    MAX(value_date) as max_value_date
FROM bai_rabobank_transactions_payload
WHERE value_date >= '2025-10-01'
AND value_date <= '2025-10-07';