# Extract Private Key from PFX for Production Certificate
# This script extracts the private key from the PFX file to create a proper PEM setup

param(
    [string]$PfxPath = "api_rabo.pfx",
    [string]$OutputKeyFile = "api_rabobank_centerparcs_nl.key",
    [string]$OutputCertFile = "api_rabobank_centerparcs_nl_cert.pem"
)

Write-Host "=== Extract Private Key from Production PFX ===" -ForegroundColor Green

# Use provided password (hardcoded for extraction)
Write-Host "Using provided PFX password..." -ForegroundColor Yellow
$password = "EPsBGVecfWa9jGb6Bd3B"
$securePassword = ConvertTo-SecureString -String $password -AsPlainText -Force

try {
    # Load PFX certificate
    Write-Host "Loading PFX certificate from: $PfxPath" -ForegroundColor Cyan
    $fullPath = Resolve-Path $PfxPath
    Write-Host "Full path: $fullPath" -ForegroundColor Gray
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($fullPath, $securePassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
    
    Write-Host "Certificate loaded successfully:" -ForegroundColor Green
    Write-Host "  Subject: $($cert.Subject)" -ForegroundColor White
    Write-Host "  Serial: $($cert.SerialNumber)" -ForegroundColor White
    Write-Host "  Has Private Key: $($cert.HasPrivateKey)" -ForegroundColor White
    
    if (-not $cert.HasPrivateKey) {
        throw "Certificate does not contain a private key"
    }
    
    # Export certificate as PEM
    Write-Host "Exporting certificate to PEM format..." -ForegroundColor Cyan
    $certBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $certBase64 = [System.Convert]::ToBase64String($certBytes)
    $certPem = "-----BEGIN CERTIFICATE-----`n"
    
    for ($i = 0; $i -lt $certBase64.Length; $i += 64) {
        $line = $certBase64.Substring($i, [Math]::Min(64, $certBase64.Length - $i))
        $certPem += "$line`n"
    }
    $certPem += "-----END CERTIFICATE-----"
    
    # Save certificate PEM
    $certPem | Out-File -FilePath $OutputCertFile -Encoding ASCII -NoNewline
    Write-Host "Certificate saved to: $OutputCertFile" -ForegroundColor Green
    
    # Export private key
    Write-Host "Exporting private key..." -ForegroundColor Cyan
    
    # Get the private key
    $privateKey = $cert.PrivateKey
    if ($privateKey -eq $null) {
        throw "Could not access private key"
    }
    
    # Export private key - try different methods for compatibility
    $keyPem = ""
    try {
        # Try modern method first (PKCS#8)
        $keyBytes = $privateKey.ExportPkcs8PrivateKey()
        $keyBase64 = [System.Convert]::ToBase64String($keyBytes)
        $keyPem = "-----BEGIN PRIVATE KEY-----`n"
        
        for ($i = 0; $i -lt $keyBase64.Length; $i += 64) {
            $line = $keyBase64.Substring($i, [Math]::Min(64, $keyBase64.Length - $i))
            $keyPem += "$line`n"
        }
        $keyPem += "-----END PRIVATE KEY-----"
        Write-Host "Exported using PKCS#8 format" -ForegroundColor Green
    }
    catch {
        Write-Host "PKCS#8 export failed, trying legacy method..." -ForegroundColor Yellow
        # Fallback to legacy RSA format
        $keyBytes = $privateKey.ExportRSAPrivateKey()
        $keyBase64 = [System.Convert]::ToBase64String($keyBytes)
        $keyPem = "-----BEGIN RSA PRIVATE KEY-----`n"
        
        for ($i = 0; $i -lt $keyBase64.Length; $i += 64) {
            $line = $keyBase64.Substring($i, [Math]::Min(64, $keyBase64.Length - $i))
            $keyPem += "$line`n"
        }
        $keyPem += "-----END RSA PRIVATE KEY-----"
        Write-Host "Exported using RSA format" -ForegroundColor Green
    }
    
    # Save private key PEM
    $keyPem | Out-File -FilePath $OutputKeyFile -Encoding ASCII -NoNewline
    Write-Host "Private key saved to: $OutputKeyFile" -ForegroundColor Green
    
    Write-Host "`n=== SUCCESS ===" -ForegroundColor Green
    Write-Host "Now you can use PEM format with separate files:" -ForegroundColor White
    Write-Host "  Certificate: $OutputCertFile" -ForegroundColor White
    Write-Host "  Private Key: $OutputKeyFile" -ForegroundColor White
    
    Write-Host "`nUpdate GetAccountList.cs jobjApiSettings:" -ForegroundColor Yellow
    Write-Host '{' -ForegroundColor White
    Write-Host '  "CertificatePath": "' + (Resolve-Path $OutputCertFile).Path.Replace('\', '\\') + '",' -ForegroundColor White
    Write-Host '  "PrivateKeyPath": "' + (Resolve-Path $OutputKeyFile).Path.Replace('\', '\\') + '"' -ForegroundColor White
    Write-Host '}' -ForegroundColor White
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if ($cert) {
        $cert.Dispose()
    }
}