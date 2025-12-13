# Rabobank BAI API Tool

Een generieke C# library voor het aanspreken van de Rabobank Business Account Insight (BAI) API. Deze tool is ontworpen om herbruikbaar te zijn voor verschillende klanten met configureerbare instellingen.

## Features

- **OAuth2 Token Management**: Automatische token refresh en fallback mechanismen
- **mTLS Authentication**: Secure client certificate authentication
- **Client Configuration**: Per-client configureerbare instellingen
- **Robuuste Error Handling**: Uitgebreide foutafhandeling en retry logic
- **Logging**: Structured logging met configureerbare niveaus
- **Modular Architecture**: Uitbreidbare en onderhoudbare code structuur

## Project Structuur

```
RabobankBAI/
├── src/
│   ├── Core/
│   │   ├── Interfaces/          # Service interfaces
│   │   └── Services/           # Core service implementations
│   ├── Configuration/          # Configuration models
│   ├── Models/                # Data models
│   ├── Services/              # Business services
│   ├── Utils/                 # Utility classes
│   └── Program.cs             # Main entry point
├── config/
│   ├── clients/               # Client-specific configurations
│   └── appsettings.json       # Application settings
├── certificates/              # mTLS certificates (per environment)
├── tokens/                    # Token storage (auto-created)
├── output/                    # API response output
└── Documentation/             # Project documentation
```

## Quick Start

### 1. Prerequisites

- .NET 8.0 Runtime
- Rabobank API credentials (Client ID, Client Secret)
- mTLS certificates (PEM format)

### 2. Configuration Setup

1. **Kopieer de bestaande certificaten**:
   ```bash
   # Kopieer je certificaten naar de juiste folder
   cp path/to/your/certificate.pem certificates/
   cp path/to/your/private.key certificates/
   ```

2. **Configureer een client**:
   ```bash
   # Kopieer de template en pas aan
   cp config/clients/sandbox-template.json config/clients/mijn-klant.json
   ```

3. **Update de configuratie** in `config/clients/mijn-klant.json`:
   ```json
   {
     "clientName": "mijn-klant",
     "environment": "sandbox",
     "apiConfig": {
       "clientId": "jouw-client-id",
       "clientSecret": "jouw-client-secret",
       "tokenUrl": "https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/token",
       "apiBaseUrl": "https://api-sandbox.rabobank.nl/openapi/sandbox/payments/insight",
       "redirectUri": "http://localhost:8080/callback"
     },
     "certificates": {
       "certificatePath": "certificates/certificate.pem",
       "privateKeyPath": "certificates/private.key"
     },
     "accounts": {
       "defaultAccountId": "jouw-account-id"
     }
   }
   ```

### 3. Building & Running

```bash
# Build het project
dotnet build

# Run het project
dotnet run

# Of run met specifieke configuratie
dotnet run -- --client mijn-klant
```

## Usage Examples

### Basic Token Management

```csharp
// Service injection setup
var serviceProvider = ConfigureServices();
var tokenManager = serviceProvider.GetService<ITokenManager>();
var configManager = serviceProvider.GetService<IConfigurationManager>();

// Load client configuration
var clientConfig = await configManager.LoadClientConfigurationAsync("mijn-klant");

// Ensure valid token
var tokenResult = await tokenManager.EnsureValidTokenAsync(clientConfig);

if (tokenResult.Success)
{
    Console.WriteLine($"Access token: {tokenResult.AccessToken}");
    // Use token for API calls
}
```

### Authorization Code Exchange

```csharp
// Als je een authorization code hebt gekregen
string authCode = "received-auth-code";
var tokenResult = await tokenManager.ExchangeAuthorizationCodeAsync(clientConfig, authCode);

if (tokenResult.Success)
{
    // Tokens zijn opgeslagen en klaar voor gebruik
    Console.WriteLine("Fresh tokens obtained!");
}
```

### Manual Token Refresh

```csharp
// Force token refresh
var tokenResult = await tokenManager.EnsureValidTokenAsync(clientConfig, forceRefresh: true);
```

## Configuration

### Client Configuration

Elke client heeft zijn eigen configuratie bestand in `config/clients/`. De configuratie bevat:

- **API Settings**: Client credentials, endpoints
- **Certificate Settings**: mTLS certificate paths
- **Account Mapping**: Account IDs en IBAN mappings
- **Behavior Settings**: Timeouts, retry attempts, logging

### Environment Variables

Je kunt ook environment variables gebruiken voor sensitive data:

```bash
export RABOBANK_CLIENT_SECRET="your-secret"
export RABOBANK_CERT_PATH="/path/to/cert.pem"
```

## Token Management

De TokenManager handelt automatisch de volgende scenario's af:

1. **Fresh Token Exchange**: Authorization code → Access + Refresh tokens
2. **Access Token Refresh**: Refresh token → New access token
3. **Full Token Refresh**: Refresh token → New access + refresh tokens
4. **Fallback Logic**: Bij refresh failure, automatische fallback naar auth code
5. **Token Validation**: Expiry checking en proactive refresh

### Token Storage

Tokens worden veilig opgeslagen in JSON bestanden per client:
- `tokens/{client-name}_tokens.json`
- Automatische cleanup van expired tokens
- Structured format voor debugging

## Security

### Certificate Management

- **mTLS Required**: Alle API calls gebruiken client certificates
- **Certificate Validation**: Automatische certificate expiry checking
- **Secure Storage**: Certificates worden veilig opgeslagen per environment

### Token Security

- **Secure Storage**: Tokens worden lokaal opgeslagen (niet in source control)
- **Automatic Refresh**: Proactive token refresh voorkomt expiry
- **Fallback Mechanisms**: Robuuste error handling

### Best Practices

1. **Nooit credentials in source control**
2. **Gebruik environment-specific certificates**
3. **Regular certificate renewal monitoring**
4. **Implement proper access controls**

## Logging

Structured logging met verschillende niveaus:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "RabobankBAI": "Debug"
    }
  }
}
```

Log output bevat:
- Token management operations
- API call details
- Error information
- Performance metrics

## Testing

### Unit Tests

```bash
# Run unit tests
dotnet test
```

### Integration Tests

Voor integration tests heb je nodig:
- Valid API credentials
- Working certificates
- Network access to Rabobank APIs

## Deployment

### UiPath Integration

Voor UiPath deployment:

1. **Build Release Package**:
   ```bash
   dotnet publish -c Release -o publish/
   ```

2. **Copy to UiPath Environment**:
   - Kopieer alle bestanden naar UiPath robot directory
   - Configureer client settings
   - Test workflows

3. **UiPath Workflow Example**:
   ```csharp
   // In UiPath Invoke Code activity
   var tokenManager = new TokenManager(logger, httpClient, certManager);
   var tokenResult = await tokenManager.EnsureValidTokenAsync(clientConfig);
   ```

### Production Deployment

1. **Environment Setup**:
   - Production certificates
   - Production API endpoints
   - Secure credential storage

2. **Configuration**:
   - Update `environment` naar "production"
   - Production API URLs
   - Disable debug logging

3. **Monitoring**:
   - Certificate expiry alerts
   - Token refresh monitoring
   - API health checks

## Troubleshooting

### Common Issues

1. **Certificate Not Found**:
   ```
   Error: Certificate file not found: certificates/certificate.pem
   ```
   - Check file paths in configuration
   - Ensure certificates exist
   - Verify file permissions

2. **Invalid Grant Error**:
   ```
   Error: invalid_grant - refresh token expired
   ```
   - Refresh token heeft expired
   - Nieuwe authorization code nodig
   - Check token expiry settings

3. **mTLS Connection Failed**:
   ```
   Error: SSL connection could not be established
   ```
   - Check certificate format (PEM)
   - Verify certificate validity
   - Check network connectivity

### Debug Mode

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "RabobankBAI": "Debug"
    }
  }
}
```

## Contributing

1. Fork het project
2. Create een feature branch
3. Commit je changes
4. Push naar de branch
5. Create een Pull Request

## Support

Voor vragen en support:
- Check de [Project Documentation](Documentation/)
- Review de [API Documentation](Documentation/API-Documentation.md)
- Create een GitHub issue

## License

Dit project is ontwikkeld voor interne gebruik en klant implementaties.

---

**Version**: 1.0.0  
**Last Updated**: 16 September 2025