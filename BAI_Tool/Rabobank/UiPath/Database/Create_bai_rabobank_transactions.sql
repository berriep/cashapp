-- Table: rpa_data.bai_rabobank_transactions

-- DROP TABLE IF EXISTS rpa_data.bai_rabobank_transactions;

CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_transactions
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    audit_id uuid NOT NULL,
    attempt_nr smallint DEFAULT 1,
    iban character varying(34) COLLATE pg_catalog."default" NOT NULL,
    currency character varying(3) COLLATE pg_catalog."default" NOT NULL,
    booking_date date NOT NULL,
    entry_reference character varying(35) COLLATE pg_catalog."default" NOT NULL,
    transaction_amount numeric(18,2) NOT NULL,
    transaction_currency character varying(3) COLLATE pg_catalog."default" NOT NULL,
    bank_transaction_code character varying(50) COLLATE pg_catalog."default" NOT NULL,
    value_date date,
    interbank_settlement_date date,
    end_to_end_id character varying(35) COLLATE pg_catalog."default",
    batch_entry_reference character varying(35) COLLATE pg_catalog."default",
    acctsvcr_ref character varying(35) COLLATE pg_catalog."default",
    instruction_id character varying(35) COLLATE pg_catalog."default",
    debtor_iban character varying(34) COLLATE pg_catalog."default",
    debtor_name character varying(140) COLLATE pg_catalog."default",
    debtor_agent_bic character varying(11) COLLATE pg_catalog."default",
    creditor_iban character varying(34) COLLATE pg_catalog."default",
    creditor_name character varying(140) COLLATE pg_catalog."default",
    creditor_agent_bic character varying(11) COLLATE pg_catalog."default",
    creditor_currency character varying(3) COLLATE pg_catalog."default",
    creditor_id character varying(35) COLLATE pg_catalog."default",
    ultimate_debtor character varying(140) COLLATE pg_catalog."default",
    ultimate_creditor character varying(140) COLLATE pg_catalog."default",
    initiating_party_name character varying(140) COLLATE pg_catalog."default",
    mandate_id character varying(35) COLLATE pg_catalog."default",
    remittance_information_unstructured character varying(140) COLLATE pg_catalog."default",
    remittance_information_structured character varying(140) COLLATE pg_catalog."default",
    purpose_code character varying(4) COLLATE pg_catalog."default",
    reason_code character varying(4) COLLATE pg_catalog."default",
    payment_information_identification character varying(35) COLLATE pg_catalog."default",
    number_of_transactions integer,
    currency_exchange_rate numeric(12,6),
    currency_exchange_source_currency character varying(3) COLLATE pg_catalog."default",
    currency_exchange_target_currency character varying(3) COLLATE pg_catalog."default",
    instructed_amount numeric(18,2),
    instructed_amount_currency character varying(3) COLLATE pg_catalog."default",
    rabo_booking_datetime timestamp with time zone NOT NULL,
    rabo_detailed_transaction_type character varying(10) COLLATE pg_catalog."default" NOT NULL,
    rabo_transaction_type_name character varying(50) COLLATE pg_catalog."default",
    balance_after_booking_amount numeric(18,2),
    balance_after_booking_currency character varying(3) COLLATE pg_catalog."default",
    balance_after_booking_type character varying(50) COLLATE pg_catalog."default",
    source_system character varying(20) COLLATE pg_catalog."default" DEFAULT 'BAI_API'::character varying,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    retrieved_at timestamp with time zone DEFAULT now(),
    CONSTRAINT bai_rabobank_transactions_pkey PRIMARY KEY (id),
    CONSTRAINT bai_rabobank_transactions_audit_id_attempt_nr_fkey FOREIGN KEY (audit_id, attempt_nr)
        REFERENCES rpa_data.bai_api_audit_log (id, attempt_nr) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS rpa_data.bai_rabobank_transactions
    OWNER to rpa_pvcp_acc;
-- Index: idx_bai_tx_acctsvcr_ref

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_acctsvcr_ref;

CREATE INDEX IF NOT EXISTS idx_bai_tx_acctsvcr_ref
    ON rpa_data.bai_rabobank_transactions USING btree
    (acctsvcr_ref COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default
    WHERE acctsvcr_ref IS NOT NULL;
-- Index: idx_bai_tx_amount

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_amount;

CREATE INDEX IF NOT EXISTS idx_bai_tx_amount
    ON rpa_data.bai_rabobank_transactions USING btree
    (transaction_amount ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_tx_audit_id_attempt

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_audit_id_attempt;

CREATE INDEX IF NOT EXISTS idx_bai_tx_audit_id_attempt
    ON rpa_data.bai_rabobank_transactions USING btree
    (audit_id ASC NULLS LAST, attempt_nr ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_tx_creditor_agent_bic

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_creditor_agent_bic;

CREATE INDEX IF NOT EXISTS idx_bai_tx_creditor_agent_bic
    ON rpa_data.bai_rabobank_transactions USING btree
    (creditor_agent_bic COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default
    WHERE creditor_agent_bic IS NOT NULL;
-- Index: idx_bai_tx_creditor_iban

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_creditor_iban;

CREATE INDEX IF NOT EXISTS idx_bai_tx_creditor_iban
    ON rpa_data.bai_rabobank_transactions USING btree
    (creditor_iban COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default
    WHERE creditor_iban IS NOT NULL;
-- Index: idx_bai_tx_debtor_agent_bic

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_debtor_agent_bic;

CREATE INDEX IF NOT EXISTS idx_bai_tx_debtor_agent_bic
    ON rpa_data.bai_rabobank_transactions USING btree
    (debtor_agent_bic COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default
    WHERE debtor_agent_bic IS NOT NULL;
-- Index: idx_bai_tx_debtor_iban

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_debtor_iban;

CREATE INDEX IF NOT EXISTS idx_bai_tx_debtor_iban
    ON rpa_data.bai_rabobank_transactions USING btree
    (debtor_iban COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default
    WHERE debtor_iban IS NOT NULL;
-- Index: idx_bai_tx_end_to_end_id

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_end_to_end_id;

CREATE INDEX IF NOT EXISTS idx_bai_tx_end_to_end_id
    ON rpa_data.bai_rabobank_transactions USING btree
    (end_to_end_id COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default
    WHERE end_to_end_id IS NOT NULL;
-- Index: idx_bai_tx_entry_reference

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_entry_reference;

CREATE INDEX IF NOT EXISTS idx_bai_tx_entry_reference
    ON rpa_data.bai_rabobank_transactions USING btree
    (entry_reference COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_tx_iban_booking_date

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_iban_booking_date;

CREATE INDEX IF NOT EXISTS idx_bai_tx_iban_booking_date
    ON rpa_data.bai_rabobank_transactions USING btree
    (iban COLLATE pg_catalog."default" ASC NULLS LAST, booking_date DESC NULLS FIRST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_tx_instruction_id

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_instruction_id;

CREATE INDEX IF NOT EXISTS idx_bai_tx_instruction_id
    ON rpa_data.bai_rabobank_transactions USING btree
    (instruction_id COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default
    WHERE instruction_id IS NOT NULL;
-- Index: idx_bai_tx_mandate_id

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_mandate_id;

CREATE INDEX IF NOT EXISTS idx_bai_tx_mandate_id
    ON rpa_data.bai_rabobank_transactions USING btree
    (mandate_id COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default
    WHERE mandate_id IS NOT NULL;
-- Index: idx_bai_tx_rabo_booking_dt

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_rabo_booking_dt;

CREATE INDEX IF NOT EXISTS idx_bai_tx_rabo_booking_dt
    ON rpa_data.bai_rabobank_transactions USING btree
    (rabo_booking_datetime DESC NULLS FIRST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_tx_retrieved_at

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_retrieved_at;

CREATE INDEX IF NOT EXISTS idx_bai_tx_retrieved_at
    ON rpa_data.bai_rabobank_transactions USING btree
    (retrieved_at DESC NULLS FIRST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_tx_source_system

-- DROP INDEX IF EXISTS rpa_data.idx_bai_tx_source_system;

CREATE INDEX IF NOT EXISTS idx_bai_tx_source_system
    ON rpa_data.bai_rabobank_transactions USING btree
    (source_system COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;