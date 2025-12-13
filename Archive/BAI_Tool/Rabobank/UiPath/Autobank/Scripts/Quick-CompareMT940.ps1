# Quick MT940 Comparison Script
# Fast comparison focusing on reference differences and key formatting issues

param(
    [Parameter(Mandatory=$true)]
    [string]$OriginalFile,
    
    [Parameter(Mandatory=$true)]
    [string]$GeneratedFile,
    
    [switch]$ShowOnlyDifferences = $false,
    [switch]$FocusOnReferences = $true,
    [int]$MaxTransactions = 10
)

Write-Host "⚡ Quick MT940 Comparison" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Gray

# Check if files exist
if (-not (Test-Path $OriginalFile)) {
    Write-Host "❌ Original file not found: $OriginalFile" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $GeneratedFile)) {
    Write-Host "❌ Generated file not found: $GeneratedFile" -ForegroundColor Red
    exit 1
}

Write-Host "📁 Files:" -ForegroundColor Yellow
Write-Host "   Original:  $OriginalFile" -ForegroundColor White
Write-Host "   Generated: $GeneratedFile" -ForegroundColor White

# Read files
$originalContent = Get-Content $OriginalFile -Raw
$generatedContent = Get-Content $GeneratedFile -Raw

# Extract transactions (lines starting with :61:)
$originalTransactions = ($originalContent -split "`n") | Where-Object { $_ -match "^:61:" }
$generatedTransactions = ($generatedContent -split "`n") | Where-Object { $_ -match "^:61:" }

Write-Host "`n📊 Quick Stats:" -ForegroundColor Yellow
Write-Host "   Original Transactions: $($originalTransactions.Count)" -ForegroundColor White
Write-Host "   Generated Transactions: $($generatedTransactions.Count)" -ForegroundColor White

if ($originalTransactions.Count -ne $generatedTransactions.Count) {
    Write-Host "   ⚠️ Transaction count mismatch!" -ForegroundColor Red
}

# Compare transactions up to MaxTransactions
$compareCount = [Math]::Min($originalTransactions.Count, [Math]::Min($generatedTransactions.Count, $MaxTransactions))

Write-Host "`n🔍 Comparing First $compareCount Transactions:" -ForegroundColor Yellow

$matchCount = 0
$referenceIssues = @()
$formatIssues = @()

for ($i = 0; $i -lt $compareCount; $i++) {
    $original = $originalTransactions[$i].Trim()
    $generated = $generatedTransactions[$i].Trim()
    
    $transactionNum = $i + 1
    
    if ($original -eq $generated) {
        $matchCount++
        if (-not $ShowOnlyDifferences) {
            Write-Host "   ✅ Transaction $transactionNum - MATCH" -ForegroundColor Green
        }
    } else {
        Write-Host "`n   ❌ Transaction $transactionNum - DIFFERENCE" -ForegroundColor Red
        Write-Host "      Original:  $original" -ForegroundColor Gray
        Write-Host "      Generated: $generated" -ForegroundColor Gray
        
        # Analyze specific differences
        if ($FocusOnReferences) {
            # Extract reference parts (after N###)
            if ($original -match "N\d+([A-Z]*)(.*?)(?:\s|$)") {
                $origRef = $matches[1] + $matches[2]
            } else {
                $origRef = "N/A"
            }
            
            if ($generated -match "N\d+([A-Z]*)(.*?)(?:\s|$)") {
                $genRef = $matches[1] + $matches[2]
            } else {
                $genRef = "N/A"
            }
            
            if ($origRef -ne $genRef) {
                $referenceIssues += "Transaction $transactionNum`: '$origRef' → '$genRef'"
            }
        }
        
        # Check for common formatting issues
        if ($original.Length -ne $generated.Length) {
            $formatIssues += "Transaction $transactionNum`: Length difference (${original.Length} vs ${generated.Length})"
        }
        
        if ($original -match "EREF.*EREF" -or $generated -match "EREF.*EREF") {
            $formatIssues += "Transaction $transactionNum`: Double EREF detected"
        }
    }
}

# Summary
Write-Host "`n📈 Comparison Summary:" -ForegroundColor Yellow
Write-Host "   Exact Matches: $matchCount/$compareCount" -ForegroundColor $(if ($matchCount -eq $compareCount) { "Green" } else { "Red" })
Write-Host "   Differences: $($compareCount - $matchCount)/$compareCount" -ForegroundColor $(if ($matchCount -eq $compareCount) { "Green" } else { "Red" })

if ($referenceIssues.Count -gt 0) {
    Write-Host "`n🔗 Reference Issues:" -ForegroundColor Red
    $referenceIssues | ForEach-Object { Write-Host "   • $_" -ForegroundColor Gray }
}

if ($formatIssues.Count -gt 0) {
    Write-Host "`n⚠️ Format Issues:" -ForegroundColor Red
    $formatIssues | ForEach-Object { Write-Host "   • $_" -ForegroundColor Gray }
}

# Check for missing OO9T references
$originalOO9T = ($originalContent | Select-String "OO9T\w+").Matches.Count
$generatedOO9T = ($generatedContent | Select-String "OO9T\w+").Matches.Count

Write-Host "`n🎯 Key Reference Check:" -ForegroundColor Yellow
Write-Host "   OO9T references - Original: $originalOO9T, Generated: $generatedOO9T" -ForegroundColor White

if ($originalOO9T -ne $generatedOO9T) {
    Write-Host "   ❌ OO9T reference count mismatch - Check batch_entry_reference mapping!" -ForegroundColor Red
} else {
    Write-Host "   ✅ OO9T reference count matches" -ForegroundColor Green
}

# Quick pattern analysis
Write-Host "`n📋 Pattern Analysis:" -ForegroundColor Yellow

$patterns = @(
    @{ Name = "N501EREF"; Original = ($originalContent | Select-String "N501EREF").Matches.Count; Generated = ($generatedContent | Select-String "N501EREF").Matches.Count }
    @{ Name = "N586PREF"; Original = ($originalContent | Select-String "N586PREF").Matches.Count; Generated = ($generatedContent | Select-String "N586PREF").Matches.Count }
    @{ Name = "N626"; Original = ($originalContent | Select-String "N626").Matches.Count; Generated = ($generatedContent | Select-String "N626").Matches.Count }
    @{ Name = "Double EREF"; Original = ($originalContent | Select-String "EREF.*EREF").Matches.Count; Generated = ($generatedContent | Select-String "EREF.*EREF").Matches.Count }
)

foreach ($pattern in $patterns) {
    $status = if ($pattern.Original -eq $pattern.Generated) { "✅" } else { "❌" }
    Write-Host "   $status $($pattern.Name): Original=$($pattern.Original), Generated=$($pattern.Generated)" -ForegroundColor $(if ($pattern.Original -eq $pattern.Generated) { "Green" } else { "Red" })
}

# Recommendations
Write-Host "`n💡 Quick Fix Recommendations:" -ForegroundColor Yellow

if ($referenceIssues.Count -gt 0) {
    Write-Host "   🔧 Reference Issues:" -ForegroundColor Cyan
    Write-Host "      • Check batch_entry_reference column in database" -ForegroundColor Gray
    Write-Host "      • Verify GetSafeColumnValue priority order" -ForegroundColor Gray
    Write-Host "      • Run Validate-MT940ReferenceData.ps1 to check data quality" -ForegroundColor Gray
}

if ($originalOO9T -ne $generatedOO9T) {
    Write-Host "   🎯 Missing OO9T References:" -ForegroundColor Cyan
    Write-Host "      • Database query: Check if batch_entry_reference contains OO9T values" -ForegroundColor Gray
    Write-Host "      • VB Code: Verify referenceField assignment uses batch_entry_reference first" -ForegroundColor Gray
}

if (($generatedContent | Select-String "EREF.*EREF").Matches.Count -gt 0) {
    Write-Host "   🚫 Double EREF Issue:" -ForegroundColor Cyan
    Write-Host "      • Fix transaction type code logic to avoid duplicate EREF" -ForegroundColor Gray
    Write-Host "      • Check Select Case statements in VB.NET code" -ForegroundColor Gray
}

Write-Host "`n🏁 Quick comparison completed!" -ForegroundColor Green
Write-Host "   For detailed analysis, run the full Compare-MT940Files.ps1 script" -ForegroundColor Gray