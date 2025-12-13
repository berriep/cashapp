# CAMT.053 Database Generator - UiPath Implementation Guide

## Overview
Complete implementatiegids voor het gebruik van de CAMT.053 Database Generator in UiPath workflows. Deze tool converteert Rabobank database data naar ISO 20022 CAMT.053 XML format.

## Project Components

### Core Files
- `CAMT053DatabaseGenerator.cs` - Hoofdgenerator class
- `CAMT053GeneratorWrapper.cs` - UiPath-compatible wrapper
- `CAMT053GeneratorTest.cs` - Test utilities
- `create_account_info_table.sql` - Database setup script

### Database Tables Required
- `bai_rabobank_balances_payload` - Balance data
- `bai_rabobank_transactions_payload` - Transaction data  
- `bai_rabobank_account_info` - Account owner information

## Database Setup

### 1. Execute Account Info Setup
```sql
-- Run the SQL script to create account info table
\i create_account_info_table.sql
```

### 2. Verify Database Schema
```sql
-- Check if all required tables exist
SELECT table_name 
FROM information_schema.tables 
WHERE table_name IN (
    'bai_rabobank_balances_payload',
    'bai_rabobank_transactions_payload', 
    'bai_rabobank_account_info'
);
```

## UiPath Implementation

### 1. Add C# Files to UiPath Project

1. Copy all `.cs` files to your UiPath project folder
2. Add them as "Included Files" in UiPath Studio
3. Ensure Npgsql NuGet package is installed

### 2. Required NuGet Packages

Add these packages to your UiPath project:
```
Npgsql (latest version)
System.Xml.Linq (if not already included)
```

### 3. Invoke Code Activity Setup

#### Activity: Generate CAMT.053 XML
```csharp
// Input parameters (all String type):
// - connectionString: PostgreSQL connection string
// - iban: Target IBAN (e.g., "NL31RABO0300087233")  
// - startDate: Start date (YYYY-MM-DD format)
// - endDate: End date (YYYY-MM-DD format)

// Output parameter (String type):
// - xmlResult: Generated CAMT.053 XML or null if no data

xmlResult = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(
    connectionString, iban, startDate, endDate);
```

#### Activity: Check Data Availability
```csharp
// Check if data exists before generating XML
dataAvailable = CAMT053GeneratorWrapper.CheckDataAvailability(
    connectionString, iban, startDate, endDate);
```

#### Activity: Get Data Summary
```csharp
// Get summary of available data
dataSummary = CAMT053GeneratorWrapper.GetDataSummary(
    connectionString, iban, startDate, endDate);
```

### 4. Connection String Configuration

#### Development Environment
```
"Host=localhost;Database=rabobank_data;Username=dev_user;Password=dev_password"
```

#### Production Environment  
```
"Host=prod-server;Database=rabobank_prod;Username=prod_user;Password=prod_password;SSL Mode=Require"
```

Store connection strings in UiPath Config or Orchestrator Assets for security.

### 5. Error Handling in UiPath

#### Try-Catch Block Structure
```csharp
try 
{
    xmlResult = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(
        connectionString, iban, startDate, endDate);
        
    if (xmlResult == null)
    {
        // No data available for the specified parameters
        logMessage = $"No data available for IBAN {iban} between {startDate} and {endDate}";
    }
    else
    {
        // Success - save XML to file
        System.IO.File.WriteAllText($"CAMT053_{iban}_{startDate}.xml", xmlResult);
    }
}
catch (Exception ex)
{
    // Handle errors
    logMessage = $"CAMT.053 generation failed: {ex.Message}";
}
```

#### Common Error Scenarios
- **Database Connection Failed**: Check connection string and network connectivity
- **Invalid Date Format**: Ensure dates are in YYYY-MM-DD format
- **Missing Balance Data**: Opening/closing balance must exist for the period
- **No Transaction Data**: Generator will create CAMT.053 with balances only
- **Account Not Found**: Check if IBAN exists in account_info table

### 6. Output Validation

#### XML Structure Check
```csharp
// Basic validation
if (!string.IsNullOrEmpty(xmlResult))
{
    bool isValidXml = xmlResult.Contains("urn:iso:std:iso:20022:tech:xsd:camt.053.001.02") 
                   && xmlResult.Contains("<Document") 
                   && xmlResult.Contains("<BkToCstmrStmt>");
                   
    if (isValidXml)
    {
        // XML is properly formatted
        // Save to output directory
    }
}
```

## Workflow Examples

### Example 1: Daily CAMT.053 Generation
```
1. Get list of IBANs from database or config
2. For each IBAN:
   a. Check data availability for yesterday
   b. If data exists, generate CAMT.053
   c. Save XML file to output directory
   d. Log success/failure
3. Send summary email with results
```

### Example 2: Bulk Historical Generation
```  
1. Read date range from input (start to end date)
2. For each date in range:
   a. Generate CAMT.053 for all configured IBANs
   b. Save files with naming convention
   c. Track progress and errors
3. Create summary report
```

### Example 3: On-Demand Generation via Orchestrator Queue
```
1. Monitor queue for CAMT.053 requests
2. Process queue item with parameters:
   - IBAN
   - Date range
   - Output location
3. Generate XML and upload to specified location
4. Update queue item with results
```

## Testing and Validation

### 1. Run Comprehensive Tests
```csharp
// Test all functionality
CAMT053GeneratorTest.RunComprehensiveTests();
```

### 2. Validate Against Real Data
1. Compare generated XML with actual Rabobank CAMT.053 files
2. Check that all required elements are present
3. Validate account names match account_info table
4. Ensure balance reconciliation is correct

### 3. Performance Considerations
- Test with large transaction volumes (1000+ transactions)
- Monitor memory usage for large datasets
- Consider batching for very large date ranges

## File Output Management

### Naming Convention
```
CAMT053_{IBAN}_{YYYYMMDD}_{HHMMSS}.xml
Example: CAMT053_NL31RABO0300087233_20251001_143022.xml
```

### Directory Structure
```
Output/
├── Daily/
│   ├── 2025-10-01/
│   └── 2025-10-02/
├── Historical/
└── Archive/
```

### File Archival
- Move files older than 30 days to Archive folder
- Compress archived files to save space
- Maintain audit trail of generated files

## Monitoring and Logging

### Key Metrics to Track
- Files generated per day
- Processing time per IBAN
- Error rates by error type
- Data availability percentage

### Log Format Example
```
2025-10-09 14:30:15 [INFO] Starting CAMT.053 generation for NL31RABO0300087233
2025-10-09 14:30:16 [INFO] Found 25 transactions for period 2025-10-08 to 2025-10-08
2025-10-09 14:30:16 [INFO] XML generated successfully: 15,234 characters
2025-10-09 14:30:16 [SUCCESS] File saved: CAMT053_NL31RABO0300087233_20251008.xml
```

## Security Considerations

### Database Access
- Use dedicated service account with minimal permissions
- Encrypt connection strings in Orchestrator Assets
- Enable SSL/TLS for database connections

### Output Files
- Ensure output directory has appropriate access controls
- Consider encrypting sensitive CAMT.053 files
- Implement audit logging for file access

### Error Information
- Avoid logging sensitive data in error messages
- Sanitize IBAN numbers in logs (show only last 4 digits)
- Use structured logging for security monitoring

## Troubleshooting Guide

### Common Issues and Solutions

#### Issue: "No data available"
**Cause**: Missing balance or transaction data for specified period
**Solution**: 
1. Check if data exists in source tables
2. Verify date format (YYYY-MM-DD)
3. Ensure balance data exists for day before start date

#### Issue: "Database connection failed"  
**Cause**: Network, authentication, or server issues
**Solution**:
1. Test connection string manually
2. Verify database server is accessible
3. Check firewall and proxy settings

#### Issue: "XML generation error"
**Cause**: Data inconsistency or formatting issues  
**Solution**:
1. Run data validation queries
2. Check for special characters in transaction descriptions
3. Verify all required fields have valid values

#### Issue: "Account name not found"
**Cause**: IBAN missing from bai_rabobank_account_info table
**Solution**:
1. Execute create_account_info_table.sql script
2. Add missing IBAN to account_info table
3. Verify account name lookup is working

## Support and Maintenance

### Regular Maintenance Tasks
- Weekly: Review error logs and resolve data issues
- Monthly: Performance monitoring and optimization
- Quarterly: Update account_info table with new IBANs
- Annually: Review and update XML schema compliance

### Backup and Recovery
- Daily backup of generated CAMT.053 files
- Weekly backup of configuration and account data
- Test recovery procedures monthly

### Version Control
- Maintain version history of all C# components
- Document changes and test thoroughly
- Use staged deployment for production updates

---

## Contact Information

For technical support and questions:
- Development Team: [team-email]
- Database Administration: [dba-email]  
- Production Support: [support-email]

## Changelog

### Version 2.0 (Current)
- Added Rabobank-specific XML elements (NtryRef, AcctSvcrRef, RltdAgts, RltdDts)
- Implemented dynamic account name lookup
- Enhanced error handling and validation
- Added comprehensive testing framework

### Version 1.0 (Previous)
- Basic CAMT.053 XML generation
- Database integration
- UiPath wrapper functionality