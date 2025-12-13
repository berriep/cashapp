# Quick switch to sandbox for testing
param(
    [switch]$ToSandbox,
    [switch]$ToProduction,
    [string]$RedirectUri
)

$configFile = "config/clients/default.json"
$config = Get-Content $configFile | ConvertFrom-Json

Write-Host "Current environment: $($config.environment)" -ForegroundColor Yellow
Write-Host "Current redirect URI: $($config.apiConfig.redirectUri)" -ForegroundColor Yellow

if ($ToSandbox) {
    Write-Host "Switching to SANDBOX environment..." -ForegroundColor Cyan
    $config.environment = "sandbox"
    $config.apiConfig.tokenUrl = "https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/token"
    $config.apiConfig.apiBaseUrl = "https://api-sandbox.rabobank.nl/openapi/sandbox/payments/insight"
    $config.certificates.validateServerCertificate = $false
    
    if (-not $RedirectUri) {
        $config.apiConfig.redirectUri = "http://localhost:8080/callback"
    }
}

if ($ToProduction) {
    Write-Host "Switching to PRODUCTION environment..." -ForegroundColor Red
    $config.environment = "production"
    $config.apiConfig.tokenUrl = "https://oauth.rabobank.nl/openapi/oauth2-premium/token"
    $config.apiConfig.apiBaseUrl = "https://api.rabobank.nl/openapi/payments/insight"
    $config.certificates.validateServerCertificate = $true
}

if ($RedirectUri) {
    Write-Host "Setting redirect URI to: $RedirectUri" -ForegroundColor Green
    $config.apiConfig.redirectUri = $RedirectUri
}

# Save configuration
$config | ConvertTo-Json -Depth 10 | Set-Content $configFile

Write-Host ""
Write-Host "Updated configuration:" -ForegroundColor Green
Write-Host "  Environment: $($config.environment)" -ForegroundColor White
Write-Host "  Token URL: $($config.apiConfig.tokenUrl)" -ForegroundColor White  
Write-Host "  Redirect URI: $($config.apiConfig.redirectUri)" -ForegroundColor White
Write-Host ""

if ($config.environment -eq "sandbox") {
    Write-Host "Ready for sandbox testing with: .\Generate-Consent.ps1" -ForegroundColor Green
} else {
    Write-Host "Ready for production with: .\Generate-Consent.ps1" -ForegroundColor Red
    Write-Host "Make sure redirect URI is registered in Rabobank Developer Portal!" -ForegroundColor Yellow
}