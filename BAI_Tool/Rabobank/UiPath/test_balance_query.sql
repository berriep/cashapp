-- Simple test version of balance query
-- Test if the basic structure works

WITH date_range AS (
    SELECT
        DATE '2025-10-01' as start_date,
        DATE '2025-10-07' as end_date
),
daily_transactions AS (
    SELECT
        iban,
        currency,
        value_date as booking_date,
        COUNT(*) as transaction_count,
        SUM(
            CASE 
                WHEN transaction_amount IS NOT NULL 
                THEN CAST(transaction_amount AS DECIMAL(15,2))
                ELSE 0 
            END
        ) as total_transactions
    FROM bai_rabobank_transactions_payload
    WHERE value_date >= (SELECT start_date FROM date_range)
    AND value_date <= (SELECT end_date FROM date_range)
    AND iban = 'NL48RABO0300002343'
    GROUP BY iban, currency, value_date
)
SELECT
    booking_date,
    iban,
    currency,
    transaction_count,
    total_transactions
FROM daily_transactions
ORDER BY booking_date DESC;