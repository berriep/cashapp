-- Query to show full datetime precision for rabo_booking_datetime
-- This query displays the complete microsecond precision timestamp from Rabobank API

SELECT 
    id, 
    audit_id, 
    attempt_nr, 
    iban, 
    currency, 
    booking_date, 
    entry_reference, 
    transaction_amount, 
    transaction_currency, 
    bank_transaction_code, 
    value_date, 
    interbank_settlement_date, 
    end_to_end_id, 
    batch_entry_reference, 
    acctsvcr_ref, 
    instruction_id, 
    debtor_iban, 
    debtor_name, 
    debtor_agent_bic, 
    creditor_iban, 
    creditor_name, 
    creditor_agent_bic, 
    creditor_currency, 
    creditor_id, 
    ultimate_debtor, 
    ultimate_creditor, 
    initiating_party_name, 
    mandate_id, 
    remittance_information_unstructured, 
    remittance_information_structured, 
    purpose_code, 
    reason_code, 
    payment_information_identification, 
    number_of_transactions, 
    currency_exchange_rate, 
    currency_exchange_source_currency, 
    currency_exchange_target_currency, 
    instructed_amount, 
    instructed_amount_currency, 
    
    -- Full datetime precision (microseconds) - multiple formats
    rabo_booking_datetime,                                              -- Default format
    rabo_booking_datetime::TEXT AS rabo_booking_datetime_text,          -- Full text representation
    TO_CHAR(rabo_booking_datetime, 'YYYY-MM-DD HH24:MI:SS.US TZ') AS rabo_booking_datetime_formatted,  -- Formatted with microseconds
    EXTRACT(EPOCH FROM rabo_booking_datetime) AS rabo_booking_datetime_epoch,  -- Unix timestamp with decimals
    
    rabo_detailed_transaction_type, 
    rabo_transaction_type_name, 
    balance_after_booking_amount, 
    balance_after_booking_currency, 
    balance_after_booking_type, 
    source_system, 
    created_at, 
    updated_at, 
    retrieved_at
FROM rpa_data.bai_rabobank_transactions
ORDER BY rabo_booking_datetime DESC
LIMIT 100;  -- Limit for performance - remove or adjust as needed

-- Example output formats:
-- rabo_booking_datetime:           2025-10-06 08:12:49.257686+00
-- rabo_booking_datetime_text:      2025-10-06 08:12:49.257686+00
-- rabo_booking_datetime_formatted: 2025-10-06 08:12:49.257686 +00
-- rabo_booking_datetime_epoch:     1728202369.257686