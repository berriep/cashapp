# Rabobank BAI API Tool - Setup & Testing Guide

## ✅ Sandbox Test Configuration Complete

De sandbox client configuratie is opgezet met de gegevens uit de Archive/Bank API folder:

### 📁 What's Been Set Up

1. **Certificaten gekopieerd**: 
   - `Cert_Premium/certificate.pem`
   - `Cert_Premium/private.key`

2. **Sandbox configuratie** (`config/clients/default.json`):
   - Client ID: `50db03679d4c3297574c26b6aab1894e`
   - Client Secret: `f0f9927a93943253218861d661e8f71e`
   - Token URL: `https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/token`
   - API Base URL: `https://api-sandbox.rabobank.nl/openapi/sandbox/payments/insight`

3. **Account mappings**:
   - `NL52RABO0125618484` → `Wp-xhZMGEWRIIgVjPwTC1aKJJ0VCRZ_4bScUVXof7e0`
   - `NL80RABO1127000002` → `hBS4MQ0Oc4LLJRiUiE_R94_-zoU4B2vwnPkXmsGh_bA`

4. **Bestaande tokens gekopieerd** voor testing

## 🚀 Prerequisites

### 1. Install .NET 8.0 SDK
```powershell
# Download en installeer .NET 8.0 SDK van:
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verificatie na installatie:
dotnet --version
```

### 2. Verify Certificate Files
```powershell
# Check dat de certificaten aanwezig zijn:
Get-ChildItem "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\Cert_Premium\"
```

## 🧪 Testing Steps

### 1. Build Project
```powershell
cd "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank"
dotnet restore
dotnet build
```

### 2. Run Initial Test
```powershell
dotnet run
```

**Expected Output:**
```
Starting Rabobank BAI API Tool...
Loaded configuration for client: sandbox-test
Token management successful
Access token length: 574
Operation type: NoAction
Application completed
```

### 3. Test Token Refresh
```powershell
# Als de tokens expired zijn, test de refresh functionality
dotnet run
```

### 4. Test met Authorization Code (indien nodig)
Als de tokens expired zijn en refresh niet werkt, kun je een nieuwe authorization code gebruiken:

1. **Kopieer de auth code** uit Archive:
   ```
   AAPdN1eL1JC5YEvoq8J2MCuBrYN4FKinohtKHnvDFuzRz5YyT8i7dFKsTwRyX79zVvKWJEaW59uGWsGS81Ra-CyMkEcnVreBGQJzyaeUWeR-2_vXVmdhZUjA9WlUONwSL-dlCCV3ED78MBoa86ojfuLiIbKwTPaQc-KV3WxTbP-i0rlXiKMpPido5v2So7jzaZnz_aUzBofKB1CiQJk1rG1kk5bZWDAXI7q9wcORLL2SzaCyYMVOXaTA-_BsgVk7riooKqgxkSsZud1UP2ib2uTA7mva-JPmWXUdz0ZwdYovhQ
   ```

2. **Update Program.cs** tijdelijk voor auth code exchange:
   ```csharp
   // In Program.cs Main method, voeg toe:
   string authCode = "your-auth-code-here";
   var exchangeResult = await tokenManager.ExchangeAuthorizationCodeAsync(clientConfig, authCode);
   ```

## 🔧 Configuration Details

### Sandbox Client Config (`config/clients/default.json`)
```json
{
  "clientName": "sandbox-test",
  "environment": "sandbox",
  "apiConfig": {
    "clientId": "50db03679d4c3297574c26b6aab1894e",
    "clientSecret": "f0f9927a93943253218861d661e8f71e",
    "tokenUrl": "https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/token",
    "apiBaseUrl": "https://api-sandbox.rabobank.nl/openapi/sandbox/payments/insight",
    "redirectUri": "http://localhost:8080/callback"
  },
  "certificates": {
    "certificatePath": "Cert_Premium/certificate.pem",
    "privateKeyPath": "Cert_Premium/private.key",
    "validateServerCertificate": false
  },
  "accounts": {
    "defaultAccountId": "Wp-xhZMGEWRIIgVjPwTC1aKJJ0VCRZ_4bScUVXof7e0",
    "accountMappings": {
      "NL52RABO0125618484": "Wp-xhZMGEWRIIgVjPwTC1aKJJ0VCRZ_4bScUVXof7e0",
      "NL80RABO1127000002": "hBS4MQ0Oc4LLJRiUiE_R94_-zoU4B2vwnPkXmsGh_bA"
    }
  },
  "settings": {
    "tokenRefreshThresholdMinutes": 5,
    "maxRetryAttempts": 3,
    "timeoutSeconds": 30,
    "enableDetailedLogging": true,
    "tokenStoragePath": "tokens",
    "enableAutomaticTokenRefresh": true
  }
}
```

### Key Settings for Testing
- **tokenRefreshThresholdMinutes**: 5 (aggressive refresh for testing)
- **enableDetailedLogging**: true (verbose output)
- **validateServerCertificate**: false (sandbox environment)

## 🐛 Troubleshooting

### Common Issues

1. **"No .NET SDKs were found"**
   - Install .NET 8.0 SDK from Microsoft
   - Restart PowerShell after installation

2. **Certificate not found**
   - Check paths in configuration
   - Verify files exist in `Cert_Premium/` folder

3. **Token expired errors**
   - Use fresh authorization code
   - Check token timestamps in `tokens/default_tokens.json`

4. **SSL/TLS errors**
   - Ensure `validateServerCertificate` is false for sandbox
   - Check certificate format (must be PEM)

### Debug Mode
Enable detailed logging by setting log level to Debug in `config/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "RabobankBAI": "Debug"
    }
  }
}
```

## 📊 Test Results Validation

### Successful Token Management
```
[INFO] Starting Rabobank BAI API Tool...
[INFO] Loaded configuration for client: sandbox-test
[INFO] Ensuring valid token for client: sandbox-test
[INFO] Existing tokens are valid, no refresh needed
[INFO] Token management successful
[INFO] Access token length: 574
[INFO] Operation type: NoAction
[INFO] Application completed
```

### Successful Token Refresh
```
[INFO] Refreshing tokens for client: sandbox-test
[INFO] Making token refresh request to: https://oauth-sandbox.rabobank.nl/...
[INFO] Token refresh successful. Operation type: AccessTokenRefresh
[INFO] Saved tokens for client: sandbox-test
```

### Successful Auth Code Exchange
```
[INFO] Exchanging authorization code for client: sandbox-test
[INFO] Making token exchange request to: https://oauth-sandbox.rabobank.nl/...
[INFO] Authorization code exchange successful. Access token length: 574
[INFO] Saved tokens for client: sandbox-test
```

## 🎯 Next Steps

Na successful setup:

1. **Implement API calls** in `RabobankApiClient.cs`
2. **Add UiPath integration** layer
3. **Create client-specific configurations**
4. **Deploy to production environment**

---

**Status**: ✅ Ready for Testing  
**Last Updated**: 16 September 2025