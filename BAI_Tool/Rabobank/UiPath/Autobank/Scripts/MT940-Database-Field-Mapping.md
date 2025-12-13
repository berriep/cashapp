# Database Field to MT940 Mapping
# Complete mapping van Rabobank database velden naar SWIFT MT940 format

## BALANCE DATA MAPPING (dt_camt053_data table)

| Database Field | MT940 Field | Usage | Example | Notes |
|----------------|-------------|-------|---------|-------|
| `iban` | `:25:` Account ID | Account identification | `:25:NL31RABO0300087233 EUR` | Combined with currency |
| `owner_name` | Not used directly | Account owner | - | Used for internal reference only |
| `day` | `:20:`, `:60F:`, `:62F:` | Report date | `:20:940S251107` | Used in multiple places |
| `currency` | `:25:`, `:60F:`, `:62F:` | Currency code | `EUR` | 3-letter ISO code |
| `opening_balance` | `:60F:` | Opening balance | `:60F:C251106EUR000000012345,67` | With C/D indicator |
| `closing_balance` | `:62F:`, `:64:`, `:65:` | Closing balance | `:62F:C251107EUR000000013456,78` | Multiple balance fields |
| `transaction_count` | Validation only | Transaction count | - | Used for validation |

## TRANSACTION DATA MAPPING (dt_camt053_tx table)

### MANDATORY :61: STATEMENT LINE FIELDS

| Database Field | MT940 Usage | Position in :61: | Example | Priority |
|----------------|-------------|------------------|---------|----------|
| `value_date` | Date | YYMMDD | `251107` | **MANDATORY** |
| `booking_date` | Entry date (if different) | MMDD | `1107` | Optional |
| `transaction_amount` | Amount + C/D indicator | Amount | `D000000000020000,00` | **MANDATORY** |
| `rabo_detailed_transaction_type` | Transaction code | N### | `N501`, `N586PREF` | **MANDATORY** |

### REFERENCE FIELD PRIORITY (NEW MAPPING)

| Database Field | MT940 Usage | Priority | Example | Notes |
|----------------|-------------|----------|---------|-------|
| `batch_entry_reference` | Primary reference | **1st** | `OO9T005180594671` | **HIGHEST PRIORITY** - Real bank refs |
| `instruction_id` | Fallback reference | **2nd** | `INSTRUCTION123` | Alternative reference |
| `end_to_end_id` | EREF reference | **3rd** | `E2E-REF-001` | Exclude "NOTPROVIDED" |
| `entry_reference` | Last resort | **4th** | `491590` | Database sequence ID |
| `acctsvcr_ref` | **NOT USED** | ❌ | `93381` | Contains generated IDs - AVOID |

### TRANSACTION TYPE SPECIFIC MAPPING

#### 586 Transactions (PREF)
```
Database → MT940
rabo_detailed_transaction_type: "586" → N586PREF
payment_information_identification → /PREF/{value}
creditor_name → /INIT//NAME/{value}
```

#### 501 Transactions (EREF)
```
Database → MT940
rabo_detailed_transaction_type: "501" → N501EREF
batch_entry_reference → //{reference} (e.g., //OO9T005180594671)
```

#### 541 Transactions (Credit EREF)
```
Database → MT940
rabo_detailed_transaction_type: "541" → N541EREF
batch_entry_reference → //{reference}
debtor_name → /ORDP//NAME/{value}
```

#### 626 Transactions (Internal Transfers)
```
Database → MT940
rabo_detailed_transaction_type: "626" → N626{reference} OR N626NONREF
batch_entry_reference (OO9T...) → N626{reference}
batch_entry_reference (NP8A...) → N626NONREF
```

#### 085/1085 Transactions (Smart Pay)
```
Database → MT940
rabo_detailed_transaction_type: "085" → N085{ref} {YYYYMM}
batch_entry_reference → Reference part
value_date → Date part (YYYYMM format)
```

#### 065/2065 Transactions (International)
```
Database → MT940
rabo_detailed_transaction_type: "065" → N065{reference}
batch_entry_reference → Reference part
```

### :86: INFORMATION FIELD MAPPING

| Database Field | :86: Tag | Usage | Example |
|----------------|----------|-------|---------|
| `end_to_end_id` | `/EREF/` | End-to-end reference | `/EREF/E2E-001` |
| `payment_information_identification` | `/PREF/` | Payment reference | `/PREF/PMT-123` |
| `rabo_detailed_transaction_type` | `/TRCD/` | Transaction code | `/TRCD/586` |
| `debtor_name` | `/ORDP//NAME/` | Ordering party | `/ORDP//NAME/JOHN DOE` |
| `creditor_name` | `/BENM//NAME/` or `/INIT//NAME/` | Beneficiary/Initiator | `/BENM//NAME/JANE SMITH` |
| `remittance_information_unstructured` | `/REMI/` | Remittance info | `/REMI/INVOICE 123` |
| `booking_date` | `/ISDT/` | Issue date | `/ISDT/2025-11-07` |

### COUNTERPARTY IBAN MAPPING

| Database Field | MT940 Usage | Condition | Example |
|----------------|-------------|-----------|---------|
| `debtor_iban` | Separate line after :61: | For credit transactions | `NL91ABNA0417164300` |
| `creditor_iban` | Separate line after :61: | For debit transactions | `NL20INGB0001234567` |
| `related_party_debtor_account_iban` | Fallback for debtor | If debtor_iban empty | - |
| `related_party_creditor_account_iban` | Fallback for creditor | If creditor_iban empty | - |

## CURRENT REFERENCE PRIORITY LOGIC (VB.NET)

```vb
' NEW PRIORITY ORDER (Fixed)
If Not String.IsNullOrEmpty(batchEntryRef) Then
    referenceField = batchEntryRef  ' OO9T... references
ElseIf Not String.IsNullOrEmpty(instructionId) Then
    referenceField = instructionId
ElseIf Not String.IsNullOrEmpty(endToEndId) AndAlso endToEndId <> "NOTPROVIDED" Then
    referenceField = endToEndId
ElseIf Not String.IsNullOrEmpty(entryRef) Then
    referenceField = entryRef
End If
```

## CRITICAL FIXES APPLIED

### ❌ BEFORE (Wrong Priority)
```vb
' OLD CODE - WRONG
Dim acctSvcrRef As String = GetSafeColumnValue(txRow, "acctsvcr_ref")  ' = "93381"
If Not String.IsNullOrEmpty(acctSvcrRef) Then
    referenceField = acctSvcrRef  ' Wrong - database ID
```

### ✅ AFTER (Correct Priority)
```vb
' NEW CODE - CORRECT
Dim batchEntryRef As String = GetSafeColumnValue(txRow, "batch_entry_reference")
If Not String.IsNullOrEmpty(batchEntryRef) Then
    referenceField = batchEntryRef  ' Correct - real bank reference
```

## DATABASE QUERY TO VALIDATE MAPPING

```sql
-- Check reference data quality
SELECT 
    entry_reference,
    batch_entry_reference,      -- Should contain OO9T... for priority transactions
    instruction_id,             -- Alternative reference
    end_to_end_id,             -- EREF (exclude 'NOTPROVIDED')
    acctsvcr_ref,              -- AVOID - contains generated IDs
    rabo_detailed_transaction_type,
    transaction_amount
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = 'NL31RABO0300087233' 
  AND booking_date = '2025-11-07'
ORDER BY entry_reference;
```

## EXPECTED RESULTS AFTER FIXES

### Original MT940 Pattern:
```
:61:251107D000000020000,00N501EREF//OO9T005180594671
```

### Generated MT940 Pattern (Should Match):
```
:61:251107D000000000020000,00N501EREF//OO9T005180594671
```

### Key Differences to Verify:
1. **Reference Source**: `OO9T...` from `batch_entry_reference` (not `acctsvcr_ref`)
2. **No Double EREF**: `N501EREF` (not `N501EREFEREF`)
3. **Correct Amount Format**: 12-digit padding for transactions

This mapping ensures the generated MT940 matches Rabobank's exact format requirements.