# RaboBank API to CAMT.053.001.02 Conversion Requirements

## Document Information
- **Document**: RaboBank API to CAMT.053.001.02 Conversion Requirements
- **Version**: 1.0
- **Date**: September 2, 2025
- **Author**: Integration Team
- **Status**: Draft

---

## 1. Overview

This document outlines the requirements and conditions for converting RaboBank API balance and transaction data into CAMT.053.001.02 (Customer Account Report) format.

CAMT.053 is an ISO 20022 XML standard message format used for bank-to-customer account statements, providing detailed account activity information in a structured, machine-readable format.

---

## 2. Data Requirements

### 2.1 Mandatory Input Data

For successful CAMT.053.001.02 generation, the following data must be available:

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
    "remittanceInformationUnstructured": "Payment description",  // Optional: Description
    "debtorName": "John Doe",                 // Optional: Payer name
    "debtorAccount": {                        // Optional: Payer account
      "iban": "NL91ABNA0417164300"
    },
    "creditorName": "Jane Smith",             // Optional: Payee name
    "creditorAccount": {                      // Optional: Payee account
      "iban": "NL91RABO0315273637"
    },
    "endToEndId": "E2E123456",               // Optional: End-to-end ID
    "mandateId": "MANDATE123",               // Optional: Direct debit mandate ID
    "purposeCode": "SALA"                    // Optional: Purpose code
  }]
}
```

---

## 3. Technical Prerequisites

### 3.1 Required Components

#### **Configuration Files:**
- `camt053_config.json` - CAMT.053 generation settings
- `field_mapping.json` - RaboBank API to CAMT.053 field mappings
- `iso20022_codes.json` - ISO 20022 code mappings (purpose codes, transaction codes)
- `bank_bic_codes.json` - BIC and institution identifier mappings

#### **Code Components:**
- `CAMT053Generator.cs` - Core CAMT.053 generation logic
- `CAMT053Validator.cs` - XML schema validation and compliance checking
- `ISO20022DataProcessor.cs` - RaboBank API data transformation
- `XMLNamespaceManager.cs` - XML namespace and schema handling

#### **Templates and Schemas:**
- `camt.053.001.02.xsd` - Official ISO 20022 XSD schema
- `camt053_template.xml` - Base CAMT.053 XML structure template
- `camt053_sample.xml` - Sample output for reference

### 3.2 System Requirements

#### **UiPath Environment:**
- UiPath Studio/Robot with .NET 6.0+ support
- Newtonsoft.Json package for JSON processing
- System.Xml.Linq for XML manipulation
- System.Xml.Schema for XSD validation

#### **File System Access:**
- Read access to API data files (JSON format)
- Read access to XSD schema files
- Write access to output directory for CAMT.053 files
- Configuration directory access

---

## 4. CAMT.053.001.02 Format Specifications

### 4.1 Required CAMT.053 Elements

| Element | XPath | Description | Source | Mandatory |
|---------|-------|-------------|---------|-----------|
| Message Identification | //GrpHdr/MsgId | Unique message identifier | Generated | Yes |
| Creation Date Time | //GrpHdr/CreDtTm | Message creation timestamp | Current time | Yes |
| Statement Identification | //Stmt/Id | Unique statement identifier | Generated | Yes |
| Electronic Sequence Number | //Stmt/ElctrncSeqNb | Statement sequence number | Configuration | No |
| Account | //Stmt/Acct | Account information | Configuration | Yes |
| From Date | //Stmt/FrToDt/FrDtTm | Statement period start | Today -1 00:00:00 | Yes |
| To Date | //Stmt/FrToDt/ToDtTm | Statement period end | Today -1 23:59:59 | Yes |
| Opening Balance | //Stmt/Bal[Tp/CdOrPrtry/Cd='OPBD'] | Opening balance | Balance Today -2 | Yes |
| Closing Balance | //Stmt/Bal[Tp/CdOrPrtry/Cd='CLBD'] | Closing balance | Balance Today -1 | Yes |
| Entry | //Stmt/Ntry | Transaction entries | Transactions Today -1 | No |

### 4.2 CAMT.053 XML Structure

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02" 
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <BkToCstmrStmt>
    <GrpHdr>
      <MsgId>STMT20240901001</MsgId>
      <CreDtTm>2024-09-02T08:30:00</CreDtTm>
    </GrpHdr>
    <Stmt>
      <Id>20240901001</Id>
      <ElctrncSeqNb>1</ElctrncSeqNb>
      <CreDtTm>2024-09-02T08:30:00</CreDtTm>
      <Acct>
        <Id>
          <IBAN>NL91RABO0315273637</IBAN>
        </Id>
        <Ccy>EUR</Ccy>
        <Ownr>
          <Nm>Account Holder Name</Nm>
        </Ownr>
        <Svcr>
          <FinInstnId>
            <BIC>RABONL2U</BIC>
          </FinInstnId>
        </Svcr>
      </Acct>
      <FrToDt>
        <FrDtTm>2024-09-01T00:00:00</FrDtTm>
        <ToDtTm>2024-09-01T23:59:59</ToDtTm>
      </FrToDt>
      <Bal>
        <Tp>
          <CdOrPrtry>
            <Cd>OPBD</Cd>
          </CdOrPrtry>
        </Tp>
        <Amt Ccy="EUR">1234.56</Amt>
        <CdtDbtInd>CRDT</CdtDbtInd>
        <Dt>
          <Dt>2024-09-01</Dt>
        </Dt>
      </Bal>
      <Bal>
        <Tp>
          <CdOrPrtry>
            <Cd>CLBD</Cd>
          </CdOrPrtry>
        </Tp>
        <Amt Ccy="EUR">1384.56</Amt>
        <CdtDbtInd>CRDT</CdtDbtInd>
        <Dt>
          <Dt>2024-09-01</Dt>
        </Dt>
      </Bal>
      <Ntry>
        <Amt Ccy="EUR">150.00</Amt>
        <CdtDbtInd>CRDT</CdtDbtInd>
        <Sts>BOOK</Sts>
        <BookgDt>
          <Dt>2024-09-01</Dt>
        </BookgDt>
        <ValDt>
          <Dt>2024-09-01</Dt>
        </ValDt>
        <BkTxCd>
          <Domn>
            <Cd>PMNT</Cd>
            <Fmly>
              <Cd>RCDT</Cd>
              <SubFmlyCd>ESCT</SubFmlyCd>
            </Fmly>
          </Domn>
        </BkTxCd>
        <NtryDtls>
          <TxDtls>
            <Refs>
              <EndToEndId>E2E123456</EndToEndId>
              <TxId>TXN123456</TxId>
            </Refs>
            <RltdPties>
              <Dbtr>
                <Nm>John Doe</Nm>
              </Dbtr>
              <DbtrAcct>
                <Id>
                  <IBAN>NL91ABNA0417164300</IBAN>
                </Id>
              </DbtrAcct>
              <Cdtr>
                <Nm>Jane Smith</Nm>
              </Cdtr>
              <CdtrAcct>
                <Id>
                  <IBAN>NL91RABO0315273637</IBAN>
                </Id>
              </CdtrAcct>
            </RltdPties>
            <RmtInf>
              <Ustrd>Payment description</Ustrd>
            </RmtInf>
          </TxDtls>
        </NtryDtls>
      </Ntry>
    </Stmt>
  </BkToCstmrStmt>
</Document>
```

---

## 5. Business Rules and Validations

### 5.1 Balance Validation Rules

#### **Opening vs Closing Balance:**
- Opening balance must equal previous day's closing balance
- Currency must be consistent across all balance entries
- Date sequence must be logical (Today -2 < Today -1)
- Balance types must use correct ISO 20022 codes:
  - `OPBD` = Opening Booked Balance
  - `CLBD` = Closing Booked Balance

#### **Transaction Reconciliation:**
```
Opening Balance + Sum(Credit Entries) - Sum(Debit Entries) = Closing Balance
```

### 5.2 Data Transformation Rules

#### **Amount Formatting:**
- Maintain decimal precision (up to 5 decimal places for currencies)
- Use decimal point notation (not comma)
- Include currency code in Ccy attribute
- Amounts must be positive values (sign indicated by CdtDbtInd)

#### **Date Formatting:**
- Convert ISO 8601 dates to XML date format (YYYY-MM-DD)
- Include time zones where applicable
- DateTime elements use full ISO 8601 format
- Date elements use date-only format

#### **Text Content Rules:**
- Maximum length constraints per ISO 20022 specification
- Character set restrictions (Basic Latin + selected Unicode)
- No leading/trailing whitespace
- Special character handling for XML compliance

#### **Code Mappings:**
- Credit/Debit Indicator: `CRDT` or `DBIT`
- Entry Status: `BOOK` (booked), `PDNG` (pending), `INFO` (information)
- Balance Type Codes: `OPBD`, `CLBD`, `ITBD`, `PRCD`
- Bank Transaction Codes: ISO 20022 4-character codes

### 5.3 Error Handling Rules

#### **Missing Data:**
- If opening balance missing: Generate error, cannot proceed
- If closing balance missing: Generate error, cannot proceed
- If account information missing: Generate error, cannot proceed
- If transactions missing: Generate warning, create balance-only statement

#### **Data Validation Errors:**
- Invalid IBAN format: Skip transaction, log error
- Invalid currency code: Generate error, cannot proceed
- Invalid amount format: Skip transaction, log error
- Missing mandatory fields: Skip transaction, log error

#### **XML Schema Validation:**
- Schema validation failure: Generate error with specific validation details
- Namespace issues: Generate error, check template configuration
- Element order violations: Generate error, review XML structure

---

## 6. Configuration Requirements

### 6.1 CAMT.053 Configuration (camt053_config.json)

```json
{
  "MessageIdentification": {
    "Prefix": "STMT",
    "DateFormat": "yyyyMMdd",
    "SequenceNumberLength": 3
  },
  "Account": {
    "IBAN": "NL91RABO0315273637",
    "Currency": "EUR",
    "OwnerName": "Account Holder Name",
    "ServicerBIC": "RABONL2U",
    "ServicerName": "Rabobank Nederland"
  },
  "Statement": {
    "IdPrefix": "STMT",
    "ElectronicSequenceNumber": 1,
    "IncludeIntermediateBalances": false,
    "TimeZone": "Europe/Amsterdam"
  },
  "Validation": {
    "SchemaValidationEnabled": true,
    "SchemaPath": "Schemas/camt.053.001.02.xsd",
    "StrictModeEnabled": true,
    "SkipInvalidTransactions": false
  },
  "Output": {
    "Encoding": "UTF-8",
    "Indentation": true,
    "NamespacePrefix": "",
    "IncludeXMLDeclaration": true
  }
}
```

### 6.2 Field Mapping Configuration (field_mapping.json)

```json
{
  "BalanceMapping": {
    "RaboBank_API": {
      "amount": "balances[0].amount.amount",
      "currency": "balances[0].amount.currency",
      "indicator": "balances[0].creditDebitIndicator",
      "dateTime": "balances[0].dateTime",
      "balanceType": "balances[0].balanceType"
    },
    "CAMT053_XML": {
      "amount": "//Bal/Amt",
      "currency": "//Bal/Amt/@Ccy",
      "indicator": "//Bal/CdtDbtInd",
      "date": "//Bal/Dt/Dt",
      "typeCode": "//Bal/Tp/CdOrPrtry/Cd"
    }
  },
  "TransactionMapping": {
    "RaboBank_API": {
      "id": "transactionId",
      "amount": "transactionAmount.amount",
      "currency": "transactionAmount.currency",
      "indicator": "creditDebitIndicator",
      "bookingDate": "bookingDate",
      "valueDate": "valueDate",
      "reference": "remittanceInformationUnstructured",
      "endToEndId": "endToEndId",
      "debtorName": "debtorName",
      "debtorIBAN": "debtorAccount.iban",
      "creditorName": "creditorName",
      "creditorIBAN": "creditorAccount.iban"
    },
    "CAMT053_XML": {
      "amount": "//Ntry/Amt",
      "currency": "//Ntry/Amt/@Ccy",
      "indicator": "//Ntry/CdtDbtInd",
      "status": "//Ntry/Sts",
      "bookingDate": "//Ntry/BookgDt/Dt",
      "valueDate": "//Ntry/ValDt/Dt",
      "transactionId": "//NtryDtls/TxDtls/Refs/TxId",
      "endToEndId": "//NtryDtls/TxDtls/Refs/EndToEndId",
      "debtorName": "//NtryDtls/TxDtls/RltdPties/Dbtr/Nm",
      "debtorIBAN": "//NtryDtls/TxDtls/RltdPties/DbtrAcct/Id/IBAN",
      "creditorName": "//NtryDtls/TxDtls/RltdPties/Cdtr/Nm",
      "creditorIBAN": "//NtryDtls/TxDtls/RltdPties/CdtrAcct/Id/IBAN",
      "reference": "//NtryDtls/TxDtls/RmtInf/Ustrd"
    }
  }
}
```

### 6.3 ISO 20022 Code Mappings (iso20022_codes.json)

```json
{
  "BalanceTypeCodes": {
    "closingBooked": "CLBD",
    "openingBooked": "OPBD",
    "interimBooked": "ITBD",
    "previousClosingBooked": "PRCD"
  },
  "CreditDebitIndicator": {
    "CRDT": "CRDT",
    "DBIT": "DBIT"
  },
  "EntryStatus": {
    "booked": "BOOK",
    "pending": "PDNG",
    "future": "FUTR",
    "information": "INFO"
  },
  "BankTransactionCodes": {
    "SEPA_CREDIT_TRANSFER": {
      "Domain": "PMNT",
      "Family": "RCDT",
      "SubFamily": "ESCT"
    },
    "SEPA_DIRECT_DEBIT": {
      "Domain": "PMNT",
      "Family": "DDBT",
      "SubFamily": "ESDD"
    },
    "CARD_PAYMENT": {
      "Domain": "PMNT",
      "Family": "CCRD",
      "SubFamily": "POSD"
    }
  },
  "PurposeCodes": {
    "SALA": "Salary Payment",
    "PENS": "Pension Payment",
    "SUPP": "Supplier Payment",
    "TRAD": "Trade Services",
    "TREA": "Treasury"
  }
}
```

---

## 7. Quality Assurance Requirements

### 7.1 Validation Checks

#### **Pre-Generation Validation:**
- [ ] All required input files present and readable
- [ ] JSON structure validation against defined schema
- [ ] Date consistency and logical sequence validation
- [ ] Currency consistency validation
- [ ] Amount format and range validation
- [ ] IBAN format validation (if provided)
- [ ] BIC format validation (if provided)

#### **Post-Generation Validation:**
- [ ] XML schema validation against camt.053.001.02.xsd
- [ ] XML well-formedness verification
- [ ] Balance reconciliation verification
- [ ] Character encoding verification (UTF-8)
- [ ] Namespace declaration verification
- [ ] Element order compliance verification
- [ ] Data integrity verification (no data loss during transformation)

#### **Business Logic Validation:**
- [ ] Opening balance + transactions = closing balance
- [ ] All mandatory fields populated
- [ ] Date ranges consistency
- [ ] Currency consistency across all elements
- [ ] Transaction reference uniqueness

### 7.2 Testing Requirements

#### **Unit Tests:**
- Balance calculation accuracy
- Date format conversion (ISO 8601 to XML date)
- Amount format conversion and precision
- Text sanitization and character encoding
- XML element generation and structure
- Error handling scenarios
- Code mapping accuracy

#### **Integration Tests:**
- End-to-end CAMT.053 generation with real API data
- Multiple currency handling
- Large transaction volume handling (1000+ transactions)
- Edge case scenarios (weekend dates, holidays, different time zones)
- XML schema validation with various data combinations
- Performance testing with large datasets

#### **Validation Tests:**
- XSD schema validation with generated files
- Business rule validation testing
- Error condition testing (missing data, invalid data)
- Boundary value testing (maximum amounts, date ranges)

---

## 8. Security and Compliance

### 8.1 Data Security Requirements

#### **Data Handling:**
- API data must be processed in secure environment
- Generated CAMT.053 files must be stored securely
- Temporary files must be cleaned up after processing
- No sensitive data logging in plain text
- Encryption for data at rest and in transit

#### **Access Control:**
- Configuration files must have restricted access
- XSD schema files must be protected from modification
- Output directory must have appropriate permissions
- Audit trail for all CAMT.053 generation activities
- Role-based access control for system components

### 8.2 Regulatory Compliance

#### **ISO 20022 Standards:**
- CAMT.053 format must comply with ISO 20022 specifications version 001.02
- XML schema validation must be enforced
- Character sets must be ISO 20022-compatible
- Message structure must follow ISO 20022 guidelines
- Code values must use official ISO 20022 code lists

#### **Banking Regulations:**
- Generated statements must be audit-compliant
- Timestamps must be accurate and traceable
- Data integrity must be verifiable through checksums
- Retention policies must be followed
- Regulatory reporting requirements compliance

#### **Data Protection:**
- GDPR compliance for personal data handling
- Data minimization principles
- Consent management for data processing
- Right to erasure implementation
- Data breach notification procedures

---

## 9. Implementation Guidelines

### 9.1 Development Process

#### **Phase 1: Setup and Configuration**
1. Download and validate ISO 20022 XSD schema files
2. Create configuration files (camt053_config.json, field_mapping.json, iso20022_codes.json)
3. Implement CAMT053Generator.cs with core XML generation logic
4. Implement XML schema validation functionality
5. Create comprehensive unit tests for core functions

#### **Phase 2: Data Transformation and Mapping**
1. Implement RaboBank API to CAMT.053 field mapping
2. Develop data transformation and validation logic
3. Implement error handling and logging
4. Create integration tests with sample data
5. Validate XML output against schema

#### **Phase 3: Integration and Testing**
1. Integrate with UiPath workflow
2. Test with real RaboBank API data
3. Perform end-to-end testing
4. Validate business logic and reconciliation
5. Performance testing and optimization

#### **Phase 4: Production Deployment**
1. Security review and penetration testing
2. User acceptance testing
3. Production environment setup
4. Monitoring and alerting configuration
5. Documentation and training delivery

### 9.2 Error Handling Strategy

#### **Error Categories:**
- **Fatal Errors**: Missing mandatory data, schema validation failures
- **Warning Errors**: Missing optional data, data quality issues
- **Information**: Processing status, statistics

#### **Error Handling Actions:**
- **Fatal**: Stop processing, generate error report, notify administrators
- **Warning**: Continue processing, log warnings, include in summary report
- **Information**: Log for audit trail, include in processing summary

### 9.3 Monitoring and Maintenance

#### **Operational Monitoring:**
- CAMT.053 generation success/failure rates
- Processing time metrics and performance trends
- Data quality metrics and error rates
- XML schema validation success rates
- File size and transaction volume statistics

#### **Maintenance Activities:**
- Regular validation of ISO 20022 compliance
- Schema updates for new ISO 20022 versions
- Configuration updates for business requirement changes
- Performance optimization and tuning
- Security updates and vulnerability patches

#### **Alerting and Notifications:**
- Failed generation attempts
- Schema validation failures
- Data quality threshold breaches
- Performance degradation alerts
- Security incident notifications

---

## 10. Performance Considerations

### 10.1 Performance Requirements

#### **Processing Speed:**
- Generate CAMT.053 file within 30 seconds for up to 1000 transactions
- Memory usage should not exceed 500MB during processing
- Support concurrent processing of multiple statements
- Scalable to handle peak processing loads

#### **File Size Limitations:**
- Support files up to 100MB in size
- Handle up to 10,000 transactions per statement
- Efficient XML generation to minimize memory footprint
- Streaming XML generation for large datasets

### 10.2 Optimization Strategies

#### **XML Generation Optimization:**
- Use XmlWriter for streaming XML generation
- Implement lazy loading for large datasets
- Memory-efficient data processing pipelines
- Batch processing for multiple statements

#### **Data Processing Optimization:**
- Efficient JSON parsing and data extraction
- Minimize object creation and garbage collection
- Use StringBuilder for string concatenation
- Parallel processing where applicable

---

## 11. Appendices

### Appendix A: Sample Files

#### **Sample Input Files:**
- `sample_balance_today_minus2.json` - Opening balance sample
- `sample_balance_today_minus1.json` - Closing balance sample
- `sample_transactions_today_minus1.json` - Transaction data sample

#### **Sample Output Files:**
- `sample_camt053_output.xml` - Complete CAMT.053 output example
- `sample_camt053_minimal.xml` - Minimal CAMT.053 with balances only

#### **Sample Configuration Files:**
- `sample_camt053_config.json` - Configuration example
- `sample_field_mapping.json` - Field mapping example
- `sample_iso20022_codes.json` - Code mapping example

### Appendix B: Error Codes and Messages

| Error Code | Description | Severity | Action Required |
|------------|-------------|----------|-----------------|
| CAMT053-001 | Missing opening balance data | Fatal | Provide balance data for previous day |
| CAMT053-002 | Missing closing balance data | Fatal | Provide balance data for current day |
| CAMT053-003 | Balance reconciliation failed | Warning | Verify transaction data completeness |
| CAMT053-004 | Invalid date format in source data | Warning | Correct date format in source data |
| CAMT053-005 | Invalid amount format | Warning | Correct amount format in source data |
| CAMT053-006 | XML schema validation failed | Fatal | Review generated XML structure |
| CAMT053-007 | Invalid IBAN format | Warning | Correct IBAN format or exclude from output |
| CAMT053-008 | Missing mandatory account information | Fatal | Provide complete account configuration |
| CAMT053-009 | Currency mismatch in transactions | Warning | Verify currency consistency |
| CAMT053-010 | Transaction amount out of range | Warning | Verify transaction amount validity |

### Appendix C: XML Schema Information

#### **Schema Details:**
- **Standard**: ISO 20022
- **Message**: Customer Account Report (camt.053.001.02)
- **Namespace**: urn:iso:std:iso:20022:tech:xsd:camt.053.001.02
- **Version**: 001.02
- **File**: camt.053.001.02.xsd

#### **Key Schema Elements:**
- Document (root element)
- BkToCstmrStmt (Bank to Customer Statement)
- GrpHdr (Group Header)
- Stmt (Statement)
- Bal (Balance)
- Ntry (Entry)
- NtryDtls (Entry Details)

### Appendix D: References

#### **Standards and Specifications:**
- ISO 20022 Customer Account Report Message Definition
- ISO 20022 External Code Lists
- CAMT.053.001.02 Implementation Guidelines
- RaboBank API Documentation
- XML Schema Definition (XSD) 1.1 Specification

#### **Related Documentation:**
- RaboBank API Integration Guide
- ISO 4217 Currency Codes
- ISO 3166 Country Codes
- IBAN Registry and Validation Rules
- BIC Directory and Format Specification

---

**Document Control:**
- Next Review Date: December 2, 2025
- Approval Required: Technical Lead, Business Analyst, Compliance Officer
- Distribution: Development Team, Operations Team, Business Users, Compliance Team
- Change Management: All changes require impact assessment and approval
