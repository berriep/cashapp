# Check SWIFT MT940 Line Length Compliance
Write-Host "=== SWIFT MT940 LINE LENGTH VALIDATION ===" -ForegroundColor Cyan

$filePath = "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Autobank\Ouput\mt940 - generated NL31RABO0300087233 - 20251107.swi"

if (Test-Path $filePath) {
    $lines = Get-Content $filePath
    $violations = @()
    
    Write-Host "Checking $($lines.Count) lines for SWIFT 65-character limit..." -ForegroundColor Yellow
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line.Length -gt 65) {
            $violations += [PSCustomObject]@{
                LineNumber = $i + 1
                Length = $line.Length
                Excess = $line.Length - 65
                Content = $line.Substring(0, [Math]::Min(65, $line.Length)) + "..."
            }
        }
    }
    
    if ($violations.Count -eq 0) {
        Write-Host "SUCCESS: All lines comply with 65-character SWIFT limit!" -ForegroundColor Green
    } else {
        Write-Host "VIOLATIONS: Found $($violations.Count) lines exceeding SWIFT limit:" -ForegroundColor Red
        
        Write-Host "`nTop 10 violations:" -ForegroundColor Yellow
        $violations | Select-Object -First 10 | Format-Table -Property LineNumber, Length, Excess, Content -AutoSize
        
        # Show specific problematic lines
        Write-Host "`nProblematic patterns:" -ForegroundColor Yellow
        foreach ($violation in ($violations | Select-Object -First 5)) {
            $fullLine = $lines[$violation.LineNumber - 1]
            Write-Host "Line $($violation.LineNumber) ($($violation.Length) chars):" -ForegroundColor Red
            Write-Host "  $fullLine" -ForegroundColor White
        }
    }
    
    # Check for specific issues we can fix
    Write-Host "`nAnalyzing specific issues:" -ForegroundColor Cyan
    
    $longEREFs = $lines | Where-Object { $_ -match ":86:" -and $_.Length -gt 65 }
    if ($longEREFs.Count -gt 0) {
        Write-Host "ISSUE: $($longEREFs.Count) :86: lines exceed limit" -ForegroundColor Red
    }
    
    $long61s = $lines | Where-Object { $_ -match ":61:" -and $_.Length -gt 65 }
    if ($long61s.Count -gt 0) {
        Write-Host "ISSUE: $($long61s.Count) :61: lines exceed limit" -ForegroundColor Red
    }
    
    # Summary
    Write-Host "`nSUMMARY:" -ForegroundColor Cyan
    Write-Host "  Total lines: $($lines.Count)" -ForegroundColor White
    Write-Host "  Compliant: $($lines.Count - $violations.Count)" -ForegroundColor Green
    Write-Host "  Violations: $($violations.Count)" -ForegroundColor Red
    Write-Host "  Compliance rate: $([Math]::Round((($lines.Count - $violations.Count) / $lines.Count) * 100, 1))%" -ForegroundColor Yellow
    
} else {
    Write-Host "ERROR: File not found: $filePath" -ForegroundColor Red
}

Write-Host "`n=== VALIDATION COMPLETE ===" -ForegroundColor Cyan