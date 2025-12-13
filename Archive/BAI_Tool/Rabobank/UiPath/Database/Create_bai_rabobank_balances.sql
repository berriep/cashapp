-- Table: rpa_data.bai_rabobank_balances

-- DROP TABLE IF EXISTS rpa_data.bai_rabobank_balances;

CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_balances
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    audit_id uuid NOT NULL,
    attempt_nr smallint DEFAULT 1,
    iban character varying(34) COLLATE pg_catalog."default" NOT NULL,
    currency character varying(3) COLLATE pg_catalog."default" NOT NULL,
    balance_type character varying(50) COLLATE pg_catalog."default" NOT NULL,
    amount numeric(18,2) NOT NULL,
    reference_date date,
    last_change_datetime timestamp with time zone,
    retrieved_at timestamp with time zone DEFAULT now(),
    CONSTRAINT bai_rabobank_balances_pkey PRIMARY KEY (id),
    CONSTRAINT bai_rabobank_balances_audit_id_attempt_nr_fkey FOREIGN KEY (audit_id, attempt_nr)
        REFERENCES rpa_data.bai_api_audit_log (id, attempt_nr) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE,
    CONSTRAINT bai_rabobank_balances_balance_type_check CHECK (balance_type::text = ANY (ARRAY['interimBooked'::character varying, 'expected'::character varying, 'closingBooked'::character varying]::text[]))
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS rpa_data.bai_rabobank_balances
    OWNER to rpa_pvcp_acc;
-- Index: idx_bai_balance_audit_id_attempt

-- DROP INDEX IF EXISTS rpa_data.idx_bai_balance_audit_id_attempt;

CREATE INDEX IF NOT EXISTS idx_bai_balance_audit_id_attempt
    ON rpa_data.bai_rabobank_balances USING btree
    (audit_id ASC NULLS LAST, attempt_nr ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_balance_iban_type_date

-- DROP INDEX IF EXISTS rpa_data.idx_bai_balance_iban_type_date;

CREATE INDEX IF NOT EXISTS idx_bai_balance_iban_type_date
    ON rpa_data.bai_rabobank_balances USING btree
    (iban COLLATE pg_catalog."default" ASC NULLS LAST, balance_type COLLATE pg_catalog."default" ASC NULLS LAST, reference_date ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_balance_retrieved_at

-- DROP INDEX IF EXISTS rpa_data.idx_bai_balance_retrieved_at;

CREATE INDEX IF NOT EXISTS idx_bai_balance_retrieved_at
    ON rpa_data.bai_rabobank_balances USING btree
    (retrieved_at DESC NULLS FIRST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;