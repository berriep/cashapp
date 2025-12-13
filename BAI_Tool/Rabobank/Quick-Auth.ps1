# Quick Authorization Code Exchange Script
# Usage: .\Quick-Auth.ps1

Write-Host "🔑 Quick Authorization Code Exchange" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

$ProjectPath = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank"
Set-Location $ProjectPath

# Check for auth_code.txt file
$authCodeFile = "auth_code.txt"
if (Test-Path $authCodeFile) {
    $authCode = Get-Content $authCodeFile -Raw
    $authCode = $authCode.Trim()
    
    if ($authCode -eq "PLAATS_HIER_JOUW_NIEUWE_CONSENT_CODE" -or $authCode.Length -lt 50) {
        Write-Host "❌ Please update auth_code.txt with your actual consent code!" -ForegroundColor Red
        Write-Host "   Current content: $authCode" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "📝 Steps:" -ForegroundColor Cyan
        Write-Host "  1. Open: $ProjectPath\auth_code.txt" -ForegroundColor White
        Write-Host "  2. Replace the placeholder with your actual consent code" -ForegroundColor White
        Write-Host "  3. Save the file" -ForegroundColor White
        Write-Host "  4. Run this script again" -ForegroundColor White
        exit 1
    }
    
    Write-Host "✅ Found authorization code in auth_code.txt" -ForegroundColor Green
    Write-Host "📏 Length: $($authCode.Length) characters" -ForegroundColor Green
    Write-Host ""
    Write-Host "🚀 Starting exchange..." -ForegroundColor Cyan
    
    & dotnet run -- --auth-code="$authCode"
    
} else {
    Write-Host "❌ auth_code.txt not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Options:" -ForegroundColor Cyan
    Write-Host "  1. Create auth_code.txt with your consent code" -ForegroundColor White
    Write-Host "  2. Or use: .\Exchange-AuthCode.ps1 -AuthCode 'YOUR_CODE'" -ForegroundColor White
}