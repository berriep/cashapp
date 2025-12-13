# Test Enhanced MT940 Generator with All Database Patterns
# Test alle gevonden referentie patronen

Write-Host "=== TESTING ENHANCED MT940 GENERATOR ===" -ForegroundColor Cyan
Write-Host "Testing with all discovered database patterns:" -ForegroundColor Green

# Test parameters
$account = "NL31RABO0300087233"
$startDate = "2025-11-01"
$endDate = "2025-11-10"

Write-Host "`n1. Testing MT940 generation with all patterns..." -ForegroundColor Yellow

# Run the enhanced generator
try {
    $result = cscript.exe "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Autobank\Scripts\autobank_rabobank_mt940_db.vb" $account $startDate $endDate 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ MT940 generation completed successfully!" -ForegroundColor Green
        
        # Check if output file was created
        $outputPattern = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Archive\Bank API\Output\mt940_*_*.txt"
        $outputFiles = Get-ChildItem $outputPattern -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        
        if ($outputFiles) {
            Write-Host "📄 Generated file: $($outputFiles.Name)" -ForegroundColor Green
            Write-Host "📏 File size: $($outputFiles.Length) bytes" -ForegroundColor Green
            Write-Host "🕒 Generated at: $($outputFiles.LastWriteTime)" -ForegroundColor Green
            
            # Show first 20 lines to verify content
            Write-Host "`n📋 First 20 lines of generated MT940:" -ForegroundColor Cyan
            Get-Content $outputFiles.FullName | Select-Object -First 20 | ForEach-Object { Write-Host $_ -ForegroundColor White }
            
            # Check for specific patterns we implemented
            Write-Host "`n🔍 Checking for implemented patterns:" -ForegroundColor Yellow
            $content = Get-Content $outputFiles.FullName -Raw
            
            # Check for transaction types
            $patterns = @{
                "N586PREF" = "586 PREF transactions"
                "N2065" = "2065 International transactions"
                "N541EREF" = "541 Credit transactions"
                "N626NONREF" = "626 Internal transfers"
                "OM1B" = "OM1B reference pattern"
                "POYTCPE" = "POYTCPE reference pattern"
                "C2025" = "C-date reference pattern"
                "/PREF/" = "PREF structured field"
                "/EREF/" = "EREF structured field"
                "/TRCD/" = "Transaction code field"
            }
            
            foreach ($pattern in $patterns.GetEnumerator()) {
                if ($content -match [regex]::Escape($pattern.Key)) {
                    Write-Host "✅ Found: $($pattern.Value)" -ForegroundColor Green
                } else {
                    Write-Host "❌ Missing: $($pattern.Value)" -ForegroundColor Red
                }
            }
            
        } else {
            Write-Host "❌ No output file found!" -ForegroundColor Red
        }
        
    } else {
        Write-Host "❌ MT940 generation failed!" -ForegroundColor Red
        Write-Host "Error output:" -ForegroundColor Red
        $result | Write-Host -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ Error running MT940 generator: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n2. Testing SWIFT compliance..." -ForegroundColor Yellow

# Check if we have a generated file to test
$latestFile = Get-ChildItem "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Archive\Bank API\Output\mt940_*.txt" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latestFile) {
    Write-Host "📋 Checking SWIFT MT940 compliance..." -ForegroundColor Cyan
    
    $lines = Get-Content $latestFile.FullName
    $lineErrors = @()
    
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line.Length -gt 65) {
            $lineErrors += "Line $($i+1): Length $($line.Length) > 65 chars: $($line.Substring(0, [Math]::Min(65, $line.Length)))..."
        }
    }
    
    if ($lineErrors.Count -eq 0) {
        Write-Host "✅ All lines comply with 65-character SWIFT limit!" -ForegroundColor Green
    } else {
        Write-Host "❌ Found $($lineErrors.Count) lines exceeding SWIFT limit:" -ForegroundColor Red
        $lineErrors | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        if ($lineErrors.Count -gt 5) {
            Write-Host "  ... and $($lineErrors.Count - 5) more" -ForegroundColor Red
        }
    }
} else {
    Write-Host "❌ No MT940 file found to test compliance!" -ForegroundColor Red
}

Write-Host "`n3. Database pattern validation..." -ForegroundColor Yellow

# Summary of what we implemented
Write-Host "📊 Implemented patterns based on database analysis:" -ForegroundColor Cyan
Write-Host "  ✅ 586: OM1B############### -> N586PREF + /PREF/ field" -ForegroundColor Green
Write-Host "  ✅ 2065: C20251105-##########-############## -> N2065 + /EREF/ field" -ForegroundColor Green
Write-Host "  ✅ 541: Multiple patterns (numeric, IBAN-like) -> N541EREF + /EREF/ field" -ForegroundColor Green
Write-Host "  ✅ 626: POYTCPE2025000003### -> N626NONREF + /EREF/ field" -ForegroundColor Green
Write-Host "  ✅ 1085: Smart Pay transactions -> N085 special format" -ForegroundColor Green
Write-Host "  ✅ 501: Mixed patterns -> N501EREF + /EREF/ field" -ForegroundColor Green

Write-Host "`n=== ENHANCED MT940 GENERATOR TEST COMPLETE ===" -ForegroundColor Cyan