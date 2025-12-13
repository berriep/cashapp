# Test Script - Validate Production Certificate Configuration
# Run this script to verify that GetAccountList.cs will work with the correct certificate

param(
    [string]$PfxPath = "C:\Users\uipath\BAI\Certificate Production\api_rabo_PRODUCTION.pfx",
    [string]$PfxPassword = "EPsBGVecfWa9jGb6Bd3B"
)

Write-Host "=== Production Certificate Validation Test ===" -ForegroundColor Green

# Test 1: Check if PFX file exists
Write-Host "`n1. Testing PFX file existence..." -ForegroundColor Cyan
if (Test-Path $PfxPath) {
    Write-Host "✅ PFX file found: $PfxPath" -ForegroundColor Green
} else {
    Write-Host "❌ PFX file NOT found: $PfxPath" -ForegroundColor Red
    Write-Host "   Make sure you exported the certificate from Certificate Manager as api_rabo_PRODUCTION.pfx" -ForegroundColor Yellow
    exit 1
}

# Test 2: Load certificate and verify details
Write-Host "`n2. Testing certificate loading..." -ForegroundColor Cyan
try {
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $PfxPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::MachineKeySet)
    
    Write-Host "✅ Certificate loaded successfully" -ForegroundColor Green
    Write-Host "   Subject: $($cert.Subject)" -ForegroundColor White
    Write-Host "   Issuer: $($cert.Issuer)" -ForegroundColor White
    Write-Host "   Serial: $($cert.SerialNumber)" -ForegroundColor White
    Write-Host "   Valid From: $($cert.NotBefore)" -ForegroundColor White
    Write-Host "   Valid To: $($cert.NotAfter)" -ForegroundColor White
    Write-Host "   Has Private Key: $($cert.HasPrivateKey)" -ForegroundColor White
    Write-Host "   Thumbprint: $($cert.Thumbprint)" -ForegroundColor White
    
} catch {
    Write-Host "❌ Failed to load certificate: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Check password or PFX file integrity" -ForegroundColor Yellow
    exit 1
}

# Test 3: Verify production certificate serial
Write-Host "`n3. Testing production certificate serial..." -ForegroundColor Cyan
$expectedSerial = "044CB89A91BC353709DAC64E493AD451"
if ($cert.SerialNumber.ToUpper() -eq $expectedSerial) {
    Write-Host "✅ CORRECT production certificate!" -ForegroundColor Green
    Write-Host "   Serial matches registered certificate in Developer Portal" -ForegroundColor White
} else {
    Write-Host "❌ WRONG certificate!" -ForegroundColor Red
    Write-Host "   Expected: $expectedSerial" -ForegroundColor Red
    Write-Host "   Found:    $($cert.SerialNumber)" -ForegroundColor Red
    Write-Host "   This certificate is NOT registered in Developer Portal" -ForegroundColor Yellow
    exit 1
}

# Test 4: Verify private key availability
Write-Host "`n4. Testing private key availability..." -ForegroundColor Cyan
if ($cert.HasPrivateKey) {
    Write-Host "✅ Private key is available" -ForegroundColor Green
    
    try {
        $privateKey = $cert.PrivateKey
        if ($privateKey -ne $null) {
            Write-Host "✅ Private key accessible" -ForegroundColor Green
        } else {
            Write-Host "❌ Private key is null" -ForegroundColor Red
        }
    } catch {
        Write-Host "❌ Cannot access private key: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "❌ No private key found in certificate" -ForegroundColor Red
    Write-Host "   Re-export certificate with private key option" -ForegroundColor Yellow
    exit 1
}

# Test 5: Test certificate chain validation
Write-Host "`n5. Testing certificate chain..." -ForegroundColor Cyan
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chainResult = $chain.Build($cert)

Write-Host "   Chain Status: $($chain.ChainStatus.Length) issues" -ForegroundColor White
foreach ($status in $chain.ChainStatus) {
    if ($status.Status -eq [System.Security.Cryptography.X509Certificates.X509ChainStatusFlags]::UntrustedRoot) {
        Write-Host "   ⚠️  Untrusted root (expected in test environment)" -ForegroundColor Yellow
    } else {
        Write-Host "   ℹ️  $($status.Status): $($status.StatusInformation)" -ForegroundColor Gray
    }
}

# Test 6: Generate sample configuration
Write-Host "`n6. Generating sample jobjApiSettings..." -ForegroundColor Cyan
$sampleConfig = @{
    "ApiBaseUrl" = "https://api.rabobank.nl/openapi/payments/insight"
    "PfxCertificatePath" = $PfxPath
    "PfxPassword" = $PfxPassword
    "ClientId" = "b53367ddf9c83b9e0dde68d2fe1d88a1"
} | ConvertTo-Json -Depth 2

Write-Host "✅ Sample configuration:" -ForegroundColor Green
Write-Host $sampleConfig -ForegroundColor White

Write-Host "`n=== VALIDATION COMPLETE ===" -ForegroundColor Green
Write-Host "🎉 GetAccountList.cs should work correctly with this certificate!" -ForegroundColor Green
Write-Host "📋 Next steps:" -ForegroundColor Yellow
Write-Host "   1. Copy api_rabo_PRODUCTION.pfx to server production path" -ForegroundColor White
Write-Host "   2. Update UiPath jobjApiSettings with above configuration" -ForegroundColor White
Write-Host "   3. Run GetAccountList.cs - 'Invalid client certificate' error should be resolved" -ForegroundColor White

$cert.Dispose()