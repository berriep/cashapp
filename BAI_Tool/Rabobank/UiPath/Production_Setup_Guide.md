# GetAccountList.cs - Production Setup Guide

## Issue Resolution Summary

**Problem:** "Invalid client certificate" error when calling Rabobank BAI API  
**Root Cause:** Certificate mismatch between code and Developer Portal registration  
**Solution:** Use production PFX certificate with proper Developer Portal registration

## Certificate Details

### Production Certificate (CORRECT - Use This)
- **File:** `C:\Users\uipath\BAI\Certificate Production\api_rabo.pfx`
- **Serial Number:** `044CB89A91BC353709DAC64E493AD451` (hex)
- **Subject:** `CN=api.rabobank.centerparcs.nl`
- **Issuer:** `Trust Provider B.V. TLS RSA EV CA G2`
- **Password:** Stored in KeePass under entry name `"Rabo_cert pfx"`

### Test Certificate (WRONG - Do Not Use)
- **File:** `C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\Cert_Premium\certificate.pem`
- **Serial Number:** `6C299B3257B6B5A54612686AEC65CD05` (hex)
- **Subject:** Self-signed test certificate
- **Issue:** Not registered in Rabobank Developer Portal

## Production Configuration

### jobjApiSettings JSON Structure:
```json
{
  "ApiBaseUrl": "https://api.rabobank.nl/openapi/payments/insight",
  "PfxCertificatePath": "C:\\Users\\uipath\\BAI\\Certificate Production\\api_rabo.pfx",
  "PfxPassword": "GET_FROM_KEEPASS",
  "ClientId": "b53367ddf9c83b9e0dde68d2fe1d88a1"
}
```

### Steps to Deploy:

1. **Get PFX Password:**
   - Open KeePass
   - Find entry named `"Rabo_cert pfx"`
   - Copy the password

2. **Configure UiPath:**
   - Update `jobjApiSettings` with production PFX path
   - Add the KeePass password to `PfxPassword` field
   - Ensure certificate file exists on target servers

3. **Register Certificate Chain (CRITICAL):**
   - Upload `Cert_Chain_CORRECT.txt` to Rabobank Developer Portal
   - Chain content: Root → Intermediate → Production Certificate
   - Must match the certificate Serial: `044CB89A91BC353709DAC64E493AD451`

4. **Test API Call:**
   - Run GetAccountList.cs with updated configuration
   - Verify certificate loads successfully (HasPrivateKey = true)
   - Check API response for success

## Certificate Chain for Developer Portal

Upload this exact chain to Developer Portal:

**File:** `C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\Cert_Premium\Productie\Cert_Chain_CORRECT.txt`

**Chain Structure:**
```
Root Certificate: DigiCert Global Root G2
    ↓
Intermediate Certificate: Trust Provider B.V. TLS RSA EV CA G2  
    ↓
Production Certificate: api.rabobank.centerparcs.nl (Serial: 044CB89A...)
```

## Common Issues and Solutions

### "PFX certificate does not contain a private key"
- **Cause:** Wrong certificate file or incorrect password
- **Solution:** Use production PFX with KeePass password

### "Invalid client certificate"
- **Cause:** Certificate not registered in Developer Portal
- **Solution:** Upload correct certificate chain to Developer Portal

### "Failed to load PFX certificate"
- **Cause:** Incorrect password or corrupted PFX file
- **Solution:** Verify KeePass password, re-export PFX if needed

## Server Deployment

### Target Servers:
- **eucceisapp51** (Certificate source)
- **eucceisapp50** (Production target)

### Deployment Process:
1. Export PFX from eucceisapp51 certificate store
2. Securely transfer to eucceisapp50
3. Import to certificate store with KeePass password
4. Update UiPath configuration with correct paths
5. Test API connectivity

## Security Notes

- Never store PFX password in plain text
- Always use KeePass for password management
- Clean up temporary PFX files after installation
- Use proper Windows Certificate Store for production