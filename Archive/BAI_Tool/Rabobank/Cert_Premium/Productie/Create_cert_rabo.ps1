# PowerShell script voor volledige certificate chain extractie
$productionFolder = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\Cert_Premium\Productie"
$pfxPassword = "EPsBGVecfWa9jGb6Bd3B"

Write-Host "Scanning production folder: $productionFolder" -ForegroundColor Yellow

# Find all certificate files in the folder
$pfxFiles = Get-ChildItem -Path $productionFolder -Filter "*.pfx"
$pemFiles = Get-ChildItem -Path $productionFolder -Filter "*.pem"
$crtFiles = Get-ChildItem -Path $productionFolder -Filter "*.crt"
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

if ($crtFiles.Count -gt 0) {
    Write-Host "CRT files:" -ForegroundColor Cyan
    foreach ($crtFile in $crtFiles) {
        Write-Host "  - $($crtFile.Name)" -ForegroundColor White
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

    # Build certificate chain with extended validation
    $chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
    $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
    $chain.ChainPolicy.VerificationFlags = [System.Security.Cryptography.X509Certificates.X509VerificationFlags]::AllowUnknownCertificateAuthority
    $buildResult = $chain.Build($leafCert)

    Write-Host "`nChain build result: $buildResult" -ForegroundColor $(if($buildResult) { "Green" } else { "Yellow" })
    Write-Host "Chain elements from PFX: $($chain.ChainElements.Count)" -ForegroundColor Green

    # Load additional certificates from PEM/CRT files in the folder
    $additionalCerts = @()
    
    # Load PEM files
    foreach ($pemFile in $pemFiles) {
        Write-Host "Loading additional certificates from: $($pemFile.Name)" -ForegroundColor Cyan
        try {
            $pemContent = Get-Content $pemFile.FullName -Raw
            
            # Split multiple certificates in one PEM file
            $certBlocks = $pemContent -split "-----END CERTIFICATE-----" | Where-Object { $_.Trim() -ne "" }
            
            foreach ($certBlock in $certBlocks) {
                if ($certBlock.Contains("-----BEGIN CERTIFICATE-----")) {
                    $cleanBlock = $certBlock + "`r`n-----END CERTIFICATE-----"
                    try {
                        $pemCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new([System.Text.Encoding]::UTF8.GetBytes($cleanBlock))
                        $additionalCerts += $pemCert
                        Write-Host "  - Loaded: $($pemCert.Subject)" -ForegroundColor Gray
                    } catch {
                        Write-Host "  - Failed to parse certificate block in $($pemFile.Name)" -ForegroundColor Yellow
                    }
                }
            }
        } catch {
            Write-Host "  - Error reading $($pemFile.Name): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    # Load CRT files  
    foreach ($crtFile in $crtFiles) {
        Write-Host "Loading certificate from: $($crtFile.Name)" -ForegroundColor Cyan
        try {
            $crtCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($crtFile.FullName)
            $additionalCerts += $crtCert
            Write-Host "  - Loaded: $($crtCert.Subject)" -ForegroundColor Gray
        } catch {
            Write-Host "  - Error reading $($crtFile.Name): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    # Load CER files  
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
    
    # Add certificates from chain
    for ($i = 0; $i -lt $chain.ChainElements.Count; $i++) {
        $allCerts += $chain.ChainElements[$i].Certificate
    }
    
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

    # Sort certificates by chain order using our identified mapping
    $sortedCerts = @()
    
    # Find specific certificates based on our analysis:
    # Root: DigiCert Global Root G2
    # Intermediate: Trust Provider B.V. TLS RSA EV CA G2  
    # Leaf: api.rabobank.centerparcs.nl
    
    # Find each certificate specifically by subject pattern
    $leafCert = $allCerts | Where-Object { $_.Subject -like "*api.rabobank.centerparcs.nl*" } | Select-Object -First 1
    $intermediateCert = $allCerts | Where-Object { $_.Subject -like "*Trust Provider*" } | Select-Object -First 1
    $rootCert = $allCerts | Where-Object { $_.Subject -like "*DigiCert Global Root G2*" } | Select-Object -First 1

    Write-Host "`nIdentified certificates:" -ForegroundColor Yellow
    if ($leafCert) { Write-Host "Leaf: $($leafCert.Subject)" -ForegroundColor White }
    if ($intermediateCert) { Write-Host "Intermediate: $($intermediateCert.Subject)" -ForegroundColor White }
    if ($rootCert) { Write-Host "Root: $($rootCert.Subject)" -ForegroundColor White }

    # Build final chain in correct order: Root -> Intermediate -> Leaf
    # (This is the format Rabobank Developer Portal expects)
    if ($rootCert) { $sortedCerts += $rootCert }
    if ($intermediateCert) { $sortedCerts += $intermediateCert }
    if ($leafCert) { $sortedCerts += $leafCert }

    Write-Host "`nFinal certificate chain for upload:" -ForegroundColor Green
    for ($i = 0; $i -lt $sortedCerts.Count; $i++) {
        $certType = if ($sortedCerts[$i].Subject -like "*api.rabobank.centerparcs.nl*" -or $sortedCerts[$i].HasPrivateKey) { "LEAF" }
                   elseif ($sortedCerts[$i].Subject -like "*Trust Provider*") { "INTERMEDIATE" }
                   elseif ($sortedCerts[$i].Subject -eq $sortedCerts[$i].Issuer -or $sortedCerts[$i].Subject -like "*DigiCert*") { "ROOT" }
                   else { "UNKNOWN" }
        Write-Host "  $($i + 1). $certType - $($sortedCerts[$i].Subject)" -ForegroundColor White
    }

    # Create output file path
    $outputFile = Join-Path $productionFolder "Cert_Chain.txt"
    
    Write-Host "`n" + "="*80 -ForegroundColor Cyan
    Write-Host "COMPLETE CERTIFICATE CHAIN FOR RABOBANK DEVELOPER PORTAL UPLOAD" -ForegroundColor Cyan
    Write-Host "Copy ALL text below (Root -> Intermediate -> Leaf order):" -ForegroundColor Yellow
    Write-Host "="*80 -ForegroundColor Cyan

    # Create certificate chain content
    $chainContent = @()

    # Export each certificate in the sorted chain
    for ($i = 0; $i -lt $sortedCerts.Count; $i++) {
        $cert = $sortedCerts[$i]
        
        # Determine certificate type
        $certType = if ($cert.Subject -eq $cert.Issuer) { "ROOT" }
                   elseif ($cert.HasPrivateKey) { "LEAF" }
                   else { "INTERMEDIATE" }
        
        Write-Host ""
        Write-Host "# $certType CERTIFICATE" -ForegroundColor Green
        Write-Host "# Subject: $($cert.Subject)" -ForegroundColor Gray
        Write-Host "# Serial: $($cert.SerialNumber)" -ForegroundColor Gray
        
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
        Write-Host "`n❌ Error saving certificate chain: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host "`n" + "="*80 -ForegroundColor Cyan
    Write-Host "UPLOAD INSTRUCTIONS FOR RABOBANK DEVELOPER PORTAL:" -ForegroundColor Yellow
    Write-Host "1. Copy ALL certificate text above (from first -----BEGIN to last -----END)" -ForegroundColor White
    Write-Host "2. Login to Rabobank Developer Portal" -ForegroundColor White
    Write-Host "3. Navigate to your app's Certificate Management" -ForegroundColor White
    Write-Host "4. Delete the old certificate (Serial: 044CB89A91BC35370...)" -ForegroundColor White
    Write-Host "5. Upload the COMPLETE chain above as a single text block" -ForegroundColor White
    Write-Host "6. Verify new certificate serial matches: $($leafCert.SerialNumber)" -ForegroundColor White
    Write-Host "7. Total certificates in chain: $($sortedCerts.Count)" -ForegroundColor White
    Write-Host "8. Order is: Root -> Intermediate -> Leaf (as uploaded above)" -ForegroundColor White
    Write-Host "="*80 -ForegroundColor Cyan

    # Also update your GetAccountList.cs config path
    Write-Host "`nUPDATE YOUR CONFIG:" -ForegroundColor Yellow
    Write-Host "Make sure your GetAccountList.cs uses this path:" -ForegroundColor White
    Write-Host "PfxCertificatePath: $pfxPath" -ForegroundColor Cyan

} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Red
}

Write-Host "`nPress any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")