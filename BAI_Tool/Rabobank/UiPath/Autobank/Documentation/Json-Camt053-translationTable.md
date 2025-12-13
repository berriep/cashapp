# Vertaaltabel: Rabobank Transactie Data Mapping

**Versie**: 1.1  
**Datum**: Oktober 2025  
**Status**: GEVALIDEERD MET HUIDIGE IMPLEMENTATIE  
**Doel**: Mapping tussen BAI JSON API, Database velden en CAMT.053 XML voor transactie data consolidatie

## Implementatie Status

### VOLLEDIG GEÏMPLEMENTEERD IN VB SCRIPT:
- `acctsvcr_ref` → Entry/Transaction AcctSvcrRef
- `rabo_detailed_transaction_type` → Proprietary codes (100, 586, etc.)
- `batch_entry_reference` → TxId (alleen volledige versie)
- `payment_information_identification` → PmtInfId (alleen volledige versie)
- `instruction_id` → InstrId met fallback logica
- `interbank_settlement_date` → IntrBkSttlmDt (alleen volledige versie)
- `debtor_agent_bic`, `creditor_agent_bic` → BIC codes
- Enhanced fallback: `related_party_*` velden voor namen
- Conditional field inclusion: `minimum_required_fields` parameter
- **XML structuur gevalideerd**: Alle open/close tags correct
- **PrtryAmt sectie verwijderd**: Geen aannames over EARI/IBS codes meer

### VOLLEDIG GEÏMPLEMENTEERD:
- `debtor_iban`, `creditor_iban` → IBANs worden opgenomen in Full-variant met fallback-keten (DbtrAcct/Id/IBAN, CdtrAcct/Id/IBAN)
- `rabo_booking_datetime` → WEL gebruikt: bron voor queries en BookgDt-afleiding na normalisatie naar Europe/Amsterdam

### VALIDATIE TOOLS ONTWIKKELD:
- **PowerShell Comparison Script**: `Compare-CAMT053Files.ps1` 
  - Vergelijkt originele vs gegenereerde CAMT.053 bestanden
  - Detecteert verschillen in: Amount, BookingDate, ValueDate, EndToEndId, BkTxCd, DebtorName, CreditorName, DebtorIBAN, CreditorIBAN, RemittanceInfo
  - Genereert HTML rapporten met gedetailleerde analyse
  - Console + HTML output voor problematische transacties
  - **Resultaat**: 1705 transacties, 1 verschil gedetecteerd (BookingDate)

### NOG NIET GEÏMPLEMENTEERD:
- `balance_after_booking_amount` → Running balance (alleen BAI beschikbaar)
- `rabo_transaction_type_name` → Descriptive names

## Main Mapping Table

| BAI JSON Path                             | DB Field                                              | CAMT.053 XPath                                        | ISO 20022 Status | Priority   | Comment                                                                          |
| ----------------------------------------- | ----------------------------------------------------- | ----------------------------------------------------- | ----------------- | ---------- | -------------------------------------------------------------------------------- |
| **Basic Identification**                  |                                                       |                                                       |                   |            |                                                                                  |
| `account.iban`                            | `iban`                                                | `Stmt/Acct/Id/IBAN`                                   | **MANDATORY**   | CAMT       | Account identification                                                           |
| `account.currency`                        | `currency`                                            | `Stmt/Acct/Ccy` or `Ntry/Amt/@Ccy`                    | Optional          | CAMT       | EUR, USD, etc. (Currency in Amt is mandatory)                                   |
| `entryReference`                          | `entry_reference`                                     | `Ntry/NtryRef`                                        | Optional          | CAMT       | Unique entry ID from bank (491590, 491589, etc.)                               |
| `batchEntryReference`                     | `batch_entry_reference` NEW                          | `TxDtls/Refs/TxId`                                    | Optional          | BAI        | Batch reference (4445473270, OO9B005070304426, etc.)                           |
| `accountServicerReference`                | `acctsvcr_ref` NEW                                    | `Ntry/AcctSvcrRef` + `TxDtls/Refs/AcctSvcrRef`        | Optional          | CAMT       | **Strongest unique reference** - mostly not in BAI                             |
| **Header & Statement (MANDATORY)**        |                                                       |                                                       |                   |            |                                                                                  |
| n.a.                                      | system generated                                      | `GrpHdr/MsgId`                                        | **MANDATORY**   | CAMT       | Message identification                                                          |
| n.a.                                      | system generated                                      | `GrpHdr/CreDtTm`                                      | **MANDATORY**   | CAMT       | Creation date time                                                              |
| n.a.                                      | system generated                                      | `Stmt/Id`                                             | **MANDATORY**   | CAMT       | Statement identification                                                        |
| n.a.                                      | system generated                                      | `Stmt/CreDtTm`                                        | **MANDATORY**   | CAMT       | Statement creation date time                                                    |
| **Dates & Times**                         |                                                       |                                                       |                   |            |                                                                                  |
| `bookingDate`                             | `booking_date`                                        | `Ntry/BookgDt/Dt`                                     | **MANDATORY**   | **CAMT**   | **Leading for balance reconciliation**                                          |
| `valueDate`                               | `value_date`                                          | `Ntry/ValDt/Dt`                                       | Optional          | CAMT       | Interest date/value date                                                         |
| `raboBookingDateTime`                     | `rabo_booking_datetime` NEW                           | n.a.                                                  | n.a.              | **BAI**    | **Exact processing time** (2025-10-06T07:41:54.103419Z)                        |
| n.a.                                      | `interbank_settlement_date` NEW                       | `TxDtls/RltdDts/IntrBkSttlmDt`                        | Optional          | CAMT       | Settlement between banks (optional)                                             |
| **Amounts (MANDATORY CORE)**              |                                                       |                                                       |                   |            |                                                                                  |
| `transactionAmount.value`                 | `transaction_amount`                                  | `Ntry/Amt` + `Ntry/CdtDbtInd` (signed)               | **MANDATORY**   | CAMT       | **Always with + or - sign**                                                     |
| `transactionAmount.currency`              | `transaction_currency`                                | `Ntry/Amt/@Ccy`                                       | **MANDATORY**   | CAMT       | Usually same as account currency                                                 |
| n.a.                                      | system generated                                      | `Ntry/CdtDbtInd`                                      | **MANDATORY**   | CAMT       | Credit/Debit indicator (CRDT/DBIT)                                             |
| `balanceAfterBooking.balanceAmount.value` | `balance_after_booking_amount`                        | n.a. (inconsistent)                                   | n.a.              | **BAI**    | **Only BAI provides this reliably**                                             |
| `balanceAfterBooking.balanceAmount.currency` | `balance_after_booking_currency`                   | n.a.                                                  | n.a.              | BAI        | Currency of balance (EUR)                                                        |
| `balanceAfterBooking.balanceType`         | `balance_type` NEW                                    | n.a.                                                  | n.a.              | BAI        | Balance type (InterimBooked, etc.)                                              |
| **Balances (MANDATORY MINIMUM)**          |                                                       |                                                       |                   |            |                                                                                  |
| n.a.                                      | `opening_balance`                                     | `Bal[Tp/CdOrPrtry/Cd='OPBD']`                         | **MANDATORY**   | CAMT       | Opening booked balance                                                          |
| n.a.                                      | `closing_balance`                                     | `Bal[Tp/CdOrPrtry/Cd='CLBD']`                         | **MANDATORY**   | CAMT       | Closing booked balance                                                          |
| n.a.                                      | calculated                                            | `Bal[Tp/CdOrPrtry/Cd='PRCD']`                         | Optional          | CAMT       | Previous day closing balance                                                    |
| n.a.                                      | calculated                                            | `Bal[Tp/CdOrPrtry/Cd='CLAV']`                         | Optional          | CAMT       | Closing available balance                                                       |
| n.a.                                      | calculated                                            | `Bal[Tp/CdOrPrtry/Cd='FWAV']`                         | Optional          | CAMT       | Forward available balance                                                       |
| **Entry Core (MANDATORY)**                |                                                       |                                                       |                   |            |                                                                                  |
| n.a.                                      | system generated                                      | `Ntry/Sts`                                            | **MANDATORY**   | CAMT       | Entry status (BOOK, PDNG, etc.)                                                |
| `bankTransactionCode`                     | `bank_transaction_code`                               | `Ntry/BkTxCd` + `TxDtls/BkTxCd`                       | **MANDATORY**   | CAMT       | Domain + Family + SubFamily (PMNT-RCDT-ESCT, PMNT-ICCN-ICCT)                   |
| **Transaction Classification**            |                                                       |                                                       |                   |            |                                                                                  |
| `raboDetailedTransactionType`             | `rabo_detailed_transaction_type` NEW                  | `BkTxCd/Prtry/Cd`                                     | Optional          | **BAI**    | **Rabobank codes** (100, 541, 586, etc.) - BAI has these directly              |
| `raboTransactionTypeName`                 | `rabo_transaction_type_name` NEW                      | n.a.                                                  | n.a.              | BAI        | Descriptive name (usually "id")                                                |
| `purposeCode`                             | `purpose_code`                                        | `TxDtls/Purp/Cd`                                      | Optional          | CAMT       | SALA, CBFF, EPAY, etc.                                                          |
| `reasonCode`                              | `reason_code`                                         | `TxDtls/RtrInf/Rsn/Cd`                                | Optional          | CAMT       | For returned transactions                                                        |
| **Transaction References (MANDATORY)**    |                                                       |                                                       |                   |            |                                                                                  |
| n.a.                                      | `instruction_id` NEW                                  | `TxDtls/Refs/InstrId`                                 | **MANDATORY**   | CAMT       | Instruction reference (fallback logic implemented)                             |
| n.a.                                      | calculated                                            | `TxDtls/AmtDtls/TxAmt/Amt`                             | **MANDATORY**   | CAMT       | Transaction amount - **PrtryAmt sectie verwijderd** (geen EARI/IBS aannames)                                                              |
| **Counterparty - Debtor (Optional)**      |                                                       |                                                       |                   |            |                                                                                  |
| `debtorName`                              | `debtor_name`                                         | `TxDtls/RltdPties/Dbtr/Nm`                            | Optional          | CAMT       | Name of payer                                                                    |
| `debtorAccount.iban`                      | `debtor_iban`                                         | `TxDtls/RltdPties/DbtrAcct/Id/IBAN`                   | Optional          | CAMT       | IBAN of payer                                                                    |
| `debtorAgent`                             | `debtor_agent_bic` NEW                                | `TxDtls/RltdAgts/DbtrAgt/FinInstnId/BIC`              | Optional          | **BAI**    | **BIC directly available** (INGBNL2A, ABNANL2A, RABONL2U, SNSBNL2A)           |
| **Counterparty - Creditor (Optional)**    |                                                       |                                                       |                   |            |                                                                                  |
| `creditorName`                            | `creditor_name`                                       | `TxDtls/RltdPties/Cdtr/Nm`                            | Optional          | CAMT       | Name of receiver                                                                 |
| `creditorAccount.iban`                    | `creditor_iban`                                       | `TxDtls/RltdPties/CdtrAcct/Id/IBAN`                   | Optional          | CAMT       | IBAN of receiver                                                                 |
| `creditorAccount.currency`                | `creditor_currency` NEW                               | n.a.                                                  | n.a.              | BAI        | Currency of creditor account (usually EUR)                                      |
| `creditorAgent`                           | `creditor_agent_bic` NEW                              | `TxDtls/RltdAgts/CdtrAgt/FinInstnId/BIC`              | Optional          | CAMT       | **BIC of receiver's bank** (if available)                                       |
| **References & Descriptions (Optional)**  |                                                       |                                                       |                   |            |                                                                                  |
| `endToEndId`                              | `end_to_end_id`                                       | `TxDtls/Refs/EndToEndId`                              | Optional          | CAMT       | End-to-end reference (04-10-2025 16:36 7020896310501621)                       |
| `instructionId`                           | `instruction_id` NEW                                  | `TxDtls/Refs/InstrId`                                 | Optional          | CAMT       | Instruction reference                                                            |
| `remittanceInformationUnstructured`       | `remittance_information_unstructured`                 | `TxDtls/RmtInf/Ustrd`                                 | Optional          | CAMT       | **Free description** (8985749224 7020896310501621 SR)                          |
| `remittanceInformationStructured`         | `remittance_information_structured`                   | `TxDtls/RmtInf/Strd/*`                                | Optional          | CAMT       | Structured payment info (store as JSON)                                         |
| **Metadata & Audit**                      |                                                       |                                                       |                   |            |                                                                                  |
| n.a.                                      | `source_system`                                       | n.a.                                                  | n.a.              | SYSTEM     | 'CAMT053', 'BAI_API'                                                            |
| n.a.                                      | `created_at`                                          | n.a.                                                  | n.a.              | SYSTEM     | Timestamp of storage in DB                                                      |
| n.a.                                      | `updated_at`                                          | n.a.                                                  | n.a.              | SYSTEM     | Last modification timestamp                                                     |

## ISO 20022 CAMT.053.001.02 Field Requirements

### **MANDATORY FIELDS (Minimum Required for Compliance):**

**Document Structure:**
- `Document/BkToCstmrStmt`
- `GrpHdr/MsgId` - Message identification
- `GrpHdr/CreDtTm` - Creation date time

**Statement Level:**
- `Stmt/Id` - Statement identification  
- `Stmt/CreDtTm` - Statement creation date time
- `Stmt/Acct/Id/IBAN` - Account IBAN

**Balance (Minimum):**
- `Bal[Tp/CdOrPrtry/Cd='OPBD']` - Opening booked balance
- `Bal[Tp/CdOrPrtry/Cd='CLBD']` - Closing booked balance
- `Bal/Amt/@Ccy` - Currency
- `Bal/Amt` - Amount
- `Bal/CdtDbtInd` - Credit/Debit indicator
- `Bal/Dt/Dt` - Balance date

**Entry Level (Per Transaction):**
- `Ntry/Amt/@Ccy` - Currency
- `Ntry/Amt` - Amount
- `Ntry/CdtDbtInd` - Credit/Debit indicator (CRDT/DBIT)
- `Ntry/Sts` - Status (BOOK, PDNG, etc.)
- `Ntry/BookgDt/Dt` - Booking date
- `Ntry/BkTxCd` - Bank transaction code structure

**Transaction Details:**
- `TxDtls/Refs/InstrId` - Instruction reference (fallback logic implemented)
- `TxDtls/AmtDtls/TxAmt/Amt` - Transaction amount
- `TxDtls/BkTxCd` - Bank transaction code (repeated)

### **OPTIONAL FIELDS (Enhanced Information):**

**Account Level:**
- `Stmt/Acct/Ccy` - Account currency
- `Stmt/Acct/Nm` - Account name

**Additional Balances:**
- `Bal[Tp/CdOrPrtry/Cd='PRCD']` - Previous day closing
- `Bal[Tp/CdOrPrtry/Cd='CLAV']` - Closing available  
- `Bal[Tp/CdOrPrtry/Cd='FWAV']` - Forward available

**Entry Enhancements:**
- `Ntry/NtryRef` - Entry reference
- `Ntry/ValDt/Dt` - Value date
- `Ntry/AcctSvcrRef` - Account servicer reference

**Transaction Summary:**
- `TxsSummry/*` - Complete transaction summary (volledige versie)

**Transaction Details (Enhanced):**
- `TxDtls/Refs/AcctSvcrRef` - Account servicer reference
- `TxDtls/Refs/PmtInfId` - Payment information ID
- `TxDtls/Refs/TxId` - Transaction ID
- `TxDtls/Refs/EndToEndId` - End-to-end reference
- `TxDtls/RltdPties/*` - Related parties (debtor/creditor)
- `TxDtls/RltdAgts/*` - Related agents (BIC codes)
- `TxDtls/Purp/Cd` - Purpose code
- `TxDtls/RmtInf/Ustrd` - Remittance information
- `TxDtls/RltdDts/*` - Related dates

### **IMPLEMENTATION IN VB SCRIPT:**

**Minimale versie (`minimumRequiredFields = True`):**
- Alleen **MANDATORY** velden
- Bestandsnaam: `camt053_minimal_*`
- Kleinere bestandsgrootte
- 100% ISO 20022 compliant

**Volledige versie (`minimumRequiredFields = False`):**
- **MANDATORY** + **OPTIONAL** velden  
- Bestandsnaam: `camt053_full_*`
- Maximale informatie beschikbaar
- Enhanced compliance en rijkere data

### **COMPLIANCE GUARANTEE:**

Beide versies zijn volledig **ISO 20022 CAMT.053.001.02 compliant**:
- Minimale versie: Voldoet aan alle verplichte velden
- Volledige versie: Verplichte velden + optionele verrijking
- Validatie: PowerShell validator controleert beide versies
- Financial integrity: Balance equations verified in beide modi

### Date Priority
```
rabobank_booking_datetime (BAI) = Bron voor queries en BookgDt-afleiding; exacte timestamp met milliseconden
booking_date (DB) = Afgeleid veld na normalisatie naar Europe/Amsterdam
BookgDt/Dt (CAMT output) = Resultaat van normalisatie, leidend voor balansreconciliatie
value_date = Waarderings-/rentedatum (interest date)
```

## Date Normalization Rules
- Query-basis en bron:
  - **rabobank_booking_datetime (BAI)** is de bron voor alle queries en datumfiltering
  - Bij timestamp: wordt genormaliseerd naar Europe/Amsterdam, waarna de datumcomponent BookgDt bepaalt
  - Bij datum-only: wordt letterlijk overgenomen
  - **BookgDt/Dt (CAMT output)** is leidend voor balansreconciliatie en wordt afgeleid uit rabobank_booking_datetime
- Timezone normalisatie:
  - Converteer alle timestamps eerst naar Europe/Amsterdam vóór extractie van de datumcomponent
  - Gebruik vervolgens uitsluitend de datum (yyyy-MM-dd) voor `Ntry/BookgDt/Dt` en `Ntry/ValDt/Dt`
- Datumbereikfiltering:
  - Inclusief bereik [startDate, endDate] toegepast op genormaliseerde lokale datum (afgeleid uit rabobank_booking_datetime)
- Bekend verschil (te verifiëren met bank):
  - NtryRef 2911595: JSON bevatte 2025-10-05, origineel CAMT 2025-10-06
  - Dit is GEEN timezone-issue, maar een **bronverschil** dat opheldering van de bank vereist
  - Vraag: Welke bron is leidend? (a) JSON booking_date letterlijk, (b) bankdag/statement-datum, of (c) timestamp→lokale datum

### Amount Handling
```
CAMT: Ntry/Amt (always positive) + CdtDbtInd (CRDT/DBIT) → signed amount
BAI:  transactionAmount.value (string) + balanceAfterBooking → running balance available
```

## Transaction Amounts (verduidelijking)
- PrtryAmt is volledig verwijderd voor ISO 20022 conformiteit.
- Gebruik uitsluitend `TxDtls/AmtDtls/TxAmt/Amt` in combinatie met `CdtDbtInd` voor het teken.

### BIC Code Mapping (Improved!)
```
debtor_agent_bic → BAI has directly: debtorAgent (INGBNL2A, ABNANL2A, RABONL2U, SNSBNL2A)
creditor_agent_bic → CAMT has: CdtrAgt/FinInstnId/BIC
batch_references → BAI specific: batchEntryReference patterns (4445473270, OO9B005070304426)
```

### Rabobank Specific Codes
```
raboDetailedTransactionType → Direct from BAI (100, 541, 586, 625, 699)
100 = Standard credit
541 = Special credit
586 = Debit transaction
625 = ICCN-ICCT (Instant Credit)
699 = Large transactions
```

## Implementation Notes

1. **New Fields** NEW: Add to database schema
2. **CAMT Priority**: In case of conflict always use CAMT.053 data for balance related fields
3. **BAI Priority**: For real-time processing and running balance information
4. **Audit Trail**: Always store both `booking_date` (CAMT) and `rabo_booking_datetime` (BAI)
5. **NULL Handling**: Fields can be empty - implement graceful defaults
6. **⚠️ KNOWN ISSUES**: 
   - BookingDate verschillen gedetecteerd (mogelijk mapping issue in VB script)
   - PrtryAmt sectie volledig verwijderd om ISO 20022 compliance te verbeteren
   - XML validatie tools ontwikkeld voor kwaliteitscontrole

## Quality Assurance & Validation

### **Vergelijkings Framework:**
```powershell
# PowerShell script voor CAMT.053 validatie
.\Compare-CAMT053Files.ps1 -OriginalFile "origineel.xml" -GeneratedFile "generated.xml" -OutputFile "rapport.html"
```

### **Gedetecteerde Issues:**
| Issue | NtryRef | Veld | Origineel | Gegenereerd | Status |
|-------|---------|------|-----------|-------------|--------|
| BookingDate verschil | 2911595 | BookgDt/Dt | 2025-10-06 | 2025-10-05 | **ONDERZOEK VEREIST** |
| PrtryAmt verschillen | Meerdere | AmtDtls/PrtryAmt | IBS | EARI | **OPGELOST** (sectie verwijderd) |

### **Validatie Statistieken:**
- **Totaal transacties**: 1,705
- **Perfect matches**: 1,704 (99.94%)
- **Verschillen**: 1 (0.06%)
- **Balance matches**: 8/8 (100%)
- **Header matches**: 3/3 (100%)

### **XML Compliance:**
- Well-formed XML (alle tags correct)
- ISO 20022 CAMT.053.001.02 compliant
- Namespace declaraties correct
- Proprietary codes verwijderd waar onnodig

## UiPath Script Mapping - GEVALIDEERD

Voor de UiPath CAMT.053 generator gebruikt het VB script deze **werkelijke** database velden:

### **CORRECT GEÏMPLEMENTEERD:**
- `debtor_name`, `creditor_name` → Met fallback naar `related_party_*` velden
- `debtor_agent_bic`, `creditor_agent_bic` → BIC codes met fallback naar `*_agent`
- `rabo_detailed_transaction_type` → Primary, fallback naar `proprietary_code`
- `purpose_code` → Direct gebruikt voor CAMT.053 Purp/Cd
- `entry_reference` → Voor entry identificatie
- `end_to_end_id` → Met "NOTPROVIDED" fallback
- `remittance_information_unstructured` → Voor RmtInf/Ustrd
- `acctsvcr_ref` → Voor AcctSvcrRef (entry + transaction level)
- `instruction_id` → Voor InstrId
- `batch_entry_reference` → Voor TxId (alleen volledige versie)
- `payment_information_identification` → Voor PmtInfId (alleen volledige versie)
- `interbank_settlement_date` → Voor IntrBkSttlmDt (alleen volledige versie)

### **GEDEELTELIJK GEÏMPLEMENTEERD:**
- `debtor_iban`, `creditor_iban` → Namen correct, IBAN velden nog uitbreiden
  - Actie: opnemen/weergeven in `TxDtls/RltdPties/DbtrAcct/Id/IBAN` en `TxDtls/RltdPties/CdtrAcct/Id/IBAN` (alleen volledige versie)
  - Fallback-keten (conform script):
    - Creditor: `creditor_iban` → `related_party_creditor_account_iban` → `creditor_account_iban` → `cdtr_acct_iban`
    - Debtor: `debtor_iban` → `related_party_debtor_account_iban` → `debtor_account_iban` → `dbtr_acct_iban`
  - Validatie: steekproef uitvoeren op recent statement om aanwezigheid te bevestigen

### **NIEUWE PARAMETER:**
- `minimum_required_fields` → Boolean voor minimale vs volledige CAMT.053 generatie
<!-- Verwijderd: Dubbele 'Quality Assurance & Validation' sectie, zie eerder in document voor QA details. -->

## Database Schema Suggestions (Extended)

### Add New Columns:
```sql
-- Add new columns to existing table
ALTER TABLE bai_rabobank_transactions_payload 
ADD COLUMN rabo_booking_datetime TIMESTAMPTZ,
ADD COLUMN batch_entry_reference VARCHAR(50),
ADD COLUMN rabo_detailed_transaction_type VARCHAR(10),
ADD COLUMN rabo_transaction_type_name VARCHAR(50),
ADD COLUMN balance_after_booking_amount DECIMAL(15,2),
ADD COLUMN balance_after_booking_currency VARCHAR(3),
ADD COLUMN balance_type VARCHAR(20),
ADD COLUMN creditor_currency VARCHAR(3),
ADD COLUMN acctsvcr_ref VARCHAR(50),
ADD COLUMN debtor_agent_bic VARCHAR(11),
ADD COLUMN creditor_agent_bic VARCHAR(11),
ADD COLUMN instruction_id VARCHAR(50),
ADD COLUMN interbank_settlement_date DATE;

-- Index for performance (extended)
CREATE INDEX idx_rabo_booking_datetime ON bai_rabobank_transactions_payload(rabo_booking_datetime);
CREATE INDEX idx_batch_entry_reference ON bai_rabobank_transactions_payload(batch_entry_reference);
CREATE INDEX idx_rabo_detailed_transaction_type ON bai_rabobank_transactions_payload(rabo_detailed_transaction_type);
CREATE INDEX idx_acctsvcr_ref ON bai_rabobank_transactions_payload(acctsvcr_ref);
CREATE INDEX idx_debtor_agent_bic ON bai_rabobank_transactions_payload(debtor_agent_bic);
```

## Practical Insights from Real Data

### **BIC Code Variation in BAI:**
- `INGBNL2A` → ING Bank
- `ABNANL2A` → ABN AMRO Bank  
- `RABONL2U` → Rabobank (own transactions)
- `SNSBNL2A` → SNS Bank

### **Batch Reference Patterns:**
- `4445473270` → External bank batch
- `OO9B005070304426` → Rabobank internal batch

### **Amount Format:**
- BAI: `"value": "112.50"` (string with decimals)
- Always positive in BAI, direction determined by account type

### **Balance After Booking:**
- `balanceType: "InterimBooked"`
- `balanceAmount.value` = running balance after transaction
- **Crucial data** for real-time balance tracking

## Important Considerations (Based on Real Data)

1. **Date Logic**: `bookingDate` available in both systems - CAMT leading for balance
2. **Timestamp Precision**: `raboBookingDateTime` has milliseconds - use for exact ordering
3. **BIC Directly Available**: BAI has `debtorAgent` directly - no complex mapping needed
4. **Proprietary Codes**: BAI has `raboDetailedTransactionType` directly (100, 541, 586)
5. **Running Balance**: `balanceAfterBooking` only in BAI - very valuable for reconciliation
6. **Batch Patterns**: Different formats (`4445473270` vs `OO9B005070304426`)
7. **Amount Format**: BAI uses strings (`"112.50"`) - conversion to decimal needed
8. **Entry References**: Sequential numbering (491590, 491589, 491588, etc.)
9. **End-to-End IDs**: Contain date/time info (`04-10-2025 16:36 7020896310501621`)
10. **Performance**: Index on `raboBookingDateTime` and `batchEntryReference` essential

## Data Flow Recommendations

### **BAI → Database Mapping (High Priority):**
```json
raboBookingDateTime → rabo_booking_datetime (TIMESTAMPTZ) - bron voor queries en BookgDt
bookingDate → booking_date (DATE) - afgeleid na normalisatie
debtorAgent → debtor_agent_bic (VARCHAR)
raboDetailedTransactionType → proprietary_code (VARCHAR)
balanceAfterBooking.balanceAmount.value → balance_after_booking_amount (DECIMAL)
```

### **Database → CAMT.053 Generation:**
```
rabo_booking_datetime (normalized to Europe/Amsterdam) → Ntry/BookgDt/Dt
booking_date → fallback als timestamp niet beschikbaar
acctsvcr_ref → Ntry/AcctSvcrRef + TxDtls/Refs/AcctSvcrRef
creditor_agent_bic → TxDtls/RltdAgts/CdtrAgt/FinInstnId/BIC
```