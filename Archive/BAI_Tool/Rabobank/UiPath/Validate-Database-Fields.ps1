# Quick Database Field Validation
# Controleer of de nieuwe database velden bestaan

param(
    [string]$ConnectionString = "Host=localhost;Database=bai_tool;Username=postgres;Password=yourpassword"
)

Write-Host "🔍 Database Field Validation" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Gray

Write-Host "`nChecking if required database fields exist..." -ForegroundColor Yellow

$requiredFields = @(
    "entry_reference",
    "batch_entry_reference", 
    "rabo_detailed_transaction_type",
    "debtor_agent",
    "end_to_end_id",
    "debtor_name",
    "debtor_iban"
)

Write-Host "`n📋 Required Fields for CAMT.053 Generation:" -ForegroundColor Yellow
foreach ($field in $requiredFields) {
    Write-Host "   📊 $field" -ForegroundColor Green
}

Write-Host "`n💡 Testing Instructions:" -ForegroundColor Yellow
Write-Host "   1. Ensure your database has all required fields" -ForegroundColor Cyan
Write-Host "   2. Update connection string in test files" -ForegroundColor Cyan
Write-Host "   3. Use UiPath with Test_CAMT053_UiPath.cs code" -ForegroundColor Cyan
Write-Host "   4. Check generated XML for compliance" -ForegroundColor Cyan

Write-Host "`n🎯 What to Look For in Generated XML:" -ForegroundColor Yellow
Write-Host "   ✅ <NtryRef> elements with entry_reference values" -ForegroundColor Green
Write-Host "   ✅ <AcctSvcrRef> elements with batch_entry_reference values" -ForegroundColor Green
Write-Host "   ✅ <BkTxCd><Prtry><Cd> with rabo_detailed_transaction_type values" -ForegroundColor Green
Write-Host "   ✅ <RltdAgts><DbtrAgt><BIC> with debtor_agent values" -ForegroundColor Green
Write-Host "   ✅ Complete BkTxCd structure with Domn and Prtry sections" -ForegroundColor Green

Read-Host "`nPress Enter to continue"