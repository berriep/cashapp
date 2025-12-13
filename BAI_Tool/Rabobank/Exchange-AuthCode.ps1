# Authorization Code Exchange Script
# Usage: .\Exchange-AuthCode.ps1 -AuthCode "YOUR_AUTHORIZATION_CODE"

param(
    [Parameter(Mandatory=$true)]
    [string]$AuthCode,
    
    [Parameter(Mandatory=$false)]
    [string]$ClientName = "default"
)

Write-Host "Authorization Code Exchange Script" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

# Validate auth code format
if ($AuthCode.Length -lt 50) {
    Write-Host "Warning: Authorization code seems too short. Expected length is typically 200+ characters." -ForegroundColor Yellow
    Write-Host ""
}

# Set location to project directory
$ProjectPath = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank"
Set-Location $ProjectPath

Write-Host "Project Directory: $ProjectPath" -ForegroundColor Green
Write-Host "Client: $ClientName" -ForegroundColor Green
Write-Host "Auth Code Length: $($AuthCode.Length) characters" -ForegroundColor Green
Write-Host ""

# Check if .NET is available
try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK Version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host ".NET SDK not found. Please install .NET 8.0 SDK first." -ForegroundColor Red
    Write-Host "   Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "🚀 Starting authorization code exchange..." -ForegroundColor Cyan
Write-Host ""

# Run the application with auth code
try {
    $arguments = "--auth-code=$AuthCode"
    if ($ClientName -ne "default") {
        $arguments += " --client=$ClientName"
    }
    
    & dotnet run -- $arguments
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ Authorization code exchange completed!" -ForegroundColor Green
        Write-Host "💾 Tokens have been saved and are ready for use." -ForegroundColor Green
        Write-Host ""
        Write-Host "🔄 Next steps:" -ForegroundColor Cyan
        Write-Host "  1. Run 'dotnet run' to test normal token management" -ForegroundColor White
        Write-Host "  2. Tokens will auto-refresh when needed" -ForegroundColor White
        Write-Host "  3. Check 'tokens/$ClientName`_tokens.json' for saved tokens" -ForegroundColor White
    } else {
        Write-Host ""
        Write-Host "❌ Authorization code exchange failed!" -ForegroundColor Red
        Write-Host "💡 Check the error messages above for details." -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Error running application: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "📊 Token Status Check:" -ForegroundColor Cyan
$tokenFile = "tokens/$ClientName`_tokens.json"
if (Test-Path $tokenFile) {
    $tokenInfo = Get-Content $tokenFile | ConvertFrom-Json
    $retrievedAt = [DateTimeOffset]::FromUnixTimeSeconds($tokenInfo.retrieved_at).DateTime
    $expiresIn = $tokenInfo.expires_in
    $expiryTime = $retrievedAt.AddSeconds($expiresIn)
    $timeRemaining = $expiryTime - (Get-Date)
    
    Write-Host "  📄 Token file: $tokenFile" -ForegroundColor White
    Write-Host "  🕐 Retrieved: $retrievedAt" -ForegroundColor White
    Write-Host "  ⏰ Expires: $expiryTime" -ForegroundColor White
    Write-Host "  ⏳ Time remaining: $([math]::Round($timeRemaining.TotalHours, 1)) hours" -ForegroundColor White
    
    if ($timeRemaining.TotalHours -gt 1) {
        Write-Host "  ✅ Token is valid" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Token expires soon" -ForegroundColor Yellow
    }
} else {
    Write-Host "  No token file found at: $tokenFile" -ForegroundColor Red
}

Write-Host ""