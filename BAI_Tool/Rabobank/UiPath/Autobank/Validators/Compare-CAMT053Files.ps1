param(
    [Parameter(Mandatory=$true)]
    [string]$OriginalFile,
    
    [Parameter(Mandatory=$true)]
    [string]$GeneratedFile,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "comparison_report.html"
)

# CAMT.053 File Comparison Script
Write-Host "CAMT.053 File Comparison Tool" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
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

# Load XML files
try {
    Write-Host "Loading XML files..." -ForegroundColor Blue
    $originalContent = Get-Content $OriginalFile -Raw -Encoding UTF8
    $generatedContent = Get-Content $GeneratedFile -Raw -Encoding UTF8
    
    [xml]$originalXml = $originalContent
    [xml]$generatedXml = $generatedContent
    Write-Host "Files loaded successfully" -ForegroundColor Green
} catch {
    Write-Host "ERROR loading XML files: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Helper function to extract CAMT version from namespace
function Get-CamtVersion {
    param([xml]$xml)
    try {
        $namespace = $xml.DocumentElement.NamespaceURI
        if ($namespace -match 'camt\.053\.001\.(\d+)') {
            return @{
                Version = $matches[1]
                FullNamespace = $namespace
            }
        }
        return @{
            Version = "Unknown"
            FullNamespace = $namespace
        }
    } catch {
        return @{
            Version = "Error"
            FullNamespace = "N/A"
        }
    }
}

# Check CAMT versions
Write-Host "Checking CAMT.053 versions..." -ForegroundColor Blue
$originalVersion = Get-CamtVersion $originalXml
$generatedVersion = Get-CamtVersion $generatedXml

Write-Host "Original file version: CAMT.053.001.$($originalVersion.Version)" -ForegroundColor $(if ($originalVersion.Version -eq "Unknown") { "Yellow" } else { "White" })
Write-Host "Generated file version: CAMT.053.001.$($generatedVersion.Version)" -ForegroundColor $(if ($generatedVersion.Version -eq "Unknown") { "Yellow" } else { "White" })

$versionMatch = ($originalVersion.Version -eq $generatedVersion.Version)
if (-not $versionMatch) {
    Write-Host "WARNING: Version mismatch detected!" -ForegroundColor Red
    Write-Host "  Original: $($originalVersion.FullNamespace)" -ForegroundColor Yellow
    Write-Host "  Generated: $($generatedVersion.FullNamespace)" -ForegroundColor Yellow
} else {
    Write-Host "Version check: OK" -ForegroundColor Green
}
Write-Host ""

# Helper function to safely get XML element text with namespace support
function Get-SafeXmlValue {
    param($xml, $xpath)
    try {
        if ($xml -eq $null) { return $null }
        
        # Try direct property access first for simple elements
        if ($xpath -eq "NtryRef" -and $xml.NtryRef) {
            return $xml.NtryRef
        }
        if ($xpath -eq "Amt" -and $xml.Amt) {
            if ($xml.Amt -is [System.Xml.XmlElement]) {
                return $xml.Amt.InnerText
            } else {
                return $xml.Amt.ToString()
            }
        }
        if ($xpath -eq "CdtDbtInd" -and $xml.CdtDbtInd) {
            return $xml.CdtDbtInd
        }
        
        # Create namespace manager
        $nsManager = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $nsManager.AddNamespace("camt", "urn:iso:std:iso:20022:tech:xsd:camt.053.001.02")
        
        # Try with namespace first
        $nodes = $xml.SelectNodes($xpath, $nsManager)
        if ($nodes -and $nodes.Count -gt 0) {
            return $nodes[0].InnerText
        }
        
        # Fallback to simple XPath
        $nodes = $xml.SelectNodes($xpath)
        if ($nodes -and $nodes.Count -gt 0) {
            return $nodes[0].InnerText
        }
        
        return $null
    } catch {
        return $null
    }
}

# Helper function to extract text from XML elements
function Get-XmlElementText {
    param($element)
    try {
        if ($element -eq $null) { return $null }
        if ($element -is [System.Xml.XmlElement]) {
            return $element.InnerText
        } else {
            return $element.ToString()
        }
    } catch {
        return $null
    }
}

# Helper function to get nodes with namespace support  
function Get-SafeXmlNodes {
    param($xml, $xpath)
    try {
        if ($xml -eq $null) { return @() }
        
        # Create namespace manager
        $nsManager = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $nsManager.AddNamespace("camt", "urn:iso:std:iso:20022:tech:xsd:camt.053.001.02")
        
        # Try with namespace first
        $nodes = $xml.SelectNodes($xpath, $nsManager)
        if ($nodes -and $nodes.Count -gt 0) {
            return $nodes
        }
        
        # Fallback to simple XPath
        $nodes = $xml.SelectNodes($xpath)
        if ($nodes -and $nodes.Count -gt 0) {
            return $nodes
        }
        
        return @()
    } catch {
        return @()
    }
}

Write-Host "Starting comparison analysis..." -ForegroundColor Blue

# Initialize results
$results = @{
    VersionInfo = @{
        OriginalVersion = $originalVersion.Version
        GeneratedVersion = $generatedVersion.Version
        OriginalNamespace = $originalVersion.FullNamespace
        GeneratedNamespace = $generatedVersion.FullNamespace
        VersionMatch = $versionMatch
    }
    Summary = @{
        OriginalTransactions = 0
        GeneratedTransactions = 0
        MatchingTransactions = 0
        MissingTransactions = 0
        ExtraTransactions = 0
        DifferentTransactions = 0
    }
    HeaderComparison = @()
    BalanceComparison = @()
    TransactionComparison = @()
}

# 1. GET BASIC COUNTS
try {
    $originalEntries = Get-SafeXmlNodes $originalXml "//camt:Ntry"
    if ($originalEntries.Count -eq 0) {
        $originalEntries = Get-SafeXmlNodes $originalXml "//Ntry"
    }
    
    $generatedEntries = Get-SafeXmlNodes $generatedXml "//camt:Ntry"
    if ($generatedEntries.Count -eq 0) {
        $generatedEntries = Get-SafeXmlNodes $generatedXml "//Ntry"
    }
    
    $results.Summary.OriginalTransactions = $originalEntries.Count
    $results.Summary.GeneratedTransactions = $generatedEntries.Count
    
    Write-Host "Transaction Counts:" -ForegroundColor Yellow
    Write-Host "  Original: $($originalEntries.Count)" -ForegroundColor Gray
    Write-Host "  Generated: $($generatedEntries.Count)" -ForegroundColor Gray
} catch {
    Write-Host "ERROR getting transaction counts: $($_.Exception.Message)" -ForegroundColor Red
}

# 2. COMPARE HEADERS
Write-Host "Comparing Headers..." -ForegroundColor Yellow

try {
    # Account IBAN
    $origIban = Get-SafeXmlValue $originalXml "//camt:Acct/camt:Id/camt:IBAN"
    if (-not $origIban) { $origIban = Get-SafeXmlValue $originalXml "//Acct/Id/IBAN" }
    
    $genIban = Get-SafeXmlValue $generatedXml "//camt:Acct/camt:Id/camt:IBAN"
    if (-not $genIban) { $genIban = Get-SafeXmlValue $generatedXml "//Acct/Id/IBAN" }
    
    $results.HeaderComparison += @{
        Field = "Account IBAN"
        Original = $origIban
        Generated = $genIban
        Match = ($origIban -eq $genIban)
    }
    
    # Account Currency
    $origCurrency = Get-SafeXmlValue $originalXml "//camt:Acct/camt:Ccy"
    if (-not $origCurrency) { $origCurrency = Get-SafeXmlValue $originalXml "//Acct/Ccy" }
    
    $genCurrency = Get-SafeXmlValue $generatedXml "//camt:Acct/camt:Ccy"
    if (-not $genCurrency) { $genCurrency = Get-SafeXmlValue $generatedXml "//Acct/Ccy" }
    
    $results.HeaderComparison += @{
        Field = "Account Currency"
        Original = $origCurrency
        Generated = $genCurrency
        Match = ($origCurrency -eq $genCurrency)
    }
    
    # Account Name
    $origName = Get-SafeXmlValue $originalXml "//camt:Acct/camt:Nm"
    if (-not $origName) { $origName = Get-SafeXmlValue $originalXml "//Acct/Nm" }
    
    $genName = Get-SafeXmlValue $generatedXml "//camt:Acct/camt:Nm"
    if (-not $genName) { $genName = Get-SafeXmlValue $generatedXml "//Acct/Nm" }
    
    $results.HeaderComparison += @{
        Field = "Account Name"
        Original = $origName
        Generated = $genName
        Match = ($origName -eq $genName)
    }
} catch {
    Write-Host "WARNING: Error comparing headers: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 3. COMPARE BALANCES
Write-Host "Comparing Balances..." -ForegroundColor Yellow

try {
    $originalBalances = Get-SafeXmlNodes $originalXml "//camt:Bal"
    if ($originalBalances.Count -eq 0) {
        $originalBalances = Get-SafeXmlNodes $originalXml "//Bal"
    }
    
    $generatedBalances = Get-SafeXmlNodes $generatedXml "//camt:Bal"
    if ($generatedBalances.Count -eq 0) {
        $generatedBalances = Get-SafeXmlNodes $generatedXml "//Bal"
    }
    
    Write-Host "  Original balances: $($originalBalances.Count)" -ForegroundColor Gray
    Write-Host "  Generated balances: $($generatedBalances.Count)" -ForegroundColor Gray
    
    # Compare balances by creating a lookup table for generated balances first
    $generatedBalanceLookup = @{}
    foreach ($genBal in $generatedBalances) {
        $genBalType = $null
        # Try multiple ways to get balance type
        if ($genBal.Tp -and $genBal.Tp.CdOrPrtry -and $genBal.Tp.CdOrPrtry.Cd) {
            $genBalType = $genBal.Tp.CdOrPrtry.Cd
        } else {
            $genBalType = Get-SafeXmlValue $genBal "camt:Tp/camt:CdOrPrtry/camt:Cd"
            if (-not $genBalType) { $genBalType = Get-SafeXmlValue $genBal "Tp/CdOrPrtry/Cd" }
        }
        
        if ($genBalType -and -not $generatedBalanceLookup.ContainsKey($genBalType)) {
            $generatedBalanceLookup[$genBalType] = $genBal
        }
    }
    
    foreach ($origBal in $originalBalances) {
        $balType = $null
        # Try multiple ways to get balance type
        if ($origBal.Tp -and $origBal.Tp.CdOrPrtry -and $origBal.Tp.CdOrPrtry.Cd) {
            $balType = $origBal.Tp.CdOrPrtry.Cd
        } else {
            $balType = Get-SafeXmlValue $origBal "camt:Tp/camt:CdOrPrtry/camt:Cd"
            if (-not $balType) { $balType = Get-SafeXmlValue $origBal "Tp/CdOrPrtry/Cd" }
        }
        
        $origAmount = Get-XmlElementText $origBal.Amt
        if (-not $origAmount) {
            $origAmount = Get-SafeXmlValue $origBal "camt:Amt"
            if (-not $origAmount) { 
                $origAmount = Get-SafeXmlValue $origBal "Amt" 
            }
        }
        
        $origCdtDbt = Get-SafeXmlValue $origBal "camt:CdtDbtInd"
        if (-not $origCdtDbt) { $origCdtDbt = Get-SafeXmlValue $origBal "CdtDbtInd" }
        
        # Find matching balance in generated using lookup
        if ($balType -and $generatedBalanceLookup.ContainsKey($balType)) {
            $matchingGenBal = $generatedBalanceLookup[$balType]
            
            $genAmount = Get-XmlElementText $matchingGenBal.Amt
            if (-not $genAmount) {
                $genAmount = Get-SafeXmlValue $matchingGenBal "camt:Amt"
                if (-not $genAmount) { 
                    $genAmount = Get-SafeXmlValue $matchingGenBal "Amt" 
                }
            }
            
            $genCdtDbt = Get-SafeXmlValue $matchingGenBal "camt:CdtDbtInd"
            if (-not $genCdtDbt) { $genCdtDbt = Get-SafeXmlValue $matchingGenBal "CdtDbtInd" }
            
            $results.BalanceComparison += @{
                Type = $balType
                AmountMatch = ($origAmount -eq $genAmount)
                CdtDbtMatch = ($origCdtDbt -eq $genCdtDbt)
                OriginalAmount = $origAmount
                GeneratedAmount = $genAmount
                OriginalCdtDbt = $origCdtDbt
                GeneratedCdtDbt = $genCdtDbt
                Missing = $false
            }
        } else {
            $results.BalanceComparison += @{
                Type = $balType
                Missing = $true
                OriginalAmount = $origAmount
                OriginalCdtDbt = $origCdtDbt
                GeneratedAmount = $null
                GeneratedCdtDbt = $null
            }
        }
    }
} catch {
    Write-Host "WARNING: Error comparing balances: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 4. COMPARE TRANSACTIONS
Write-Host "Comparing Transactions..." -ForegroundColor Yellow

try {
    # Create lookup for generated transactions by NtryRef
    $generatedLookup = @{}
    foreach ($genEntry in $generatedEntries) {
        $ntryRef = Get-SafeXmlValue $genEntry "camt:NtryRef"
        if (-not $ntryRef) { $ntryRef = Get-SafeXmlValue $genEntry "NtryRef" }
        
        if ($ntryRef) {
            $generatedLookup[$ntryRef] = $genEntry
        }
    }
    
    $transactionIndex = 0
    foreach ($origEntry in $originalEntries) {
        $transactionIndex++
        if ($transactionIndex % 100 -eq 0) {
            Write-Host "  Processing transaction $transactionIndex of $($originalEntries.Count)..." -ForegroundColor Gray
        }
        
        $origNtryRef = Get-SafeXmlValue $origEntry "camt:NtryRef"
        if (-not $origNtryRef) { $origNtryRef = Get-SafeXmlValue $origEntry "NtryRef" }
        
        $origAmount = Get-XmlElementText $origEntry.Amt
        if (-not $origAmount) { 
            $origAmount = Get-SafeXmlValue $origEntry "camt:Amt"
            if (-not $origAmount) { 
                $origAmount = Get-SafeXmlValue $origEntry "Amt" 
            }
        }
        
        $origCdtDbt = Get-SafeXmlValue $origEntry "camt:CdtDbtInd"
        if (-not $origCdtDbt) { $origCdtDbt = Get-SafeXmlValue $origEntry "CdtDbtInd" }
        
        $origBookingDate = $null
        if ($origEntry.BookgDt -and $origEntry.BookgDt.Dt) {
            $origBookingDate = $origEntry.BookgDt.Dt
        } else {
            $origBookingDate = Get-SafeXmlValue $origEntry "camt:BookgDt/camt:Dt"
            if (-not $origBookingDate) { $origBookingDate = Get-SafeXmlValue $origEntry "BookgDt/Dt" }
        }
        
        $origValueDate = $null
        if ($origEntry.ValDt -and $origEntry.ValDt.Dt) {
            $origValueDate = $origEntry.ValDt.Dt
        } else {
            $origValueDate = Get-SafeXmlValue $origEntry "camt:ValDt/camt:Dt"
            if (-not $origValueDate) { $origValueDate = Get-SafeXmlValue $origEntry "ValDt/Dt" }
        }
        
        $comparison = @{
            NtryRef = $origNtryRef
            Found = $false
            Differences = @()
            Missing = $false
        }
        
        # Find matching generated transaction
        if ($origNtryRef -and $generatedLookup.ContainsKey($origNtryRef)) {
            $matchingGenEntry = $generatedLookup[$origNtryRef]
            $comparison.Found = $true
            $results.Summary.MatchingTransactions++
            
            # Compare basic fields
            $genAmount = Get-XmlElementText $matchingGenEntry.Amt
            if (-not $genAmount) { 
                $genAmount = Get-SafeXmlValue $matchingGenEntry "camt:Amt"
                if (-not $genAmount) { 
                    $genAmount = Get-SafeXmlValue $matchingGenEntry "Amt" 
                }
            }
            
            $genCdtDbt = Get-SafeXmlValue $matchingGenEntry "camt:CdtDbtInd"
            if (-not $genCdtDbt) { $genCdtDbt = Get-SafeXmlValue $matchingGenEntry "CdtDbtInd" }
            
            $genBookingDate = $null
            if ($matchingGenEntry.BookgDt -and $matchingGenEntry.BookgDt.Dt) {
                $genBookingDate = $matchingGenEntry.BookgDt.Dt
            } else {
                $genBookingDate = Get-SafeXmlValue $matchingGenEntry "camt:BookgDt/camt:Dt"
                if (-not $genBookingDate) { $genBookingDate = Get-SafeXmlValue $matchingGenEntry "BookgDt/Dt" }
            }
            
            $genValueDate = $null
            if ($matchingGenEntry.ValDt -and $matchingGenEntry.ValDt.Dt) {
                $genValueDate = $matchingGenEntry.ValDt.Dt
            } else {
                $genValueDate = Get-SafeXmlValue $matchingGenEntry "camt:ValDt/camt:Dt"
                if (-not $genValueDate) { $genValueDate = Get-SafeXmlValue $matchingGenEntry "ValDt/Dt" }
            }
            
            if ($origAmount -ne $genAmount) {
                $comparison.Differences += "Amount: Orig=$origAmount, Gen=$genAmount"
            }
            if ($origCdtDbt -ne $genCdtDbt) {
                $comparison.Differences += "CdtDbt: Orig=$origCdtDbt, Gen=$genCdtDbt"
            }
            if ($origBookingDate -ne $genBookingDate) {
                $comparison.Differences += "BookingDate: Orig=$origBookingDate, Gen=$genBookingDate"
            }
            if ($origValueDate -ne $genValueDate) {
                $comparison.Differences += "ValueDate: Orig=$origValueDate, Gen=$genValueDate"
            }
            
            # Compare transaction details
            $origTxDtls = $origEntry.SelectSingleNode("NtryDtls/TxDtls")
            $genTxDtls = $matchingGenEntry.SelectSingleNode("NtryDtls/TxDtls")
            
            if ($origTxDtls -and $genTxDtls) {
                # Compare EndToEndId
                $origEndToEnd = Get-SafeXmlValue $origTxDtls "Refs/EndToEndId"
                $genEndToEnd = Get-SafeXmlValue $genTxDtls "Refs/EndToEndId"
                
                if ($origEndToEnd -and $genEndToEnd -and $origEndToEnd -ne $genEndToEnd) {
                    $comparison.Differences += "EndToEndId: Orig=$origEndToEnd, Gen=$genEndToEnd"
                } elseif ($origEndToEnd -and -not $genEndToEnd) {
                    $comparison.Differences += "EndToEndId: Missing in generated (Orig=$origEndToEnd)"
                }
                
                # Compare bank transaction code
                $origBkTxCd = Get-SafeXmlValue $origTxDtls "BkTxCd/Prtry/Cd"
                $genBkTxCd = Get-SafeXmlValue $genTxDtls "BkTxCd/Prtry/Cd"
                
                if ($origBkTxCd -ne $genBkTxCd) {
                    $comparison.Differences += "BkTxCd: Orig=$origBkTxCd, Gen=$genBkTxCd"
                }
                
                # Compare related parties
                $origRltdPties = $origTxDtls.SelectSingleNode("RltdPties")
                $genRltdPties = $genTxDtls.SelectSingleNode("RltdPties")
                
                if ($origRltdPties -and -not $genRltdPties) {
                    $comparison.Differences += "RltdPties: Missing in generated"
                } elseif ($origRltdPties -and $genRltdPties) {
                    # Compare debtor info
                    $origDebtorName = Get-SafeXmlValue $origRltdPties "Dbtr/Nm"
                    $genDebtorName = Get-SafeXmlValue $genRltdPties "Dbtr/Nm"
                    
                    if ($origDebtorName -and $genDebtorName -and $origDebtorName -ne $genDebtorName) {
                        $comparison.Differences += "DebtorName: Orig=$origDebtorName, Gen=$genDebtorName"
                    }
                    
                    # Compare creditor info
                    $origCreditorName = Get-SafeXmlValue $origRltdPties "Cdtr/Nm"
                    $genCreditorName = Get-SafeXmlValue $genRltdPties "Cdtr/Nm"
                    
                    if ($origCreditorName -and $genCreditorName -and $origCreditorName -ne $genCreditorName) {
                        $comparison.Differences += "CreditorName: Orig=$origCreditorName, Gen=$genCreditorName"
                    }
                    
                    # Compare IBANs
                    $origDebtorIban = Get-SafeXmlValue $origRltdPties "DbtrAcct/Id/IBAN"
                    $genDebtorIban = Get-SafeXmlValue $genRltdPties "DbtrAcct/Id/IBAN"
                    
                    if ($origDebtorIban -and -not $genDebtorIban) {
                        $comparison.Differences += "DebtorIBAN: Missing in generated (Orig=$origDebtorIban)"
                    } elseif ($origDebtorIban -and $genDebtorIban -and $origDebtorIban -ne $genDebtorIban) {
                        $comparison.Differences += "DebtorIBAN: Orig=$origDebtorIban, Gen=$genDebtorIban"
                    }
                    
                    $origCreditorIban = Get-SafeXmlValue $origRltdPties "CdtrAcct/Id/IBAN"
                    $genCreditorIban = Get-SafeXmlValue $genRltdPties "CdtrAcct/Id/IBAN"
                    
                    if ($origCreditorIban -and -not $genCreditorIban) {
                        $comparison.Differences += "CreditorIBAN: Missing in generated (Orig=$origCreditorIban)"
                    } elseif ($origCreditorIban -and $genCreditorIban -and $origCreditorIban -ne $genCreditorIban) {
                        $comparison.Differences += "CreditorIBAN: Orig=$origCreditorIban, Gen=$genCreditorIban"
                    }
                }
                
                # Compare remittance info
                $origRmtInf = Get-SafeXmlValue $origTxDtls "RmtInf/Ustrd"
                $genRmtInf = Get-SafeXmlValue $genTxDtls "RmtInf/Ustrd"
                
                if ($origRmtInf -and -not $genRmtInf) {
                    $comparison.Differences += "RemittanceInfo: Missing in generated (Orig=$origRmtInf)"
                } elseif ($origRmtInf -and $genRmtInf -and $origRmtInf -ne $genRmtInf) {
                    $comparison.Differences += "RemittanceInfo: Orig=$origRmtInf, Gen=$genRmtInf"
                }
            }
            
            if ($comparison.Differences.Count -gt 0) {
                $results.Summary.DifferentTransactions++
            }
        } else {
            $comparison.Missing = $true
            $results.Summary.MissingTransactions++
        }
        
        $results.TransactionComparison += $comparison
    }
    
    # Check for extra transactions in generated file
    foreach ($genEntry in $generatedEntries) {
        $genNtryRef = Get-SafeXmlValue $genEntry "NtryRef"
        $found = $false
        
        foreach ($origEntry in $originalEntries) {
            $origNtryRef = Get-SafeXmlValue $origEntry "NtryRef"
            if ($origNtryRef -eq $genNtryRef) {
                $found = $true
                break
            }
        }
        
        if (-not $found) {
            $results.Summary.ExtraTransactions++
            $results.TransactionComparison += @{
                NtryRef = $genNtryRef
                Found = $false
                Extra = $true
                Differences = @("Extra transaction in generated file")
            }
        }
    }
} catch {
    Write-Host "ERROR comparing transactions: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
}

# DISPLAY RESULTS
Write-Host ""
Write-Host "COMPARISON SUMMARY" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan
Write-Host "Total Original Transactions: $($results.Summary.OriginalTransactions)" -ForegroundColor White
Write-Host "Total Generated Transactions: $($results.Summary.GeneratedTransactions)" -ForegroundColor White
Write-Host "Matching Transactions: $($results.Summary.MatchingTransactions)" -ForegroundColor Green
Write-Host "Missing Transactions: $($results.Summary.MissingTransactions)" -ForegroundColor Red
Write-Host "Extra Transactions: $($results.Summary.ExtraTransactions)" -ForegroundColor Yellow
Write-Host "Transactions with Differences: $($results.Summary.DifferentTransactions)" -ForegroundColor Magenta

# Show header comparison
Write-Host ""
Write-Host "HEADER COMPARISON" -ForegroundColor Cyan
foreach ($header in $results.HeaderComparison) {
    $status = if ($header.Match) { "OK" } else { "DIFF" }
    $color = if ($header.Match) { "Green" } else { "Red" }
    Write-Host "$status $($header.Field): Original='$($header.Original)', Generated='$($header.Generated)'" -ForegroundColor $color
}

# Show balance comparison
Write-Host ""
Write-Host "BALANCE COMPARISON" -ForegroundColor Cyan
foreach ($balance in $results.BalanceComparison) {
    if ($balance.Missing) {
        Write-Host "MISSING balance type: $($balance.Type)" -ForegroundColor Red
    } else {
        $amountStatus = if ($balance.AmountMatch) { "MATCH" } else { "DIFF" }
        $cdtDbtStatus = if ($balance.CdtDbtMatch) { "OK" } else { "DIFF" }
        $color = if ($balance.AmountMatch -and $balance.CdtDbtMatch) { "Green" } else { "Yellow" }
        Write-Host "$amountStatus $cdtDbtStatus [$($balance.Type)]: Amount Orig=$($balance.OriginalAmount), Gen=$($balance.GeneratedAmount)" -ForegroundColor $color
    }
}

# Show problematic transactions
Write-Host ""
Write-Host "PROBLEMATIC TRANSACTIONS (First 10)" -ForegroundColor Cyan
$problemTransactions = $results.TransactionComparison | Where-Object { $_.Missing -or $_.Extra -or $_.Differences.Count -gt 0 } | Select-Object -First 10

if ($problemTransactions.Count -gt 0) {
    foreach ($tx in $problemTransactions) {
        if ($tx.Missing) {
            Write-Host "MISSING NtryRef: $($tx.NtryRef)" -ForegroundColor Red
        } elseif ($tx.Extra) {
            Write-Host "EXTRA NtryRef: $($tx.NtryRef)" -ForegroundColor Red
        } else {
            Write-Host "NtryRef: $($tx.NtryRef) - Differences found" -ForegroundColor Yellow
            foreach ($diff in $tx.Differences) {
                Write-Host "  $diff" -ForegroundColor Gray
            }
        }
    }
} else {
    Write-Host "No problematic transactions found" -ForegroundColor Green
}

foreach ($tx in $problemTransactions) {
    if ($tx.Missing) {
        Write-Host "MISSING: NtryRef=$($tx.NtryRef)" -ForegroundColor Red
    } elseif ($tx.Extra) {
        Write-Host "EXTRA: NtryRef=$($tx.NtryRef)" -ForegroundColor Yellow
    } else {
        Write-Host "DIFFERENCES in NtryRef=$($tx.NtryRef):" -ForegroundColor Yellow
        foreach ($diff in $tx.Differences) {
            Write-Host "   * $diff" -ForegroundColor Gray
        }
    }
}

if ($problemTransactions.Count -lt ($results.Summary.MissingTransactions + $results.Summary.ExtraTransactions + $results.Summary.DifferentTransactions)) {
    $remaining = ($results.Summary.MissingTransactions + $results.Summary.ExtraTransactions + $results.Summary.DifferentTransactions) - $problemTransactions.Count
    Write-Host "... and $remaining more issues" -ForegroundColor Gray
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

$versionWarningHtml = ""
if (-not $versionMatch) {
    $versionWarningHtml = @"
        <div style="background-color: #fff3cd; border-left: 5px solid #f39c12; padding: 15px; margin: 20px 0; border-radius: 5px;">
            <h3 style="color: #856404; margin-top: 0;">⚠️ Version Mismatch Detected</h3>
            <p><strong>Original file:</strong> CAMT.053.001.$($originalVersion.Version)</p>
            <p><strong>Generated file:</strong> CAMT.053.001.$($generatedVersion.Version)</p>
            <p style="margin-bottom: 0;">Different CAMT.053 versions may have different field structures and requirements. Some differences in the comparison may be due to schema version changes.</p>
        </div>
"@
} else {
    $versionWarningHtml = @"
        <div style="background-color: #d5f4e6; border-left: 5px solid #27ae60; padding: 15px; margin: 20px 0; border-radius: 5px;">
            <h3 style="color: #155724; margin-top: 0;">✓ Version Match</h3>
            <p style="margin-bottom: 0;">Both files use CAMT.053.001.$($originalVersion.Version)</p>
        </div>
"@
}

$html = @"
<!DOCTYPE html>
<html>
<head>
    <title>CAMT.053 File Comparison Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; background-color: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }
        h2 { color: #34495e; margin-top: 30px; border-left: 4px solid #3498db; padding-left: 15px; }
        .version-info { background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; }
        .version-info table { margin: 10px 0; }
        .version-info th { text-align: left; padding-right: 20px; }
        .summary { background-color: #ecf0f1; padding: 15px; border-radius: 5px; margin: 20px 0; }
    <title>CAMT.053 File Comparison Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; background-color: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }
        h2 { color: #34495e; margin-top: 30px; border-left: 4px solid #3498db; padding-left: 15px; }
        .summary { background-color: #ecf0f1; padding: 15px; border-radius: 5px; margin: 20px 0; }
<body>
    <div class="container">
        <h1>CAMT.053 File Comparison Report</h1>
        
        $versionWarningHtml
        
        <div class="version-info">
            <h3>CAMT.053 Schema Version Information</h3>
            <table>
                <tr>
                    <th>Original File:</th>
                    <td>CAMT.053.001.$($originalVersion.Version)</td>
                </tr>
                <tr>
                    <th>Generated File:</th>
                    <td>CAMT.053.001.$($generatedVersion.Version)</td>
                </tr>
                <tr>
                    <th>Version Match:</th>
                    <td class="$(if ($versionMatch) { 'good' } else { 'bad' })">$(if ($versionMatch) { '✓ Yes' } else { '✗ No - Version mismatch!' })</td>
                </tr>
            </table>
            <details>
                <summary style="cursor: pointer; color: #3498db;">Show full namespace URIs</summary>
                <p><strong>Original:</strong> $($originalVersion.FullNamespace)</p>
                <p><strong>Generated:</strong> $($generatedVersion.FullNamespace)</p>
            </details>
        </div>
        
        <div class="summary">
            <h2>Summary</h2>4495e; }
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
    </style>
</head>
<body>
    <div class="container">
        <h1>CAMT.053 File Comparison Report</h1>
        
        <div class="summary">
            <h2>Summary</h2>
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
        </div>

        <h2>Header Comparison</h2>
        <table>
            <tr><th>Field</th><th>Original</th><th>Generated</th><th>Match</th></tr>
"@

foreach ($item in $results.HeaderComparison) {
    $matchClass = if ($item.Match) { "match" } else { "no-match" }
    $html += "<tr class='$matchClass'><td>$($item.Field)</td><td>$($item.Original)</td><td>$($item.Generated)</td><td>$($item.Match)</td></tr>"
}

$html += "</table>"

if ($results.BalanceComparison.Count -gt 0) {
    $html += "<h2>Balance Comparison</h2>"
    
    foreach ($balance in $results.BalanceComparison) {
        if ($balance.Missing) {
            $html += "<div class='transaction missing'><strong>Missing Balance Type: $($balance.Type)</strong></div>"
        } else {
            $status = if ($balance.AmountMatch -and $balance.CdtDbtMatch) { "good" } else { "error" }
            $html += "<div class='transaction $status'><strong>$($balance.Type) Balance</strong><br/>"
            $html += "Original: $($balance.OriginalAmount) | Generated: $($balance.GeneratedAmount)<br/>"
            if (-not $balance.AmountMatch) { $html += "Amount difference found<br/>" }
            if (-not $balance.CdtDbtMatch) { $html += "Credit/Debit difference found<br/>" }
            $html += "</div>"
        }
    }
}

$html += "<h2>Transaction Comparison</h2>"
$html += "<p><strong>Total problematic transactions:</strong> $(($results.TransactionComparison | Where-Object { $_.Missing -or $_.Extra -or $_.Differences.Count -gt 0 }).Count)</p>"

# Show first 20 problematic transactions in detail
$problemTransactions = $results.TransactionComparison | Where-Object { $_.Missing -or $_.Extra -or $_.Differences.Count -gt 0 } | Select-Object -First 20

foreach ($tx in $problemTransactions) {
    if ($tx.Missing) {
        $html += "<div class='transaction missing'><strong>NtryRef: $($tx.NtryRef)</strong> - Missing in generated file</div>"
    } elseif ($tx.Extra) {
        $html += "<div class='transaction missing'><strong>NtryRef: $($tx.NtryRef)</strong> - Extra transaction in generated file</div>"
    } elseif ($tx.Differences.Count -gt 0) {
        $html += "<div class='transaction error'><strong>NtryRef: $($tx.NtryRef)</strong> - Differences found<div class='differences'>"
        foreach ($diff in $tx.Differences) {
            $diffClass = if ($diff -like "*Generated field*") { "note" } else { "difference" }
            $html += "<div class='$diffClass'>$diff</div>"
        }
        $html += "</div></div>"
    }
}

if ($problemTransactions.Count -lt ($results.Summary.MissingTransactions + $results.Summary.ExtraTransactions + $results.Summary.DifferentTransactions)) {
    $remaining = ($results.Summary.MissingTransactions + $results.Summary.ExtraTransactions + $results.Summary.DifferentTransactions) - $problemTransactions.Count
    $html += "<div class='note'><p>... and $remaining more issues not shown in detail</p></div>"
}

$html += @"
        
        <div style="margin-top: 40px; padding: 20px; background-color: #ecf0f1; border-radius: 5px;">
            <h3>Legend</h3>
            <p><span class="good">Green</span> = Perfect Match</p>
            <p><span class="bad">Red</span> = Missing or Significant Difference</p>
            <p><span class="warning">Orange</span> = Minor Difference or Generated Field</p>
            <p><strong>Note:</strong> Generated fields (timestamps, IDs) are expected to be different</p>
            $(if (-not $versionMatch) { "<p><strong>⚠️ Warning:</strong> Version mismatch may cause schema-related differences</p>" } else { "" })
        </div>
        
        <div style="margin-top: 20px; text-align: center; color: #7f8c8d;">
            <p>Report generated on $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
            <p>Original: $(Split-Path $OriginalFile -Leaf) (CAMT.053.001.$($originalVersion.Version))</p>
            <p>Generated: $(Split-Path $GeneratedFile -Leaf) (CAMT.053.001.$($generatedVersion.Version))</p>
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