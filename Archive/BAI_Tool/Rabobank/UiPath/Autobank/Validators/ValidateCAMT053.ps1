param(
    [Parameter(Mandatory=$true)]
    [string]$XmlFilePath
)

function Validate-CAMT053 {
    param([string]$FilePath)
    
    Write-Host "Validating CAMT.053 file: $FilePath" -ForegroundColor Yellow
    
    # Check if file exists
    if (-not (Test-Path $FilePath)) {
        Write-Host "ERROR: File not found: $FilePath" -ForegroundColor Red
        return $false
    }
    
    try {
        # Load XML
        [xml]$xmlDoc = Get-Content $FilePath
        Write-Host "SUCCESS: XML file loaded successfully" -ForegroundColor Green
        
        # Basic structure validation
        $document = $xmlDoc.Document
        if (-not $document) {
            Write-Host "ERROR: Invalid XML structure - missing Document root" -ForegroundColor Red
            return $false
        }
        
        $stmt = $document.BkToCstmrStmt.Stmt
        if (-not $stmt) {
            Write-Host "ERROR: Invalid XML structure - missing Statement" -ForegroundColor Red
            return $false
        }
        
        Write-Host "SUCCESS: Basic XML structure is valid" -ForegroundColor Green
        
        # Balance integrity check
        $balances = $stmt.Bal
        $openingBalance = $null
        $closingBalance = $null
        
        foreach ($bal in $balances) {
            if ($bal.Tp.CdOrPrtry.Cd -eq "OPBD") {
                $openingBalance = [decimal]$bal.Amt.'#text'
                if ($bal.CdtDbtInd -eq "DBIT") { $openingBalance = -$openingBalance }
            }
            elseif ($bal.Tp.CdOrPrtry.Cd -eq "CLBD") {
                $closingBalance = [decimal]$bal.Amt.'#text'
                if ($bal.CdtDbtInd -eq "DBIT") { $closingBalance = -$closingBalance }
            }
        }
        
        Write-Host "Opening Balance: $openingBalance" -ForegroundColor Cyan
        Write-Host "Closing Balance: $closingBalance" -ForegroundColor Cyan
        
        # Transaction integrity check
        $entries = $stmt.Ntry
        $totalCredits = 0
        $totalDebits = 0
        $transactionCount = 0
        
        if ($entries) {
            foreach ($entry in $entries) {
                $amount = [decimal]$entry.Amt.'#text'
                $transactionCount++
                
                if ($entry.CdtDbtInd -eq "CRDT") {
                    $totalCredits += $amount
                } else {
                    $totalDebits += $amount
                }
            }
        }
        
        Write-Host "Transaction Count: $transactionCount" -ForegroundColor Cyan
        Write-Host "Total Credits: $totalCredits" -ForegroundColor Green
        Write-Host "Total Debits: $totalDebits" -ForegroundColor Red
        
        # Balance integrity check
        $calculatedClosing = $openingBalance + $totalCredits - $totalDebits
        Write-Host "Calculated Closing: $calculatedClosing" -ForegroundColor Cyan
        
        if ([Math]::Abs($calculatedClosing - $closingBalance) -lt 0.01) {
            Write-Host "SUCCESS: Balance integrity check PASSED" -ForegroundColor Green
            return $true
        } else {
            Write-Host "ERROR: Balance integrity check FAILED" -ForegroundColor Red
            Write-Host "   Expected: $closingBalance, Calculated: $calculatedClosing" -ForegroundColor Red
            return $false
        }
        
    } catch {
        Write-Host "ERROR: Validation error: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Run validation
$isValid = Validate-CAMT053 -FilePath $XmlFilePath

if ($isValid) {
    Write-Host "`nVALIDATION SUCCESSFUL: The CAMT.053 file is valid!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nVALIDATION FAILED: The CAMT.053 file has issues!" -ForegroundColor Red
    exit 1
}