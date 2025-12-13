# Simple Authorization Code Exchange Script
param(
    [Parameter(Mandatory=$true)]
    [string]$AuthCode,
    
    [Parameter(Mandatory=$false)]
    [string]$ClientName = "default"
)

Write-Host "Rabobank BAI - Authorization Code Exchange" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Set location to project directory
$ProjectPath = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank"
Set-Location $ProjectPath

Write-Host "Project Directory: $ProjectPath" -ForegroundColor Green
Write-Host "Client: $ClientName" -ForegroundColor Green
Write-Host "Auth Code Length: $($AuthCode.Length) characters" -ForegroundColor Green
Write-Host ""

# Check if .NET is available
try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK Version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host ".NET SDK not found. Please install .NET 8.0 SDK first." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Starting authorization code exchange..." -ForegroundColor Cyan
Write-Host ""

# Run the application with auth code
try {
    $arguments = "--auth-code=$AuthCode"
    
    & dotnet run -- $arguments
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "Authorization code exchange completed successfully!" -ForegroundColor Green
        Write-Host "Tokens have been saved and are ready for use." -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "Authorization code exchange failed!" -ForegroundColor Red
    }
} catch {
    Write-Host "Error running application: $_" -ForegroundColor Red
}

Write-Host ""