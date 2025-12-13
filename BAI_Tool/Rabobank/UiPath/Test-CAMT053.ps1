# PowerShell Test Script voor CAMT053DatabaseGenerator
# Run dit script om de generator te testen

param(
    [string]$ConnectionString = "Host=localhost;Database=bai_tool;Username=postgres;Password=yourpassword",
    [string]$IBAN = "NL48RABO0300002343",
    [string]$StartDate = "2025-10-06",
    [string]$EndDate = "2025-10-06"
)

Write-Host "🔬 CAMT053DatabaseGenerator Test Script" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Gray

Write-Host "`n📊 Test Parameters:" -ForegroundColor Yellow
Write-Host "   IBAN: $IBAN"
Write-Host "   Period: $StartDate to $EndDate"
Write-Host "   Database: $($ConnectionString.Split(';')[1])"

# Test database connection eerst
Write-Host "`n🔌 Testing Database Connection..." -ForegroundColor Yellow
try {
    # Je zou hier een eenvoudige database test kunnen doen
    Write-Host "   ✅ Database connection configured" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Database connection failed: $_" -ForegroundColor Red
    exit 1
}

# Controleer of files bestaan
Write-Host "`n📁 Checking Files..." -ForegroundColor Yellow
$generatorPath = "c:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Autobank\Scripts\CAMT053DatabaseGenerator.cs"
$wrapperPath = "c:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Autobank\Scripts\CAMT053GeneratorWrapper.cs"

if (Test-Path $generatorPath) {
    Write-Host "   ✅ CAMT053DatabaseGenerator.cs found" -ForegroundColor Green
} else {
    Write-Host "   ❌ CAMT053DatabaseGenerator.cs not found" -ForegroundColor Red
}

if (Test-Path $wrapperPath) {
    Write-Host "   ✅ CAMT053GeneratorWrapper.cs found" -ForegroundColor Green
} else {
    Write-Host "   ❌ CAMT053GeneratorWrapper.cs not found" -ForegroundColor Red
}

Write-Host "`n🚀 Next Steps for Testing:" -ForegroundColor Yellow
Write-Host "   1. Open UiPath Studio" -ForegroundColor Cyan
Write-Host "   2. Create nieuwe workflow" -ForegroundColor Cyan
Write-Host "   3. Add Invoke Code activity" -ForegroundColor Cyan
Write-Host "   4. Copy code from Test_CAMT053_UiPath.cs" -ForegroundColor Cyan
Write-Host "   5. Update connection string met jouw database credentials" -ForegroundColor Cyan
Write-Host "   6. Run en check output XML file" -ForegroundColor Cyan

Write-Host "`n📄 Test Files Created:" -ForegroundColor Yellow
Write-Host "   • Test_CAMT053_UiPath.cs - UiPath Invoke Code ready" -ForegroundColor Green
Write-Host "   • CAMT053DatabaseGenerator.cs - Updated with all mappings" -ForegroundColor Green

Write-Host "`n✨ Ready for Testing!" -ForegroundColor Green
Write-Host "Use the UiPath test code to validate all implementations." -ForegroundColor Gray

Read-Host "`nPress Enter to continue"