# Vertaaltabel: Rabobank Transactie Data Mapping naar MT940

**Versie**: 1.1  
**Datum**: Oktober 2025  
**Status**: GEÏMPLEMENTEERD - IN PRODUCTIE  
**Doel**: Mapping tussen BAI JSON API, Database velden en MT940 formaat voor transactie data consolidatie

## Implementatie Status

### ✅ GEÏMPLEMENTEERD:
- ✅ MT940 generatie vanuit database (autobank_rabobank_mt940_db.vb)
- ✅ Field mapping naar MT940 tags (volledig functioneel)
- ✅ Swift MT940 compliance validatie (character set cleaning)
- ✅ Error handling en fallback logica (GetSafeColumnValue)
- ✅ SWIFT Block format ondersteuning ({1:}, {2:}, {3:}, {4:}, {5:})
- ✅ Minimale vs volledige veld opties (minimum_required_fields parameter)
- ✅ Balance validatie en statistieken
- ✅ UTF-8 encoding compatibiliteit

### 🎯 NIEUWE FEATURES:
- ✅ **full_swift_format** parameter voor SWIFT block generatie
- ✅ Dual format support (Simple vs Full SWIFT)
- ✅ Enhanced IBAN mapping met fallback opties
- ✅ Uitgebreide transactietype beschrijvingen
- ✅ Multi-line :86: informatie ondersteuning
- ✅ Checksum generatie voor SWIFT trailer block

### 📋 VALIDATIE STATUS:
- ✅ Character set compliance (A-Z, 0-9, beperkte speciale tekens)
- ✅ Line length restrictions (65 chars voor :86:)
- ✅ Balance equation validatie
- ✅ Date format conversie (YYMMDD)
- ✅ Amount format conversie (comma decimaal separator)

## MT940 Format Overview

MT940 is een SWIFT standaard voor elektronische bankafschriften met twee implementatie opties:

### **FORMAT OPTIES:**
- **Simple Format** (`full_swift_format = False`): Alleen MT940 tags zonder SWIFT blocks
- **Full SWIFT Format** (`full_swift_format = True`): Complete SWIFT message met {1:}, {2:}, {3:}, {4:}, {5:} blocks

### **SWIFT BLOCKS (Full Format):**
- **{1:}** - Basic Header Block (Bank identificatie: RABONL2UAXXX)
- **{2:}** - Application Header Block (MT940 routing informatie)
- **{3:}** - User Header Block (Transaction reference)
- **{4:}** - Text Block (Eigenlijke MT940 data)
- **{5:}** - Trailer Block (Checksum voor integriteit)

### **VERPLICHTE MT940 TAGS:**
- **:20:** - Transaction Reference Number
- **:25:** - Account Identification (IBAN)
- **:28C:** - Statement/Sequence Number
- **:60F:** - Opening Balance
- **:61:** - Statement Line (per transactie)
- **:86:** - Information to Account Owner (description)
- **:62F:** - Closing Balance

### **OPTIONELE MT940 TAGS:**
- **:21:** - Related Reference
- **:60M:** - Intermediate Opening Balance
- **:62M:** - Intermediate Closing Balance
- **:64:** - Closing Available Balance (geïmplementeerd)
- **:65:** - Forward Available Balance (geïmplementeerd)
- **:86:** - Information to Account Owner (additional info)

## Main Mapping Table

| BAI JSON Path                             | DB Field                                | MT940 Tag     | Format                    | Priority | Comment                                                               |
| ----------------------------------------- | --------------------------------------- | ------------- | ------------------------- | -------- | --------------------------------------------------------------------- |
| **Statement Header**                      |                                         |               |                           |          |                                                                       |
| n.a.                                      | system generated                        | `:20:`        | Reference (max 16 chars) | **REQ**  | Unique transaction reference number                                   |
| `account.iban`                            | `iban`                                  | `:25:`        | IBAN format               | **REQ**  | Account identification                                                |
| n.a.                                      | system generated                        | `:28C:`       | Statement/Sequence Number | **REQ**  | Page/sequence number (format: nnn/nnn)                               |
| **Balances**                              |                                         |               |                           |          |                                                                       |
| n.a.                                      | `opening_balance`                       | `:60F:`       | YYMMDDCAMT                | **REQ**  | Opening balance (C=Credit, D=Debit + amount)                         |
| n.a.                                      | `closing_balance`                       | `:62F:`       | YYMMDDCAMT                | **REQ**  | Closing balance (C=Credit, D=Debit + amount)                         |
| n.a.                                      | calculated                              | `:64:`        | YYMMDDCAMT                | OPT      | Closing available balance                                             |
| n.a.                                      | calculated                              | `:65:`        | YYMMDDCAMT                | OPT      | Forward available balance                                             |
| **Transaction Data**                      |                                         |               |                           |          |                                                                       |
| `valueDate`                               | `value_date`                            | `:61:` pos1-6 | YYMMDD                    | **REQ**  | Value date                                                            |
| `bookingDate`                             | `booking_date`                          | `:61:` pos7-10| MMDD                      | OPT      | Entry date (if different from value date)                            |
| `transactionAmount.value`                 | `transaction_amount`                    | `:61:` + D/C  | [D/C]amount               | **REQ**  | Credit/Debit indicator + amount                                       |
| `raboDetailedTransactionType`             | `rabo_detailed_transaction_type`        | `:61:` code   | Nxxx                      | OPT      | Transaction type code (N586, N100, etc.)                             |
| `entryReference`                          | `entry_reference`                       | `:61:` ref    | Reference                 | OPT      | Customer reference                                                    |
| `accountServicerReference`                | `acctsvcr_ref`                          | `:61:` ref    | Bank reference            | OPT      | Account servicer reference                                            |
| **Transaction Description**               |                                         |               |                           |          |                                                                       |
| `remittanceInformationUnstructured`       | `remittance_information_unstructured`   | `:86:`        | Free text (max 65 chars) | OPT      | Transaction description                                               |
| `debtorName`                              | `debtor_name`                           | `:86:`        | Free text                 | OPT      | Counterparty name (for credits)                                       |
| `creditorName`                            | `creditor_name`                         | `:86:`        | Free text                 | OPT      | Counterparty name (for debits)                                        |
| `debtorAccount.iban`                      | `debtor_iban`                           | `:86:`        | IBAN in description       | OPT      | Counterparty IBAN                                                     |
| `creditorAccount.iban`                    | `creditor_iban`                         | `:86:`        | IBAN in description       | OPT      | Counterparty IBAN                                                     |
| `endToEndId`                              | `end_to_end_id`                         | `:86:`        | Reference in description  | OPT      | End-to-end reference                                                  |
| **Structured Information**                |                                         |               |                           |          |                                                                       |
| `purposeCode`                             | `purpose_code`                          | `:86:`        | Purpose code              | OPT      | Purpose of transaction                                                |
| `bankTransactionCode`                     | `bank_transaction_code`                 | `:61:` code   | Transaction code          | OPT      | Bank transaction code mapping                                         |

## MT940 Tag Format Specifications

### **:20: Transaction Reference Number**
```
:20:STMT20251023120000
```
- Format: Free text (max 16 characters)
- Inhoud: Unique statement reference
- Database: System generated

### **:25: Account Identification**
```
:25:NL48RABO0300002343
```
- Format: IBAN zonder spaties
- Inhoud: Account IBAN
- Database: `iban`

### **:28C: Statement/Sequence Number**
```
:28C:1/1
```
- Format: nnn/nnn (page/sequence)
- Inhoud: Statement page en sequence number
- Database: System generated (meestal 1/1)

### **:60F: Opening Balance**
```
:60F:C251006EUR540498,84
```
- Format: [C/D]YYMMDD[currency][amount]
- C = Credit (positive), D = Debit (negative)
- Date: Value date van opening balance
- Currency: 3-letter code (EUR)
- Amount: Bedrag met comma als decimaal separator
- Database: `opening_balance`, `currency`

### **:61: Statement Line**
```
:61:2510062510063384,10N586RABONL2U//2911595
```
- **Posities 1-6**: Value date (YYMMDD) - `value_date`
- **Posities 7-10**: Entry date (MMDD) - `booking_date` (optioneel)
- **Positie 11**: Credit/Debit indicator (C/D) - derived from `transaction_amount`
- **Amount**: Transaction amount - `transaction_amount`
- **Type**: Transaction type code (N586, N100, etc.) - `rabo_detailed_transaction_type`
- **Reference**: Bank/customer reference - `entry_reference` of `acctsvcr_ref`

### **:86: Information to Account Owner**
```
:86:586 BETAALAUTOMAAT GELDOPNAME 04-10-2025 16:36 7020896310501621 SR
```
- Format: Free text (max 65 characters per line, max 6 lines)
- Inhoud: Combinatie van:
  - Transaction type description
  - Counterparty name (`debtor_name`/`creditor_name`)
  - Remittance info (`remittance_information_unstructured`)
  - IBAN (`debtor_iban`/`creditor_iban`)
  - End-to-end reference (`end_to_end_id`)

### **:62F: Closing Balance**
```
:62F:C251006EUR111173,90
```
- Zelfde format als :60F: maar dan voor closing balance
- Database: `closing_balance`, `currency`

## Rabobank Specific MT940 Codes

### **Transaction Type Codes (in :61: tag):**
```
N100 = Standaard credit transactie
N541 = Speciale credit transactie  
N586 = Debit transactie (bijv. betaalautomaat)
N625 = Instant credit transactie
N699 = Grote transacties
```

### **Description Patterns (in :86: tag):**
```
586 BETAALAUTOMAAT GELDOPNAME [datetime] [reference]
100 OVERSCHRIJVING VAN [name] [reference]
625 INSTANT CREDIT [name] [reference]
541 CREDIT TRANSACTIE [details]
```

## Date Format Conversions

### **Van Database naar MT940:**
```vb
' Value date voor :61: tag (YYMMDD)
Dim valueDateMT940 As String = valueDate.ToString("yyMMdd")

' Entry date voor :61: tag (MMDD) - alleen als verschilt van value date
Dim entryDateMT940 As String = ""
If bookingDate <> valueDate Then
    entryDateMT940 = bookingDate.ToString("MMdd")
End If

' Balance dates voor :60F: en :62F: (YYMMDD)
Dim balanceDateMT940 As String = balanceDate.ToString("yyMMdd")
```

## Amount Format Conversions

### **Van Database naar MT940:**
```vb
' Credit/Debit indicator
Dim cdIndicator As String = If(amount >= 0, "C", "D")

' Amount formatting (comma als decimaal separator)
Dim amountMT940 As String = Math.Abs(amount).ToString("F2").Replace(".", ",")

' Complete balance tag
Dim balanceTag As String = String.Format("{0}{1}EUR{2}", cdIndicator, dateMT940, amountMT940)
```

## Implementation Planning

### ✅ **Phase 1: Basic MT940 Generator (COMPLETED)**
- ✅ Create VB.NET MT940 generator script (autobank_rabobank_mt940_db.vb)
- ✅ Implement required tags (:20:, :25:, :28C:, :60F:, :61:, :86:, :62F:)
- ✅ Date and amount format conversions
- ✅ Basic transaction type mapping

### ✅ **Phase 2: Enhanced Features (COMPLETED)**
- ✅ Multi-line :86: descriptions (max 6 lines, 65 chars each)
- ✅ Optional tags (:64:, :65:) 
- ✅ Rabobank specific formatting
- ✅ Error handling and validation
- ✅ SWIFT block format support ({1:}, {2:}, {3:}, {4:}, {5:})

### ✅ **Phase 3: Integration & Validation (COMPLETED)**
- ✅ Database integration (hergebruik CAMT.053 tabellen)
- ✅ SWIFT MT940 compliance validation
- ✅ Balance equation verification
- ✅ Performance optimization

### 🎯 **Phase 4: Quality Assurance (IN PROGRESS)**
- [ ] PowerShell comparison script voor MT940 (similar to CAMT.053)
- [ ] Unit tests voor edge cases
- ✅ Documentation en troubleshooting guide
- [ ] MT940 file viewer/analyzer tool

## Configuration Parameters

### **Script Parameters:**
```vb
' Hoofdparameters voor MT940 generatie
Dim minimumRequiredFields As Boolean = False  ' Default: volledige velden
Dim fullSwiftFormat As Boolean = False        ' Default: simple format

' Input tabellen (hergebruik van CAMT.053)
dt_camt053_data   ' Balance en account informatie
dt_camt053_tx     ' Transactie details

' Output variabele
mt940FilePath     ' Pad naar gegenereerde MT940 file
```

### **Format Combinations:**
- `minimum_required_fields=False, full_swift_format=False` → **mt940_full_simple_** (meest gebruikt)
- `minimum_required_fields=True, full_swift_format=False` → **mt940_minimal_simple_**
- `minimum_required_fields=False, full_swift_format=True` → **mt940_full_swift_**
- `minimum_required_fields=True, full_swift_format=True` → **mt940_minimal_swift_**

## Database Schema Requirements

### **Existing Fields (Reuse from CAMT.053):**
- `iban` → :25: Account identification
- `currency` → Balance currencies
- `opening_balance`, `closing_balance` → :60F:, :62F:
- `transaction_amount` → :61: amounts
- `value_date`, `booking_date` → :61: dates
- `entry_reference` → :61: reference
- `remittance_information_unstructured` → :86: description
- `debtor_name`, `creditor_name` → :86: names
- `rabo_detailed_transaction_type` → :61: transaction codes

### **Additional Fields Needed:**
```sql
-- MT940 specific fields
ALTER TABLE bai_rabobank_transactions_payload 
ADD COLUMN mt940_transaction_code VARCHAR(10),        -- N586, N100, etc.
ADD COLUMN mt940_description_line1 VARCHAR(65),       -- First line of :86:
ADD COLUMN mt940_description_line2 VARCHAR(65),       -- Second line of :86:
ADD COLUMN mt940_description_line3 VARCHAR(65),       -- Third line of :86:
ADD COLUMN statement_sequence_number INT DEFAULT 1;   -- For :28C: tag
```

## Swift Compliance Notes

### **Character Set:**
- MT940 uses Swift character set (A-Z, 0-9, limited special characters)
- No lowercase letters allowed
- Special characters: / - ? : ( ) . , ' + { } CR LF SPACE

### **Line Length Restrictions:**
- Maximum 65 characters per line in :86: tag
- Maximum 78 characters for other tags
- Line continuation met CRLF

### **Validation Rules:**
- All amounts must balance (opening + transactions = closing)
- Dates must be in YYMMDD format
- References must be unique per statement
- Currency codes must be ISO 4217 compliant

## Error Handling Strategy

### **Missing Data Handling:**
```vb
' Fallback voor missing entry reference
If String.IsNullOrEmpty(entryReference) Then
    entryReference = acctsvrRef
End If
If String.IsNullOrEmpty(entryReference) Then
    entryReference = transactionSequence.ToString()
End If

' Fallback voor missing description
If String.IsNullOrEmpty(description) Then
    description = String.Format("{0} TRANSACTION {1}", transactionType, amount.ToString("F2"))
End If
```

### **Character Set Validation:**
```vb
' Remove invalid characters voor Swift compliance
Function CleanForMT940(input As String) As String
    ' Convert to uppercase and remove invalid characters
    Return Regex.Replace(input.ToUpper(), "[^A-Z0-9 /\-\?:\(\)\.,'\+\{\}]", "")
End Function
```

## Expected Output Format

### **Simple Format Example** (`full_swift_format = False`):
```
:20:STMT20251006120000
:25:NL48RABO0300002343
:28C:1/1
:60F:C251005EUR540498,84
:61:2510062510063384,10N586RABONL2U//2911595
:86:586 BETAALAUTOMAAT GELDOPNAME 04-10-2025 16:36 7020896310501621 SR
:61:251006C112,50N100INGBNL2A//2911596
:86:100 OVERSCHRIJVING VAN JOHN DOE BV NL12INGB0001234567
:62F:C251006EUR111173,90
:64:C251006EUR111173,90
:65:C251007EUR111173,90
:65:C251008EUR111173,90
:65:C251009EUR111173,90
-
```

### **Full SWIFT Format Example** (`full_swift_format = True`):
```
{1:F01RABONL2UAXXX0000}
{2:O94008002510060RABONL2UAXXX000000N}
{3:{108:STMT20251006}}
{4:
:20:STMT20251006120000
:25:NL48RABO0300002343
:28C:1/1
:60F:C251005EUR540498,84
:61:2510062510063384,10N586RABONL2U//2911595
:86:586 BETAALAUTOMAAT GELDOPNAME 04-10-2025 16:36 7020896310501621 SR
:61:251006C112,50N100INGBNL2A//2911596
:86:100 OVERSCHRIJVING VAN JOHN DOE BV NL12INGB0001234567
:62F:C251006EUR111173,90
:64:C251006EUR111173,90
:65:C251007EUR111173,90
:65:C251008EUR111173,90
:65:C251009EUR111173,90
-}
{5:{CHK:A1B2C3D4}}
```

### **Key Characteristics:**
- ✅ Compact format (geen XML overhead)
- ✅ Human readable
- ✅ Fixed field positions in :61: tag
- ✅ Multi-line descriptions mogelijk in :86: (max 6 lines, 65 chars each)
- ✅ Balance validation geïmplementeerd
- ✅ Swift compliant character set (CleanForMT940 function)
- ✅ UTF-8 encoding compatible
- ✅ Dual format support (Simple/Full SWIFT)
- ✅ Enhanced IBAN mapping met fallback opties
- ✅ Checksum generatie voor SWIFT trailer

Dit vormt de basis voor een complete MT940 implementatie die parallel kan draaien met de bestaande CAMT.053 generator.