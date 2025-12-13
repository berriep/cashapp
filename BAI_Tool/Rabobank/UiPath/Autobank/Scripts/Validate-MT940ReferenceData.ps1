# Database Reference Data Validator for MT940 Generation
# Checks if all required reference fields are populated correctly

param(
    [string]$ConnectionString = "Host=localhost;Database=bai_tool;Username=postgres;Password=yourpassword",
    [string]$IBAN = "NL31RABO0300087233",
    [string]$StartDate = "2025-11-07",
    [string]$EndDate = "2025-11-07"
)

Write-Host "🔍 MT940 Reference Data Validator" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Gray

Write-Host "`n📊 Validation Parameters:" -ForegroundColor Yellow
Write-Host "   IBAN: $IBAN" -ForegroundColor White
Write-Host "   Period: $StartDate to $EndDate" -ForegroundColor White

# SQL queries for validation
$queries = @{
    "balance_check" = @"
SELECT 
    iban, day, opening_balance, closing_balance, transaction_count,
    CASE 
        WHEN opening_balance IS NULL THEN '❌ Missing opening_balance'
        WHEN closing_balance IS NULL THEN '❌ Missing closing_balance'
        ELSE '✅ Balance data OK'
    END as status
FROM rpa_data.bai_rabobank_balances 
WHERE iban = '$IBAN' AND day BETWEEN '$StartDate' AND '$EndDate'
ORDER BY day;
"@

    "reference_summary" = @"
SELECT 
    COUNT(*) as total_transactions,
    COUNT(batch_entry_reference) as has_batch_ref,
    COUNT(instruction_id) as has_instruction_id,
    COUNT(CASE WHEN end_to_end_id IS NOT NULL AND end_to_end_id != 'NOTPROVIDED' THEN 1 END) as has_end_to_end,
    COUNT(acctsvcr_ref) as has_acctsvcr_ref,
    COUNT(payment_information_identification) as has_payment_info
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = '$IBAN' AND booking_date BETWEEN '$StartDate' AND '$EndDate';
"@

    "reference_examples" = @"
SELECT 
    entry_reference,
    batch_entry_reference,
    instruction_id,
    end_to_end_id,
    acctsvcr_ref,
    rabo_detailed_transaction_type,
    transaction_amount,
    CASE 
        WHEN batch_entry_reference IS NOT NULL THEN '✅ Has batch_ref'
        WHEN instruction_id IS NOT NULL THEN '⚠️ Has instruction_id only'
        WHEN end_to_end_id IS NOT NULL AND end_to_end_id != 'NOTPROVIDED' THEN '⚠️ Has end_to_end only'
        ELSE '❌ Missing key references'
    END as reference_status
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = '$IBAN' AND booking_date BETWEEN '$StartDate' AND '$EndDate'
ORDER BY entry_reference
LIMIT 15;
"@

    "transaction_types" = @"
SELECT 
    rabo_detailed_transaction_type,
    COUNT(*) as count,
    STRING_AGG(DISTINCT 
        CASE 
            WHEN batch_entry_reference IS NOT NULL THEN 'batch_ref'
            WHEN instruction_id IS NOT NULL THEN 'instr_id'
            WHEN end_to_end_id IS NOT NULL AND end_to_end_id != 'NOTPROVIDED' THEN 'end_to_end'
            ELSE 'no_ref'
        END, ', ') as available_refs
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = '$IBAN' AND booking_date BETWEEN '$StartDate' AND '$EndDate'
GROUP BY rabo_detailed_transaction_type
ORDER BY count DESC;
"@

    "missing_references" = @"
SELECT 
    'Missing batch_entry_reference' as issue,
    COUNT(*) as count
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = '$IBAN' 
  AND booking_date BETWEEN '$StartDate' AND '$EndDate'
  AND batch_entry_reference IS NULL

UNION ALL

SELECT 
    'Missing instruction_id' as issue,
    COUNT(*) as count
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = '$IBAN' 
  AND booking_date BETWEEN '$StartDate' AND '$EndDate'
  AND instruction_id IS NULL

UNION ALL

SELECT 
    'Missing/NOTPROVIDED end_to_end_id' as issue,
    COUNT(*) as count
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = '$IBAN' 
  AND booking_date BETWEEN '$StartDate' AND '$EndDate'
  AND (end_to_end_id IS NULL OR end_to_end_id = 'NOTPROVIDED');
"@
}

# Function to execute query and display results
function Execute-ValidationQuery {
    param(
        [string]$QueryName,
        [string]$Query,
        [string]$ConnectionString
    )
    
    Write-Host "`n🔍 $QueryName" -ForegroundColor Yellow
    Write-Host ("-" * 50) -ForegroundColor Gray
    
    try {
        # For demonstration, show the SQL query
        Write-Host "SQL Query:" -ForegroundColor Cyan
        Write-Host $Query -ForegroundColor White
        
        Write-Host "`n💡 To execute this query:" -ForegroundColor Yellow
        Write-Host "   1. Connect to PostgreSQL: psql '$ConnectionString'" -ForegroundColor Gray
        Write-Host "   2. Run the query above" -ForegroundColor Gray
        Write-Host "   3. Or use pgAdmin/DBeaver with the connection string" -ForegroundColor Gray
        
    }
    catch {
        Write-Host "❌ Error preparing query: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Execute all validation queries
Write-Host "`n🏗️ Running Validation Queries..." -ForegroundColor Green

Execute-ValidationQuery "Balance Data Check" $queries["balance_check"] $ConnectionString
Execute-ValidationQuery "Reference Field Summary" $queries["reference_summary"] $ConnectionString  
Execute-ValidationQuery "Reference Examples" $queries["reference_examples"] $ConnectionString
Execute-ValidationQuery "Transaction Types by Reference" $queries["transaction_types"] $ConnectionString
Execute-ValidationQuery "Missing Reference Analysis" $queries["missing_references"] $ConnectionString

# Provide recommendations
Write-Host "`n💡 Validation Recommendations:" -ForegroundColor Yellow
Write-Host @"
1. 🎯 Priority Reference Fields for MT940:
   • batch_entry_reference → Contains OO9T... references (HIGHEST PRIORITY)
   • instruction_id → Alternative reference source
   • end_to_end_id → EREF references (exclude 'NOTPROVIDED')

2. 🔧 Expected Results:
   • batch_entry_reference should contain references like 'OO9T005180594671'
   • instruction_id should be populated for most transactions
   • end_to_end_id should have real values, not 'NOTPROVIDED'

3. ⚠️ Common Issues:
   • If batch_entry_reference is NULL → Check API field mapping
   • If all references are database IDs → Wrong column mapping in insert
   • If acctsvcr_ref contains generated IDs → Should not be used for MT940

4. 🚀 Next Steps:
   • Run queries above in database client
   • Check if batch_entry_reference contains real bank references
   • Update MT940 generator priority logic based on results
"@ -ForegroundColor White

Write-Host "`n📋 Quick Manual Check Commands:" -ForegroundColor Cyan
Write-Host @"
# Connect to database:
psql "$ConnectionString"

# Quick reference check:
\x
SELECT entry_reference, batch_entry_reference, instruction_id, end_to_end_id 
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = '$IBAN' AND booking_date = '$StartDate' 
LIMIT 5;
"@ -ForegroundColor White

Write-Host "`n✅ Validation script completed!" -ForegroundColor Green
Write-Host "   Run the SQL queries above to check your reference data quality." -ForegroundColor Gray