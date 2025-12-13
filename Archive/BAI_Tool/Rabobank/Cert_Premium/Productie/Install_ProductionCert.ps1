# Script om het productie certificaat correct te installeren
# Dit koppelt het uitgegeven certificaat aan de bestaande private key

Write-Host "=== Installeer Productie Certificaat ===" -ForegroundColor Green

# Zoek naar .cer bestanden in de huidige map
$cerFiles = Get-ChildItem -Path . -Filter "*.cer"

Write-Host "Gevonden .cer bestanden:" -ForegroundColor Cyan
foreach ($cerFile in $cerFiles) {
    Write-Host "  - $($cerFile.Name)" -ForegroundColor White
    
    # Controleer de serial number van elk .cer bestand
    try {
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cerFile.FullName)
        Write-Host "    Serial: $($cert.SerialNumber)" -ForegroundColor Gray
        Write-Host "    Subject: $($cert.Subject)" -ForegroundColor Gray
        Write-Host "    Issuer: $($cert.Issuer)" -ForegroundColor Gray
        
        # Check of dit het productie certificaat is
        if ($cert.SerialNumber -eq "044CB89A91BC353709DAC64E493AD451") {
            Write-Host "    ✓ DIT IS HET PRODUCTIE CERTIFICAAT!" -ForegroundColor Green
            $productionCert = $cerFile.FullName
        }
        Write-Host ""
    }
    catch {
        Write-Host "    ERROR: Kan certificaat niet lezen: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Als we het productie certificaat hebben gevonden, installeer het
if ($productionCert) {
    Write-Host "Installeren van productie certificaat..." -ForegroundColor Yellow
    Write-Host "Bestand: $productionCert" -ForegroundColor White
    
    try {
        # Gebruik certreq om het certificaat te koppelen aan de private key
        $result = & certreq -accept $productionCert
        Write-Host "certreq resultaat: $result" -ForegroundColor Gray
        
        Write-Host "✓ Certificaat geïnstalleerd!" -ForegroundColor Green
        Write-Host "Nu kun je het exporteren vanuit Certificate Manager met de private key." -ForegroundColor White
        
        # Controleer of het nu in de store staat
        Write-Host "`nControleren Certificate Store..." -ForegroundColor Cyan
        $installedCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.SerialNumber -eq "044CB89A91BC353709DAC64E493AD451" }
        
        if ($installedCert) {
            Write-Host "✓ Certificaat gevonden in Personal Store:" -ForegroundColor Green
            Write-Host "  Subject: $($installedCert.Subject)" -ForegroundColor White
            Write-Host "  Serial: $($installedCert.SerialNumber)" -ForegroundColor White
            Write-Host "  Has Private Key: $($installedCert.HasPrivateKey)" -ForegroundColor White
            
            if ($installedCert.HasPrivateKey) {
                Write-Host "`n🎉 SUCCESS! Je kunt nu het certificaat exporteren naar PFX!" -ForegroundColor Green
            } else {
                Write-Host "`n⚠️  Certificaat geïnstalleerd maar geen private key gekoppeld" -ForegroundColor Yellow
            }
        } else {
            Write-Host "❌ Certificaat niet gevonden in Personal Store" -ForegroundColor Red
        }
        
    }
    catch {
        Write-Host "ERROR bij installeren: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "Geen productie certificaat (.cer) gevonden met Serial: 044CB89A91BC353709DAC64E493AD451" -ForegroundColor Red
    Write-Host "Zoek naar het .cer bestand dat je van Networking4all/Rabobank hebt ontvangen." -ForegroundColor Yellow
}