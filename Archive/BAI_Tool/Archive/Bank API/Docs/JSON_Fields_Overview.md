# Rabobank JSON Transaction Fields Overview

## Account Level Fields
| Field | Description | Example Value | Notes |
|-------|-------------|---------------|-------|
| `account.iban` | Bank account IBAN | NL31RABO0300087233 | Always present |
| `account.currency` | Account currency | EUR | ISO 4217 currency code |

## Transaction Level Fields
| Field | Description | Example Value | Notes |
|-------|-------------|---------------|-------|
| `bookingDate` | Date transaction was booked | 2025-09-01 | YYYY-MM-DD format |
| `valueDate` | Value date of transaction | 2025-09-01 | YYYY-MM-DD format |
| `raboBookingDateTime` | Detailed booking timestamp | 2025-09-01T00:00:00.000000Z | ISO 8601 format |
| `entryReference` | Unique transaction reference | 000000000000092071 | Rabobank internal ID |
| `transactionAmount.currency` | Transaction currency | EUR | ISO 4217 currency code |
| `transactionAmount.amount` | Transaction amount | 815,79 | Decimal with comma separator |
| `remittanceInformationUnstructured` | Transaction description | Nr 882449 | Free text description |
| `raboDetailedTransactionType` | Rabobank transaction type code | 633 | Internal Rabobank code |
| `raboTransactionTypeName` | Transaction type name | st | Short transaction type |
| `reasonCode` | Transaction reason code | AG01 | SEPA reason code |
| `bankTransactionCode` | SEPA bank transaction code | PMNT-RCDT-ESCT | ISO 20022 code |
| `creditorAgent` | Creditor bank BIC | RABONL2U | SWIFT BIC code |
| `initiatingPartyName` | Party that initiated transaction | CSV IMPORT | Source system/party |
| `debtorAccount.iban` | Debtor account IBAN | NL31RABO0300087233 | Account being debited |
| `creditorAccount.iban` | Creditor account IBAN | NL84ABNA0115909877 | Account being credited |
| `creditorAccount.currency` | Creditor account currency | EUR | ISO 4217 currency code |
| `creditorName` | Creditor name | ONTVANGSTEN HILTERMANN L | Beneficiary name |
| `balanceAfterBooking.balanceAmount.currency` | Balance currency | EUR | ISO 4217 currency code |
| `balanceAfterBooking.balanceAmount.amount` | Account balance after transaction | 2301996,95 | Running balance |
| `balanceAfterBooking.balanceType` | Type of balance | InterimBooked | Balance status |

---

# Rabobank JSON Balance Fields Overview

## Account Level Fields
| Field | Description | Example Value | Notes |
|-------|-------------|---------------|-------|
| `account.iban` | Bank account IBAN | NL31RABO0300087233 | Always present |
| `account.currency` | Account currency | EUR | ISO 4217 currency code |

## PiggyBanks Fields (Savings Goals)
| Field | Description | Example Value | Notes |
|-------|-------------|---------------|-------|
| `piggyBanks[].piggyBankBalance` | Savings goal balance | 20000.00 | Decimal with dot separator |
| `piggyBanks[].piggyBankName` | Savings goal name | Car | Free text name |

## Balance Fields
| Field | Description | Example Value | Notes |
|-------|-------------|---------------|-------|
| `balances[].balanceType` | Type of balance | expected, closingBooked, interimBooked | Balance category |
| `balances[].balanceAmount.currency` | Balance currency | EUR | ISO 4217 currency code |
| `balances[].balanceAmount.amount` | Balance amount | -4538860,77 | Decimal with comma separator |
| `balances[].lastChangeDateTime` | Last change timestamp | 2025-09-01T14:07:17Z | ISO 8601 format |
| `balances[].referenceDate` | Reference date for balance | 2025-09-01 | YYYY-MM-DD format |

## Balance Types Explained
- **expected** - Expected balance including pending transactions
- **closingBooked** - Official closing balance for the day
- **interimBooked** - Current interim balance (real-time)
