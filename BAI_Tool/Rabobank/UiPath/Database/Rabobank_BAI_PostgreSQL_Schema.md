# Technical Approach for Rabobank Business Account Insight (BAI)

This documentation describes the technical implementation for storing **Business Account Insight (BAI)** data in **PostgreSQL**, with full traceability via an externally generated `audit_id` (from RPA UiPath). BAI encompasses both **balances** and **transactions** data from Rabobank APIs. Each balance API call contains multiple balanceTypes (such as `interimBooked`, `expected`, `closingBooked`), and each transaction API call contains multiple transaction records, all stored separately with full audit traceability. The structure supports **retries** via an additional `attempt_nr` column, allowing repeated API calls under the same `audit_id` to be registered.

## Why External audit_id?

The choice for an **externally generated audit_id** (instead of database auto-generated IDs) serves a critical business requirement: **direct traceability between input files and database records**. 

**Business Context:**
- UiPath RPA process downloads API responses and saves them as JSON files
- These input files are named using the `audit_id`: `t_{audit_id}_{BookingDate}_yyyyymmdd_hhmmss.json`, `b_{audit_id}_{BookingDate}_yyyyymmdd_hhmmss_.json`
- When processing these files into the database, the same `audit_id` is used as the primary linking key
- This creates **1:1 traceability**: any database record can be traced back to its exact source file
- **Debugging advantage**: when data issues arise, developers can immediately locate the original API response file
- **Audit compliance**: complete data lineage from API call → file storage → database storage

**Technical Implementation:**
- UiPath generates UUID `audit_id` before API call
- API response is saved as `{endpoint}_{audit_id}.json`
- Database records use the same `audit_id` as foreign key
- Result: seamless traceability across the entire data pipeline

---

## 1. Audit Log Table

The audit table records metadata for each API call, including status, duration, and origin. Retries are supported via `attempt_nr`, allowing the same `audit_id` to occur multiple times without conflict.

```sql
CREATE TABLE IF NOT EXISTS rpa_data.bai_api_audit_log (
    id UUID NOT NULL,                      -- externally generated audit_id (e.g., UiPath)
    attempt_nr SMALLINT NOT NULL DEFAULT 1, -- retry counter, starts at 1
    timestamp TIMESTAMPTZ NOT NULL DEFAULT now(),
    bank CHARACTER VARYING(50) NOT NULL,
    endpoint CHARACTER VARYING(255) NOT NULL,        -- e.g., /balances
    http_method CHARACTER VARYING(10),
    response_status INTEGER,
    response_time_ms INTEGER,
    caller_id CHARACTER VARYING(100),
    correlation_id UUID,
    error_message TEXT,
    closingdate DATE,                      -- closing date for the API call
    iban CHARACTER VARYING(34),                          -- IBAN for the API call
    CONSTRAINT bai_api_audit_log_pkey PRIMARY KEY (id, attempt_nr)
);

-- Indexes for performance analysis and quick lookup
CREATE INDEX IF NOT EXISTS idx_bai_audit_bank_endpoint_ts ON rpa_data.bai_api_audit_log (bank, endpoint, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_bai_audit_status ON rpa_data.bai_api_audit_log (response_status);
CREATE INDEX IF NOT EXISTS idx_bai_audit_correlation ON rpa_data.bai_api_audit_log (correlation_id);
```

---

## 2. Rabobank Balance Table

Each `balanceType` from the payload is stored as a separate row and linked to the `audit_id` and `attempt_nr` of the API call.

```sql
CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_balances (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    audit_id UUID NOT NULL,
    attempt_nr SMALLINT DEFAULT 1,
    iban CHARACTER VARYING(34) NOT NULL,
    currency CHARACTER VARYING(3) NOT NULL,
    balance_type CHARACTER VARYING(50) NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    reference_date DATE,                 -- only present for closingBooked
    last_change_datetime TIMESTAMPTZ,    -- present for interimBooked and expected
    retrieved_at TIMESTAMPTZ DEFAULT now(),
    CONSTRAINT bai_rabobank_balances_pkey PRIMARY KEY (id),
    CONSTRAINT bai_rabobank_balances_audit_id_attempt_nr_fkey FOREIGN KEY (audit_id, attempt_nr)
        REFERENCES rpa_data.bai_api_audit_log (id, attempt_nr) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE,
    CONSTRAINT bai_rabobank_balances_balance_type_check CHECK (balance_type::text = ANY (ARRAY['interimBooked'::character varying, 'expected'::character varying, 'closingBooked'::character varying]::text[]))
);

-- Indexes for fast selection and filtering
CREATE INDEX IF NOT EXISTS idx_bai_balance_iban_type_date
    ON rpa_data.bai_rabobank_balances USING btree
    (iban, balance_type, reference_date);

CREATE INDEX IF NOT EXISTS idx_bai_balance_audit_id_attempt
    ON rpa_data.bai_rabobank_balances USING btree
    (audit_id, attempt_nr);

CREATE INDEX IF NOT EXISTS idx_bai_balance_retrieved_at
    ON rpa_data.bai_rabobank_balances USING btree
    (retrieved_at DESC);
```

---

## 3. Rabobank Transactions Table

Each transaction from the API response is stored as a separate row and linked to the `audit_id` and `attempt_nr` of the API call. The structure contains all relevant fields from the Rabobank Transactions API, including debtor/creditor information, amounts, and Rabobank-specific metadata.

**Field Notes:**
- **Mandatory (NOT NULL)**: fields that are always present in every transaction
- **Optional**: fields that can be NULL depending on transaction type or direction
- **Direction-dependent**: `debtor_*` fields are only populated for **incoming** payments, `creditor_*` for **outgoing** payments
- **SEPA-specific**: `creditor_id`, `mandate_id` are only present for SEPA direct debit transactions
- **Currency-specific**: `currency_exchange_*` and `instructed_amount_*` are only present for multi-currency transactions

```sql
CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_transactions (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    audit_id UUID NOT NULL,
    attempt_nr SMALLINT DEFAULT 1,
    
    -- Account information (always present)
    iban CHARACTER VARYING(34) NOT NULL,                    -- Own account (from account.iban)
    currency CHARACTER VARYING(3) NOT NULL,                 -- Account currency
    
    -- Transaction basic data (mandatory)
    booking_date DATE NOT NULL,                   -- Date transaction was booked
    entry_reference CHARACTER VARYING(35) NOT NULL,         -- Unique bank reference (UNIQUE candidate)
    transaction_amount NUMERIC(18,2) NOT NULL,    -- Transaction amount (transactionAmount.value)
    transaction_currency CHARACTER VARYING(3) NOT NULL,     -- Transaction currency
    bank_transaction_code CHARACTER VARYING(50) NOT NULL,   -- Bank transaction code (e.g., PMNT-RCDT-ESCT)
    
    -- Transaction optional basic data
    value_date DATE,                              -- Value date (usually = booking_date)
    interbank_settlement_date DATE,               -- Settlement between banks (optional)
    end_to_end_id CHARACTER VARYING(35),                    -- End-to-end identification (often present)
    batch_entry_reference CHARACTER VARYING(35),            -- Batch reference (rarely used)
    acctsvcr_ref CHARACTER VARYING(35),                     -- Account Servicer Reference (strongest unique ref)
    instruction_id CHARACTER VARYING(35),                   -- Instruction reference
    
    -- Debtor (payer) information — ONLY for INCOMING payments
    debtor_iban CHARACTER VARYING(34),                      -- Payer's IBAN (NULL for outgoing)
    debtor_name CHARACTER VARYING(140),                     -- Payer's name
    debtor_agent_bic CHARACTER VARYING(11),                 -- Payer's BIC (debtorAgent from BAI JSON)
    
    -- Creditor (payee) information — ONLY for OUTGOING payments  
    creditor_iban CHARACTER VARYING(34),                    -- Payee's IBAN (NULL for incoming)
    creditor_name CHARACTER VARYING(140),                   -- Payee's name
    creditor_agent_bic CHARACTER VARYING(11),               -- Payee's BIC (creditorAgent from BAI JSON)
    creditor_currency CHARACTER VARYING(3),                 -- Currency of creditor account (usually EUR)
    creditor_id CHARACTER VARYING(35),                      -- SEPA Creditor ID (only for direct debit)
    
    -- Ultimate parties (optional — mainly for batch/corporate payments)
    ultimate_debtor CHARACTER VARYING(140),                 -- Ultimate debtor name
    ultimate_creditor CHARACTER VARYING(140),               -- Ultimate creditor name
    initiating_party_name CHARACTER VARYING(140),           -- Initiating party name
    
    -- SEPA Direct Debit specific
    mandate_id CHARACTER VARYING(35),                       -- SEPA mandate ID (only for direct debit)
    
    -- Payment information (usually present)
    remittance_information_unstructured CHARACTER VARYING(140),  -- Free text description
    remittance_information_structured CHARACTER VARYING(140),    -- Structured payment reference (e.g., ISO payment ref)
    purpose_code CHARACTER VARYING(4),                      -- Purpose code (e.g., EPAY, SALA, PENS)
    reason_code CHARACTER VARYING(4),                       -- Reason code (e.g., AG01 for return payments)
    
    -- Batch information (optional)
    payment_information_identification CHARACTER VARYING(35),  -- Payment Information ID
    number_of_transactions INTEGER,                   -- Number of transactions in batch
    
    -- Multi-currency exchange information (only for FX transactions)
    currency_exchange_rate NUMERIC(12,6),         -- Exchange rate
    currency_exchange_source_currency CHARACTER VARYING(3), -- Source currency
    currency_exchange_target_currency CHARACTER VARYING(3), -- Target currency
    instructed_amount NUMERIC(18,2),              -- Original amount (before conversion)
    instructed_amount_currency CHARACTER VARYING(3),        -- Currency of original amount
    
    -- Rabobank specific fields (mandatory)
    rabo_booking_datetime timestamp(6) with time zone NOT NULL,   -- Exact booking timestamp with microsecond precision
    rabo_detailed_transaction_type CHARACTER VARYING(10) NOT NULL,  -- Rabobank transaction type code
    rabo_transaction_type_name CHARACTER VARYING(50),       -- Rabobank transaction type name (usually present)
    
    -- Balance after transaction (optional — not always present)
    balance_after_booking_amount NUMERIC(18,2),   -- Balance after booking
    balance_after_booking_currency CHARACTER VARYING(3),    -- Currency of balance (usually EUR)
    balance_after_booking_type CHARACTER VARYING(50),       -- Balance type (e.g., InterimBooked)
    
    -- Metadata
    source_system CHARACTER VARYING(20) DEFAULT 'BAI_API'::CHARACTER VARYING,  -- Source system ('CAMT053', 'BAI_API')
    created_at timestamp(6) with time zone DEFAULT now(),         -- Timestamp of storage in DB
    updated_at timestamp(6) with time zone DEFAULT now(),         -- Last modification timestamp
    retrieved_at timestamp(6) with time zone DEFAULT now(),        -- When data was retrieved
    
    CONSTRAINT bai_rabobank_transactions_pkey PRIMARY KEY (id),
    CONSTRAINT bai_rabobank_transactions_audit_id_attempt_nr_fkey FOREIGN KEY (audit_id, attempt_nr)
        REFERENCES rpa_data.bai_api_audit_log (id, attempt_nr) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE
);

-- Indexes for fast selection and filtering
CREATE INDEX IF NOT EXISTS idx_bai_tx_iban_booking_date
    ON rpa_data.bai_rabobank_transactions USING btree
    (iban, booking_date DESC);

CREATE INDEX IF NOT EXISTS idx_bai_tx_audit_id_attempt
    ON rpa_data.bai_rabobank_transactions USING btree
    (audit_id, attempt_nr);

CREATE INDEX IF NOT EXISTS idx_bai_tx_entry_reference
    ON rpa_data.bai_rabobank_transactions USING btree
    (entry_reference);

CREATE INDEX IF NOT EXISTS idx_bai_tx_debtor_iban
    ON rpa_data.bai_rabobank_transactions USING btree
    (debtor_iban) WHERE debtor_iban IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_bai_tx_creditor_iban
    ON rpa_data.bai_rabobank_transactions USING btree
    (creditor_iban) WHERE creditor_iban IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_bai_tx_debtor_agent_bic
    ON rpa_data.bai_rabobank_transactions USING btree
    (debtor_agent_bic) WHERE debtor_agent_bic IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_bai_tx_creditor_agent_bic
    ON rpa_data.bai_rabobank_transactions USING btree
    (creditor_agent_bic) WHERE creditor_agent_bic IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_bai_tx_retrieved_at
    ON rpa_data.bai_rabobank_transactions USING btree
    (retrieved_at DESC);

CREATE INDEX IF NOT EXISTS idx_bai_tx_acctsvcr_ref
    ON rpa_data.bai_rabobank_transactions USING btree
    (acctsvcr_ref) WHERE acctsvcr_ref IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_bai_tx_instruction_id
    ON rpa_data.bai_rabobank_transactions USING btree
    (instruction_id) WHERE instruction_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_bai_tx_source_system
    ON rpa_data.bai_rabobank_transactions USING btree
    (source_system);

CREATE INDEX IF NOT EXISTS idx_bai_tx_amount
    ON rpa_data.bai_rabobank_transactions USING btree
    (transaction_amount);

CREATE INDEX IF NOT EXISTS idx_bai_tx_rabo_booking_dt
    ON rpa_data.bai_rabobank_transactions USING btree
    (rabo_booking_datetime DESC);

CREATE INDEX IF NOT EXISTS idx_bai_tx_end_to_end_id
    ON rpa_data.bai_rabobank_transactions USING btree
    (end_to_end_id) WHERE end_to_end_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_bai_tx_mandate_id
    ON rpa_data.bai_rabobank_transactions USING btree
    (mandate_id) WHERE mandate_id IS NOT NULL;

-- Optional: UNIQUE constraint to prevent duplicates (entry_reference is unique per transaction)
-- Note: only add if you're certain entry_reference is ALWAYS unique within your dataset
-- CREATE UNIQUE INDEX idx_bai_tx_entry_ref_unique ON rpa_data.bai_rabobank_transactions (entry_reference, audit_id, attempt_nr);
```

---

## 4. Rabobank Account Information Table

This table stores basic account information for Rabobank accounts, including IBAN, owner name, and account status. This is used as a reference table for account metadata.

```sql
CREATE SEQUENCE IF NOT EXISTS rpa_data.bai_rabobank_account_info_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_account_info (
    id INTEGER NOT NULL DEFAULT nextval('bai_rabobank_account_info_id_seq'::regclass),
    iban CHARACTER VARYING(34) NOT NULL,
    owner_name CHARACTER VARYING(255) NOT NULL,
    currency CHARACTER VARYING(3) NOT NULL DEFAULT 'EUR'::CHARACTER VARYING,
    resource_id CHARACTER VARYING(255),
    status CHARACTER VARYING(20) DEFAULT 'enabled'::CHARACTER VARYING,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now(),
    CONSTRAINT bai_rabobank_account_info_pkey PRIMARY KEY (id),
    CONSTRAINT bai_rabobank_account_info_iban_key UNIQUE (iban)
);

-- Index for fast IBAN lookup
CREATE INDEX IF NOT EXISTS idx_bai_account_iban
    ON rpa_data.bai_rabobank_account_info USING btree
    (iban);
```

---

## 5. Example Transactions Payload and INSERT Statements

### Rabobank Transactions API Payload (Fragment)

```json
{
  "account": {
    "currency": "EUR",
    "iban": "NL12RABO0330208888"
  },
  "transactions": {
    "booked": [
      {
        "bookingDate": "2025-10-06",
        "creditorAccount": {
          "currency": "EUR",
          "iban": "NL12RABO0330208888"
        },
        "debtorAccount": {
          "iban": "NL19RABO0144142392"
        },
        "debtorAgent": "RABONL2U",
        "debtorName": "M. Velthuis eo M. Moes",
        "endToEndId": "06-10-2025 23:35 7020684525586059",
        "entryReference": "491838",
        "batchEntryReference": "OO9B005073280517",
        "purposeCode": "EPAY",
        "raboBookingDateTime": "2025-10-06T21:36:03.426472Z",
        "raboDetailedTransactionType": "100",
        "raboTransactionTypeName": "id",
        "remittanceInformationUnstructured": "8990570215 7020684525586059 HH xui-40561309",
        "transactionAmount": {
          "value": "25.00",
          "currency": "EUR"
        },
        "valueDate": "2025-10-06",
        "balanceAfterBooking": {
          "balanceType": "InterimBooked",
          "balanceAmount": {
            "value": "239.00",
            "currency": "EUR"
          }
        },
        "bankTransactionCode": "PMNT-RCDT-ESCT"
      }
    ]
  }
}
```

### Storage in PostgreSQL

```sql
-- First create audit record (same as for balances)
INSERT INTO rpa_data.bai_api_audit_log (id, attempt_nr, bank, endpoint, response_status)
VALUES ('f8a3c2d1-4b5e-6789-abcd-ef0123456789', 1, 'Rabobank', '/transactions', 200);

-- Then store transactions (each transaction = 1 row)
INSERT INTO rpa_data.bai_rabobank_transactions (
    audit_id, 
    attempt_nr, 
    iban, 
    currency, 
    booking_date, 
    value_date,
    entry_reference,
    end_to_end_id,
    batch_entry_reference,
    transaction_amount,
    transaction_currency,
    debtor_iban,
    debtor_name,
    debtor_agent_bic,
    creditor_iban,
    creditor_currency,
    remittance_information_unstructured,
    purpose_code,
    bank_transaction_code,
    rabo_booking_datetime,
    rabo_detailed_transaction_type,
    rabo_transaction_type_name,
    balance_after_booking_amount,
    balance_after_booking_currency,
    balance_after_booking_type,
    source_system
) VALUES (
    'f8a3c2d1-4b5e-6789-abcd-ef0123456789',
    1,
    'NL12RABO0330208888',
    'EUR',
    '2025-10-06',
    '2025-10-06',
    '491838',
    '06-10-2025 23:35 7020684525586059',
    'OO9B005073280517',
    25.00,
    'EUR',
    'NL19RABO0144142392',
    'M. Velthuis eo M. Moes',
    'RABONL2U',
    'NL12RABO0330208888',
    'EUR',
    '8990570215 7020684525586059 HH xui-40561309',
    'EPAY',
    'PMNT-RCDT-ESCT',
    '2025-10-06T21:36:03.426472Z',
    '100',
    'id',
    239.00,
    'EUR',
    'InterimBooked',
    'BAI_API'
);
```

---

## 6. Generic Pattern for Other Tables

For future API endpoints (such as metadata, logs, or other banking data), use **the same linking pattern**:

```sql
CREATE TABLE IF NOT EXISTS rpa_data.bai_<endpoint>_payload (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    audit_id UUID NOT NULL,
    attempt_nr SMALLINT DEFAULT 1,
    -- endpoint-specific columns
    retrieved_at TIMESTAMPTZ DEFAULT now(),
    CONSTRAINT bai_<endpoint>_payload_pkey PRIMARY KEY (id),
    CONSTRAINT bai_<endpoint>_payload_audit_fkey FOREIGN KEY (audit_id, attempt_nr) 
        REFERENCES rpa_data.bai_api_audit_log(id, attempt_nr) 
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_bai_<endpoint>_audit_id_attempt 
    ON rpa_data.bai_<endpoint>_payload (audit_id, attempt_nr);
```

**Important:** every table linked to an API call must contain `(audit_id, attempt_nr)` as a foreign key. This keeps track of which attempt of the API call was the source of the data.

---

## 6. BookingDate Timezone Conversion

### 6.1 Background

Rabobank API returns two date fields for transactions:
- **`bookingDate`**: UTC-based date in YYYY-MM-DD format (API field)
- **`raboBookingDateTime`**: Precise UTC timestamp with microseconds (API field)

### 6.2 Issue

For transactions booked near midnight UTC, the `bookingDate` may differ from the local (Europe/Amsterdam) booking date, causing inconsistencies with original Rabobank CAMT.053 files.

**Example:**
```
raboBookingDateTime: "2025-10-05T22:08:03Z" (UTC)
→ Europe/Amsterdam: "2025-10-06 00:08:03" (after midnight!)

Results:
- Original Rabobank CAMT.053: <BookgDt><Dt>2025-10-06</Dt></BookgDt>
- BAI JSON bookingDate: "2025-10-05"
- Generated CAMT.053: "2025-10-05" ❌ INCONSISTENT
```

### 6.3 Solution

Following Rabobank guidance for timezone consistency:

**Implementation (Import Time Conversion):**
- **Convert**: `raboBookingDateTime` (UTC) → local date during database import
- **Override**: Replace API `bookingDate` with derived local date
- **Preserve**: `valueDate` remains unchanged (represents interest date, not timezone dependent)

**Code Implementation:**
```csharp
// In insert_transactions.cs - during import
DateTime raboBookingDT = DateTime.Parse(raboBookingDateTime);
DateTime localBookingDT = raboBookingDT.ToLocalTime(); // UTC → Europe/Amsterdam
string derivedBookingDate = localBookingDT.ToString("yyyy-MM-dd");
// Store derivedBookingDate instead of API bookingDate
```

### 6.4 Rationale

| **Aspect** | **Justification** |
|------------|-------------------|
| **Consistency** | Matches original Rabobank CAMT.053 behavior |
| **Compliance** | Follows bank guidance for timezone handling |
| **Performance** | One-time conversion during import (no export overhead) |
| **Audit Trail** | Full traceability via debug logging |
| **Data Quality** | Eliminates timezone-related date discrepancies |

### 6.5 Audit Compliance

**Data Transformation Documentation:**
- **Source**: Rabobank BAI API
- **Transformation**: UTC to Local Date Conversion  
- **Justification**: Bank guidance for timezone consistency
- **Traceability**: Original and derived values logged for audit purposes

**Bank Confirmation:**
```
Date: [TO BE FILLED]
Channel: [Email/Phone/Support Ticket]
Contact: [Rabobank contact person/department]
Confirmation: "Rabobank confirmed that for timezone consistency in CAMT.053/MT940 exports, 
bookingDate should be derived from raboBookingDateTime converted to local timezone, 
rather than using the UTC-based bookingDate from the API response."
Reference: [Ticket number/email reference]
```

---

## 7. Recommendations and Extensions

| Component            | Recommendation                                                                                                  |
| -------------------- | --------------------------------------------------------------------------------------------------------------- |
| **Retries**          | Use `attempt_nr` to make repeated API calls unique and fully traceable.                                         |
| **Other tables**     | Add `(audit_id, attempt_nr)` as FK to `bai_api_audit_log` in all tables.                                        |
| **Timezones**        | Use `TIMESTAMPTZ` for correct handling of UTC times from the API.                                               |
| **Data consistency** | Use `CHECK` constraint for allowed balanceTypes and validate critical fields (IBAN, amounts).                   |
| **Indexing**         | **Balances:** `(iban, balance_type, reference_date)` and `(audit_id, attempt_nr)`<br>**Transactions:** `(iban, booking_date)`, `(entry_reference)`, `(debtor_iban)`, `(creditor_iban)`, `(end_to_end_id)`, `(mandate_id)` |
| **Archiving**        | **Annual** archiving for low-frequency environments with 480K transactions/year.                          |
| **Logging**          | Add `endpoint` and `response_time_ms` to audit table for performance analysis.                                  |
| **Duplicate detect** | For transactions: use `entry_reference` as natural unique identifier to prevent duplicate inserts (UNIQUE index). |
| **Performance**      | Low query frequency: archive transactions older than **3 years** to maintain long-term reporting capabilities.            |
| **Full-text search** | For searching in `remittance_information_unstructured`: consider a GIN index: `CREATE INDEX idx_bai_tx_remittance_gin ON bai_rabobank_transactions USING gin(to_tsvector('dutch', remittance_information_unstructured));` |
| **SEPA fields**      | `creditor_id` and `mandate_id` are only present for SEPA direct debit transactions.                             |
| **Multi-currency**   | For currency conversions, `currency_exchange_*` and `instructed_amount_*` fields are populated.                 |
| **Ultimate parties** | `ultimate_debtor` and `ultimate_creditor` are mainly used for batch/corporate payments.                         |
| **Low Volume Strategy** | For 480K/year with infrequent queries: 3-year production retention, annual archiving, quarterly monitoring, maintains multi-year reporting. |

---

## 8. Archiving Strategy

### 8.1 Why Archive?

**Transactions table grows rapidly:**
- 28 accounts with in total 40.000 transactions per month
- 480.000 transactions per year

**Performance degradation:**
- Indexes become larger and slower
- Queries over large datasets take longer
- Backup/restore time increases

### 8.2 Recommended Archiving Strategy

#### Option A: Separate Archive Table (Recommended)

**Advantages:**
- Production data remains small and fast
- Archive remains available for reporting
- Easy to restore if needed

```sql
-- Archive table for transactions (identical structure)
CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_transactions_archive (
    LIKE rpa_data.bai_rabobank_transactions INCLUDING ALL
);

-- Partitioning on booking_date for fast archiving per year
CREATE INDEX IF NOT EXISTS idx_bai_tx_archive_booking_year 
    ON rpa_data.bai_rabobank_transactions_archive (EXTRACT(YEAR FROM booking_date), iban);

-- Annual archiving: move data older than 3 years
INSERT INTO rpa_data.bai_rabobank_transactions_archive
SELECT * FROM rpa_data.bai_rabobank_transactions
WHERE booking_date < CURRENT_DATE - INTERVAL '3 years';

-- Delete archived data from production table
DELETE FROM rpa_data.bai_rabobank_transactions
WHERE booking_date < CURRENT_DATE - INTERVAL '3 years';

-- VACUUM to reclaim space
VACUUM ANALYZE rpa_data.bai_rabobank_transactions;
```

**Automation with Scheduled Job:**

```sql
-- PostgreSQL cron extension (install once)
CREATE EXTENSION IF NOT EXISTS pg_cron;

-- Annual archiving (first day of year at 02:00)
SELECT cron.schedule(
    'archive-transactions-annual',
    '0 2 1 1 *',  -- Cron syntax: minute hour day month weekday (January 1st)
    $$
    BEGIN;
    INSERT INTO rpa_data.bai_rabobank_transactions_archive
    SELECT * FROM rpa_data.bai_rabobank_transactions
    WHERE booking_date < CURRENT_DATE - INTERVAL '3 years'
    ON CONFLICT DO NOTHING;
    
    DELETE FROM rpa_data.bai_rabobank_transactions
    WHERE booking_date < CURRENT_DATE - INTERVAL '3 years';
    
    VACUUM ANALYZE rpa_data.bai_rabobank_transactions;
    COMMIT;
    $$
);
```

#### Option B: Table Partitioning (PostgreSQL 10+)

**Advantages:**
- Automatic data separation by period
- Easy to drop/archive old partitions
- Queries remain fast through partition pruning

```sql
-- Main table with partitioning
CREATE TABLE rpa_data.bai_rabobank_transactions (
    id UUID DEFAULT gen_random_uuid(),
    audit_id UUID NOT NULL,
    attempt_nr SMALLINT DEFAULT 1,
    -- ... all other columns ...
    booking_date DATE NOT NULL,
    retrieved_at TIMESTAMPTZ DEFAULT now()
) PARTITION BY RANGE (booking_date);

-- Partitions per year
CREATE TABLE rpa_data.bai_tx_2023 PARTITION OF rpa_data.bai_rabobank_transactions
    FOR VALUES FROM ('2023-01-01') TO ('2024-01-01');

CREATE TABLE rpa_data.bai_tx_2024 PARTITION OF rpa_data.bai_rabobank_transactions
    FOR VALUES FROM ('2024-01-01') TO ('2025-01-01');

CREATE TABLE rpa_data.bai_tx_2025 PARTITION OF rpa_data.bai_rabobank_transactions
    FOR VALUES FROM ('2025-01-01') TO ('2026-01-01');

-- Indexes on each partition
CREATE INDEX idx_bai_tx_2025_iban ON rpa_data.bai_tx_2025 (iban, booking_date DESC);
CREATE INDEX idx_bai_tx_2025_entry_ref ON rpa_data.bai_tx_2025 (entry_reference);

-- Archive old partition (e.g., 2023)
-- Step 1: Detach partition
ALTER TABLE rpa_data.bai_rabobank_transactions DETACH PARTITION rpa_data.bai_tx_2023;

-- Step 2: Rename to archive table
ALTER TABLE rpa_data.bai_tx_2023 RENAME TO bai_tx_2023_archive;

-- Step 3: Move to separate tablespace (optional)
ALTER TABLE rpa_data.bai_tx_2023_archive SET TABLESPACE archive_tablespace;

-- Step 4: Export to file (optional)
COPY rpa_data.bai_tx_2023_archive TO '/backup/transactions_2023.csv' WITH CSV HEADER;

-- Step 5: Drop old partition (after backup!)
-- DROP TABLE rpa_data.bai_tx_2023_archive;
```

**Automatically Create New Partitions:**

```sql
-- Function to automatically create next year partition
CREATE OR REPLACE FUNCTION create_next_year_partition()
RETURNS void AS $$
DECLARE
    next_year INT := EXTRACT(YEAR FROM CURRENT_DATE) + 1;
    partition_name TEXT := 'bai_tx_' || next_year;
    start_date DATE := (next_year || '-01-01')::DATE;
    end_date DATE := ((next_year + 1) || '-01-01')::DATE;
BEGIN
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF rpa_data.bai_rabobank_transactions
         FOR VALUES FROM (%L) TO (%L)',
        partition_name, start_date, end_date
    );
    
    -- Create indexes
    EXECUTE format('CREATE INDEX IF NOT EXISTS idx_%I_iban ON rpa_data.%I (iban, booking_date DESC)', partition_name, partition_name);
    EXECUTE format('CREATE INDEX IF NOT EXISTS idx_%I_entry_ref ON rpa_data.%I (entry_reference)', partition_name, partition_name);
END;
$$ LANGUAGE plpgsql;

-- Schedule to create new partition on December 1st
SELECT cron.schedule(
    'create-next-year-partition',
    '0 0 1 12 *',  -- December 1st at 00:00
    'SELECT create_next_year_partition();'
);
```

### 8.3 Balance Archiving

**Balances grow slower** (3 records per day per account), but can also be archived:

```sql
-- Archive table for balances
CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_balances_archive (
    LIKE rpa_data.bai_rabobank_balances INCLUDING ALL
);

-- Archive balances older than 3 years (annually)
INSERT INTO rpa_data.bai_rabobank_balances_archive
SELECT * FROM rpa_data.bai_rabobank_balances
WHERE reference_date < CURRENT_DATE - INTERVAL '3 years'
   OR last_change_datetime < CURRENT_DATE - INTERVAL '3 years';

DELETE FROM rpa_data.bai_rabobank_balances
WHERE reference_date < CURRENT_DATE - INTERVAL '3 years'
   OR last_change_datetime < CURRENT_DATE - INTERVAL '3 years';
```

### 8.4 Audit Log Archiving

**Maintaining Audit Log Links:**

**IMPORTANT:** When archiving transactions/balances, consider the Foreign Key constraint to `bai_api_audit_log`.

**Option 1: Archive Audit Log Too**

```sql
-- Archive table for audit log
CREATE TABLE IF NOT EXISTS rpa_data.bai_api_audit_log_archive (
    LIKE rpa_data.bai_api_audit_log INCLUDING ALL
);

-- Archive audit records linked to archived transactions
INSERT INTO rpa_data.bai_api_audit_log_archive
SELECT DISTINCT a.*
FROM rpa_data.bai_api_audit_log a
WHERE a.timestamp < CURRENT_DATE - INTERVAL '3 years';

-- Delete audit records (only after archiving transactions!)
DELETE FROM rpa_data.bai_api_audit_log
WHERE timestamp < CURRENT_DATE - INTERVAL '3 years';
```

**Option 2: Keep Audit Log, Archive Only Payloads**

```sql
-- Drop FK constraint when archiving, recreate without cascade
ALTER TABLE IF EXISTS rpa_data.bai_rabobank_transactions_archive
DROP CONSTRAINT IF EXISTS bai_rabobank_transactions_audit_id_attempt_nr_fkey;

-- Audit log stays in production, archived transactions have no FK
```

### 8.5 Retention Periods (Legal)

**Netherlands (Fiscal):**
- **Minimum 7 years** for fiscal administration (Dutch Bookkeeping Records Retention Act)
- **Bank transactions:** retention requirement starts January 1st after end of fiscal year
- **Recommendation:** 
  - Production data: **3 years** (fast access, supports multi-year reporting)
  - Archive data: **4 years** (online available)
  - Cold storage: **7+ years** (tape/cloud backup)

**Recommended Retention Schema:**

| Period | Storage | Availability | Action |
|---------|--------|-----------------|-------|
| 0-3 years | Production DB | Immediate (< 1s) | Active queries, multi-year reporting |
| 3-7 years | Archive DB | Fast (< 10s) | Historical reporting, audits |
| 7+ years | Cold storage / tape | Very slow (days) | Compliance |

### 8.6 Archiving Implementation Plan

**Step-by-Step:**

1. **Month 1: Setup Archive Tables**
```sql
   CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_transactions_archive (...);
   CREATE TABLE IF NOT EXISTS rpa_data.bai_rabobank_balances_archive (...);
   CREATE TABLE IF NOT EXISTS rpa_data.bai_api_audit_log_archive (...);
```2. **Month 2: Test Run Archiving** (dry-run with SELECT)
   ```sql
-- Count how many records will be archived
   SELECT COUNT(*) FROM rpa_data.bai_rabobank_transactions
   WHERE booking_date < CURRENT_DATE - INTERVAL '3 years';
   ```

3. **Month 3: First Archiving** (with backup!)
   ```sql
   -- Create backup BEFORE archiving
   pg_dump -t rpa_data.bai_rabobank_transactions > backup_before_archive.sql
   
   -- Execute archiving
   INSERT INTO ... archive SELECT ...
   DELETE FROM ... WHERE ...
   VACUUM ANALYZE;
   ```

4. **Year 2+: Automation**
   ```sql
   -- Schedule annual job
   SELECT cron.schedule(...);
   ```

### 8.7 Monitoring and Alerts

```sql
-- View to monitor archiving status
CREATE OR REPLACE VIEW v_archiving_status AS
SELECT 
    'Transactions Production' as table_name,
    COUNT(*) as record_count,
    MIN(booking_date) as oldest_date,
    MAX(booking_date) as newest_date,
    pg_size_pretty(pg_total_relation_size('rpa_data.bai_rabobank_transactions')) as table_size
FROM rpa_data.bai_rabobank_transactions
UNION ALL
SELECT 
    'Transactions Archive',
    COUNT(*),
    MIN(booking_date),
    MAX(booking_date),
    pg_size_pretty(pg_total_relation_size('rpa_data.bai_rabobank_transactions_archive'))
FROM rpa_data.bai_rabobank_transactions_archive
UNION ALL
SELECT 
    'Balances Production',
    COUNT(*),
    MIN(COALESCE(reference_date, last_change_datetime::DATE)),
    MAX(COALESCE(reference_date, last_change_datetime::DATE)),
    pg_size_pretty(pg_total_relation_size('rpa_data.bai_rabobank_balances'))
FROM rpa_data.bai_rabobank_balances;

-- Check if archiving is needed
SELECT * FROM v_archiving_status;
```

**Alert Query (Run Quarterly):**

```sql
-- Warning if production table becomes too large
SELECT 
    CASE 
        WHEN COUNT(*) > 1500000 THEN 'WARNING: Transactions table > 1.5M records - consider archiving!'
        WHEN MIN(booking_date) < CURRENT_DATE - INTERVAL '3 years' THEN 'WARNING: Data older than 3 years present'
        ELSE 'OK'
    END as status,
    COUNT(*) as records,
    MIN(booking_date) as oldest
FROM rpa_data.bai_rabobank_transactions;
```

### 8.8 Restore Procedure (Emergency Recovery)

**If you need to restore archived data:**

```sql
-- Restore specific year from archive
INSERT INTO rpa_data.bai_rabobank_transactions
SELECT * FROM rpa_data.bai_rabobank_transactions_archive
WHERE booking_date BETWEEN '2023-01-01' AND '2023-12-31';

-- Or: re-attach partition (when using partitioning)
ALTER TABLE rpa_data.bai_rabobank_transactions 
ATTACH PARTITION rpa_data.bai_tx_2023_archive 
FOR VALUES FROM ('2023-01-01') TO ('2024-01-01');
```

---

**Summary Recommendations:**
- **Option A (Separate archive)** for simplicity and flexibility
- **Option B (Partitioning)** for large volumes (> 10M records/year)
- **Automate** with pg_cron
- **Monitor** monthly with archiving_status view
- **Retain** minimum 7 years (legal requirement)
- **Test** restore procedure annually

---

**Result:** This final setup provides a scalable, audit-proof, and performant structure for storing and analyzing Rabobank **balance and transactions payloads**, including retry support within an RPA/UiPath-driven environment. All data is traceable via `audit_id` and `attempt_nr` back to the original API call.
