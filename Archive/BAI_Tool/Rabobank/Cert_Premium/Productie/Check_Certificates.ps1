# Check wat voor certificaten we hebben en of ze hetzelfde zijn

Write-Host "=== CHECKING CERTIFICATES ===" -ForegroundColor Green
Write-Host ""

# Check PEM certificate
Write-Host "1. PEM Certificate Details:" -ForegroundColor Yellow
if (Test-Path "api_rabobank_centerparcs_nl.pem") {
    try {
        $pemInfo = certutil -dump "api_rabobank_centerparcs_nl.pem" | Select-String "Serial Number|Subject:"
        $pemInfo | ForEach-Object { Write-Host "   $_" }
    } catch {
        Write-Host "   Error reading PEM: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "   PEM file not found" -ForegroundColor Red
}

Write-Host ""

# Check if we can read PFX without password (unlikely but worth checking)
Write-Host "2. PFX Certificate Details (attempting without password):" -ForegroundColor Yellow
if (Test-Path "api_rabo.pfx") {
    try {
        # Try without password first
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2("api_rabo.pfx", "", "DefaultKeySet")
        Write-Host "   Serial Number: $($cert.SerialNumber)" -ForegroundColor Green
        Write-Host "   Subject: $($cert.Subject)" -ForegroundColor Green
        Write-Host "   Has Private Key: $($cert.HasPrivateKey)" -ForegroundColor Green
        $cert.Dispose()
    } catch {
        Write-Host "   PFX requires password (expected)" -ForegroundColor Cyan
    }
} else {
    Write-Host "   PFX file not found" -ForegroundColor Red
}

Write-Host ""

# Check available files
Write-Host "3. Available Certificate Files:" -ForegroundColor Yellow
Get-ChildItem -Filter "*cert*" | Select-Object Name, Length | ForEach-Object {
    Write-Host "   $($_.Name) ($($_.Length) bytes)"
}

Get-ChildItem -Filter "*.pem" | Select-Object Name, Length | ForEach-Object {
    Write-Host "   $($_.Name) ($($_.Length) bytes)"
}

Get-ChildItem -Filter "*.pfx" | Select-Object Name, Length | ForEach-Object {
    Write-Host "   $($_.Name) ($($_.Length) bytes)"
}

Get-ChildItem -Filter "*.key" | Select-Object Name, Length | ForEach-Object {
    Write-Host "   $($_.Name) ($($_.Length) bytes)"
}

Write-Host ""
Write-Host "=== ANALYSIS ===" -ForegroundColor Green
Write-Host "We need to determine:"
Write-Host "1. Does the PFX contain the same certificate as the PEM? (Serial: 044cb89a...)"
Write-Host "2. Is there a separate private key file for the PEM?"
Write-Host "3. What's the password for the PFX (from KeePass)?"
Write-Host ""
Write-Host "Next step: Check if there's a private key file in parent directory..."