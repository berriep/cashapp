# Generate Rabobank Consent URL and Open Browser
# This script generates the OAuth2 consent URL and opens it in your browser

param(
    [Parameter(Mandatory=$false)]
    [string]$ClientName = "default",
    
    [Parameter(Mandatory=$false)]
    [string]$Environment = "sandbox",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipRedirectUri
)

Write-Host "Rabobank OAuth2 Consent Generator" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Set project directory
$ProjectPath = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank"
Set-Location $ProjectPath

# Load client configuration
$configFile = "config/clients/$ClientName.json"
if (-not (Test-Path $configFile)) {
    Write-Host "Configuration file not found: $configFile" -ForegroundColor Red
    Write-Host "Available configurations:" -ForegroundColor Yellow
    Get-ChildItem "config/clients/*.json" | ForEach-Object { Write-Host "  $($_.BaseName)" -ForegroundColor White }
    exit 1
}

try {
    $config = Get-Content $configFile | ConvertFrom-Json
    Write-Host "Loaded configuration for: $($config.clientName)" -ForegroundColor Green
} catch {
    Write-Host "Error loading configuration: $_" -ForegroundColor Red
    exit 1
}

# Extract configuration
$clientId = $config.apiConfig.clientId
$redirectUri = $config.apiConfig.redirectUri
$environment = $config.environment

# Determine authorization URL based on environment
if ($environment -eq "sandbox") {
    $authUrl = "https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/authorize"
    Write-Host "Environment: Sandbox" -ForegroundColor Yellow
} else {
    $authUrl = "https://oauth.rabobank.nl/openapi/oauth2-premium/authorize"
    Write-Host "Environment: Production" -ForegroundColor Red
}

# Required scopes for BAI API
$scopes = "bai.accountinformation.read"

# Generate state parameter for security
$state = [System.Web.Security.Membership]::GeneratePassword(16, 0)

# Build consent URL
$consentUrl = $authUrl + "?" + 
    "response_type=code" +
    "&client_id=$([System.Web.HttpUtility]::UrlEncode($clientId))"

if (-not $SkipRedirectUri) {
    $consentUrl += "&redirect_uri=$([System.Web.HttpUtility]::UrlEncode($redirectUri))"
}

$consentUrl += "&scope=$([System.Web.HttpUtility]::UrlEncode($scopes))" +
    "&state=$([System.Web.HttpUtility]::UrlEncode($state))"

Write-Host ""
Write-Host "Configuration Details:" -ForegroundColor Cyan
Write-Host "  Client ID: $clientId" -ForegroundColor White
if (-not $SkipRedirectUri) {
    Write-Host "  Redirect URI: $redirectUri" -ForegroundColor White
} else {
    Write-Host "  Redirect URI: <omitted>" -ForegroundColor Gray
}
Write-Host "  Scopes: $scopes" -ForegroundColor White
Write-Host "  State: $state" -ForegroundColor White
Write-Host ""

Write-Host "Generated Consent URL:" -ForegroundColor Green
Write-Host $consentUrl -ForegroundColor Yellow
Write-Host ""

# Save state for later verification
$stateFile = "oauth_state.txt"
$state | Out-File -FilePath $stateFile -Encoding UTF8
Write-Host "State parameter saved to: $stateFile" -ForegroundColor Green

# Copy URL to clipboard
try {
    $consentUrl | Set-Clipboard
    Write-Host "URL copied to clipboard!" -ForegroundColor Green
} catch {
    Write-Host "Could not copy to clipboard (clipboard not available)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "1. The consent URL will open in your browser" -ForegroundColor White
Write-Host "2. Login with your Rabobank credentials" -ForegroundColor White
Write-Host "3. Grant permission for BAI account information access" -ForegroundColor White
if (-not $SkipRedirectUri) {
    Write-Host "4. You will be redirected to: $redirectUri" -ForegroundColor White
} else {
    Write-Host "4. You will be redirected to your registered redirect URI" -ForegroundColor White
}
Write-Host "5. Copy the 'code' parameter from the redirect URL" -ForegroundColor White
Write-Host "6. Use the code with: .\Simple-Exchange.ps1 -AuthCode 'YOUR_CODE'" -ForegroundColor White
Write-Host ""

# Ask user if they want to open the browser
$openBrowser = Read-Host "Open consent URL in browser? (y/n) [default: y]"
if ($openBrowser -eq "" -or $openBrowser -eq "y" -or $openBrowser -eq "yes") {
    try {
        Start-Process $consentUrl
        Write-Host "Browser opened with consent URL" -ForegroundColor Green
    } catch {
        Write-Host "Could not open browser. Please copy the URL above manually." -ForegroundColor Yellow
    }
} else {
    Write-Host "Please copy the URL above and open it in your browser manually." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Waiting for authorization..." -ForegroundColor Cyan
Write-Host "After completing consent, you will receive a redirect URL like:" -ForegroundColor White
Write-Host "http://localhost:8080/callback?code=YOUR_AUTH_CODE&state=$state" -ForegroundColor Gray
Write-Host ""
Write-Host "Extract the code parameter and run:" -ForegroundColor White
Write-Host ".\Simple-Exchange.ps1 -AuthCode 'YOUR_AUTH_CODE'" -ForegroundColor Yellow