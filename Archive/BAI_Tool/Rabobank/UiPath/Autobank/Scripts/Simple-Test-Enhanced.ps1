# Simple Test Enhanced MT940 Generator
Write-Host "=== TESTING ENHANCED MT940 GENERATOR ===" -ForegroundColor Cyan

# Test parameters
$account = "NL31RABO0300087233"
$startDate = "2025-11-01"
$endDate = "2025-11-10"

Write-Host "Testing MT940 generation with all patterns..." -ForegroundColor Yellow

# Run the enhanced generator
try {
    $result = cscript.exe "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Autobank\Scripts\autobank_rabobank_mt940_db.vbs" $account $startDate $endDate 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SUCCESS: MT940 generation completed!" -ForegroundColor Green
        
        # Check if output file was created
        $outputPattern = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Archive\Bank API\Output\mt940_*_*.txt"
        $outputFiles = Get-ChildItem $outputPattern -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        
        if ($outputFiles) {
            Write-Host "Generated file: $($outputFiles.Name)" -ForegroundColor Green
            Write-Host "File size: $($outputFiles.Length) bytes" -ForegroundColor Green
            
            # Show first 15 lines to verify content
            Write-Host "`nFirst 15 lines of generated MT940:" -ForegroundColor Cyan
            Get-Content $outputFiles.FullName | Select-Object -First 15 | ForEach-Object { Write-Host $_ }
            
            # Check for patterns
            Write-Host "`nChecking for implemented patterns:" -ForegroundColor Yellow
            $content = Get-Content $outputFiles.FullName -Raw
            
            if ($content -match "N586PREF") { Write-Host "FOUND: N586PREF transactions" -ForegroundColor Green }
            if ($content -match "N2065") { Write-Host "FOUND: N2065 International transactions" -ForegroundColor Green }
            if ($content -match "N541EREF") { Write-Host "FOUND: N541EREF Credit transactions" -ForegroundColor Green }
            if ($content -match "N626NONREF") { Write-Host "FOUND: N626NONREF Internal transfers" -ForegroundColor Green }
            if ($content -match "OM1B") { Write-Host "FOUND: OM1B reference pattern" -ForegroundColor Green }
            if ($content -match "POYTCPE") { Write-Host "FOUND: POYTCPE reference pattern" -ForegroundColor Green }
            if ($content -match "/PREF/") { Write-Host "FOUND: /PREF/ structured field" -ForegroundColor Green }
            if ($content -match "/EREF/") { Write-Host "FOUND: /EREF/ structured field" -ForegroundColor Green }
            
        } else {
            Write-Host "ERROR: No output file found!" -ForegroundColor Red
        }
        
    } else {
        Write-Host "ERROR: MT940 generation failed!" -ForegroundColor Red
        $result | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    }
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== TEST COMPLETE ===" -ForegroundColor Cyan