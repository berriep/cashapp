# Update Certificate Chain met het juiste productie certificaat
# We moeten het productie certificaat toevoegen aan de chain

Write-Host "=== Update Certificate Chain ===" -ForegroundColor Green

# Lees het huidige PEM certificaat (dit is het juiste productie certificaat)
$productionCertPath = "api_rabobank_centerparcs_nl.pem"
if (Test-Path $productionCertPath) {
    Write-Host "Productie certificaat gevonden: $productionCertPath" -ForegroundColor Green
    
    # Controleer serial number
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($productionCertPath)
    Write-Host "Serial Number: $($cert.SerialNumber)" -ForegroundColor White
    
    if ($cert.SerialNumber -eq "044CB89A91BC353709DAC64E493AD451") {
        Write-Host "✓ JUIST productie certificaat!" -ForegroundColor Green
        
        # Lees het productie certificaat PEM
        $prodCertPem = Get-Content $productionCertPath -Raw
        
        # Lees de huidige chain (Root + Intermediate)
        $currentChain = Get-Content "Cert_Chain_CORRECT.txt" -Raw
        
        # Maak nieuwe chain: Root + Intermediate + Production Certificate
        $newChain = $currentChain + "`n" + $prodCertPem
        
        # Sla op als nieuwe chain
        $newChain | Out-File -FilePath "Cert_Chain_COMPLETE.txt" -Encoding ASCII -NoNewline
        
        Write-Host "✓ Nieuwe complete chain gemaakt: Cert_Chain_COMPLETE.txt" -ForegroundColor Green
        Write-Host "Chain bevat nu:" -ForegroundColor White
        Write-Host "  1. DigiCert Global Root G2 (Root)" -ForegroundColor Gray
        Write-Host "  2. Trust Provider B.V. TLS RSA EV CA G2 (Intermediate)" -ForegroundColor Gray
        Write-Host "  3. api.rabobank.centerparcs.nl (Production Leaf)" -ForegroundColor Gray
        Write-Host "     Serial: 044CB89A91BC353709DAC64E493AD451" -ForegroundColor Gray
        
    } else {
        Write-Host "❌ VERKEERD certificaat in PEM bestand!" -ForegroundColor Red
        Write-Host "Expected: 044CB89A91BC353709DAC64E493AD451" -ForegroundColor Red
        Write-Host "Found:    $($cert.SerialNumber)" -ForegroundColor Red
    }
} else {
    Write-Host "❌ Productie certificaat PEM niet gevonden: $productionCertPath" -ForegroundColor Red
}