-- DAILY BALANCE AUDIT REPORT - DATE RANGE
-- Controleert: Opening Balance + Transacties = Closing Balance voor periode
-- Gebruikt value_date (yyyy-mm-dd) voor transactie linking met balances

WITH date_range AS (
    SELECT
        DATE '2025-10-01' as start_date,
        DATE '2025-10-07' as end_date
),
iban_filter AS (
    SELECT unnest(ARRAY[
         'NL48RABO0300002343'
    ]::text[]) as target_iban
    WHERE ARRAY_LENGTH(ARRAY[
        'NL48RABO0300002343'
    ]::text[], 1) > 0
),
opening_balances AS (
    SELECT
        iban,
        currency,
        amount as opening_balance,
        reference_date,
        audit_id as opening_audit_id
    FROM bai_rabobank_balances_payload
    WHERE balance_type = 'closingBooked'
    AND reference_date >= (SELECT start_date FROM date_range)
    AND reference_date <= (SELECT end_date FROM date_range)
    AND (NOT EXISTS (SELECT 1 FROM iban_filter) OR iban IN (SELECT target_iban FROM iban_filter))
),
closing_balances AS (
    SELECT
        iban,
        currency,
        amount as closing_balance,
        reference_date,
        audit_id as closing_audit_id
    FROM bai_rabobank_balances_payload
    WHERE balance_type = 'closingBooked'
    AND reference_date >= (SELECT start_date FROM date_range)
    AND reference_date <= (SELECT end_date FROM date_range)
    AND (NOT EXISTS (SELECT 1 FROM iban_filter) OR iban IN (SELECT target_iban FROM iban_filter))
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
    AND (NOT EXISTS (SELECT 1 FROM iban_filter) OR iban IN (SELECT target_iban FROM iban_filter))
    GROUP BY iban, currency, value_date
)
SELECT
    t.booking_date as report_date,
    COALESCE(o.iban, c.iban, t.iban) as iban,
    COALESCE(o.currency, c.currency, t.currency) as currency,
    CASE WHEN o.opening_balance IS NOT NULL THEN 'YES' ELSE 'MISSING' END as has_opening,
    CASE WHEN c.closing_balance IS NOT NULL THEN 'YES' ELSE 'MISSING' END as has_closing,
    CASE WHEN t.transaction_count > 0 THEN 'YES' ELSE 'NO_TX' END as has_transactions,
    COALESCE(o.opening_balance, 0) as opening_balance,
    COALESCE(t.total_transactions, 0) as sum_transactions,
    COALESCE(t.transaction_count, 0) as transaction_count,
    COALESCE(c.closing_balance, 0) as closing_balance,
    COALESCE(o.opening_balance, 0) + COALESCE(t.total_transactions, 0) as expected_closing,
    COALESCE(c.closing_balance, 0) - (COALESCE(o.opening_balance, 0) + COALESCE(t.total_transactions, 0)) as difference,
    CASE
        WHEN o.opening_balance IS NULL THEN 'MISSING_OPENING'
        WHEN c.closing_balance IS NULL THEN 'MISSING_CLOSING'
        WHEN ABS(COALESCE(c.closing_balance, 0) - (COALESCE(o.opening_balance, 0) + COALESCE(t.total_transactions, 0))) < 0.01 THEN 'PERFECT_MATCH'
        WHEN ABS(COALESCE(c.closing_balance, 0) - (COALESCE(o.opening_balance, 0) + COALESCE(t.total_transactions, 0))) < 1.00 THEN 'MINOR_DIFF'
        ELSE 'MAJOR_DIFF'
    END as audit_status,
    o.opening_audit_id,
    c.closing_audit_id
FROM daily_transactions t
FULL OUTER JOIN opening_balances o ON t.iban = o.iban AND t.currency = o.currency AND t.booking_date = o.reference_date + INTERVAL '1 day'
FULL OUTER JOIN closing_balances c ON t.iban = c.iban AND t.currency = c.currency AND t.booking_date = c.reference_date
ORDER BY
    audit_status DESC,
    ABS(COALESCE(c.closing_balance, 0) - (COALESCE(o.opening_balance, 0) + COALESCE(t.total_transactions, 0))) DESC,
    report_date DESC,
    iban;