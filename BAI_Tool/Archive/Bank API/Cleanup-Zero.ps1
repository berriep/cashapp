# Zero Folder Cleanup Script
# Verplaatst development/prototype bestanden naar Archive

Write-Host "🧹 Starting Zero folder cleanup..." -ForegroundColor Green

# Zorg dat we in de juiste directory zijn
Set-Location "c:\Users\bpeijmen\Downloads\Zero\Zero"

# Controleer of Archive directory bestaat
if (!(Test-Path "Archive")) {
    New-Item -ItemType Directory -Path "Archive" -Force
    Write-Host "📁 Created Archive directory" -ForegroundColor Yellow
}

# Files die naar Archive moeten
$filesToArchive = @(
    # CAMT053 Development Files
    "CAMT053Generator.cs",
    "CAMT053Generator.csproj", 
    "CAMT053Main.cs",
    "CAMT053Standalone.cs",
    "CAMT053TestProgram.cs",
    "Generate-CAMT053.ps1",
    
    # API Client Development
    "RabobankApiClient.cs",
    "RabobankApiClient.cs.backup",
    "Program.cs",
    "Program.cs.backup", 
    "RabobankZero.csproj",
    "Zero.sln",
    
    # Utility Classes
    "BalanceModels.cs",
    "Config.cs", 
    "SignatureGenerator.cs",
    "TokenManager.cs",
    "TestDataConverter.cs",
    
    # Python Scripts
    "generate_consent.py",
    "generate_correct_consent.py",
    "generate_simple_consent.py",
    
    # Config & Temp Files
    "auth_code.txt",
    "config.json",
    "tokens.json", 
    "test_write.tmp",
    
    # Documentation Drafts
    "conclusie.md",
    "TDD-SDD-UiPath-Integration.md",
    "TDD-SDD-UiPath-Integration copy.md"
)

# Directories die naar Archive moeten
$dirsToArchive = @(
    "bin",
    "obj",
    "Cert_Premium"
)

# Individual UiPath components (niet de 11_CompleteTokenManager)
$uipathFilesToArchive = @(
    "UiPath\0_AutoTokenManager.cs",
    "UiPath\1_CheckTokenValidity.cs", 
    "UiPath\2_ExchangeAuthCodeForTokens.cs",
    "UiPath\3_RefreshTokens.cs",
    "UiPath\4_SaveTokensToFile.cs",
    "UiPath\5_LoadTokensFromFile.cs", 
    "UiPath\6_GetBalances.cs",
    "UiPath\7_GetTransactions.cs",
    "UiPath\9_DailyClosing.cs",
    "UiPath\Testdata"
)

Write-Host "📦 Moving files to Archive..." -ForegroundColor Blue

# Verplaats bestanden
$movedCount = 0
foreach ($file in $filesToArchive) {
    if (Test-Path $file) {
        try {
            Move-Item $file "Archive\" -Force
            Write-Host "  ✅ Moved: $file" -ForegroundColor Green
            $movedCount++
        }
        catch {
            Write-Host "  ❌ Failed to move: $file - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "  ⚠️  Not found: $file" -ForegroundColor Yellow
    }
}

# Verplaats directories
foreach ($dir in $dirsToArchive) {
    if (Test-Path $dir) {
        try {
            Move-Item $dir "Archive\" -Force
            Write-Host "  ✅ Moved directory: $dir" -ForegroundColor Green
            $movedCount++
        }
        catch {
            Write-Host "  ❌ Failed to move directory: $dir - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "  ⚠️  Directory not found: $dir" -ForegroundColor Yellow
    }
}

# Verplaats UiPath components
foreach ($file in $uipathFilesToArchive) {
    if (Test-Path $file) {
        try {
            # Maak UiPath subdir in Archive als het niet bestaat
            if (!(Test-Path "Archive\UiPath")) {
                New-Item -ItemType Directory -Path "Archive\UiPath" -Force
            }
            Move-Item $file "Archive\UiPath\" -Force
            Write-Host "  ✅ Moved: $file" -ForegroundColor Green
            $movedCount++
        }
        catch {
            Write-Host "  ❌ Failed to move: $file - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "  ⚠️  Not found: $file" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "🎉 Cleanup completed!" -ForegroundColor Green
Write-Host "📊 Total items moved: $movedCount" -ForegroundColor Cyan

Write-Host ""
Write-Host "✅ REMAINING PRODUCTION FILES:" -ForegroundColor Green
Write-Host "  - CAMT053Generator_Old.cs + .exe" -ForegroundColor White
Write-Host "  - MT940Generator.cs + .exe" -ForegroundColor White  
Write-Host "  - TestDataConverter/ (complete folder)" -ForegroundColor White
Write-Host "  - Output/ (generated files)" -ForegroundColor White
Write-Host "  - UiPath/11_CompleteTokenManager.cs" -ForegroundColor White
Write-Host "  - README.md" -ForegroundColor White
Write-Host "  - Docs/ (requirements)" -ForegroundColor White
Write-Host "  - UiPath Robot Implementatie Gids.md" -ForegroundColor White

Write-Host ""
Write-Host "📁 All development files moved to Archive/" -ForegroundColor Blue
