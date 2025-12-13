-- Table: rpa_data.bai_rabobank_account_info

-- DROP TABLE IF EXISTS rpa_data.bai_rabobank_account_info;

CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_account_info
(
    id integer NOT NULL DEFAULT nextval('bai_rabobank_account_info_id_seq'::regclass),
    iban character varying(34) COLLATE pg_catalog."default" NOT NULL,
    owner_name character varying(255) COLLATE pg_catalog."default" NOT NULL,
    currency character varying(3) COLLATE pg_catalog."default" NOT NULL DEFAULT 'EUR'::character varying,
    resource_id character varying(255) COLLATE pg_catalog."default",
    status character varying(20) COLLATE pg_catalog."default" DEFAULT 'enabled'::character varying,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    CONSTRAINT bai_rabobank_account_info_pkey PRIMARY KEY (id),
    CONSTRAINT bai_rabobank_account_info_iban_key UNIQUE (iban)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS rpa_data.bai_rabobank_account_info
    OWNER to rpa_pvcp_acc;
-- Index: idx_bai_account_iban

-- DROP INDEX IF EXISTS rpa_data.idx_bai_account_iban;

CREATE INDEX IF NOT EXISTS idx_bai_account_iban
    ON rpa_data.bai_rabobank_account_info USING btree
    (iban COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;