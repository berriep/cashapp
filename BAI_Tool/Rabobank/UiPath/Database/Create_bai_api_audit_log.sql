-- Table: rpa_data.bai_api_audit_log

-- DROP TABLE IF EXISTS rpa_data.bai_api_audit_log;

CREATE TABLE IF NOT EXISTS rpa_data.bai_api_audit_log
(
    id uuid NOT NULL,
    attempt_nr smallint NOT NULL DEFAULT 1,
    "timestamp" timestamp with time zone NOT NULL DEFAULT now(),
    bank character varying(50) COLLATE pg_catalog."default" NOT NULL,
    endpoint character varying(255) COLLATE pg_catalog."default" NOT NULL,
    http_method character varying(10) COLLATE pg_catalog."default",
    response_status integer,
    response_time_ms integer,
    caller_id character varying(100) COLLATE pg_catalog."default",
    correlation_id uuid,
    error_message text COLLATE pg_catalog."default",
    closingdate date,
    iban character varying COLLATE pg_catalog."default",
    CONSTRAINT bai_api_audit_log_pkey PRIMARY KEY (id, attempt_nr)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS rpa_data.bai_api_audit_log
    OWNER to rpa_pvcp_acc;
-- Index: idx_bai_audit_bank_endpoint_ts

-- DROP INDEX IF EXISTS rpa_data.idx_bai_audit_bank_endpoint_ts;

CREATE INDEX IF NOT EXISTS idx_bai_audit_bank_endpoint_ts
    ON rpa_data.bai_api_audit_log USING btree
    (bank COLLATE pg_catalog."default" ASC NULLS LAST, endpoint COLLATE pg_catalog."default" ASC NULLS LAST, "timestamp" DESC NULLS FIRST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_audit_correlation

-- DROP INDEX IF EXISTS rpa_data.idx_bai_audit_correlation;

CREATE INDEX IF NOT EXISTS idx_bai_audit_correlation
    ON rpa_data.bai_api_audit_log USING btree
    (correlation_id ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_bai_audit_status

-- DROP INDEX IF EXISTS rpa_data.idx_bai_audit_status;

CREATE INDEX IF NOT EXISTS idx_bai_audit_status
    ON rpa_data.bai_api_audit_log USING btree
    (response_status ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;