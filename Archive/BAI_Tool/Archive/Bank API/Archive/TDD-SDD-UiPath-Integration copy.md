# Technical Design Document (TDD) & System Design Document (SDD)
## Rabobank API UiPath Integration

**Document Version:** 1.1  
**Date:** 27 Augustus 2025  
**Project:** Rabobank Business Account Insight API - UiPath Integration with Multi-Account Support

---

## 1. Technical Design Document (TDD)

### 1.1 Executive Summary

Dit document beschrijft de technische implementatie voor een UiPath-compatible Rabobank API client die automatisch tokens beheert en transacties ophaalt met flexibele datumbereiken.

### 1.2 Technical Requirements

#### 1.2.1 Functional Requirements
- **FR-001:** OAuth2 token exchange van consent auth code naar access/refresh tokens
- **FR-002:** Automatische token refresh zonder handmatige interventie
- **FR-003:** Token persistence over weekends en shutdowns
- **FR-004:** Multi-account processing met configureerbare account mapping
- **FR-005:** Flexibele transactie ophaling met configureerbare datum ranges
- **FR-006:** Balance API integration voor opening/closing balances
- **FR-007:** IBAN-based file naming voor account-specifieke organisatie
- **FR-008:** UiPath-compatible output (JSON files + exit codes)
- **FR-009:** Robuuste error handling en logging

#### 1.2.2 Non-Functional Requirements
- **NFR-001:** Unattended operation (24/7 zonder user input)
- **NFR-002:** Security: Encrypted token storage
- **NFR-003:** Performance: < 30 seconden voor token refresh
- **NFR-004:** Reliability: 99% uptime voor token operations
- **NFR-005:** Maintainability: Configurable parameters
- **NFR-006:** Compliance: PSD2 en Premium API standards

### 1.3 Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   UiPath Robot  │───▶│  RaboAPI.exe    │───▶│ Rabobank API    │
│                 │    │                 │    │                 │
│ - Orchestrator  │    │ - Token Manager │    │ - OAuth2        │
│ - Scheduler     │    │ - API Client    │    │ - Transactions  │
│ - Error Handler │    │ - File Handler  │    │ - mTLS          │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌─────────────────┐
                       │  File System    │
                       │                 │
                       │ - tokens.json   │
                       │ - config.json   │
                       │ - transactions/ │
                       │ - logs/         │
                       └─────────────────┘
```

### 1.4 Component Design

#### 1.4.1 Token Manager Component
```csharp
public class TokenManager
{
    // Core Methods
    Task<TokenResponse> ExchangeAuthCodeForTokens(string authCode)
    Task<TokenResponse> RefreshTokens(string refreshToken)
    Task<TokenResponse> LoadTokens()
    Task SaveTokens(TokenResponse tokens)
    bool IsTokenValid(TokenResponse tokens)
    bool RequiresRefresh(TokenResponse tokens, int bufferMinutes = 30)
}
```

**Responsibilities:**
- OAuth2 flow management
- Token lifecycle management
- Encrypted token persistence
- Automatic refresh logic

#### 1.4.2 API Client Component
```csharp
public class RabobankApiClient
{
    Task<string> GetTransactions(
        string accountId,
        DateTime dateFrom,
        DateTime dateTo,
        string iban
    )
    
    Task<string> GetCamtDataSet(
        string accountId,
        DateTime dateFrom,
        DateTime dateTo,
        string iban
    )
    
    Task<BalanceResponse> GetBalance(
        string accountId,
        DateTime referenceDate
    )
}
```

**Responsibilities:**
- Multi-account transaction processing
- Balance API integration (opening/closing balances)
- IBAN-based file naming and organization
- mTLS certificate handling
- RSA-SHA512 signature generation
- API request execution
- Response processing and validation

#### 1.4.3 UiPath Integration Layer
```csharp
public class UiPathInterface
{
    // Command Line Interface
    static int Main(string[] args)
    
    // Operations
    Task<int> RefreshTokens()           // Exit code: 0=success, 1=error
    Task<int> GetTransactionsMultiAccount(string fromDate, string toDate)
    Task<int> HealthCheck()
    
    // Multi-Account Processing
    Task ProcessAccount(string iban, string accountId, DateTime dateFrom, DateTime dateTo)
    
    // Output
    void WriteJsonOutput(object data, string filename)
    void WriteLog(string level, string message)
}
```

### 1.5 Data Flow Design

#### 1.5.1 Token Refresh Flow
```
1. UiPath Scheduler (every 6 hours)
   ↓
2. RaboAPI.exe --operation=refresh-tokens
   ↓
3. Load existing tokens from encrypted file
   ↓
4. Check token validity (expires_in - 30 minutes buffer)
   ↓
5. If expired: Call refresh API with refresh_token
   ↓
6. Save new tokens (encrypted)
   ↓
7. Return exit code (0=success, 1=failure)
```

#### 1.5.2 Multi-Account Transaction Retrieval Flow
```
1. UiPath Process (daily/on-demand)
   ↓
2. RaboAPI.exe 2025-08-01 2025-08-26
   ↓
3. Load multi-account configuration from config.json
   ↓
4. For each account in AccountIds:
   a. Load valid tokens (auto-refresh if needed)
   b. Generate API signature with current timestamp
   c. Get opening balance (dateFrom - 1 day)
   d. Call transactions API with date range
   e. Get closing balance (dateTo)
   f. Generate CAMT dataset with Balance API integration
   g. Save account-specific files:
      - transactions_{ibanShort}_{timestamp}.json
      - camt_dataset_{ibanShort}_{timestamp}.json
   ↓
5. Return exit code + processing summary
```

### 1.6 Security Implementation

#### 1.6.1 Token Encryption
```csharp
public class SecureTokenStorage
{
    // AES-256 encryption with machine key
    string EncryptToken(string tokenJson)
    string DecryptToken(string encryptedData)
    
    // Use Windows DPAPI for key management
    // Tokens only readable by same user on same machine
}
```

#### 1.6.2 Certificate Security
- Private keys stored in protected certificate store
- mTLS validation with Rabobank root CA
- Certificate expiry monitoring

### 1.7 Error Handling Strategy

#### 1.7.1 Token Errors
| Error Code | Description | UiPath Action |
|------------|-------------|---------------|
| 1001 | Token expired, refresh failed | Retry with new auth code |
| 1002 | Refresh token invalid | Request new consent |
| 1003 | Network timeout | Retry after 5 minutes |
| 1004 | Certificate expired | Alert administrator |

#### 1.7.2 API Errors
| Error Code | Description | UiPath Action |
|------------|-------------|---------------|
| 2001 | Invalid signature | Retry once |
| 2002 | Rate limit exceeded | Wait and retry |
| 2003 | Account not found | Skip and continue |
| 2004 | No transactions found | Normal completion |

---

## 2. System Design Document (SDD)

### 2.1 System Architecture

#### 2.1.1 Deployment Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                    UiPath Orchestrator                     │
│                                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │   Scheduler     │  │  Process Queue  │  │  Monitoring  │ │
│  │                 │  │                 │  │              │ │
│  │ - Token Refresh │  │ - Transaction   │  │ - Alerts     │ │
│  │   (every 6h)    │  │   Jobs          │  │ - Logs       │ │
│  │ - Health Check  │  │ - Retry Logic   │  │ - Dashboards │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     UiPath Robot VM                        │
│                                                             │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                RaboAPI.exe                              │ │
│  │                                                         │ │
│  │  Config/       Data/           Logs/                   │ │
│  │  ├─config.json  ├─tokens.enc    ├─app.log              │ │
│  │  ├─certs/       ├─transactions/ ├─error.log            │ │
│  │  └─settings.ini  └─temp/        └─audit.log            │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Rabobank API Gateway                      │
│                                                             │
│  OAuth2 Endpoint ────► Business Account Insight API        │
│  Certificate Auth ───► Transaction Data                    │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Configuration Management

#### 2.2.1 Application Configuration (config.json)
```json
{
  "rabobank": {
    "clientId": "50db03679d4c3297574c26b6aab1894e",
    "apiBaseUrl": "https://api-sandbox.rabobank.nl/openapi/sandbox/payments/insight",
    "balanceApiBaseUrl": "https://api-sandbox.rabobank.nl/openapi/sandbox/payments/insight",
    "oauthBaseUrl": "https://oauth.rabobank.nl/openapi/oauth2-premium",
    "certificatePath": "./certificate.pem",
    "privateKeyPath": "./private.key",
    "consentId": "3f909cbc-e94e-46b8-817a-8e4d86b4c39d"
  },
  "accounts": {
    "AccountIds": {
      "NL52RABO0125618484": "Wp-xhZMGEWRIIgVjPwTC1aKJJ0VCRZ_4bScUVXof7e0",
      "NL80RABO1127000002": "hBS4MQ0Oc4LLJRiUiE_R94_-zoU4B2vwnPkXmsGh_bA"
    }
  },
  "tokenManagement": {
    "refreshBufferMinutes": 30,
    "maxRetryAttempts": 3,
    "retryDelaySeconds": 300,
    "encryptionEnabled": false
  },
  "output": {
    "transactionDirectory": "./Output",
    "logDirectory": "./logs",
    "dateFormat": "yyyyMMdd_HHmmss",
    "maxFileSize": "50MB",
    "retentionDays": 90,
    "ibanBasedNaming": true
  }
}
```

#### 2.2.2 UiPath Variables
| Variable | Type | Description | Example |
|----------|------|-------------|---------|
| `RaboAPI_ExePath` | String | Path to executable | `C:\UiPath\RaboAPI\RaboAPI.exe` |
| `RaboAPI_ConfigPath` | String | Config directory | `C:\UiPath\RaboAPI\config` |
| `RaboAPI_DateFrom` | String | Start date | `2025-08-01` |
| `RaboAPI_DateTo` | String | End date | `2025-08-26` |
| `RaboAPI_AccountMode` | String | Processing mode | `multi-account` |
| `RaboAPI_OutputPath` | String | Output directory | `C:\UiPath\RaboAPI\Output` |

### 2.3 Process Workflows

#### 2.3.1 Daily Multi-Account Transaction Retrieval Workflow
```
┌─────────────────┐
│ UiPath Trigger  │
│ (Scheduled)     │
└─────────┬───────┘
          │
          ▼
┌─────────────────┐     ┌─────────────────┐
│ Calculate Dates │────▶│ Load Multi-     │
│ (Yesterday)     │     │ Account Config  │
└─────────────────┘     └─────────┬───────┘
                                  │
                                  ▼
┌─────────────────┐     ┌─────────────────┐
│ For Each Account│◄────│ Call RaboAPI    │
│ - Check Tokens  │     │ dateFrom dateTo │
│ - Get Balances  │     │                 │
│ - Get Transactions    │                 │
│ - Generate CAMT │     │                 │
│ - Save Files    │     │                 │
└─────────┬───────┘     └─────────┬───────┘
          │                       │
          ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│ Process Results │────▶│ Update Database │
│ (Parse JSON)    │     │ (Business Logic)│
│ Account-specific│     │ Per Account     │
└─────────────────┘     └─────────────────┘
```

#### 2.3.2 Weekend Token Maintenance Workflow
```
┌─────────────────┐
│ UiPath Trigger  │
│ (Every 6 hours) │
└─────────┬───────┘
          │
          ▼
┌─────────────────┐     ┌─────────────────┐
│ Call RaboAPI    │────▶│ Check Exit Code │
│ refresh-tokens  │     │ 0=OK, 1=Error   │
└─────────────────┘     └─────────┬───────┘
                                  │
                                  ▼
                        ┌─────────────────┐
                        │ Log Result &    │
                        │ Alert if Failed │
                        └─────────────────┘
```

### 2.4 Command Line Interface

#### 2.4.1 Operations
```bash
# Multi-Account Processing (Simplified Interface)
RaboAPI.exe 2025-08-01 2025-08-26

# Token Management
RaboAPI.exe --operation=refresh-tokens
RaboAPI.exe --operation=exchange-auth-code --auth-code="ABC123..."

# System Operations
RaboAPI.exe --operation=health-check
RaboAPI.exe --operation=validate-config
```

#### 2.4.2 Output Format
```json
{
  "operation": "multi-account-processing",
  "timestamp": "2025-08-27T12:19:07.000Z",
  "success": true,
  "exitCode": 0,
  "accounts": [
    {
      "iban": "NL52RABO0125618484",
      "accountId": "Wp-xhZMGEWRIIgVjPwTC1aKJJ0VCRZ_4bScUVXof7e0",
      "dateFrom": "2025-08-01T00:00:00.000Z",
      "dateTo": "2025-08-26T23:59:59.999Z",
      "transactionCount": 0,
      "openingBalance": 100.00,
      "closingBalance": 100.00,
      "currency": "EUR",
      "files": {
        "transactions": "transactions_52RABO01_20250827_121907.json",
        "camtDataset": "camt_dataset_52RABO01_20250827_121907.json"
      }
    },
    {
      "iban": "NL80RABO1127000002",
      "accountId": "hBS4MQ0Oc4LLJRiUiE_R94_-zoU4B2vwnPkXmsGh_bA",
      "dateFrom": "2025-08-01T00:00:00.000Z",
      "dateTo": "2025-08-26T23:59:59.999Z",
      "transactionCount": 0,
      "openingBalance": 100.00,
      "closingBalance": 100.00,
      "currency": "EUR",
      "files": {
        "transactions": "transactions_80RABO11_20250827_121908.json",
        "camtDataset": "camt_dataset_80RABO11_20250827_121908.json"
      }
    }
  ],
  "errors": []
}
```

### 2.5 Monitoring and Alerting

#### 2.5.1 Health Checks
- **Token Validity:** Check every hour if tokens expire within 24 hours
- **Certificate Expiry:** Monthly check for certificate expiration
- **API Connectivity:** Daily ping to Rabobank API endpoints
- **File System:** Monitor disk space and file permissions

#### 2.5.2 Alerting Rules
| Alert | Condition | Severity | Action |
|-------|-----------|----------|--------|
| Token Expired | No valid tokens available | Critical | Request new consent |
| Certificate Expiry | Certificate expires in < 30 days | Warning | Renew certificate |
| API Rate Limit | 429 responses > 5 in 1 hour | Warning | Reduce frequency |
| Disk Space Low | < 1GB available | Warning | Clean old files |

### 2.6 Disaster Recovery

#### 2.6.1 Token Recovery
1. **Backup Strategy:** Daily encrypted backup of tokens to network share
2. **Recovery Procedure:** Restore from backup + refresh tokens
3. **Fallback:** Request new consent with stored auth code

#### 2.6.2 Certificate Recovery
1. **Certificate Backup:** Store certificates in secure vault
2. **Automatic Renewal:** 30 days before expiry
3. **Emergency Procedure:** Manual certificate deployment

### 2.7 Performance Requirements

#### 2.7.1 Response Times
- Token refresh: < 10 seconds
- Transaction retrieval: < 30 seconds for 100 transactions
- Health check: < 5 seconds

#### 2.7.2 Throughput
- Multi-account processing: Up to 10 accounts simultaneously
- Maximum 500 transactions per API call
- Maximum 10 API calls per minute (rate limiting)
- Daily capacity: 50,000 transactions per account
- Balance API calls: Up to 60 per hour per account

### 2.8 Security Considerations

#### 2.8.1 Data Protection
- All tokens encrypted at rest using AES-256
- Private keys stored in Windows Certificate Store
- Log files exclude sensitive data (tokens, signatures)
- Network traffic over HTTPS/mTLS only

#### 2.8.2 Access Control
- Application runs under dedicated service account
- Minimal file system permissions
- Certificate access restricted to application user
- API credentials stored in secure configuration

---

## 3. Implementation Roadmap

### Phase 1: Core Development (Week 1) ✅ COMPLETED
- [x] Token Manager implementation
- [x] Multi-account API Client with Balance API integration
- [x] Simplified command line interface (dateFrom dateTo)
- [x] Multi-account configuration management
- [x] IBAN-based file naming system

### Phase 2: UiPath Integration (Week 2)
- [ ] UiPath workflow development for multi-account processing
- [ ] Enhanced error handling and logging
- [ ] Testing with UiPath Robot
- [ ] Documentation and training updates

### Phase 3: Production Deployment (Week 3)
- [ ] Security hardening
- [ ] Performance optimization
- [ ] Monitoring setup for multi-account operations
- [ ] Go-live support

---

**Document Approval:**
- Technical Lead: _________________
- UiPath Architect: _________________
- Security Officer: _________________
- Date: _________________ 