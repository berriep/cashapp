# Quick Production Setup
# Use this when you know the exact registered redirect URI

Write-Host "Production Redirect URI Options:" -ForegroundColor Cyan
Write-Host "1. http://localhost:8080/callback" -ForegroundColor White
Write-Host "2. https://localhost:8443/callback" -ForegroundColor White  
Write-Host "3. Custom URI (you'll need to enter it)" -ForegroundColor White
Write-Host ""

$choice = Read-Host "Enter option number (1-3) or 'c' to check current config"

switch ($choice) {
    "1" {
        .\Switch-Environment.ps1 -ToProduction -RedirectUri "http://localhost:8080/callback"
        Write-Host "Switched to production with HTTP localhost redirect" -ForegroundColor Green
    }
    "2" {
        .\Switch-Environment.ps1 -ToProduction -RedirectUri "https://localhost:8443/callback"
        Write-Host "Switched to production with HTTPS localhost redirect" -ForegroundColor Green
    }
    "3" {
        $customUri = Read-Host "Enter your registered redirect URI"
        .\Switch-Environment.ps1 -ToProduction -RedirectUri $customUri
        Write-Host "Switched to production with custom redirect: $customUri" -ForegroundColor Green
    }
    "c" {
        $config = Get-Content "config/clients/default.json" | ConvertFrom-Json
        Write-Host "Current config:" -ForegroundColor Yellow
        Write-Host "  Environment: $($config.environment)" -ForegroundColor White
        Write-Host "  Redirect URI: $($config.apiConfig.redirectUri)" -ForegroundColor White
        Write-Host "  Client ID: $($config.apiConfig.clientId)" -ForegroundColor White
    }
    default {
        Write-Host "Invalid choice. Current environment unchanged." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Ready to generate consent URL with: .\Generate-Consent.ps1" -ForegroundColor Green