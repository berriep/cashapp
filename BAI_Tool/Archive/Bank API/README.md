# Rabobank Zero - C# Implementation

Clean C# implementation of Rabobank Business Account Insight API integration.

## 🎯 Purpose

This is a fresh start implementation designed for:
- Clean, working API integration
- Easy UiPath integration later
- Following exact API specification

## 📁 Files Structure

```
Rabo/Zero/
├── config.json              # Configuration with credentials and URLs
├── RabobankZero.csproj      # .NET 6 project file
├── Program.cs               # Main application entry point
├── Config.cs                # Configuration model
├── TokenManager.cs          # OAuth2 token management
├── SignatureGenerator.cs    # HTTP signature generation
├── RabobankApiClient.cs     # API client for transactions
├── auth_code.txt           # Fresh authorization code (provided)
├── Cert_Premium/           # Certificates folder
│   ├── certificate.pem     # mTLS certificate
│   └── private.key         # Private key for signatures
└── tokens.json             # Generated after first run

```

## 🚀 How to Run

1. **Prerequisites:**
   - .NET 6 SDK installed
   - Valid `auth_code.txt` file (✅ already present)
   - Valid certificates in `Cert_Premium/` (✅ already present)

2. **Build and Run:**
   ```bash
   cd /Users/barry/Projects/Banken/Rabo/Zero
   dotnet restore
   dotnet build
   dotnet run
   ```

## 🔧 Configuration

The `config.json` file contains all necessary configuration:
- ✅ Client credentials (from your working Python implementation)
- ✅ API URLs (sandbox endpoints)
- ✅ Certificate paths
- ✅ Account IDs (both available accounts)

## 🔍 Key Features

1. **TokenManager.cs:**
   - Exchanges auth_code for fresh tokens
   - Refreshes expired access tokens
   - Saves/loads tokens from file

2. **SignatureGenerator.cs:**
   - Generates RSA-SHA512 signatures per API spec
   - Calculates SHA-512 digest for empty body
   - Creates certificate fingerprint for keyId

3. **RabobankApiClient.cs:**
   - Makes authenticated API calls
   - Handles mTLS certificate authentication
   - Includes all required headers per API specification

4. **Program.cs:**
   - Orchestrates the complete flow
   - Detailed logging for debugging
   - Error handling with clear messages

## 📋 What This Implementation Does

1. **Load Configuration** from `config.json`
2. **Get Valid Tokens:**
   - Try to load existing tokens from `tokens.json`
   - If no tokens exist → exchange auth_code for new tokens
   - If tokens expired → refresh using refresh_token
3. **Fetch Transactions:**
   - Generate proper HTTP signatures
   - Include all required headers
   - Make authenticated API call
   - Save response to timestamped file

## 🎯 Ready for UiPath

This C# implementation is designed for easy UiPath integration:
- ✅ All methods can be copied into UiPath Invoke Code activities
- ✅ Uses standard .NET libraries (System.Security.Cryptography, HttpClient)
- ✅ Clear separation of concerns
- ✅ Comprehensive error handling and logging

## 📝 Next Steps

1. **Test the implementation** → Run `dotnet run`
2. **If it works** → Port to UiPath Invoke Code activities
3. **If it doesn't work** → Debug using the detailed console output

## ⚠️ Important Notes

- The implementation follows the exact API specification from `Business-Account-Insight-Transactions-1.2.14.json`
- Uses RSA-SHA512 signatures (not SHA256 like previous attempts)
- Includes certificate fingerprint as keyId
- All required headers are present: Date, Digest, X-Request-ID, Signature, Signature-Certificate

This should resolve the "Invalid signature" errors we've been encountering!