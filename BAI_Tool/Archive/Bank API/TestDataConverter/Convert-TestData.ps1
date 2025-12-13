# Test Data Converter - RaboBank API Compatible Version
# Converteert CSV testdata naar JSON bestanden in exacte RaboBank API structuur
# Parameters:
# -FromDate: Start datum (yyyy-MM-dd) - optioneel, default = vroegste datum in data
# -ToDate: Eind datum (yyyy-MM-dd) - optioneel, default = laatste datum in data
# -InputPath: Pad naar input bestanden - optioneel, default = .\TestData
# -OutputPath: Pad naar output bestanden - optioneel, default = .\Output

param(
    [Parameter(Mandatory=$false)]
    [string]$FromDate,
    
    [Parameter(Mandatory=$false)]
    [string]$ToDate,
    
    [Parameter(Mandatory=$false)]
    [string]$InputPath = ".\TestData",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".\Output"
)

Write-Host "=== RaboBank API Compatible Test Data Converter ===" -ForegroundColor Green
Write-Host "Converteert CSV transacties naar exacte RaboBank API JSON structuur" -ForegroundColor Yellow
Write-Host "Gebaseerd op officiële RaboBank Business Account Insight API" -ForegroundColor Yellow
Write-Host ""

# Show usage if help requested
if ($FromDate -eq "help" -or $ToDate -eq "help" -or $FromDate -eq "-h" -or $FromDate -eq "--help") {
    Write-Host "GEBRUIK:" -ForegroundColor Cyan
    Write-Host "  .\Convert-TestData.ps1 [-FromDate yyyy-MM-dd] [-ToDate yyyy-MM-dd] [-InputPath path] [-OutputPath path]" -ForegroundColor White
    Write-Host ""
    Write-Host "VOORBEELDEN:" -ForegroundColor Cyan
    Write-Host "  .\Convert-TestData.ps1                                    # Gebruik alle data uit bestanden"
    Write-Host "  .\Convert-TestData.ps1 -FromDate 2025-08-29              # Van 29 aug tot laatste datum in data"
    Write-Host "  .\Convert-TestData.ps1 -ToDate 2025-09-01                # Van eerste datum in data tot 1 sept"
    Write-Host "  .\Convert-TestData.ps1 -FromDate 2025-08-29 -ToDate 2025-09-01  # Specifieke range"
    Write-Host ""
    Write-Host "FEATURES:" -ForegroundColor Cyan
    Write-Host "  • Genereert bestanden voor ELKE dag in de range (ook zonder transacties)"
    Write-Host "  • Lege transactie bestanden voor dagen zonder data"
    Write-Host "  • Balans bestanden voor alle dagen (doorvoer van vorige dag als geen transacties)"
    Write-Host "  • Consistente naamgeving: type_volledigIban_yyyyMMdd_timestamp.json"
    exit 0
}

# Parse date parameters
$startDate = $null
$endDate = $null

if ($FromDate) {
    try {
        $startDate = [DateTime]::ParseExact($FromDate, "yyyy-MM-dd", $null)
        Write-Host "Start datum: $($startDate.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan
    } catch {
        Write-Host "FOUT: Ongeldige FromDate format. Gebruik yyyy-MM-dd" -ForegroundColor Red
        Write-Host "Voorbeeld: .\Convert-TestData.ps1 -FromDate 2025-08-29" -ForegroundColor Yellow
        exit 1
    }
}

if ($ToDate) {
    try {
        $endDate = [DateTime]::ParseExact($ToDate, "yyyy-MM-dd", $null)
        Write-Host "Eind datum: $($endDate.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan
    } catch {
        Write-Host "FOUT: Ongeldige ToDate format. Gebruik yyyy-MM-dd" -ForegroundColor Red
        Write-Host "Voorbeeld: .\Convert-TestData.ps1 -ToDate 2025-09-01" -ForegroundColor Yellow
        exit 1
    }
}

if ($startDate -and $endDate -and $startDate -gt $endDate) {
    Write-Host "FOUT: FromDate moet voor ToDate liggen" -ForegroundColor Red
    exit 1
}

if (-not $startDate -and -not $endDate) {
    Write-Host "Datum range: wordt bepaald uit data bestanden" -ForegroundColor Cyan
} elseif (-not $startDate) {
    Write-Host "Datum range: eerste datum uit data - $($endDate.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan
} elseif (-not $endDate) {
    Write-Host "Datum range: $($startDate.ToString('yyyy-MM-dd')) - laatste datum uit data" -ForegroundColor Cyan
} else {
    Write-Host "Datum range: $($startDate.ToString('yyyy-MM-dd')) - $($endDate.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan
}

Write-Host ""

# Ensure directories exist
if (!(Test-Path $InputPath)) {
    New-Item -ItemType Directory -Path $InputPath -Force | Out-Null
}
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

Write-Host "Input directory: $InputPath" -ForegroundColor Cyan
Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host ""

# Check for CSV files
$csvFiles = Get-ChildItem -Path $InputPath -Filter "*.csv"
if ($csvFiles.Count -eq 0) {
    Write-Host "FOUT: Geen CSV bestanden gevonden in $InputPath" -ForegroundColor Red
    Write-Host "Verwachte bestandsnamen: CSV_A_[IBAN]_EUR_[YYYYMMDD]_[YYYYMMDD].csv" -ForegroundColor Yellow
    exit 1
}

Write-Host "Gevonden CSV bestanden: $($csvFiles.Count)" -ForegroundColor Green
foreach ($file in $csvFiles) {
    Write-Host "  - $($file.Name)" -ForegroundColor Gray
}
Write-Host ""

# Function to parse decimal values
function Parse-Decimal {
    param([string]$value)
    
    if ([string]::IsNullOrWhiteSpace($value)) { return 0 }
    
    # Handle Dutch format: +123,45 or -123,45
    $value = $value.Replace("+", "").Replace(" ", "")
    
    if ($value.Contains(",")) {
        # Handle European format: 1.234.567,89 -> 1234567.89
        $lastCommaIndex = $value.LastIndexOf(',')
        if ($lastCommaIndex -gt 0) {
            $integerPart = $value.Substring(0, $lastCommaIndex).Replace(".", "")
            $decimalPart = $value.Substring($lastCommaIndex + 1)
            $value = $integerPart + "." + $decimalPart
        } else {
            # Simple comma as decimal separator
            $value = $value.Replace(",", ".")
        }
    }
    
    try {
        return [decimal]$value
    } catch {
        return 0
    }
}

# Function to parse CSV line with proper quote handling
function Parse-CsvLine {
    param([string]$line)
    
    $fields = @()
    $current = ""
    $inQuotes = $false
    $i = 0
    
    while ($i -lt $line.Length) {
        $c = $line[$i]
        
        if ($c -eq '"') {
            if ($inQuotes -and ($i + 1) -lt $line.Length -and $line[$i + 1] -eq '"') {
                # Escaped quote
                $current += '"'
                $i += 2
            } else {
                # Start or end quotes
                $inQuotes = !$inQuotes
                $i++
            }
        } elseif ($c -eq ',' -and !$inQuotes) {
            # Field separator
            $fields += $current
            $current = ""
            $i++
        } else {
            $current += $c
            $i++
        }
    }
    
    # Add last field
    $fields += $current
    return $fields
}

# Function to extract account from filename
function Extract-Account {
    param([string]$fileName)
    
    # Expected format: CSV_A_NL08RABO0100929575_EUR_20250829_20250901
    $parts = $fileName.Split('_')
    
    if ($parts.Length -ge 3) {
        return $parts[2] # Should be the IBAN
    }
    
    # Fallback: look for IBAN pattern
    $tokens = $fileName.Split('_', '-', ' ')
    foreach ($token in $tokens) {
        if ($token.StartsWith("NL") -and $token.Length -ge 15) {
            return $token
        }
    }
    
    return "UNKNOWN_ACCOUNT"
}

# Function to convert CSV transaction to exact RaboBank API format
function ConvertTo-RaboBankTransaction {
    param(
        $csvRow,
        $ownAccount,
        $runningBalance
    )
    
    # Parse amount - CSV uses Dutch format
    $amountStr = $csvRow.Bedrag.Trim('"', ' ')
    $amount = Parse-Decimal $amountStr
    $isCredit = $amount -gt 0
    $absAmount = [Math]::Abs($amount)
    
    # Parse dates
    $dateStr = $csvRow.Datum.Trim('"', ' ')
    $date = [DateTime]::Parse($dateStr)
    
    # Parse other fields
    $sequenceNumber = $csvRow.Volgnr.Trim('"', ' ')
    $counterpartyIban = $csvRow.CounterpartyIban.Trim('"', ' ')
    $counterpartyName = $csvRow.CounterpartyName.Trim('"', ' ')
    $description = $csvRow.Description1.Trim('"', ' ')
    
    # Generate timestamp with microseconds (RaboBank format)
    $timestamp = $date.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ")
    
    # Build transaction in exact RaboBank API structure
    $transaction = [PSCustomObject]@{
        bookingDate = $date.ToString("yyyy-MM-dd")
        valueDate = $date.ToString("yyyy-MM-dd")
        raboBookingDateTime = $timestamp
        entryReference = $sequenceNumber
        transactionAmount = @{
            amount = $absAmount.ToString("F2")
            currency = "EUR"
        }
        remittanceInformationUnstructured = $description
        raboDetailedTransactionType = "633"  # Standard SEPA
        raboTransactionTypeName = "st"       # Standard transfer
        reasonCode = "AG01"                  # Standard reason
        bankTransactionCode = "PMNT-RCDT-ESCT"
        creditorAgent = "RABONL2U"
        initiatingPartyName = "CSV IMPORT"
    }
    
    # Set creditor/debtor accounts based on transaction direction
    if ($isCredit) {
        # Money coming in - we are creditor
        $transaction | Add-Member -NotePropertyName "creditorAccount" -NotePropertyValue @{
            iban = $ownAccount
            currency = "EUR"
        }
        if (-not [string]::IsNullOrEmpty($counterpartyIban)) {
            $transaction | Add-Member -NotePropertyName "debtorAccount" -NotePropertyValue @{
                iban = $counterpartyIban
            }
        }
        if (-not [string]::IsNullOrEmpty($counterpartyName)) {
            $transaction | Add-Member -NotePropertyName "debtorName" -NotePropertyValue $counterpartyName
        }
    } else {
        # Money going out - we are debtor  
        $transaction | Add-Member -NotePropertyName "debtorAccount" -NotePropertyValue @{
            iban = $ownAccount
        }
        if (-not [string]::IsNullOrEmpty($counterpartyIban)) {
            $transaction | Add-Member -NotePropertyName "creditorAccount" -NotePropertyValue @{
                iban = $counterpartyIban
                currency = "EUR"
            }
        }
        if (-not [string]::IsNullOrEmpty($counterpartyName)) {
            $transaction | Add-Member -NotePropertyName "creditorName" -NotePropertyValue $counterpartyName
        }
    }
    
    # Add balance after booking if running balance provided
    if ($runningBalance -ne $null) {
        $transaction | Add-Member -NotePropertyName "balanceAfterBooking" -NotePropertyValue @{
            balanceType = "InterimBooked"
            balanceAmount = @{
                amount = $runningBalance.ToString("F2")
                currency = "EUR"
            }
        }
    }
    
    return $transaction
}

# Function to build exact RaboBank API response structure
function Build-RaboBankResponse {
    param(
        $account,
        $transactions
    )
    
    # Generate account ID hash (similar to RaboBank format)
    # Generate a simple hash-based identifier
    $accountHash = $account.GetHashCode().ToString() + "_" + [DateTime]::Now.Ticks.ToString().Substring(10)
    
    return [PSCustomObject]@{
        account = @{
            currency = "EUR"
            iban = $account
        }
        transactions = @{
            "_links" = @{
                account = "/accounts/$accountHash"
                next = $null
            }
            booked = $transactions
        }
    }
}

Write-Host ""
Write-Host "=== Processing Balance Data ===" -ForegroundColor Green
Write-Host "Verwerken van balance TXT bestanden..." -ForegroundColor Yellow

# Process balance TXT files first
$balanceFiles = Get-ChildItem -Path $InputPath -Filter "Balance_*.txt"
$balanceData = @{}

foreach ($balanceFile in $balanceFiles) {
    Write-Host "Processing balance file: $($balanceFile.Name)" -ForegroundColor Yellow
    
    try {
        $content = Get-Content $balanceFile.FullName -Raw
        
        # Extract IBAN from filename and content
        if ($balanceFile.Name -match "Balance_(.+?)\.txt") {
            $ibanPart = $matches[1].Replace(' ', '')
            # Remove 'NL' prefix if already present to avoid double 'NL'
            if ($ibanPart.StartsWith('NL')) {
                $fullIban = $ibanPart
            } else {
                $fullIban = "NL$ibanPart"
            }
            
            # Extract balance information from content
            $beginSaldo = 0
            $eindSaldo = 0
            if ($content -match "Beginsaldo\s+([0-9,.]+)\s+(CR|D)") {
                $beginSaldo = Parse-Decimal $matches[1]
                if ($matches[2] -eq "D") { $beginSaldo = -$beginSaldo }
                Write-Host "  Begin saldo: €$($beginSaldo.ToString('N2'))" -ForegroundColor Yellow
            } else {
                Write-Host "  Geen begin saldo gevonden" -ForegroundColor Red
            }
            
            if ($content -match "Eindsaldo\s+([0-9,.]+)\s+(CR|D)") {
                $eindSaldo = Parse-Decimal $matches[1]
                if ($matches[2] -eq "D") { $eindSaldo = -$eindSaldo }
                Write-Host "  Eind saldo: €$($eindSaldo.ToString('N2'))" -ForegroundColor Yellow
            } else {
                Write-Host "  Geen eind saldo gevonden" -ForegroundColor Red
            }
            
            # Extract date range
            $fromDate = $null
            $toDate = $null
            $aanmaakDatum = $null
            if ($content -match "Datum vanaf\s+(\d{2}-\d{2}-\d{4})") {
                $fromDate = [DateTime]::ParseExact($matches[1], "dd-MM-yyyy", $null)
            }
            if ($content -match "Datum tot en met\s+(\d{2}-\d{2}-\d{4})") {
                $toDate = [DateTime]::ParseExact($matches[1], "dd-MM-yyyy", $null)
            }
            if ($content -match "Datum aanmaak afschrift\s*\r?\n\s*(\d{2}-\d{2}-\d{4})") {
                try {
                    $aanmaakDatum = [DateTime]::ParseExact($matches[1], "dd-MM-yyyy", $null)
                    Write-Host "  Datum aanmaak afschrift: $($aanmaakDatum.ToString('dd-MM-yyyy'))" -ForegroundColor Yellow
                } catch {
                    $aanmaakDatum = $null
                    Write-Host "  Fout bij parsen aanmaak datum: $($matches[1])" -ForegroundColor Red
                }
            } else {
                $aanmaakDatum = $null
                Write-Host "  Geen 'Datum aanmaak afschrift' gevonden" -ForegroundColor Yellow
            }
            
            $balanceData[$fullIban] = @{
                BeginSaldo = $beginSaldo
                EindSaldo = $eindSaldo
                FromDate = $fromDate
                ToDate = $toDate
                AanmaakDatum = $aanmaakDatum
                FileName = $balanceFile.Name
            }
            
            Write-Host "  Account: $fullIban" -ForegroundColor Cyan
            Write-Host "  Periode: $($fromDate.ToString('dd-MM-yyyy')) - $($toDate.ToString('dd-MM-yyyy'))" -ForegroundColor Cyan
            Write-Host "  Begin saldo: €$($beginSaldo.ToString('N2'))" -ForegroundColor Cyan
            Write-Host "  Eind saldo: €$($eindSaldo.ToString('N2'))" -ForegroundColor Cyan
        }
        
    } catch {
        Write-Host "  FOUT bij verwerken van $($balanceFile.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Converting Transaction Data ===" -ForegroundColor Green

$allTransactionsByAccount = @{}

        foreach ($csvFile in $csvFiles) {
            Write-Host "Processing: $($csvFile.Name)" -ForegroundColor Yellow
            
            try {
                $account = Extract-Account $csvFile.BaseName
                Write-Host "  Account: $account" -ForegroundColor Cyan
                
                # Read CSV content
                $lines = Get-Content $csvFile.FullName
                
                if ($lines.Count -le 1) {
                    Write-Host "  CSV bestand is leeg of heeft alleen een header" -ForegroundColor Yellow
                    continue
                }
                
                $transactions = @()
                
                # Get start balance from balance data if available
                $accountBalanceData = $balanceData[$account]
                $runningBalance = if ($accountBalanceData) { $accountBalanceData.BeginSaldo } else { 1000000 }
                
                Write-Host "  Start saldo: €$($runningBalance.ToString('N2'))" -ForegroundColor Cyan
                
                # Skip header, process data lines
                for ($i = 1; $i -lt $lines.Count; $i++) {
                    $line = $lines[$i]
                    if ([string]::IsNullOrWhiteSpace($line)) { continue }
                    
                    try {
                        $fields = Parse-CsvLine $line
                        
                        if ($fields.Count -lt 8) {
                            Write-Host "    Regel $($i+1): onvoldoende velden ($($fields.Count))" -ForegroundColor Yellow
                            continue
                        }
                        
                        # Create CSV row object with proper field mapping
                        $csvRow = [PSCustomObject]@{
                            IBAN = $fields[0].Trim('"', ' ')
                            Currency = $fields[1].Trim('"', ' ')
                            BIC = $fields[2].Trim('"', ' ')
                            Volgnr = $fields[3].Trim('"', ' ')
                            Datum = $fields[4].Trim('"', ' ')
                            ValueDate = $fields[5].Trim('"', ' ')
                            Bedrag = $fields[6].Trim('"', ' ')
                            BalanceAfter = $fields[7].Trim('"', ' ')
                            CounterpartyIban = if ($fields.Count -gt 8) { $fields[8].Trim('"', ' ') } else { "" }
                            CounterpartyName = if ($fields.Count -gt 9) { $fields[9].Trim('"', ' ') } else { "" }
                            UltimateParty = if ($fields.Count -gt 10) { $fields[10].Trim('"', ' ') } else { "" }
                            InitiatingParty = if ($fields.Count -gt 11) { $fields[11].Trim('"', ' ') } else { "" }
                            CounterpartyBIC = if ($fields.Count -gt 12) { $fields[12].Trim('"', ' ') } else { "" }
                            Code = if ($fields.Count -gt 13) { $fields[13].Trim('"', ' ') } else { "" }
                            BatchId = if ($fields.Count -gt 14) { $fields[14].Trim('"', ' ') } else { "" }
                            Reference = if ($fields.Count -gt 15) { $fields[15].Trim('"', ' ') } else { "" }
                            MandateRef = if ($fields.Count -gt 16) { $fields[16].Trim('"', ' ') } else { "" }
                            CreditorId = if ($fields.Count -gt 17) { $fields[17].Trim('"', ' ') } else { "" }
                            PaymentRef = if ($fields.Count -gt 18) { $fields[18].Trim('"', ' ') } else { "" }
                            Description1 = if ($fields.Count -gt 19) { $fields[19].Trim('"', ' ') } else { "" }
                            Description2 = if ($fields.Count -gt 20) { $fields[20].Trim('"', ' ') } else { "" }
                            Description3 = if ($fields.Count -gt 21) { $fields[21].Trim('"', ' ') } else { "" }
                        }
                        
                        # Calculate running balance
                        $amount = Parse-Decimal $csvRow.Bedrag
                        $runningBalance += $amount
                        
                        # Convert to RaboBank API format
                        $apiTransaction = ConvertTo-RaboBankTransaction -csvRow $csvRow -ownAccount $account -runningBalance $runningBalance
                        
                        $transaction = @{
                            Date = [DateTime]::Parse($csvRow.Datum)
                            ApiTransaction = $apiTransaction
                            Account = $account
                        }
                        
                        $transactions += $transaction
                        
                    } catch {
                        Write-Host "    Fout bij regel $($i+1): $($_.Exception.Message)" -ForegroundColor Red
                        continue
                    }
                }
                
                Write-Host "  Totaal transacties: $($transactions.Count)" -ForegroundColor Cyan
                
                if ($transactions.Count -eq 0) {
                    Write-Host "  Geen transacties gevonden, overslaan..." -ForegroundColor Yellow
                    continue
                }
                
                # Group by date for individual day files
                $transactionsByDate = $transactions | Group-Object { $_.Date.ToString("yyyy-MM-dd") } | Sort-Object Name
                
                # Determine date range for this account
                $accountStartDate = $startDate
                $accountEndDate = $endDate
                
                if (-not $accountStartDate -and $transactionsByDate.Count -gt 0) {
                    $accountStartDate = [DateTime]::ParseExact($transactionsByDate[0].Name, "yyyy-MM-dd", $null)
                }
                if (-not $accountEndDate -and $transactionsByDate.Count -gt 0) {
                    $accountEndDate = [DateTime]::ParseExact($transactionsByDate[-1].Name, "yyyy-MM-dd", $null)
                }
                
                if (-not $accountStartDate -or -not $accountEndDate) {
                    Write-Host "  Geen datum range beschikbaar voor dit account, overslaan..." -ForegroundColor Yellow
                    continue
                }
                
                Write-Host "  Datum range: $($accountStartDate.ToString('yyyy-MM-dd')) - $($accountEndDate.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan
                Write-Host "  Dagen met transacties: $($transactionsByDate.Count)" -ForegroundColor Cyan
                
                # Generate files for each day in the range
                $currentDate = $accountStartDate
                while ($currentDate -le $accountEndDate) {
                    $dateString = $currentDate.ToString("yyyy-MM-dd")
                    $dayGroup = $transactionsByDate | Where-Object { $_.Name -eq $dateString }
                    
                    if ($dayGroup) {
                        # Day with transactions
                        $dayTransactions = $dayGroup.Group | Sort-Object { $_.ApiTransaction.entryReference }
                        Write-Host "    $($currentDate.ToString('yyyy-MM-dd')): $($dayTransactions.Count) transacties" -ForegroundColor Gray
                        
                        # Create RaboBank API response structure
                        $apiTransactions = @($dayTransactions | ForEach-Object { $_.ApiTransaction })
                        $raboBankResponse = Build-RaboBankResponse -account $account -transactions $apiTransactions
                    } else {
                        # Day without transactions - create empty response
                        Write-Host "    $($currentDate.ToString('yyyy-MM-dd')): 0 transacties (lege file)" -ForegroundColor Gray
                        $raboBankResponse = Build-RaboBankResponse -account $account -transactions @()
                    }
                    
                    # Save to JSON file with consistent naming convention
                    $timestamp = "122048"
                    $outputFileName = "transactions_$($account)_$($currentDate.ToString('yyyyMMdd'))_$timestamp.json"
                    $outputFilePath = Join-Path $OutputPath $outputFileName
                    
                    $json = $raboBankResponse | ConvertTo-Json -Depth 10
                    $json | Out-File -FilePath $outputFilePath -Encoding UTF8
                    
                    Write-Host "      → $outputFileName" -ForegroundColor Green
                    
                    # Store for balance calculation
                    if (-not $allTransactionsByAccount.ContainsKey($account)) {
                        $allTransactionsByAccount[$account] = @()
                    }
                    
                    $dayTransactionsForBalance = if ($dayGroup) { $dayGroup.Group } else { @() }
                    $allTransactionsByAccount[$account] += @{ 
                        Date = $currentDate; 
                        Transactions = $dayTransactionsForBalance; 
                        RunningBalance = $runningBalance 
                    }
                    
                    $currentDate = $currentDate.AddDays(1)
                }
                
            } catch {
                Write-Host "  FOUT bij verwerken van $($csvFile.Name): $($_.Exception.Message)" -ForegroundColor Red
            }
        }

Write-Host ""
Write-Host "=== Generating Balance Data ===" -ForegroundColor Green
Write-Host "Creëren van balans JSON bestanden met echte saldo data..." -ForegroundColor Yellow

foreach ($accountData in $allTransactionsByAccount.GetEnumerator()) {
    $account = $accountData.Key
    $dailyData = $accountData.Value | Sort-Object { $_.Date }
    
    Write-Host "  Account: $account" -ForegroundColor Cyan
    
    # Get balance data for this account if available
    $accountBalanceData = $balanceData[$account]
    $startBalance = if ($accountBalanceData -and $accountBalanceData.BeginSaldo -ne $null) { 
        [double]$accountBalanceData.BeginSaldo 
    } else { 
        [double]1000000 
    }
    
    # Determine date range for balance generation
    $balanceStartDate = $startDate
    $balanceEndDate = $endDate
    
    if (-not $balanceStartDate -and $dailyData.Count -gt 0) {
        $balanceStartDate = ($dailyData | Sort-Object { $_.Date })[0].Date
    }
    if (-not $balanceEndDate -and $dailyData.Count -gt 0) {
        $balanceEndDate = ($dailyData | Sort-Object { $_.Date })[-1].Date
    }
    
    if (-not $balanceStartDate -or -not $balanceEndDate) {
        Write-Host "    Geen datum range beschikbaar voor balans generatie" -ForegroundColor Yellow
        continue
    }
    
    Write-Host "    Start saldo: €$($startBalance.ToString('N2'))" -ForegroundColor Cyan
    Write-Host "    Datum range: $($balanceStartDate.ToString('yyyy-MM-dd')) - $($balanceEndDate.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan
    
    # Create balance file for each day in range with running calculation
    $currentDate = $balanceStartDate
    $runningBalance = [double]$startBalance
    
    while ($currentDate -le $balanceEndDate) {
        # Find transaction data for this day
        $dayData = $dailyData | Where-Object { $_.Date.ToString("yyyy-MM-dd") -eq $currentDate.ToString("yyyy-MM-dd") }
        
        # Calculate balance changes for this day based on transactions
        if ($dayData -and $dayData.Transactions.Count -gt 0) {
            $dailyChange = 0
            foreach ($transaction in $dayData.Transactions) {
                # Get amount from transaction
                $amountStr = if ($transaction.ApiTransaction.transactionAmount) {
                    $transaction.ApiTransaction.transactionAmount.amount
                } else {
                    $transaction.ApiTransaction.amount
                }
                $amount = Parse-Decimal $amountStr
                
                # Determine if this is incoming or outgoing money based on credit/debit indicator
                if ($transaction.ApiTransaction.creditDebitIndicator -eq "Credit") {
                    $dailyChange += $amount
                } else {
                    $dailyChange -= $amount
                }
            }
            $runningBalance += $dailyChange
            Write-Host "    $($currentDate.ToString('yyyy-MM-dd')): €$($dailyChange.ToString('N2')) → €$($runningBalance.ToString('N2')) ($($dayData.Transactions.Count) transacties)" -ForegroundColor Gray
        } else {
            # No transactions for this day, keep previous balance
            Write-Host "    $($currentDate.ToString('yyyy-MM-dd')): €0,00 → €$($runningBalance.ToString('N2')) (0 transacties)" -ForegroundColor Gray
        }
        
        # Find the most recent transaction date up to current date for lastChangeDateTime
        $lastTransactionDate = $currentDate
        $transactionsUpToDate = $dailyData | Where-Object { $_.Date -le $currentDate -and $_.Transactions.Count -gt 0 } | Sort-Object { $_.Date } -Descending
        if ($transactionsUpToDate -and $transactionsUpToDate.Count -gt 0) {
            $lastTransactionDate = $transactionsUpToDate[0].Date
        }
        # Ensure we have a valid date
        if (-not $lastTransactionDate) {
            $lastTransactionDate = $currentDate
        }

        # Create RaboBank API balance structure (correct format)
        $raboBankBalance = @{
            account = @{
                currency = "EUR"
                iban = $account
            }
            balances = @(
                @{
                    balanceAmount = @{
                        amount = $runningBalance.ToString("F2")
                        currency = "EUR"
                    }
                    balanceType = "expected"
                    lastChangeDateTime = $lastTransactionDate.AddHours(14).AddMinutes(7).AddSeconds(17).ToString("yyyy-MM-ddTHH:mm:ssZ")
                },
                @{
                    balanceAmount = @{
                        amount = ($runningBalance * 0.85).ToString("F2")  # More realistic closing booked
                        currency = "EUR"
                    }
                    balanceType = "closingBooked"
                    referenceDate = $currentDate.ToString("yyyy-MM-dd")
                },
                @{
                    balanceAmount = @{
                        amount = ($runningBalance * 0.95).ToString("F2")  # More realistic interim booked
                        currency = "EUR"
                    }
                    balanceType = "interimBooked"
                    lastChangeDateTime = $lastTransactionDate.ToString("yyyy-MM-ddT00:00:00Z")
                }
            )
            piggyBanks = @(
                @{
                    piggyBankBalance = "20000.00"
                    piggyBankName = "Car"
                },
                @{
                    piggyBankBalance = "10000.00"
                    piggyBankName = "Vacation"
                }
            )
        }
        
        # Save balance JSON with consistent naming convention
        $timestamp = "122048"
        $outputFileName = "balance_$($account)_$($currentDate.ToString('yyyyMMdd'))_$timestamp.json"
        $outputFilePath = Join-Path $OutputPath $outputFileName
        
        $json = $raboBankBalance | ConvertTo-Json -Depth 10
        $json | Out-File -FilePath $outputFilePath -Encoding UTF8
        
        $transactionCount = if ($dayData) { $dayData.Transactions.Count } else { 0 }
        Write-Host "      → $outputFileName (Balance: €$($runningBalance.ToString('N2')))" -ForegroundColor Green
        
        $currentDate = $currentDate.AddDays(1)
    }
}

Write-Host ""
Write-Host "=== Conversie voltooid! ===" -ForegroundColor Green
Write-Host "JSON bestanden zijn opgeslagen in: $OutputPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Gebruik deze bestanden om MT940/CAMT.053 conversie te testen." -ForegroundColor Yellow
