-- Check alle transaction types in de database
-- Vergelijk met geïmplementeerde types in MT940 script

SELECT 
    rabo_detailed_transaction_type,
    COUNT(*) as transaction_count,
    ROUND(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER (), 2) as percentage,
    MIN(booking_date) as first_seen,
    MAX(booking_date) as last_seen,
    STRING_AGG(DISTINCT 
        CASE 
            WHEN debtor_name IS NOT NULL THEN LEFT(debtor_name, 30)
            WHEN creditor_name IS NOT NULL THEN LEFT(creditor_name, 30)
            ELSE 'Unknown'
        END, 
        ', ' 
        ORDER BY 
            CASE 
                WHEN debtor_name IS NOT NULL THEN LEFT(debtor_name, 30)
                WHEN creditor_name IS NOT NULL THEN LEFT(creditor_name, 30)
                ELSE 'Unknown'
            END
    ) as sample_counterparties
FROM rpa_data.bai_rabobank_transactions
GROUP BY rabo_detailed_transaction_type
ORDER BY transaction_count DESC;

-- Geïmplementeerde types in MT940 script (ter referentie):
-- 64, 593, 113, 193, 93, 2033, 540, 544, 586, 1085, 541, 626, 625, 2065, 065, 501

-- Toon types die NIET geïmplementeerd zijn
SELECT 
    rabo_detailed_transaction_type,
    COUNT(*) as count
FROM rpa_data.bai_rabobank_transactions
WHERE rabo_detailed_transaction_type NOT IN ('64', '593', '113', '193', '93', '2033', '540', '544', '586', '1085', '541', '626', '625', '2065', '065', '501')
GROUP BY rabo_detailed_transaction_type
ORDER BY count DESC;
