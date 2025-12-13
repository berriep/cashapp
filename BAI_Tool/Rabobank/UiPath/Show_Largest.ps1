# Script to show largest transactions
param([string]$JsonFilePath = "2025-09-30.json")

try {
    Write-Host "=== ANALYSE GROOTSTE TRANSACTIES ===" -ForegroundColor Green
    
    $content = Get-Content $JsonFilePath -Raw
    $pattern = '"transactionAmount"\s*:\s*\{\s*"value"\s*:\s*"([^"]+)"'
    $regexMatches = [regex]::Matches($content, $pattern)
    
    $amounts = @()
    foreach ($match in $regexMatches) {
        $amounts += [decimal]$match.Groups[1].Value
    }
    
    # Sort and show largest negative amounts
    $negativeAmounts = $amounts | Where-Object { $_ -lt 0 } | Sort-Object
    $positiveAmounts = $amounts | Where-Object { $_ -gt 0 } | Sort-Object -Descending
    
    Write-Host "`n=== 10 GROOTSTE NEGATIEVE TRANSACTIES ===" -ForegroundColor Red
    $negativeAmounts | Select-Object -First 10 | ForEach-Object { 
        Write-Host "  $($_.ToString("N2")) EUR" -ForegroundColor Red 
    }
    
    Write-Host "`n=== 10 GROOTSTE POSITIEVE TRANSACTIES ===" -ForegroundColor Green  
    $positiveAmounts | Select-Object -First 10 | ForEach-Object { 
        Write-Host "  +$($_.ToString("N2")) EUR" -ForegroundColor Green 
    }
    
    $total = ($amounts | Measure-Object -Sum).Sum
    Write-Host "`n=== FINALE BEREKENING ===" -ForegroundColor Yellow
    Write-Host "Som van alle 500 transactionAmount.value velden:" -ForegroundColor White
    Write-Host "$($total.ToString("N2")) EUR" -ForegroundColor Yellow -BackgroundColor DarkBlue
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}