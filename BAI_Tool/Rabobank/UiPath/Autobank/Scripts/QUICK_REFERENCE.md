# CAMT.053 Generator - Quick Reference

## Essential Commands for Daily Use

### 1. UiPath Invoke Code - Generate CAMT.053
```csharp
// Generate CAMT.053 XML from database
xmlResult = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(
    connectionString,  // "Host=server;Database=db;Username=user;Password=pass"
    iban,             // "NL31RABO0300087233" 
    startDate,        // "2025-10-01"
    endDate           // "2025-10-01"
);
```

### 2. Check Data Availability First
```csharp
// Always check before generating
dataAvailable = CAMT053GeneratorWrapper.CheckDataAvailability(
    connectionString, iban, startDate, endDate
);

if (dataAvailable)
{
    // Proceed with XML generation
}
```

### 3. Get Data Summary for Logging
```csharp
// Get summary for logs and reporting
summary = CAMT053GeneratorWrapper.GetDataSummary(
    connectionString, iban, startDate, endDate
);
// Returns: "Balance: €1,234.56, Transactions: 15, Period: 2025-10-01"
```

## Required Variables in UiPath

### Input Variables (String Type)
- `connectionString` - Database connection
- `iban` - Target IBAN
- `startDate` - Start date (YYYY-MM-DD)
- `endDate` - End date (YYYY-MM-DD)

### Output Variables
- `xmlResult` (String) - Generated XML or null
- `dataAvailable` (Boolean) - Data availability flag
- `summary` (String) - Data summary text

## Error Handling Pattern

```csharp
try 
{
    if (CAMT053GeneratorWrapper.CheckDataAvailability(connectionString, iban, startDate, endDate))
    {
        xmlResult = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(
            connectionString, iban, startDate, endDate);
        
        if (!string.IsNullOrEmpty(xmlResult))
        {
            // SUCCESS - Save file
            fileName = $"CAMT053_{iban}_{startDate}.xml";
            System.IO.File.WriteAllText(outputPath + fileName, xmlResult);
            logMessage = $"SUCCESS: {fileName} generated";
        }
    }
    else
    {
        logMessage = $"NO DATA: {iban} for {startDate}";
    }
}
catch (Exception ex)
{
    logMessage = $"ERROR: {ex.Message}";
}
```

## Common IBAN Values
```
NL31RABO0300087233 - Center Parcs Europe B.V.
NL48RABO0300002343 - Center Parcs Europe B.V
NL14RABO0118337572 - Sandur Vastgoed B.V.
NL50RABO0101000502 - Center Parcs Germany Holding BV
```

## Date Format Examples
```
Single Day:   startDate="2025-10-01", endDate="2025-10-01"
Week Range:   startDate="2025-10-01", endDate="2025-10-07"  
Month Range:  startDate="2025-10-01", endDate="2025-10-31"
```

## File Naming Convention
```
Output Format: CAMT053_{IBAN}_{STARTDATE}_{TIMESTAMP}.xml
Example: CAMT053_NL31RABO0300087233_20251001_143022.xml
```

## Quick Testing Commands

### Test Database Connection
```csharp
testResult = CAMT053GeneratorTest.TestDatabaseConnection(connectionString);
```

### Run All Tests  
```csharp
CAMT053GeneratorTest.RunComprehensiveTests();
```

### Generate Mock XML (No Database)
```csharp
mockXml = CAMT053GeneratorTest.TestXmlGeneration();
```

## Connection Strings

### Local Development
```
"Host=localhost;Database=rabobank_data;Username=dev_user;Password=dev_password"
```

### Production (Use Orchestrator Assets)
```
"Host=prod-db.company.com;Database=rabobank_prod;Username=svc_uipath;Password=***;SSL Mode=Require"
```

## Output Validation Checklist

✓ xmlResult is not null or empty
✓ Contains `urn:iso:std:iso:20022:tech:xsd:camt.053.001.02` 
✓ Contains `<Document>` and `<BkToCstmrStmt>`
✓ Contains target IBAN
✓ Contains balance and transaction data
✓ File saves successfully without errors

## Performance Guidelines

- **Single Day**: < 5 seconds
- **Weekly Range**: < 30 seconds  
- **Monthly Range**: < 2 minutes
- **Memory Usage**: < 100MB per IBAN

## Emergency Troubleshooting

### Problem: No XML Generated (null result)
1. Check data availability first
2. Verify date format (YYYY-MM-DD)
3. Check if balance data exists for previous day
4. Validate database connection

### Problem: Database Connection Failed
1. Test connection string manually
2. Check VPN/network connectivity
3. Verify credentials in Orchestrator Assets
4. Confirm database server is running

### Problem: Invalid XML Structure
1. Run mock XML generation test
2. Check for special characters in transaction data
3. Validate account info table has IBAN
4. Review error logs for specific issues

## Support Contacts

**Database Issues**: DBA Team - [dba-email]
**UiPath Workflow**: Automation Team - [automation-email]  
**Production Problems**: Support Team - [support-email]
**Code Updates**: Development Team - [dev-email]