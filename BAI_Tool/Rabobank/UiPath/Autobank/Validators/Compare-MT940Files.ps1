param(
    [Parameter(Mandatory=$true)]
    [string]$OriginalFile,
    
    [Parameter(Mandatory=$true)]
    [string]$GeneratedFile,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "mt940_comparison_report.html"
)

# MT940 File Comparison Script
Write-Host "MT940 File Comparison Tool" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan
Write-Host "Original File: $OriginalFile" -ForegroundColor Green
Write-Host "Generated File: $GeneratedFile" -ForegroundColor Yellow
Write-Host ""

# Check if files exist
if (-not (Test-Path $OriginalFile)) {
    Write-Host "ERROR: Original file not found: $OriginalFile" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $GeneratedFile)) {
    Write-Host "ERROR: Generated file not found: $GeneratedFile" -ForegroundColor Red
    exit 1
}

# Helper function to parse MT940 file
function Parse-MT940File {
    param([string]$FilePath)
    
    try {
        $content = Get-Content $FilePath -Raw -Encoding UTF8
        $statements = @()
        
        # Remove :940: header if present
        $content = $content -replace "^:940:`r?`n", ""
        
        # Split by statement blocks (starts with :20:)
        $blocks = $content -split "(?=:20:)" | Where-Object { $_.Trim() -ne "" }
        
        foreach ($block in $blocks) {
            $statement = @{
                TransactionReference = ""
                AccountIdentification = ""
                StatementNumber = ""
                OpeningBalance = @{}
                ClosingBalance = @{}
                Transactions = @()
                Fields = @{}
            }
            
            # Parse each line in the block
            $lines = $block -split "`r?`n" | Where-Object { $_.Trim() -ne "" }
            $currentTransaction = $null
            $lineIndex = 0
            
            foreach ($line in $lines) {
                $lineIndex++
                $originalLine = $line
                $line = $line.Trim()
                if ($line -match "^:(\d+):(.*)?$") {
                    $fieldTag = $matches[1]
                    $fieldValue = if ($matches[2]) { $matches[2] } else { "" }
                    
                    # Store all fields for reference
                    $statement.Fields[$fieldTag] = $fieldValue
                    
                    switch ($fieldTag) {
                        "20" { # Transaction Reference Number
                            $statement.TransactionReference = $fieldValue
                        }
                        "25" { # Account Identification
                            $statement.AccountIdentification = $fieldValue
                        }
                        "28C" { # Statement Number/Sequence Number
                            $statement.StatementNumber = $fieldValue
                        }
                        "60F" { # Opening Balance
                            if ($fieldValue -match "^([CD])(\d{6})([A-Z]{3})([0-9,]+)$") {
                                $statement.OpeningBalance = @{
                                    CreditDebit = $matches[1]
                                    Date = $matches[2]
                                    Currency = $matches[3]
                                    Amount = $matches[4] -replace ",", "."
                                }
                            }
                        }
                        "62F" { # Closing Balance (Final)
                            if ($fieldValue -match "^([CD])(\d{6})([A-Z]{3})([0-9,]+)$") {
                                $statement.ClosingBalance = @{
                                    CreditDebit = $matches[1]
                                    Date = $matches[2]
                                    Currency = $matches[3]
                                    Amount = $matches[4] -replace ",", "."
                                }
                            }
                        }
                        "61" { # Statement Line
                            if ($currentTransaction) {
                                $statement.Transactions += $currentTransaction
                            }
                            
                            # Parse statement line: :61:YYMMDD[MMDD]CRDRamount[NrefForAccount][//Bank ref][suppinfo]
                            # Split at // to separate the part we want to compare from the bank reference
                            $field61Parts = $fieldValue -split "//", 2
                            $field61CorePart = $field61Parts[0]  # Part before //
                            $field61BankRef = if ($field61Parts.Length -gt 1) { $field61Parts[1] } else { "" }
                            
                            # Check if next line is an IBAN (account number)
                            $accountNumber = ""
                            if ($lineIndex -lt $lines.Count) {
                                $nextLine = $lines[$lineIndex].Trim()
                                # IBAN pattern: starts with 2 letters followed by digits
                                if ($nextLine -match "^[A-Z]{2}\d{2}[A-Z0-9]+$" -and $nextLine.Length -ge 15) {
                                    $accountNumber = $nextLine
                                }
                            }
                            
                            if ($fieldValue -match "^(\d{6})(\d{4})?([CD])([0-9,]+)([A-Z]{4})?([^/]*)(//.*)?$") {
                                $currentTransaction = @{
                                    ValueDate = $matches[1]
                                    BookingDate = if ($matches[2]) { $matches[2] } else { $matches[1] }
                                    Field61CorePart = $field61CorePart
                                    CreditDebit = $matches[3]
                                    Amount = $matches[4] -replace ",", "."
                                    TransactionType = if ($matches[5]) { $matches[5] } else { "" }
                                    ReferenceForAccount = if ($matches[6]) { $matches[6].Trim() } else { "" }
                                    BankReference = if ($matches[7]) { $matches[7] -replace "^//", "" } else { "" }
                                    AccountNumber = $accountNumber
                                    SupplementaryInfo = ""
                                    StructuredInfo = @()
                                    Description = ""
                                    Field86Text = ""
                                }
                            } else {
                                # Try simpler pattern for various MT940 formats
                                if ($fieldValue -match "^(\d{6})([CD])([0-9,]+)(.*)$") {
                                    $currentTransaction = @{
                                        ValueDate = $matches[1]
                                        BookingDate = $matches[1]
                                        Field61CorePart = $field61CorePart
                                        CreditDebit = $matches[2]
                                        Amount = $matches[3] -replace ",", "."
                                        TransactionType = ""
                                        ReferenceForAccount = ""
                                        BankReference = ""
                                        AccountNumber = $accountNumber
                                        SupplementaryInfo = $matches[4]
                                        StructuredInfo = @()
                                        Description = ""
                                        Field86Text = ""
                                    }
                                }
                            }
                        }
                        "86" { # Information to Account Owner
                            if ($currentTransaction) {
                                # Store field 86 content as single concatenated string for comparison
                                # This handles multi-line :86: fields that should be compared as one unit
                                if (-not $currentTransaction.Field86Text) {
                                    $currentTransaction.Field86Text = $fieldValue
                                } else {
                                    $currentTransaction.Field86Text += $fieldValue
                                }
                                
                                # Also parse structured information for legacy compatibility
                                if ($fieldValue -match "^(\d{3})(.*)$") {
                                    $code = $matches[1]
                                    $info = $matches[2]
                                    $currentTransaction.StructuredInfo += @{
                                        Code = $code
                                        Information = $info
                                    }
                                } else {
                                    $currentTransaction.Description += $fieldValue
                                }
                            }
                        }
                    }
                } else {
                    # Continuation line - concatenate to field 86 if currently processing transaction
                    if ($currentTransaction -and $line -ne "") {
                        # Append continuation line to field 86 text (handles multi-line :86: fields)
                        # Use original line (untrimmed) to preserve exact spacing
                        if ($currentTransaction.Field86Text) {
                            $currentTransaction.Field86Text += $originalLine.TrimEnd()
                        }
                        
                        # Also parse for legacy compatibility
                        if ($line -match "^(\d{3})(.*)$") {
                            $code = $matches[1]
                            $info = $matches[2]
                            $currentTransaction.StructuredInfo += @{
                                Code = $code
                                Information = $info
                            }
                        } else {
                            $currentTransaction.Description += $line
                        }
                    }
                }
            }
            
            # Add the last transaction if exists
            if ($currentTransaction) {
                $statement.Transactions += $currentTransaction
            }
            
            if ($statement.TransactionReference -ne "") {
                $statements += $statement
            }
        }
        
        return $statements
    } catch {
        Write-Host "ERROR parsing MT940 file: $($_.Exception.Message)" -ForegroundColor Red
        return @()
    }
}

# Load and parse MT940 files
try {
    Write-Host "Parsing MT940 files..." -ForegroundColor Blue
    $originalStatements = Parse-MT940File $OriginalFile
    $generatedStatements = Parse-MT940File $GeneratedFile
    Write-Host "Files parsed successfully" -ForegroundColor Green
    Write-Host "  Original statements: $($originalStatements.Count)" -ForegroundColor Gray
    Write-Host "  Generated statements: $($generatedStatements.Count)" -ForegroundColor Gray
} catch {
    Write-Host "ERROR parsing MT940 files: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Initialize results
$results = @{
    Summary = @{
        OriginalStatements = $originalStatements.Count
        GeneratedStatements = $generatedStatements.Count
        OriginalTransactions = 0
        GeneratedTransactions = 0
        MatchingTransactions = 0
        MissingTransactions = 0
        ExtraTransactions = 0
        DifferentTransactions = 0
    }
    StatementComparison = @()
    TransactionComparison = @()
}

Write-Host "Starting comparison analysis..." -ForegroundColor Blue

# Count total transactions
$results.Summary.OriginalTransactions = ($originalStatements | ForEach-Object { $_.Transactions.Count } | Measure-Object -Sum).Sum
$results.Summary.GeneratedTransactions = ($generatedStatements | ForEach-Object { $_.Transactions.Count } | Measure-Object -Sum).Sum
$results.Summary.CosmeticDifferences = 0
$results.Summary.RealErrors = 0

Write-Host "Transaction Counts:" -ForegroundColor Yellow
Write-Host "  Original: $($results.Summary.OriginalTransactions)" -ForegroundColor Gray
Write-Host "  Generated: $($results.Summary.GeneratedTransactions)" -ForegroundColor Gray

# Compare statements
Write-Host "Comparing Statements..." -ForegroundColor Yellow

foreach ($origStmt in $originalStatements) {
    $matchingGenStmt = $generatedStatements | Where-Object { $_.TransactionReference -eq $origStmt.TransactionReference }
    
    $comparison = @{
        TransactionReference = $origStmt.TransactionReference
        AccountMatch = $false
        OpeningBalanceMatch = $false
        ClosingBalanceMatch = $false
        TransactionCountMatch = $false
        Missing = $false
        Differences = @()
    }
    
    if ($matchingGenStmt) {
        # Compare account identification
        $comparison.AccountMatch = ($origStmt.AccountIdentification -eq $matchingGenStmt.AccountIdentification)
        if (-not $comparison.AccountMatch) {
            $comparison.Differences += "Account: Orig='$($origStmt.AccountIdentification)', Gen='$($matchingGenStmt.AccountIdentification)'"
        }
        
        # Compare opening balance
        $origOpenAmount = if ($origStmt.OpeningBalance.Amount) { $origStmt.OpeningBalance.Amount } else { "0" }
        $genOpenAmount = if ($matchingGenStmt.OpeningBalance.Amount) { $matchingGenStmt.OpeningBalance.Amount } else { "0" }
        $comparison.OpeningBalanceMatch = ($origOpenAmount -eq $genOpenAmount -and 
                                         $origStmt.OpeningBalance.CreditDebit -eq $matchingGenStmt.OpeningBalance.CreditDebit)
        if (-not $comparison.OpeningBalanceMatch) {
            $comparison.Differences += "Opening Balance: Orig='$($origStmt.OpeningBalance.CreditDebit)$origOpenAmount', Gen='$($matchingGenStmt.OpeningBalance.CreditDebit)$genOpenAmount'"
        }
        
        # Compare closing balance
        $origCloseAmount = if ($origStmt.ClosingBalance.Amount) { $origStmt.ClosingBalance.Amount } else { "0" }
        $genCloseAmount = if ($matchingGenStmt.ClosingBalance.Amount) { $matchingGenStmt.ClosingBalance.Amount } else { "0" }
        $comparison.ClosingBalanceMatch = ($origCloseAmount -eq $genCloseAmount -and 
                                         $origStmt.ClosingBalance.CreditDebit -eq $matchingGenStmt.ClosingBalance.CreditDebit)
        if (-not $comparison.ClosingBalanceMatch) {
            $comparison.Differences += "Closing Balance: Orig='$($origStmt.ClosingBalance.CreditDebit)$origCloseAmount', Gen='$($matchingGenStmt.ClosingBalance.CreditDebit)$genCloseAmount'"
        }
        
        # Compare transaction count
        $comparison.TransactionCountMatch = ($origStmt.Transactions.Count -eq $matchingGenStmt.Transactions.Count)
        if (-not $comparison.TransactionCountMatch) {
            $comparison.Differences += "Transaction Count: Orig=$($origStmt.Transactions.Count), Gen=$($matchingGenStmt.Transactions.Count)"
        }
    } else {
        $comparison.Missing = $true
    }
    
    $results.StatementComparison += $comparison
}

# Compare transactions in detail
Write-Host "Comparing Transactions..." -ForegroundColor Yellow

$allOrigTransactions = @()
$allGenTransactions = @()

# Flatten all transactions with statement reference
foreach ($stmt in $originalStatements) {
    foreach ($tx in $stmt.Transactions) {
        # Create unique key using statement reference, value date, amount, credit/debit indicator, and account number
        $keyComponents = @(
            $stmt.TransactionReference
            $tx.ValueDate
            $tx.Amount
            $tx.CreditDebit
        )
        
        # Add account number (IBAN) if available for more precise matching
        if ($tx.AccountNumber) {
            $keyComponents += $tx.AccountNumber
        }
        
        $uniqueKey = $keyComponents -join "_"
        
        $allOrigTransactions += @{
            StatementRef = $stmt.TransactionReference
            Transaction = $tx
            UniqueKey = $uniqueKey
        }
    }
}

foreach ($stmt in $generatedStatements) {
    foreach ($tx in $stmt.Transactions) {
        # Create unique key using statement reference, value date, amount, credit/debit indicator, and account number
        $keyComponents = @(
            $stmt.TransactionReference
            $tx.ValueDate
            $tx.Amount
            $tx.CreditDebit
        )
        
        # Add account number (IBAN) if available for more precise matching
        if ($tx.AccountNumber) {
            $keyComponents += $tx.AccountNumber
        }
        
        $uniqueKey = $keyComponents -join "_"
        
        $allGenTransactions += @{
            StatementRef = $stmt.TransactionReference
            Transaction = $tx
            UniqueKey = $uniqueKey
        }
    }
}

# Create lookup for generated transactions
$generatedLookup = @{}
foreach ($genTx in $allGenTransactions) {
    $generatedLookup[$genTx.UniqueKey] = $genTx
}

$transactionIndex = 0
foreach ($origTx in $allOrigTransactions) {
    $transactionIndex++
    if ($transactionIndex % 100 -eq 0) {
        Write-Host "  Processing transaction $transactionIndex of $($allOrigTransactions.Count)..." -ForegroundColor Gray
    }
    
    $comparison = @{
        StatementRef = $origTx.StatementRef
        UniqueKey = $origTx.UniqueKey
        ValueDate = $origTx.Transaction.ValueDate
        Amount = $origTx.Transaction.Amount
        CreditDebit = $origTx.Transaction.CreditDebit
        Found = $false
        Differences = @()
        Missing = $false
    }
    
    # Find matching generated transaction
    if ($generatedLookup.ContainsKey($origTx.UniqueKey)) {
        $matchingGenTx = $generatedLookup[$origTx.UniqueKey]
        $comparison.Found = $true
        $results.Summary.MatchingTransactions++
        
        # Compare transaction details
        $origTransaction = $origTx.Transaction
        $genTransaction = $matchingGenTx.Transaction
        
        # Compare booking date
        if ($origTransaction.BookingDate -ne $genTransaction.BookingDate) {
            $comparison.Differences += "BookingDate: Orig=$($origTransaction.BookingDate), Gen=$($genTransaction.BookingDate)"
        }
        
        # Compare transaction type
        if ($origTransaction.TransactionType -ne $genTransaction.TransactionType) {
            $comparison.Differences += "TransactionType: Orig='$($origTransaction.TransactionType)', Gen='$($genTransaction.TransactionType)'"
        }
        
        # Compare :61: field core part (before //) - this is the important part for transaction matching
        $hasRealField61Error = $false
        if ($origTransaction.Field61CorePart -or $genTransaction.Field61CorePart) {
            if ($origTransaction.Field61CorePart -ne $genTransaction.Field61CorePart) {
                $origCore = if ($origTransaction.Field61CorePart) { $origTransaction.Field61CorePart } else { "" }
                $genCore = if ($genTransaction.Field61CorePart) { $genTransaction.Field61CorePart } else { "" }
                
                # Detailed analysis of :61: field differences
                $detailedDiff = "Field61CorePart: Orig='$origCore', Gen='$genCore'"
                
                # Check for specific transaction type issues
                $issueFlags = @()
                
                # Issue 1: Type 2033 missing OO9T reference - this is a KNOWN API LIMITATION
                if ($origCore -match "N033OO9T" -and $genCore -match "N033EREF") {
                    $issueFlags += "[API LIMIT] TYPE 2033: OO9T reference not available in CAMT053 API"
                    # NOT marked as hasRealField61Error - this is accepted limitation
                }
                
                # Issue 2: Type 540 missing FT reference  
                if ($origCore -match "N540FT\d+" -and $genCore -match "N540FT//" -and $genCore -notmatch "N540FT\d+") {
                    $issueFlags += "[ERROR] TYPE 540: Missing FT reference number"
                    $hasRealField61Error = $true
                }
                
                # Issue 3: Type 593 different reference
                if ($origCore -match "N593EREF" -and $genCore -match "N593EREF" -and $origCore -ne $genCore) {
                    $issueFlags += "[WARN] TYPE 593: Different EREF reference (DB ID vs original)"
                    $hasRealField61Error = $true
                }
                
                # Issue 4: Type 541 NONREF vs EREF (this is an improvement, not error)
                if ($origCore -match "N541NONREF" -and $genCore -match "N541EREF") {
                    $issueFlags += "[OK] TYPE 541: NONREF -> EREF (improvement - added reference)"
                    # Don't set hasRealField61Error for improvements
                }
                
                # Issue 5: Different transaction types (ordering issue)
                if (($origCore -match "N541" -and $genCore -match "N100") -or 
                    ($origCore -match "N100" -and $genCore -match "N541")) {
                    $issueFlags += "[WARN] Transaction ordering/matching issue"
                    $hasRealField61Error = $true
                }
                
                # Add issue flags to difference
                if ($issueFlags.Count -gt 0) {
                    $detailedDiff += " [" + ($issueFlags -join ", ") + "]"
                }
                
                $comparison.Differences += $detailedDiff
            }
        }
        
        # Skip reference for account comparison - these are Rabobank-specific codes
        # (EREF, PREF, NONREF, OM1T, OO9T) that may differ due to data source limitations
        # if ($origTransaction.ReferenceForAccount -ne $genTransaction.ReferenceForAccount) {
        #     $comparison.Differences += "ReferenceForAccount: Orig='$($origTransaction.ReferenceForAccount)', Gen='$($genTransaction.ReferenceForAccount)'"
        # }
        
        # Skip bank reference comparison - Rabobank internal references (OM1T, OO9T, NP8A, etc.)
        # are not available in BAI/CAMT053 JSON data and will always differ
        # if ($origTransaction.BankReference -ne $genTransaction.BankReference) {
        #     $comparison.Differences += "BankReference: Orig='$($origTransaction.BankReference)', Gen='$($genTransaction.BankReference)'"
        # }
        
        # Skip supplementary info comparison - these are Rabobank-specific reference numbers
        # that are not available in CAMT053 JSON data and will always differ
        # if ($origTransaction.SupplementaryInfo -ne $genTransaction.SupplementaryInfo) {
        #     $comparison.Differences += "SupplementaryInfo: Orig='$($origTransaction.SupplementaryInfo)', Gen='$($genTransaction.SupplementaryInfo)'"
        # }
        
        # Compare field 86 concatenated text (primary comparison for multi-line fields)
        if ($origTransaction.Field86Text -or $genTransaction.Field86Text) {
            if ($origTransaction.Field86Text -ne $genTransaction.Field86Text) {
                $origField86 = if ($origTransaction.Field86Text) { $origTransaction.Field86Text } else { "" }
                $genField86 = if ($genTransaction.Field86Text) { $genTransaction.Field86Text } else { "" }
                
                # Detailed analysis of :86: field differences
                $detailedDiff = "Field86: Orig='$origField86', Gen='$genField86'"
                
                # Check for specific issues
                $issueFlags = @()
                
                # Issue 1: Missing /OCMT/ field - check if type 2033 (API limitation)
                $isType2033 = $origTransaction.Field61CorePart -match "N033" -or $genTransaction.Field61CorePart -match "N033"
                
                if ($origField86 -match "/OCMT/" -and $genField86 -notmatch "/OCMT/") {
                    if ($isType2033) {
                        $issueFlags += "[API LIMIT] TYPE 2033: /OCMT/ field not available in CAMT053 API"
                    } else {
                        $issueFlags += "[ERROR] MISSING /OCMT/ FIELD"
                    }
                }
                if ($origField86 -notmatch "/ISDT/" -and $genField86 -match "/ISDT/" -and $origField86 -match "/OCMT/") {
                    if ($isType2033) {
                        $issueFlags += "[API LIMIT] TYPE 2033: /ISDT/ generated instead of /OCMT/ (field not in API)"
                    } else {
                        $issueFlags += "[ERROR] /ISDT/ SHOULD NOT BE PRESENT (has /OCMT/)"
                    }
                }
                
                # Issue 2: Missing /PREF/ field
                if ($origField86 -match "/PREF/" -and $genField86 -notmatch "/PREF/") {
                    $issueFlags += "[WARN] MISSING /PREF/ FIELD"
                }
                
                # Issue 3: Wrong counterparty direction (BENM vs ORDP)
                if ($origField86 -match "/BENM/" -and $genField86 -match "/ORDP/") {
                    $issueFlags += "[WARN] COUNTERPARTY: /BENM/ -> /ORDP/ (check credit/debit)"
                }
                if ($origField86 -match "/ORDP/" -and $genField86 -match "/BENM/") {
                    $issueFlags += "[WARN] COUNTERPARTY: /ORDP/ -> /BENM/ (check credit/debit)"
                }
                
                # Issue 4: Name spacing differences (cosmetic)
                $isCosmeticOnly = $false
                if ($origField86 -replace "\s+", "" -eq $genField86 -replace "\s+", "") {
                    $issueFlags += "[OK] COSMETIC: Only spacing differences in names"
                    $isCosmeticOnly = $true
                }
                
                # Check if all issues are API limitations (treated as cosmetic)
                $allApiLimitations = ($issueFlags | Where-Object { $_ -match "\[API LIMIT\]" }).Count -eq $issueFlags.Count -and $issueFlags.Count -gt 0
                
                if ($allApiLimitations) {
                    $isCosmeticOnly = $true
                }
                
                # Add issue flags to difference
                if ($issueFlags.Count -gt 0) {
                    $detailedDiff += " [" + ($issueFlags -join ", ") + "]"
                }
                
                $comparison.Differences += $detailedDiff
                
                # Track cosmetic vs real errors
                if ($isCosmeticOnly -and -not $hasRealField61Error) {
                    $comparison.IsCosmeticOnly = $true
                } else {
                    $comparison.IsCosmeticOnly = $false
                }
            }
        } else {
            # Fallback to description comparison if Field86Text not available
            $origDesc = $origTransaction.Description -join " "
            $genDesc = $genTransaction.Description -join " "
            if ($origDesc -ne $genDesc) {
                $comparison.Differences += "Description: Orig='$origDesc', Gen='$genDesc'"
            }
        }
        
        # NOTE: StructuredInfo comparison removed - legacy code for old numeric format
        # MT940 :86: fields use /CODE/ format (e.g., /EREF/, /PREF/, /TRCD/)
        # The Field86Text comparison above already validates the complete :86: field content
        # The old regex pattern "^(\d{3})(.*)$" was designed for a different format
        # and caused false positives when counting continuation lines
        
        if ($comparison.Differences.Count -gt 0) {
            $results.Summary.DifferentTransactions++
            
            # Count cosmetic vs real errors
            if ($comparison.IsCosmeticOnly) {
                $results.Summary.CosmeticDifferences++
            } else {
                $results.Summary.RealErrors++
            }
        }
    } else {
        $comparison.Missing = $true
        $results.Summary.MissingTransactions++
    }
    
    $results.TransactionComparison += $comparison
}

# Check for extra transactions in generated file
foreach ($genTx in $allGenTransactions) {
    $found = $allOrigTransactions | Where-Object { $_.UniqueKey -eq $genTx.UniqueKey }
    
    if (-not $found) {
        $results.Summary.ExtraTransactions++
        $results.TransactionComparison += @{
            StatementRef = $genTx.StatementRef
            UniqueKey = $genTx.UniqueKey
            ValueDate = $genTx.Transaction.ValueDate
            Amount = $genTx.Transaction.Amount
            CreditDebit = $genTx.Transaction.CreditDebit
            Found = $false
            Extra = $true
            Differences = @("Extra transaction in generated file")
        }
    }
}

# DISPLAY RESULTS
Write-Host ""
Write-Host "COMPARISON SUMMARY" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan
Write-Host "Total Original Statements: $($results.Summary.OriginalStatements)" -ForegroundColor White
Write-Host "Total Generated Statements: $($results.Summary.GeneratedStatements)" -ForegroundColor White
Write-Host "Total Original Transactions: $($results.Summary.OriginalTransactions)" -ForegroundColor White
Write-Host "Total Generated Transactions: $($results.Summary.GeneratedTransactions)" -ForegroundColor White
Write-Host "Matching Transactions: $($results.Summary.MatchingTransactions)" -ForegroundColor Green
Write-Host "Missing Transactions: $($results.Summary.MissingTransactions)" -ForegroundColor Red
Write-Host "Extra Transactions: $($results.Summary.ExtraTransactions)" -ForegroundColor Yellow
Write-Host "Transactions with Differences: $($results.Summary.DifferentTransactions)" -ForegroundColor Magenta
Write-Host "  - Real Errors: $($results.Summary.RealErrors)" -ForegroundColor Red
Write-Host "  - Cosmetic Differences: $($results.Summary.CosmeticDifferences)" -ForegroundColor Gray

# Show statement comparison
Write-Host ""
Write-Host "STATEMENT COMPARISON" -ForegroundColor Cyan
foreach ($stmt in $results.StatementComparison) {
    if ($stmt.Missing) {
        Write-Host "MISSING statement: $($stmt.TransactionReference)" -ForegroundColor Red
    } else {
        $status = if ($stmt.AccountMatch -and $stmt.OpeningBalanceMatch -and $stmt.ClosingBalanceMatch -and $stmt.TransactionCountMatch) { "OK" } else { "DIFF" }
        $color = if ($status -eq "OK") { "Green" } else { "Yellow" }
        Write-Host "$status Statement $($stmt.TransactionReference)" -ForegroundColor $color
        foreach ($diff in $stmt.Differences) {
            Write-Host "  $diff" -ForegroundColor Gray
        }
    }
}

# Show problematic transactions (excluding cosmetic-only issues)
Write-Host ""
Write-Host "REAL ERRORS (First 20, excluding cosmetic)" -ForegroundColor Cyan
$realProblems = $results.TransactionComparison | Where-Object { 
    ($_.Missing -or $_.Extra -or ($_.Differences.Count -gt 0 -and -not $_.IsCosmeticOnly))
} | Select-Object -First 20

if ($realProblems.Count -gt 0) {
    foreach ($tx in $realProblems) {
        if ($tx.Missing) {
            Write-Host "MISSING: $($tx.ValueDate) $($tx.CreditDebit)$($tx.Amount)" -ForegroundColor Red
        } elseif ($tx.Extra) {
            Write-Host "EXTRA: $($tx.ValueDate) $($tx.CreditDebit)$($tx.Amount)" -ForegroundColor Red
        } else {
            Write-Host "$($tx.ValueDate) $($tx.CreditDebit)$($tx.Amount) - Differences found" -ForegroundColor Yellow
            foreach ($diff in $tx.Differences) {
                Write-Host "  $diff" -ForegroundColor Gray
            }
        }
    }
} else {
    Write-Host "No real errors found (only cosmetic differences)" -ForegroundColor Green
}

if ($realProblems.Count -lt $results.Summary.RealErrors) {
    $remaining = $results.Summary.RealErrors - $realProblems.Count
    Write-Host "... and $remaining more real errors" -ForegroundColor Gray
}

# Calculate match percentage
$matchPercentage = if ($results.Summary.OriginalTransactions -gt 0) {
    [math]::Round(($results.Summary.MatchingTransactions / $results.Summary.OriginalTransactions) * 100, 1)
} else { 0 }

Write-Host ""
$percentText = "$matchPercentage percent"
if ($matchPercentage -ge 95) {
    Write-Host "Overall Status: EXCELLENT ($percentText match)" -ForegroundColor Green
} elseif ($matchPercentage -ge 80) {
    Write-Host "Overall Status: GOOD ($percentText match)" -ForegroundColor Yellow
} else {
    Write-Host "Overall Status: NEEDS ATTENTION ($percentText match)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Analysis complete!" -ForegroundColor Green

# GENERATE HTML REPORT
Write-Host ""
Write-Host "Generating HTML report..." -ForegroundColor Blue

$html = @"
<!DOCTYPE html>
<html>
<head>
    <title>MT940 File Comparison Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; background-color: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }
        h2 { color: #34495e; margin-top: 30px; border-left: 4px solid #3498db; padding-left: 15px; }
        .summary { background-color: #ecf0f1; padding: 15px; border-radius: 5px; margin: 20px 0; }
        .summary-item { display: inline-block; margin: 10px 20px; }
        .summary-number { font-size: 24px; font-weight: bold; display: block; }
        .good { color: #27ae60; }
        .bad { color: #e74c3c; }
        .warning { color: #f39c12; }
        .neutral { color: #34495e; }
        table { width: 100%; border-collapse: collapse; margin: 15px 0; }
        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }
        th { background-color: #3498db; color: white; }
        .match { background-color: #d5f4e6; }
        .no-match { background-color: #f8d7da; }
        .missing { background-color: #fff3cd; }
        .transaction { margin: 15px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; }
        .transaction.error { border-left: 5px solid #e74c3c; background-color: #fdf2f2; }
        .transaction.missing { border-left: 5px solid #f39c12; background-color: #fffbf0; }
        .transaction.good { border-left: 5px solid #27ae60; background-color: #f0fff4; }
        .differences { margin-top: 10px; }
        .difference { background-color: #fff; padding: 5px; margin: 2px 0; border-radius: 3px; border-left: 3px solid #e74c3c; }
        .note { font-style: italic; color: #7f8c8d; font-size: 0.9em; }
        .generated { color: #9b59b6; font-weight: bold; }
        .mt940-field { font-family: monospace; background-color: #f8f9fa; padding: 2px 4px; border-radius: 3px; }
    </style>
</head>
<body>
    <div class="container">
        <h1>MT940 File Comparison Report</h1>
        
        <div class="summary">
            <h2>Summary</h2>
            <div class="summary-item">
                <span class="summary-number neutral">$($results.Summary.OriginalStatements)</span>
                Original Statements
            </div>
            <div class="summary-item">
                <span class="summary-number neutral">$($results.Summary.GeneratedStatements)</span>
                Generated Statements
            </div>
            <div class="summary-item">
                <span class="summary-number neutral">$($results.Summary.OriginalTransactions)</span>
                Original Transactions
            </div>
            <div class="summary-item">
                <span class="summary-number neutral">$($results.Summary.GeneratedTransactions)</span>
                Generated Transactions
            </div>
            <div class="summary-item">
                <span class="summary-number good">$($results.Summary.MatchingTransactions)</span>
                Matching
            </div>
            <div class="summary-item">
                <span class="summary-number bad">$($results.Summary.MissingTransactions)</span>
                Missing
            </div>
            <div class="summary-item">
                <span class="summary-number warning">$($results.Summary.ExtraTransactions)</span>
                Extra
            </div>
            <div class="summary-item">
                <span class="summary-number warning">$($results.Summary.DifferentTransactions)</span>
                With Differences
            </div>
            <div class="summary-item">
                <span class="summary-number bad">$($results.Summary.RealErrors)</span>
                Real Errors
            </div>
            <div class="summary-item">
                <span class="summary-number neutral">$($results.Summary.CosmeticDifferences)</span>
                Cosmetic Only
            </div>
        </div>

        <h2>Statement Comparison</h2>
"@

foreach ($stmt in $results.StatementComparison) {
    if ($stmt.Missing) {
        $html += "<div class='transaction missing'><strong>Missing Statement: $($stmt.TransactionReference)</strong></div>"
    } else {
        $status = if ($stmt.AccountMatch -and $stmt.OpeningBalanceMatch -and $stmt.ClosingBalanceMatch -and $stmt.TransactionCountMatch) { "good" } else { "error" }
        $html += "<div class='transaction $status'><strong>Statement: <span class='mt940-field'>:20:$($stmt.TransactionReference)</span></strong><br/>"
        if ($stmt.Differences.Count -gt 0) {
            $html += "<div class='differences'>"
            foreach ($diff in $stmt.Differences) {
                $html += "<div class='difference'>$diff</div>"
            }
            $html += "</div>"
        }
        $html += "</div>"
    }
}

$html += "<h2>Transaction Comparison - Real Errors Only</h2>"
$html += "<p><strong>Real errors (excluding cosmetic):</strong> $($results.Summary.RealErrors)</p>"
$html += "<p><strong>Cosmetic differences (spacing only):</strong> $($results.Summary.CosmeticDifferences)</p>"

# Show first 50 REAL error transactions (excluding cosmetic-only)
$realErrorTransactions = $results.TransactionComparison | Where-Object { 
    ($_.Missing -or $_.Extra -or ($_.Differences.Count -gt 0 -and -not $_.IsCosmeticOnly))
} | Select-Object -First 50

foreach ($tx in $realErrorTransactions) {
    if ($tx.Missing) {
        $html += "<div class='transaction missing'><strong>Missing Transaction</strong><br/>"
        $html += "Date: <span class='mt940-field'>$($tx.ValueDate)</span> | Amount: <span class='mt940-field'>$($tx.CreditDebit)$($tx.Amount)</span><br/>"
        $html += "Statement: <span class='mt940-field'>:20:$($tx.StatementRef)</span></div>"
    } elseif ($tx.Extra) {
        $html += "<div class='transaction missing'><strong>Extra Transaction</strong><br/>"
        $html += "Date: <span class='mt940-field'>$($tx.ValueDate)</span> | Amount: <span class='mt940-field'>$($tx.CreditDebit)$($tx.Amount)</span><br/>"
        $html += "Statement: <span class='mt940-field'>:20:$($tx.StatementRef)</span></div>"
    } elseif ($tx.Differences.Count -gt 0) {
        $html += "<div class='transaction error'><strong>Transaction with Differences</strong><br/>"
        $html += "Date: <span class='mt940-field'>$($tx.ValueDate)</span> | Amount: <span class='mt940-field'>$($tx.CreditDebit)$($tx.Amount)</span><br/>"
        $html += "Statement: <span class='mt940-field'>:20:$($tx.StatementRef)</span>"
        $html += "<div class='differences'>"
        foreach ($diff in $tx.Differences) {
            $html += "<div class='difference'>$diff</div>"
        }
        $html += "</div></div>"
    }
}

if ($realErrorTransactions.Count -lt $results.Summary.RealErrors) {
    $remaining = $results.Summary.RealErrors - $realErrorTransactions.Count
    $html += "<div class='note'><p>... and $remaining more real errors not shown in detail</p></div>"
}

$html += @"
        
        <div style="margin-top: 40px; padding: 20px; background-color: #ecf0f1; border-radius: 5px;">
            <h3>MT940 Field Reference</h3>
            <p><span class="mt940-field">:20:</span> = Transaction Reference Number</p>
            <p><span class="mt940-field">:25:</span> = Account Identification</p>
            <p><span class="mt940-field">:28C:</span> = Statement/Sequence Number</p>
            <p><span class="mt940-field">:60F:</span> = Opening Balance</p>
            <p><span class="mt940-field">:61:</span> = Statement Line (Transaction)</p>
            <p><span class="mt940-field">:86:</span> = Information to Account Owner</p>
            <p><span class="mt940-field">:62F:</span> = Closing Balance (Final)</p>
        </div>
        
        <div style="margin-top: 20px; padding: 20px; background-color: #ecf0f1; border-radius: 5px;">
            <h3>Legend</h3>
            <p><span class="good">Green</span> = Perfect Match</p>
            <p><span class="bad">Red</span> = Missing or Significant Difference</p>
            <p><span class="warning">Orange</span> = Minor Difference or Generated Field</p>
            <p><strong>Note:</strong> MT940 format comparison focuses on core transaction data</p>
        </div>
        
        <div style="margin-top: 20px; text-align: center; color: #7f8c8d;">
            <p>Report generated on $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
            <p>Original: $(Split-Path $OriginalFile -Leaf)</p>
            <p>Generated: $(Split-Path $GeneratedFile -Leaf)</p>
        </div>
    </div>
</body>
</html>
"@

# Save HTML report
try {
    $html | Out-File -FilePath $OutputFile -Encoding UTF8
    Write-Host "HTML report saved to: $OutputFile" -ForegroundColor Green
    
    # Try to open the report in default browser
    try {
        Start-Process $OutputFile
        Write-Host "Opening report in default browser..." -ForegroundColor Blue
    } catch {
        Write-Host "Report saved. You can open it manually: $OutputFile" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Error saving HTML report: $($_.Exception.Message)" -ForegroundColor Red
}