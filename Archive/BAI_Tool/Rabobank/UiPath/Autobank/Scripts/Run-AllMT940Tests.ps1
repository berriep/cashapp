# All-in-One MT940 Test Runner
# Validates data, generates MT940, and compares results

param(
    [string]$ConnectionString = "Host=localhost;Database=bai_tool;Username=postgres;Password=yourpassword",
    [string]$IBAN = "NL31RABO0300087233", 
    [string]$StartDate = "2025-11-07",
    [string]$EndDate = "2025-11-07",
    [string]$OriginalFile = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Autobank\Ouput\MT940_A_NL31RABO0300087233_EUR_20251107_20251107.swi",
    [switch]$SkipDataValidation = $false,
    [switch]$SkipComparison = $false
)

Write-Host "All-in-One MT940 Test Runner" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Gray

$scriptDir = $PSScriptRoot
Write-Host "Script Directory: $scriptDir" -ForegroundColor Yellow

# Step 1: Data Validation (unless skipped)
if (-not $SkipDataValidation) {
    Write-Host "`nStep 1: Validating Reference Data..." -ForegroundColor Green
    $validationScript = Join-Path $scriptDir "Validate-MT940ReferenceData.ps1"
    if (Test-Path $validationScript) {
        & $validationScript -ConnectionString $ConnectionString -IBAN $IBAN -StartDate $StartDate -EndDate $EndDate
    } else {
        Write-Host "   Warning: Validation script not found: $validationScript" -ForegroundColor Yellow
    }
} else {
    Write-Host "`nStep 1: Skipping data validation" -ForegroundColor Gray
}

# Step 2: Check if we can generate MT940 (manual step for now)
Write-Host "`nStep 2: MT940 Generation" -ForegroundColor Green
Write-Host "   Since VB.NET requires UiPath/manual execution:" -ForegroundColor Yellow
Write-Host "   Manual Steps:" -ForegroundColor White
Write-Host "   1. Open UiPath Studio or Visual Studio" -ForegroundColor White
Write-Host "   2. Load the VB.NET script: autobank_rabobank_mt940_db.vb" -ForegroundColor White  
Write-Host "   3. Set these parameters:" -ForegroundColor White
Write-Host "      IBAN: $IBAN" -ForegroundColor White
Write-Host "      Date: $StartDate" -ForegroundColor White
Write-Host "      Connection: $ConnectionString" -ForegroundColor White
Write-Host "   4. Execute the script" -ForegroundColor White
Write-Host "   5. Check C:\temp\ for generated MT940_*.swi file" -ForegroundColor White

# Step 3: Find latest generated file
Write-Host "`nStep 3: Finding Generated Files..." -ForegroundColor Green
$tempFiles = Get-ChildItem "C:\temp\MT940_*.swi" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending

if ($tempFiles.Count -gt 0) {
    $latestGenerated = $tempFiles[0].FullName
    Write-Host "   Found latest generated file:" -ForegroundColor Green
    Write-Host "      $latestGenerated" -ForegroundColor White
    Write-Host "      Created: $($tempFiles[0].LastWriteTime)" -ForegroundColor Gray
    
    # Check if it's recent (within last hour)
    $age = (Get-Date) - $tempFiles[0].LastWriteTime
    if ($age.TotalHours -lt 1) {
        Write-Host "      File is recent (less than 1 hour old)" -ForegroundColor Green
        $useLatestGenerated = $true
    } else {
        Write-Host "      Warning: File is older than 1 hour - may not be from current test" -ForegroundColor Yellow
        $useLatestGenerated = $false
    }
} else {
    Write-Host "   No generated MT940 files found in C:\temp\" -ForegroundColor Red
    Write-Host "      Please run the VB.NET generator first" -ForegroundColor Gray
    $useLatestGenerated = $false
}

# Step 4: Quick Comparison (if files available)
if (-not $SkipComparison -and $useLatestGenerated -and (Test-Path $OriginalFile)) {
    Write-Host "`n⚡ Step 4: Quick Comparison..." -ForegroundColor Green
    $compareScript = Join-Path $scriptDir "Quick-CompareMT940.ps1"
    if (Test-Path $compareScript) {
        & $compareScript -OriginalFile $OriginalFile -GeneratedFile $latestGenerated -FocusOnReferences
    } else {
        Write-Host "   ⚠️ Quick compare script not found: $compareScript" -ForegroundColor Yellow
    }
} elseif (-not (Test-Path $OriginalFile)) {
    Write-Host "`n⚠️ Step 4: Skipping comparison - Original file not found:" -ForegroundColor Yellow
    Write-Host "      $OriginalFile" -ForegroundColor Gray
} else {
    Write-Host "`n⏭️ Step 4: Skipping comparison" -ForegroundColor Gray
}

# Step 5: Detailed Analysis (optional)
Write-Host "`n📊 Step 5: Detailed Analysis Options" -ForegroundColor Green
Write-Host "   For deeper analysis, run these scripts:" -ForegroundColor Yellow

$detailedCompareScript = Join-Path (Split-Path $scriptDir) "Validators\Compare-MT940Files.ps1"
if (Test-Path $detailedCompareScript) {
    Write-Host "   🔍 Detailed Comparison:" -ForegroundColor Cyan
    Write-Host "      `& '$detailedCompareScript' -OriginalFile '$OriginalFile' -GeneratedFile '$latestGenerated'" -ForegroundColor White
}

Write-Host "   📊 Database Query for Reference Check:" -ForegroundColor Cyan
Write-Host "      psql `"$ConnectionString`" -c `"SELECT entry_reference, batch_entry_reference, instruction_id, end_to_end_id, rabo_detailed_transaction_type FROM rpa_data.bai_rabobank_transactions WHERE iban = '$IBAN' AND booking_date = '$StartDate' ORDER BY entry_reference LIMIT 5;`"" -ForegroundColor White

# Summary and next steps
Write-Host "`n🎯 Test Summary:" -ForegroundColor Yellow
Write-Host "   1. Data validation: $(if (-not $SkipDataValidation) { 'Completed' } else { 'Skipped' })" -ForegroundColor White
Write-Host "   2. MT940 generation: Manual step required" -ForegroundColor White  
Write-Host "   3. File detection: $(if ($useLatestGenerated) { 'Found recent file' } else { 'No recent file found' })" -ForegroundColor White
Write-Host "   4. Quick comparison: $(if (-not $SkipComparison -and $useLatestGenerated) { 'Completed' } else { 'Skipped' })" -ForegroundColor White

Write-Host "`n🔧 Next Actions:" -ForegroundColor Yellow
if (-not $useLatestGenerated) {
    Write-Host "   🎯 PRIORITY: Generate new MT940 file using VB.NET script" -ForegroundColor Red
    Write-Host "      • Use UiPath or compile VB.NET script manually" -ForegroundColor Gray
    Write-Host "      • Check database connection and data availability" -ForegroundColor Gray
}

Write-Host "   🔍 Check database references manually:" -ForegroundColor Cyan
Write-Host "      • Verify batch_entry_reference contains OO9T... values" -ForegroundColor Gray
Write-Host "      • Check if instruction_id and end_to_end_id are populated" -ForegroundColor Gray

Write-Host "   📊 Compare results:" -ForegroundColor Cyan
Write-Host "      • Focus on transaction reference differences" -ForegroundColor Gray
Write-Host "      • Look for N501EREFEREF double EREF issues" -ForegroundColor Gray

Write-Host "`nAll-in-one test completed!" -ForegroundColor Green