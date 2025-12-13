# Certificate chain generator for Rabobank Developer Portal
$productionFolder = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\Cert_Premium\Productie"
$pfxPassword = "EPsBGVecfWa9jGb6Bd3B"

Write-Host "Creating certificate chain..." -ForegroundColor Green

try {
    # Load PFX file
    $pfxPath = Join-Path $productionFolder "api_rabo.pfx"
    $certCollection = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
    $certCollection.Import($pfxPath, $pfxPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)

    # Load additional certificates
    $cerFiles = Get-ChildItem -Path $productionFolder -Filter "*.cer"
    $allCerts = @($certCollection[0])  # Leaf certificate
    
    foreach ($cerFile in $cerFiles) {
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cerFile.FullName)
        $allCerts += $cert
    }

    # Find specific certificates
    $leafCert = $allCerts | Where-Object { $_.Subject -like "*api.rabobank.centerparcs.nl*" } | Select-Object -First 1
    $intermediateCert = $allCerts | Where-Object { $_.Subject -like "*Trust Provider*" } | Select-Object -First 1  
    $rootCert = $allCerts | Where-Object { $_.Subject -like "*DigiCert Global Root G2*" } | Select-Object -First 1

    Write-Host "Found certificates:"
    Write-Host "- Leaf: $($leafCert.Subject)"
    Write-Host "- Intermediate: $($intermediateCert.Subject)"  
    Write-Host "- Root: $($rootCert.Subject)"

    # Create certificate chain in correct order (Root -> Intermediate -> Leaf)
    $chainContent = @()
    
    # Root certificate
    if ($rootCert) {
        $certBytes = $rootCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        $base64Cert = [System.Convert]::ToBase64String($certBytes)
        $chainContent += "-----BEGIN CERTIFICATE-----"
        for ($i = 0; $i -lt $base64Cert.Length; $i += 64) {
            $length = [Math]::Min(64, $base64Cert.Length - $i)
            $chainContent += $base64Cert.Substring($i, $length)
        }
        $chainContent += "-----END CERTIFICATE-----"
    }
    
    # Intermediate certificate
    if ($intermediateCert) {
        $certBytes = $intermediateCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        $base64Cert = [System.Convert]::ToBase64String($certBytes)
        $chainContent += "-----BEGIN CERTIFICATE-----"
        for ($i = 0; $i -lt $base64Cert.Length; $i += 64) {
            $length = [Math]::Min(64, $base64Cert.Length - $i)
            $chainContent += $base64Cert.Substring($i, $length)
        }
        $chainContent += "-----END CERTIFICATE-----"
    }
    
    # Leaf certificate
    if ($leafCert) {
        $certBytes = $leafCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        $base64Cert = [System.Convert]::ToBase64String($certBytes)
        $chainContent += "-----BEGIN CERTIFICATE-----"
        for ($i = 0; $i -lt $base64Cert.Length; $i += 64) {
            $length = [Math]::Min(64, $base64Cert.Length - $i)
            $chainContent += $base64Cert.Substring($i, $length)
        }
        $chainContent += "-----END CERTIFICATE-----"
    }

    # Write to file
    $outputFile = Join-Path $productionFolder "Cert_Chain.txt"
    $chainContent -join "`r`n" | Out-File -FilePath $outputFile -Encoding UTF8 -Force
    
    Write-Host ""
    Write-Host "Certificate chain saved to: $outputFile" -ForegroundColor Green
    Write-Host "Ready for upload to Rabobank Developer Portal!" -ForegroundColor Green
    Write-Host "Certificate serial: $($leafCert.SerialNumber)" -ForegroundColor Yellow

} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")