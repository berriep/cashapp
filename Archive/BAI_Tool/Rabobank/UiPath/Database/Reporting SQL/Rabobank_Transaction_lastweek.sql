SELECT 
    bam.owner_name,
    brt.iban,
    brt.currency,
    brt.booking_date,
    brt.entry_reference,
    brt.transaction_amount,
    brt.transaction_currency,
    brt.debtor_iban,
    brt.debtor_name,
    brt.debtor_agent_bic,
    brt.creditor_iban,
    brt.remittance_information_unstructured,
    brt.rabo_booking_datetime,
    brt.rabo_detailed_transaction_type,
    brt.rabo_transaction_type_name,
    brt.balance_after_booking_amount,
    brt.balance_after_booking_currency,
    brt.balance_after_booking_type
FROM bai_rabobank_transactions brt
INNER JOIN bai_api_audit_log baal 
    ON brt.audit_id = baal.id
INNER JOIN bai_rabobank_account_info brai 
    ON brt.iban = brai.iban
INNER JOIN bai_owner_mapping bam 
    ON bam.iban = brai.iban 
    AND bam.team = 'Treasury' 
    AND bam.purpose = 'Reporting'
    AND bam.is_active = TRUE
WHERE 
    baal.closingdate >= date_trunc('week', CURRENT_DATE) - INTERVAL '1 weeks' -- Monday one week ago
    AND baal.closingdate < date_trunc('week', CURRENT_DATE) - INTERVAL '0 week' -- Last Sunday zondag
ORDER BY brt.iban, brt.booking_date ASC;