# PowerShell script voor volledige certificate chain extractie
$productionFolder = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\Cert_Premium\Productie"
$pfxPassword = "EPsBGVecfWa9jGb6Bd3B"

Write-Host "Scanning production folder: $productionFolder" -ForegroundColor Yellow

# Find all certificate files in the folder
$pfxFiles = Get-ChildItem -Path $productionFolder -Filter "*.pfx"
$pemFiles = Get-ChildItem -Path $productionFolder -Filter "*.pem"
$cerFiles = Get-ChildItem -Path $productionFolder -Filter "*.cer"

if ($pfxFiles.Count -eq 0) {
    Write-Host "ERROR: No PFX files found in $productionFolder" -ForegroundColor Red
    exit
}

Write-Host "Found certificate files:" -ForegroundColor Green
Write-Host "PFX files:" -ForegroundColor Cyan
foreach ($pfxFile in $pfxFiles) {
    Write-Host "  - $($pfxFile.Name)" -ForegroundColor White
}

if ($pemFiles.Count -gt 0) {
    Write-Host "PEM files:" -ForegroundColor Cyan
    foreach ($pemFile in $pemFiles) {
        Write-Host "  - $($pemFile.Name)" -ForegroundColor White
    }
}

if ($cerFiles.Count -gt 0) {
    Write-Host "CER files:" -ForegroundColor Cyan
    foreach ($cerFile in $cerFiles) {
        Write-Host "  - $($cerFile.Name)" -ForegroundColor White
    }
}

# Use the first PFX file (or api_rabo.pfx if it exists)
$targetPfx = $pfxFiles | Where-Object { $_.Name -eq "api_rabo.pfx" } | Select-Object -First 1
if ($targetPfx -eq $null) {
    $targetPfx = $pfxFiles[0]
}

$pfxPath = $targetPfx.FullName
Write-Host "`nUsing PFX file: $($targetPfx.Name)" -ForegroundColor Yellow

try {
    # Load PFX certificate collection
    $certCollection = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
    $certCollection.Import($pfxPath, $pfxPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)

    Write-Host "`nFound $($certCollection.Count) certificate(s) in PFX" -ForegroundColor Green

    # Find leaf certificate (with private key)
    $leafCert = $null
    foreach ($cert in $certCollection) {
        Write-Host "Certificate: $($cert.Subject)" -ForegroundColor White
        Write-Host "  Serial: $($cert.SerialNumber)" -ForegroundColor Gray
        Write-Host "  Has Private Key: $($cert.HasPrivateKey)" -ForegroundColor Gray
        
        if ($cert.HasPrivateKey) {
            $leafCert = $cert
        }
    }

    if ($leafCert -eq $null) {
        Write-Host "ERROR: No certificate with private key found!" -ForegroundColor Red
        exit
    }

    # Load additional certificates from CER files
    $additionalCerts = @()
    
    foreach ($cerFile in $cerFiles) {
        Write-Host "Loading certificate from: $($cerFile.Name)" -ForegroundColor Cyan
        try {
            $cerCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cerFile.FullName)
            $additionalCerts += $cerCert
            Write-Host "  - Loaded: $($cerCert.Subject)" -ForegroundColor Gray
        } catch {
            Write-Host "  - Error reading $($cerFile.Name): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    # Create comprehensive certificate collection
    $allCerts = @()
    $allCerts += $leafCert
    
    # Add certificates from files (avoid duplicates)
    foreach ($additionalCert in $additionalCerts) {
        $isDuplicate = $false
        foreach ($existingCert in $allCerts) {
            if ($existingCert.Thumbprint -eq $additionalCert.Thumbprint) {
                $isDuplicate = $true
                break
            }
        }
        if (-not $isDuplicate) {
            $allCerts += $additionalCert
            Write-Host "Added external certificate: $($additionalCert.Subject)" -ForegroundColor Green
        }
    }

    # Find specific certificates
    $leafCertificate = $allCerts | Where-Object { $_.Subject -like "*api.rabobank.centerparcs.nl*" } | Select-Object -First 1
    $intermediateCert = $allCerts | Where-Object { $_.Subject -like "*Trust Provider*" } | Select-Object -First 1
    $rootCert = $allCerts | Where-Object { $_.Subject -like "*DigiCert Global Root G2*" } | Select-Object -First 1

    Write-Host "`nIdentified certificates:" -ForegroundColor Yellow
    if ($leafCertificate) { Write-Host "Leaf: $($leafCertificate.Subject)" -ForegroundColor White }
    if ($intermediateCert) { Write-Host "Intermediate: $($intermediateCert.Subject)" -ForegroundColor White }
    if ($rootCert) { Write-Host "Root: $($rootCert.Subject)" -ForegroundColor White }

    # Build final chain in correct order: Root -> Intermediate -> Leaf
    $sortedCerts = @()
    if ($rootCert) { $sortedCerts += $rootCert }
    if ($intermediateCert) { $sortedCerts += $intermediateCert }
    if ($leafCertificate) { $sortedCerts += $leafCertificate }

    Write-Host "`nFinal certificate chain for upload:" -ForegroundColor Green
    for ($i = 0; $i -lt $sortedCerts.Count; $i++) {
        $certType = if ($sortedCerts[$i].Subject -like "*api.rabobank.centerparcs.nl*") { "LEAF" }
                   elseif ($sortedCerts[$i].Subject -like "*Trust Provider*") { "INTERMEDIATE" }
                   elseif ($sortedCerts[$i].Subject -like "*DigiCert*") { "ROOT" }
                   else { "UNKNOWN" }
        Write-Host "  $($i + 1). $certType - $($sortedCerts[$i].Subject)" -ForegroundColor White
    }

    # Create output file
    $outputFile = Join-Path $productionFolder "Cert_Chain.txt"
    $chainContent = @()

    Write-Host "`n" + "="*80 -ForegroundColor Cyan
    Write-Host "COMPLETE CERTIFICATE CHAIN FOR RABOBANK DEVELOPER PORTAL UPLOAD" -ForegroundColor Cyan
    Write-Host "="*80 -ForegroundColor Cyan

    # Export each certificate in the sorted chain
    for ($i = 0; $i -lt $sortedCerts.Count; $i++) {
        $cert = $sortedCerts[$i]
        
        $certBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        $base64Cert = [System.Convert]::ToBase64String($certBytes)

        Write-Host "-----BEGIN CERTIFICATE-----" -ForegroundColor White
        $chainContent += "-----BEGIN CERTIFICATE-----"
        
        for ($j = 0; $j -lt $base64Cert.Length; $j += 64) {
            $length = [Math]::Min(64, $base64Cert.Length - $j)
            $line = $base64Cert.Substring($j, $length)
            Write-Host $line -ForegroundColor White
            $chainContent += $line
        }
        Write-Host "-----END CERTIFICATE-----" -ForegroundColor White
        $chainContent += "-----END CERTIFICATE-----"
    }

    # Write certificate chain to file
    try {
        $chainContent -join "`r`n" | Out-File -FilePath $outputFile -Encoding UTF8 -Force
        Write-Host "`n✅ Certificate chain saved to: $outputFile" -ForegroundColor Green
        Write-Host "📋 File ready for upload to Rabobank Developer Portal!" -ForegroundColor Green
    } catch {
        Write-Host "`nError saving certificate chain: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host "`n" + "="*80 -ForegroundColor Cyan
    Write-Host "UPLOAD INSTRUCTIONS:" -ForegroundColor Yellow
    Write-Host "1. Open Cert_Chain.txt file" -ForegroundColor White
    Write-Host "2. Copy ALL content (Ctrl+A, Ctrl+C)" -ForegroundColor White
    Write-Host "3. Login to Rabobank Developer Portal" -ForegroundColor White
    Write-Host "4. Navigate to Certificate Management" -ForegroundColor White
    Write-Host "5. Delete old certificate" -ForegroundColor White
    Write-Host "6. Upload the complete chain" -ForegroundColor White
    Write-Host "7. Verify certificate serial: $($leafCertificate.SerialNumber)" -ForegroundColor White
    Write-Host "="*80 -ForegroundColor Cyan

} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nPress any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")