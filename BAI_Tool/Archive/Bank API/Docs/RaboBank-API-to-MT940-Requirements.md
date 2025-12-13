# RaboBank API to MT940 Conversion Requirements

## Document Information
- **Document**: RaboBank API to MT940 Conversion Requirements
- **Version**: 1.0
- **Date**: September 2, 2025
- **Author**: Integration Team
- **Status**: Draft

---

## 1. Overview

This document outlines the requirements and conditions for converting RaboBank API balance and transaction data into MT940 (SWIFT Message Type 940) bank statement format.

MT940 is a SWIFT standard format used for electronic transmission of bank account statements, widely used in European banking for automated reconciliation processes.

---

## 2. Data Requirements

### 2.1 Mandatory Input Data

For successful MT940 generation, the following data must be available:

#### **Balance Data (2 time points required):**
- **Opening Balance** (Balance Today -2): Closing balance from previous business day
- **Closing Balance** (Balance Today -1): Closing balance from statement day

#### **Transaction Data:**
- **Transactions (Today -1)**: All transactions for the statement day

### 2.2 Data Quality Requirements

#### **Balance Data Structure:**
```json
{
  "balances": [{
    "balanceType": "closingBooked",           // Required: Must be "closingBooked"
    "amount": {
      "amount": "1234.56",                    // Required: Decimal amount
      "currency": "EUR"                       // Required: ISO 4217 currency code
    },
    "creditDebitIndicator": "CRDT",           // Required: "CRDT" or "DBIT"
    "dateTime": "2024-09-01T23:59:59Z"       // Required: ISO 8601 timestamp
  }]
}
```

#### **Transaction Data Structure:**
```json
{
  "transactions": [{
    "transactionId": "TXN123456",             // Required: Unique transaction ID
    "bookingDate": "2024-09-01",              // Required: YYYY-MM-DD format
    "valueDate": "2024-09-01",                // Required: YYYY-MM-DD format
    "transactionAmount": {
      "amount": "150.00",                     // Required: Decimal amount
      "currency": "EUR"                       // Required: ISO 4217 currency code
    },
    "creditDebitIndicator": "CRDT",           // Required: "CRDT" or "DBIT"
    "remittanceInformationUnstructured": "Payment description"  // Optional: Description
  }]
}
```

---

## 3. Technical Prerequisites

### 3.1 Required Components

#### **Configuration Files:**
- `mt940_config.json` - MT940 generation settings
- `field_mapping.json` - RaboBank API to MT940 field mappings
- `bank_codes.json` - BIC and bank identifier mappings

#### **Code Components:**
- `MT940Generator.cs` - Core MT940 generation logic
- `MT940Validator.cs` - Format validation and compliance checking
- `DataProcessor.cs` - RaboBank API data transformation

#### **Templates:**
- `mt940_template.txt` - Base MT940 structure template

### 3.2 System Requirements

#### **UiPath Environment:**
- UiPath Studio/Robot with .NET 6.0+ support
- Newtonsoft.Json package for JSON processing
- System.Text.RegularExpressions for data sanitization

#### **File System Access:**
- Read access to API data files (JSON format)
- Write access to output directory for MT940 files
- Configuration directory access

---

## 4. MT940 Format Specifications

### 4.1 Required MT940 Fields

| Field | Tag | Description | Source | Mandatory |
|-------|-----|-------------|---------|-----------|
| Transaction Reference | :20: | Unique statement reference | Generated | Yes |
| Account Identification | :25: | Bank/Account number | Configuration | Yes |
| Statement Number | :28C: | Statement sequence number | Configuration | Yes |
| Opening Balance | :60M: | Previous day closing balance | Balance Today -2 | Yes |
| Statement Line | :61: | Individual transactions | Transactions Today -1 | Yes |
| Information to Account Owner | :86: | Transaction details | Transaction descriptions | Optional |
| Closing Balance | :62M: | Current day closing balance | Balance Today -1 | Yes |

### 4.2 MT940 Structure Example

```
{1:F01DEUTDEFFXP9Y0000000000}
{2:O9402110250603DEUTDEFFCXXX00000000002506032110N}
{3:{108:                }}
{4:
:20:410193537800
:25:37070060/19353780000
:28C:00106/002
:60M:C250603EUR6694,95
:61:2506020603C2553,12NTRFNONREF//RTE2ECIeUsYWkg
:86:168?00SEPA INSTANT PAYMENT?20EREF+IPAED0EE9F8940C4
:62M:C250603EUR9343,67
-}
```

---

## 5. Business Rules and Validations

### 5.1 Balance Validation Rules

#### **Opening vs Closing Balance:**
- Opening balance must equal previous day's closing balance
- Currency must be consistent across all balance entries
- Date sequence must be logical (Today -2 < Today -1)

#### **Transaction Reconciliation:**
```
Opening Balance + Sum(Credit Transactions) - Sum(Debit Transactions) = Closing Balance
```

### 5.2 Data Transformation Rules

#### **Amount Formatting:**
- Convert decimal points to commas (1234.56 → 1234,56)
- Maintain 2 decimal places for currency amounts
- Remove leading zeros from amounts

#### **Date Formatting:**
- Convert ISO 8601 dates to YYMMDD format for MT940
- Value dates use MMDD format within statement lines
- All dates must be valid business days

#### **Text Sanitization:**
- Remove special characters from reference texts
- Limit reference text length to MT940 field constraints
- Replace invalid characters with spaces or remove them

### 5.3 Error Handling Rules

#### **Missing Data:**
- If opening balance missing: Generate error, cannot proceed
- If closing balance missing: Generate error, cannot proceed
- If transactions missing: Generate warning, create balance-only statement

#### **Data Inconsistencies:**
- Balance reconciliation failure: Generate warning, include in MT940 comments
- Invalid transaction dates: Skip transaction, log error
- Invalid amounts: Skip transaction, log error

---

## 6. Configuration Requirements

### 6.1 MT940 Configuration (mt940_config.json)

```json
{
  "SenderBIC": "DEUTDEFFXXX",
  "ReceiverBIC": "DEUTDEFFCXXX",
  "AccountNumber": "37070060/19353780000",
  "BankCode": "37070060",
  "StatementNumberPrefix": "001",
  "CurrencyCode": "EUR",
  "TimeZone": "CET",
  "BusinessDayOnly": true,
  "ValidationEnabled": true,
  "OutputFormat": "MT940",
  "OutputEncoding": "UTF-8"
}
```

### 6.2 Field Mapping Configuration (field_mapping.json)

```json
{
  "BalanceMapping": {
    "amount": "balances[0].amount.amount",
    "currency": "balances[0].amount.currency", 
    "indicator": "balances[0].creditDebitIndicator",
    "dateTime": "balances[0].dateTime"
  },
  "TransactionMapping": {
    "id": "transactionId",
    "amount": "transactionAmount.amount",
    "currency": "transactionAmount.currency",
    "indicator": "creditDebitIndicator",
    "bookingDate": "bookingDate",
    "valueDate": "valueDate",
    "reference": "remittanceInformationUnstructured"
  }
}
```

---

## 7. Quality Assurance Requirements

### 7.1 Validation Checks

#### **Pre-Generation Validation:**
- [ ] All required input files present and readable
- [ ] JSON structure validation against schema
- [ ] Date consistency validation
- [ ] Currency consistency validation
- [ ] Amount format validation

#### **Post-Generation Validation:**
- [ ] MT940 syntax validation
- [ ] Balance reconciliation verification
- [ ] Character encoding verification
- [ ] Field length compliance
- [ ] SWIFT message structure compliance

### 7.2 Testing Requirements

#### **Unit Tests:**
- Balance calculation accuracy
- Date format conversion
- Amount format conversion
- Text sanitization
- Error handling scenarios

#### **Integration Tests:**
- End-to-end MT940 generation with real API data
- Multiple currency handling
- Large transaction volume handling
- Edge case scenarios (weekend dates, holidays)

---

## 8. Security and Compliance

### 8.1 Data Security Requirements

#### **Data Handling:**
- API data must be processed in secure environment
- Generated MT940 files must be stored securely
- Temporary files must be cleaned up after processing
- No sensitive data logging in plain text

#### **Access Control:**
- Configuration files must have restricted access
- Output directory must have appropriate permissions
- Audit trail for all MT940 generation activities

### 8.2 Regulatory Compliance

#### **SWIFT Standards:**
- MT940 format must comply with SWIFT MT940 specifications
- Character sets must be SWIFT-compatible
- Field lengths must not exceed SWIFT limits
- Message structure must follow SWIFT guidelines

#### **Banking Regulations:**
- Generated statements must be audit-compliant
- Timestamps must be accurate and traceable
- Data integrity must be verifiable
- Retention policies must be followed

---

## 9. Implementation Guidelines

### 9.1 Development Process

#### **Phase 1: Setup and Configuration**
1. Create configuration files (mt940_config.json, field_mapping.json)
2. Implement MT940Generator.cs with core logic
3. Implement validation and error handling
4. Create unit tests for core functions

#### **Phase 2: Integration and Testing**
1. Integrate with UiPath workflow
2. Test with sample RaboBank API data
3. Validate MT940 output format
4. Perform reconciliation testing

#### **Phase 3: Production Deployment**
1. Security review and approval
2. Performance testing with production data volumes
3. Monitoring and alerting setup
4. Documentation and training

### 9.2 Monitoring and Maintenance

#### **Operational Monitoring:**
- MT940 generation success/failure rates
- Processing time metrics
- Data quality metrics
- Error rate tracking

#### **Maintenance Activities:**
- Regular validation of MT940 format compliance
- Configuration updates for new requirements
- Performance optimization
- Security updates and patches

---

## 10. Appendices

### Appendix A: Sample Files

#### Sample Input Files:
- `sample_balance_today_minus2.json`
- `sample_balance_today_minus1.json`
- `sample_transactions_today_minus1.json`

#### Sample Output Files:
- `sample_mt940_output.txt`

### Appendix B: Error Codes and Messages

| Error Code | Description | Action Required |
|------------|-------------|-----------------|
| MT940-001 | Missing opening balance | Provide balance data for previous day |
| MT940-002 | Missing closing balance | Provide balance data for current day |
| MT940-003 | Balance reconciliation failed | Verify transaction data completeness |
| MT940-004 | Invalid date format | Correct date format in source data |
| MT940-005 | Invalid amount format | Correct amount format in source data |

### Appendix C: References

- SWIFT MT940 Customer Statement Message Specification
- RaboBank API Documentation
- ISO 4217 Currency Codes
- ISO 8601 Date/Time Format Standard

---

### Appendix D: Field-by-Field Mapping – Rabobank JSON to MT940

#### D.1 Example Rabobank Transaction JSON
```json
{
  "bookingDate": "2024-09-01",
  "valueDate": "2024-09-01",
  "transactionAmount": {
    "amount": "150.00",
    "currency": "EUR"
  },
  "creditDebitIndicator": "CRDT",
  "transactionId": "TXN123456",
  "remittanceInformationUnstructured": "Payment description",
  "debtorName": "John Doe",
  "debtorAccount": {
    "iban": "NL01RABO0123456789"
  },
  "creditorName": "Acme BV",
  "creditorAccount": {
    "iban": "NL80RABO1127000002"
  }
}
```

#### D.2 MT940 Statement Line Example
```
:61:240901C150,00NTRFNONREF//John Doe
:86:Payment description
```

#### D.3 Field Mapping Table

| MT940 Field         | Tag   | Rabobank JSON Field                       | Example Value           | Notes |
|---------------------|-------|-------------------------------------------|------------------------|-------|
| Transaction Date    | :61:  | bookingDate                               | 240901                 | Format: YYMMDD |
| Credit/Debit        | :61:  | creditDebitIndicator                      | C                      | C = Credit, D = Debit |
| Amount              | :61:  | transactionAmount.amount                  | 150,00                 | Decimal point to comma |
| Currency            | :61:  | transactionAmount.currency                | EUR                    | |
| Transaction Type    | :61:  | (fixed or from bankTransactionCode)       | NTRF                   | Use mapping/config |
| Reference           | :61:  | transactionId or NONREF                   | TXN123456              | Use NONREF if not present |
| Counterparty        | :61:  | debtorName or creditorName                | John Doe               | |
| Counterparty IBAN   | :61:  | debtorAccount.iban or creditorAccount.iban| NL01RABO0123456789     | Optional |
| Description         | :86:  | remittanceInformationUnstructured         | Payment description     | |

#### D.4 Full Example – JSON to MT940

**Input JSON:**
```json
{
  "bookingDate": "2024-09-01",
  "transactionAmount": { "amount": "150.00", "currency": "EUR" },
  "creditDebitIndicator": "CRDT",
  "transactionId": "TXN123456",
  "remittanceInformationUnstructured": "Payment description",
  "debtorName": "John Doe"
}
```

**MT940 Output:**
```
:61:240901C150,00NTRFNONREF//John Doe
:86:Payment description
```

#### D.5 Notes for Developers

- Always convert decimal points to commas for MT940 amounts.
- Dates must be formatted as YYMMDD.
- Use "C" for credits, "D" for debits.
- If transactionId is missing, use "NONREF".
- Counterparty name is optional but recommended for reconciliation.
- Description field (:86:) can be truncated to fit MT940 limits.
- See field_mapping.json for configurable mappings.

---

**Document Control:**
 Next Review Date: December 2, 2025
 Approval Required: Technical Lead, Business Analyst
 Distribution: Development Team, Operations Team, Business Users
