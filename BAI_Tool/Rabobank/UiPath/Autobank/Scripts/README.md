# CAMT.053 Database Generator Scripts

Deze scripts genereren CAMT.053 XML bestanden uit Rabobank database data voor gebruik in UiPath workflows.

## Bestanden

- `CAMT053DatabaseGenerator.cs` - Hoofdklasse voor CAMT.053 generatie
- `CAMT053GeneratorWrapper.cs` - UiPath Invoke Code wrapper

## Gebruik in UiPath

### 1. Invoke Code Activity Setup

Gebruik een **Invoke Code** activity met de volgende instellingen:

**Language:** C#
**Code:** (zie hieronder)

**Input Parameters:**
- `connectionString` (String) - PostgreSQL connection string
- `iban` (String) - IBAN van de rekening
- `startDate` (String) - Startdatum in formaat YYYY-MM-DD
- `endDate` (String) - Einddatum in formaat YYYY-MM-DD

**Output Parameters:**
- `result` (String) - Gegenereerde CAMT.053 XML

### 2. Code voor Invoke Code Activity

```csharp
// Add necessary using statements
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using Npgsql;

// Include the CAMT053DatabaseGenerator class code here
// [PASTE THE ENTIRE CAMT053DatabaseGenerator.cs CONTENT HERE]

// Include the CAMT053GeneratorWrapper class code here
// [PASTE THE ENTIRE CAMT053GeneratorWrapper.cs CONTENT HERE]

// Main execution
result = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(connectionString, iban, startDate, endDate);
```

### 3. Connection String Format

```
Host=your-host;Port=5432;Database=your-database;Username=your-username;Password=your-password;SSL Mode=Require;Trust Server Certificate=true
```

### 4. Voorbeeld Workflow

1. **Check Data Availability** (optioneel):
   ```csharp
   bool dataAvailable = CAMT053GeneratorWrapper.CheckDataAvailability(connectionString, iban, startDate, endDate);
   ```

2. **Get Data Summary** (optioneel):
   ```csharp
   string summary = CAMT053GeneratorWrapper.GetDataSummary(connectionString, iban, startDate, endDate);
   ```

3. **Generate CAMT.053 XML**:
   ```csharp
   string camt053Xml = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(connectionString, iban, startDate, endDate);
   ```

4. **Save to File**:
   - Gebruik Write Text File activity om de XML op te slaan

### 5. Error Handling

Het script retourneert een error XML als er iets misgaat:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
    <BkToCstmrStmt>
        <GrpHdr>
            <MsgId>ERROR</MsgId>
            <CreDtTm>2025-10-09T14:30:00</CreDtTm>
        </GrpHdr>
        <Stmt>
            <Id>ERROR</Id>
            <AddtlStmtInf>
                <AddtlInf>Escape error message</AddtlInf>
            </AddtlStmtInf>
        </Stmt>
    </BkToCstmrStmt>
</Document>
```

Controleer of `MsgId` = "ERROR" om errors te detecteren.

## Database Requirements

### Tabellen
- `bai_rabobank_balances_payload`
- `bai_rabobank_transactions_payload`

### Indexes (aanbevolen voor performance)
```sql
CREATE INDEX idx_bai_balance_iban_type_date ON bai_rabobank_balances_payload (iban, balance_type, reference_date);
CREATE INDEX idx_bai_tx_iban_booking_date ON bai_rabobank_transactions_payload (iban, booking_date DESC);
```

## Parameters

| Parameter | Type | Beschrijving | Voorbeeld |
|-----------|------|-------------|----------|
| `connectionString` | String | PostgreSQL connection string | `Host=localhost;Port=5432;Database=bankdb;Username=user;Password=pass` |
| `iban` | String | IBAN van de rekening | `NL12RABO0330208888` |
| `startDate` | String | Startdatum (YYYY-MM-DD) | `2025-10-01` |
| `endDate` | String | Einddatum (YYYY-MM-DD) | `2025-10-31` |

## Output

- **Success:** Geldige CAMT.053 XML string volgens ISO 20022 standaard
- **Error:** Error XML met foutmelding

## Dependencies

- Npgsql (PostgreSQL client voor .NET)
- System.Xml
- System.Data

Zorg ervoor dat Npgsql.dll beschikbaar is in je UiPath project.