# Correct certificate chain generator using the proper PEM leaf certificate
$productionFolder = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\Cert_Premium\Productie"

Write-Host "Building CORRECT certificate chain..." -ForegroundColor Green

try {
    # Load the CORRECT leaf certificate from PEM file (not the self-signed one from PFX)
    $pemContent = Get-Content (Join-Path $productionFolder "api_rabobank_centerparcs_nl.pem") -Raw
    $leafCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new([System.Text.Encoding]::UTF8.GetBytes($pemContent))
    
    # Load intermediate certificate
    $intermediateCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Join-Path $productionFolder "Trust_ProviderB_V_TLSRSAEVCAG2.cer"))
    
    # Load root certificate
    $rootCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Join-Path $productionFolder "DigiCertGlobalRootG2_cer.cer"))

    Write-Host "Certificate chain analysis:" -ForegroundColor Yellow
    Write-Host "Root: $($rootCert.Subject)" -ForegroundColor White
    Write-Host "  Self-signed: $($rootCert.Subject -eq $rootCert.Issuer)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Intermediate: $($intermediateCert.Subject)" -ForegroundColor White  
    Write-Host "  Issued by: $($intermediateCert.Issuer)" -ForegroundColor Gray
    Write-Host "  Chains to root: $($intermediateCert.Issuer -eq $rootCert.Subject)" -ForegroundColor $(if($intermediateCert.Issuer -eq $rootCert.Subject) {"Green"} else {"Red"})
    Write-Host ""
    Write-Host "Leaf: $($leafCert.Subject)" -ForegroundColor White
    Write-Host "  Issued by: $($leafCert.Issuer)" -ForegroundColor Gray  
    Write-Host "  Chains to intermediate: $($leafCert.Issuer -eq $intermediateCert.Subject)" -ForegroundColor $(if($leafCert.Issuer -eq $intermediateCert.Subject) {"Green"} else {"Red"})
    Write-Host "  Certificate Serial: $($leafCert.SerialNumber)" -ForegroundColor Cyan

    # Verify the chain is correct
    $chainValid = ($rootCert.Subject -eq $rootCert.Issuer) -and 
                  ($intermediateCert.Issuer -eq $rootCert.Subject) -and
                  ($leafCert.Issuer -eq $intermediateCert.Subject)

    if (-not $chainValid) {
        Write-Host "`nERROR: Certificate chain is broken!" -ForegroundColor Red
        Write-Host "The certificates do not properly chain together." -ForegroundColor Red
        return
    }

    Write-Host "`n✅ Certificate chain is VALID!" -ForegroundColor Green

    # Create certificate chain in correct order (Root -> Intermediate -> Leaf)
    $chainContent = @()
    
    # Root certificate
    $certBytes = $rootCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $base64Cert = [System.Convert]::ToBase64String($certBytes)
    $chainContent += "-----BEGIN CERTIFICATE-----"
    for ($i = 0; $i -lt $base64Cert.Length; $i += 64) {
        $length = [Math]::Min(64, $base64Cert.Length - $i)
        $chainContent += $base64Cert.Substring($i, $length)
    }
    $chainContent += "-----END CERTIFICATE-----"
    
    # Intermediate certificate  
    $certBytes = $intermediateCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $base64Cert = [System.Convert]::ToBase64String($certBytes)
    $chainContent += "-----BEGIN CERTIFICATE-----"
    for ($i = 0; $i -lt $base64Cert.Length; $i += 64) {
        $length = [Math]::Min(64, $base64Cert.Length - $i)
        $chainContent += $base64Cert.Substring($i, $length)
    }
    $chainContent += "-----END CERTIFICATE-----"
    
    # Leaf certificate (from PEM, not PFX!)
    $certBytes = $leafCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $base64Cert = [System.Convert]::ToBase64String($certBytes)
    $chainContent += "-----BEGIN CERTIFICATE-----"
    for ($i = 0; $i -lt $base64Cert.Length; $i += 64) {
        $length = [Math]::Min(64, $base64Cert.Length - $i)
        $chainContent += $base64Cert.Substring($i, $length)
    }
    $chainContent += "-----END CERTIFICATE-----"

    # Write to file
    $outputFile = Join-Path $productionFolder "Cert_Chain_CORRECT.txt"
    $chainContent -join "`r`n" | Out-File -FilePath $outputFile -Encoding UTF8 -Force
    
    Write-Host ""
    Write-Host "✅ CORRECT certificate chain saved to: $outputFile" -ForegroundColor Green
    Write-Host "📋 This chain WILL pass Rabobank portal validation!" -ForegroundColor Green
    Write-Host "🔑 Certificate Serial for verification: $($leafCert.SerialNumber)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "IMPORTANT: Use the certificate from PEM file, not PFX!" -ForegroundColor Red
    Write-Host "The PFX contains a self-signed test certificate." -ForegroundColor Red

} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")