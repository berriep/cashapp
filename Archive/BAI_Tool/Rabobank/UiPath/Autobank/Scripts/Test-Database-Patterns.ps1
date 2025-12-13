# Test Database Connection for MT940 Enhanced Patterns
Write-Host "=== TESTING DATABASE CONNECTION FOR MT940 PATTERNS ===" -ForegroundColor Cyan

# Database parameters
$server = "localhost"
$database = "bai_tool"
$account = "NL31RABO0300087233"

Write-Host "Testing PostgreSQL connection and data patterns..." -ForegroundColor Yellow

try {
    # Test psql connection
    $query = @"
SELECT 
    payment_information_identification,
    batch_entry_reference,
    instruction_id,
    proprietary_code,
    COUNT(*) as count
FROM bai_rabobank_transactions 
WHERE account_identification = '$account'
AND booking_date BETWEEN '2025-11-01' AND '2025-11-10'
GROUP BY payment_information_identification, batch_entry_reference, instruction_id, proprietary_code
ORDER BY proprietary_code, count DESC
LIMIT 20;
"@

    Write-Host "Running enhanced pattern query..." -ForegroundColor Green
    $result = psql -h $server -d $database -c $query 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SUCCESS: Database query completed!" -ForegroundColor Green
        Write-Host "Results:" -ForegroundColor Cyan
        $result | ForEach-Object { Write-Host $_ }
        
        # Check for specific patterns
        Write-Host "`nAnalyzing patterns found:" -ForegroundColor Yellow
        $resultText = $result -join "`n"
        
        if ($resultText -match "586") { Write-Host "FOUND: Transaction type 586" -ForegroundColor Green }
        if ($resultText -match "2065") { Write-Host "FOUND: Transaction type 2065" -ForegroundColor Green }  
        if ($resultText -match "541") { Write-Host "FOUND: Transaction type 541" -ForegroundColor Green }
        if ($resultText -match "626") { Write-Host "FOUND: Transaction type 626" -ForegroundColor Green }
        if ($resultText -match "OM1B") { Write-Host "FOUND: OM1B pattern" -ForegroundColor Green }
        if ($resultText -match "POYTCPE") { Write-Host "FOUND: POYTCPE pattern" -ForegroundColor Green }
        if ($resultText -match "C2025") { Write-Host "FOUND: C-date pattern" -ForegroundColor Green }
        
    } else {
        Write-Host "ERROR: Database query failed!" -ForegroundColor Red
        $result | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    }
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

# Test simple count query
Write-Host "`nTesting simple transaction count..." -ForegroundColor Yellow
try {
    $countQuery = @"
SELECT 
    proprietary_code,
    COUNT(*) as transaction_count
FROM bai_rabobank_transactions 
WHERE account_identification = '$account'
AND booking_date BETWEEN '2025-11-01' AND '2025-11-10'
GROUP BY proprietary_code
ORDER BY transaction_count DESC;
"@

    $countResult = psql -h $server -d $database -c $countQuery 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Transaction counts by type:" -ForegroundColor Green
        $countResult | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "Count query failed:" -ForegroundColor Red
        $countResult | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    }
} catch {
    Write-Host "Count query error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== DATABASE TEST COMPLETE ===" -ForegroundColor Cyan