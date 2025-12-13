# CAMT.053 Generator PowerShell Script
# Converteert JSON testdata naar CAMT.053 XML format

param(
    [string]$TestDataPath = ".\TestDataConverter\Output",
    [string]$OutputPath = ".\Output"
)

Write-Host "=== CAMT.053 Generator (PowerShell versie) ===" -ForegroundColor Green
Write-Host "Converteert JSON testdata naar CAMT.053 XML format"
Write-Host

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Find available accounts
function Get-AvailableAccounts {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        return @()
    }
    
    $accounts = @{}
    $balanceFiles = Get-ChildItem -Path $Path -Filter "balance_*.json"
    
    foreach ($file in $balanceFiles) {
        $fileName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $parts = $fileName.Split('_')
        if ($parts.Length -ge 2) {
            $iban = $parts[1]
            $accounts[$iban] = $true
        }
    }
    
    return $accounts.Keys | Sort-Object
}

# Find available dates for account
function Get-AvailableDates {
    param([string]$Account, [string]$Path)
    
    $dates = @{}
    $pattern = "balance_${Account}_*.json"
    $balanceFiles = Get-ChildItem -Path $Path -Filter $pattern
    
    foreach ($file in $balanceFiles) {
        $fileName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $parts = $fileName.Split('_')
        if ($parts.Length -ge 3) {
            $dateStr = $parts[2]  # YYYYMMDD
            if ($dateStr.Length -eq 8) {
                $formattedDate = "$($dateStr.Substring(0,4))-$($dateStr.Substring(4,2))-$($dateStr.Substring(6,2))"
                $dates[$formattedDate] = $true
            }
        }
    }
    
    return $dates.Keys | Sort-Object
}

# Check if statement can be generated
function Test-CanGenerateStatement {
    param([string]$Account, [string]$Date, [string]$Path)
    
    $dateObj = [DateTime]::ParseExact($Date, "yyyy-MM-dd", $null)
    $dateStr = $dateObj.ToString("yyyyMMdd")
    
    # Check for current balance (closing)
    $currentBalanceFile = Join-Path $Path "balance_${Account}_${dateStr}_122048.json"
    $hasCurrentBalance = Test-Path $currentBalanceFile
    
    # Check for transaction file
    $transactionFile = Join-Path $Path "transactions_${Account}_${dateStr}_122048.json"
    $hasTransactions = Test-Path $transactionFile
    
    # Check for opening balance (previous days)
    $hasOpeningBalance = $false
    for ($i = 1; $i -le 7; $i++) {
        $checkDate = $dateObj.AddDays(-$i).ToString("yyyyMMdd")
        $openingBalanceFile = Join-Path $Path "balance_${Account}_${checkDate}_122048.json"
        if (Test-Path $openingBalanceFile) {
            $hasOpeningBalance = $true
            break
        }
    }
    
    if (-not $hasCurrentBalance) {
        Write-Host "    $Date`: Geen closing balance bestand gevonden" -ForegroundColor Yellow
        return $false
    }
    
    if (-not $hasOpeningBalance) {
        Write-Host "    $Date`: Geen opening balance gevonden" -ForegroundColor Yellow
        return $false
    }
    
    if (-not $hasTransactions) {
        Write-Host "    $Date`: Geen transactie bestand gevonden (wel toegestaan voor balance-only statement)" -ForegroundColor Yellow
    }
    
    return $true
}

# Parse decimal value
function ConvertTo-Decimal {
    param([string]$Value)
    
    if ([string]::IsNullOrWhiteSpace($Value)) { return 0 }
    
    $Value = $Value.Replace(",", ".")
    $result = 0
    if ([decimal]::TryParse($Value, [System.Globalization.NumberStyles]::Number, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$result)) {
        return $result
    }
    
    return 0
}

# Generate CAMT.053 XML
function New-CAMT053Xml {
    param(
        [string]$Account,
        [string]$Date,
        [string]$Path,
        [string]$OutputPath
    )
    
    Write-Host "    Genereren CAMT.053 voor $Date..." -ForegroundColor Cyan
    
    try {
        # Load balance data
        $balanceData = Get-BalanceData -Account $Account -Date $Date -Path $Path
        $transactionData = Get-TransactionData -Account $Account -Date $Date -Path $Path
        
        # Generate XML
        $xml = Generate-CAMT053XML -BalanceData $balanceData -TransactionData $transactionData -Date $Date -Account $Account
        
        # Save file
        $outputFileName = "camt053_${Account}_$($Date.Replace('-', '')).xml"
        $outputFilePath = Join-Path $OutputPath $outputFileName
        
        $xml | Out-File -FilePath $outputFilePath -Encoding UTF8
        
        Write-Host "      ✓ Opgeslagen: $outputFileName" -ForegroundColor Green
        
        $fileInfo = Get-Item $outputFilePath
        Write-Host "        Bestand grootte: $($fileInfo.Length) bytes" -ForegroundColor Gray
        
        return $true
    }
    catch {
        Write-Host "      ✗ FOUT: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Get balance data
function Get-BalanceData {
    param([string]$Account, [string]$Date, [string]$Path)
    
    $dateObj = [DateTime]::ParseExact($Date, "yyyy-MM-dd", $null)
    $dateStr = $dateObj.ToString("yyyyMMdd")
    
    # Current balance (closing)
    $currentBalanceFile = Join-Path $Path "balance_${Account}_${dateStr}_122048.json"
    $closingBalance = $null
    
    if (Test-Path $currentBalanceFile) {
        $json = Get-Content $currentBalanceFile -Raw | ConvertFrom-Json
        $closingBalanceData = $json.balances | Where-Object { $_.balanceType -eq "closingBooked" } | Select-Object -First 1
        if ($closingBalanceData) {
            $amount = ConvertTo-Decimal $closingBalanceData.balanceAmount.amount
            $closingBalance = @{
                Amount = [Math]::Abs($amount)
                Currency = $closingBalanceData.balanceAmount.currency
                CreditDebitIndicator = if ($amount -ge 0) { "CRDT" } else { "DBIT" }
                Date = $Date
                IBAN = $json.account.iban
            }
        }
    }
    
    # Opening balance (previous day)
    $openingBalance = $null
    for ($i = 1; $i -le 7; $i++) {
        $checkDate = $dateObj.AddDays(-$i).ToString("yyyyMMdd")
        $openingBalanceFile = Join-Path $Path "balance_${Account}_${checkDate}_122048.json"
        if (Test-Path $openingBalanceFile) {
            $json = Get-Content $openingBalanceFile -Raw | ConvertFrom-Json
            $openingBalanceData = $json.balances | Where-Object { $_.balanceType -eq "closingBooked" } | Select-Object -First 1
            if ($openingBalanceData) {
                $amount = ConvertTo-Decimal $openingBalanceData.balanceAmount.amount
                $openingBalance = @{
                    Amount = [Math]::Abs($amount)
                    Currency = $openingBalanceData.balanceAmount.currency
                    CreditDebitIndicator = if ($amount -ge 0) { "CRDT" } else { "DBIT" }
                    Date = $dateObj.AddDays(-$i).ToString("yyyy-MM-dd")
                    IBAN = $json.account.iban
                }
                break
            }
        }
    }
    
    return @{
        OpeningBalance = $openingBalance
        ClosingBalance = $closingBalance
    }
}

# Get transaction data
function Get-TransactionData {
    param([string]$Account, [string]$Date, [string]$Path)
    
    $dateObj = [DateTime]::ParseExact($Date, "yyyy-MM-dd", $null)
    $dateStr = $dateObj.ToString("yyyyMMdd")
    
    $transactionFile = Join-Path $Path "transactions_${Account}_${dateStr}_122048.json"
    
    if (-not (Test-Path $transactionFile)) {
        return @()
    }
    
    $json = Get-Content $transactionFile -Raw | ConvertFrom-Json
    $transactions = @()
    
    if ($json.transactions) {
        foreach ($txn in $json.transactions) {
            $amount = ConvertTo-Decimal $txn.transactionAmount.amount
            $transaction = @{
                TransactionId = $txn.transactionId
                Amount = [Math]::Abs($amount)
                Currency = $txn.transactionAmount.currency
                CreditDebitIndicator = $txn.creditDebitIndicator
                BookingDate = $Date
                ValueDate = $Date
                RemittanceInfo = if ($txn.remittanceInformationUnstructured) { $txn.remittanceInformationUnstructured } else { "" }
                EndToEndId = if ($txn.endToEndId) { $txn.endToEndId } else { "" }
                DebtorName = if ($txn.debtorName) { $txn.debtorName } else { "" }
                CreditorName = if ($txn.creditorName) { $txn.creditorName } else { "" }
            }
            $transactions += $transaction
        }
    }
    
    return $transactions
}

# Generate CAMT.053 XML
function Generate-CAMT053XML {
    param($BalanceData, $TransactionData, [string]$Date, [string]$Account)
    
    $dateObj = [DateTime]::ParseExact($Date, "yyyy-MM-dd", $null)
    $messageId = "STMT$($dateObj.ToString('yyyyMMdd'))001"
    $statementId = "$($dateObj.ToString('yyyyMMdd'))001"
    $creationDateTime = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss")
    $iban = $BalanceData.ClosingBalance.IBAN
    
    $xml = @"
<?xml version="1.0" encoding="UTF-8"?>
<Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
  <BkToCstmrStmt>
    <GrpHdr>
      <MsgId>$messageId</MsgId>
      <CreDtTm>$creationDateTime</CreDtTm>
    </GrpHdr>
    <Stmt>
      <Id>$statementId</Id>
      <ElctrncSeqNb>1</ElctrncSeqNb>
      <CreDtTm>$creationDateTime</CreDtTm>
      <Acct>
        <Id>
          <IBAN>$iban</IBAN>
        </Id>
        <Ccy>$($BalanceData.ClosingBalance.Currency)</Ccy>
        <Ownr>
          <Nm>Test Account Holder</Nm>
        </Ownr>
        <Svcr>
          <FinInstnId>
            <BIC>RABONL2U</BIC>
          </FinInstnId>
        </Svcr>
      </Acct>
      <FrToDt>
        <FrDtTm>${Date}T00:00:00</FrDtTm>
        <ToDtTm>${Date}T23:59:59</ToDtTm>
      </FrToDt>
      <Bal>
        <Tp>
          <CdOrPrtry>
            <Cd>OPBD</Cd>
          </CdOrPrtry>
        </Tp>
        <Amt Ccy="$($BalanceData.OpeningBalance.Currency)">$($BalanceData.OpeningBalance.Amount.ToString("F2", [System.Globalization.CultureInfo]::InvariantCulture))</Amt>
        <CdtDbtInd>$($BalanceData.OpeningBalance.CreditDebitIndicator)</CdtDbtInd>
        <Dt>
          <Dt>$($BalanceData.OpeningBalance.Date)</Dt>
        </Dt>
      </Bal>
      <Bal>
        <Tp>
          <CdOrPrtry>
            <Cd>CLBD</Cd>
          </CdOrPrtry>
        </Tp>
        <Amt Ccy="$($BalanceData.ClosingBalance.Currency)">$($BalanceData.ClosingBalance.Amount.ToString("F2", [System.Globalization.CultureInfo]::InvariantCulture))</Amt>
        <CdtDbtInd>$($BalanceData.ClosingBalance.CreditDebitIndicator)</CdtDbtInd>
        <Dt>
          <Dt>$($BalanceData.ClosingBalance.Date)</Dt>
        </Dt>
      </Bal>
"@

    # Add transactions
    foreach ($transaction in $TransactionData) {
        $xml += @"

      <Ntry>
        <Amt Ccy="$($transaction.Currency)">$($transaction.Amount.ToString("F2", [System.Globalization.CultureInfo]::InvariantCulture))</Amt>
        <CdtDbtInd>$($transaction.CreditDebitIndicator)</CdtDbtInd>
        <Sts>BOOK</Sts>
        <BookgDt>
          <Dt>$($transaction.BookingDate)</Dt>
        </BookgDt>
        <ValDt>
          <Dt>$($transaction.ValueDate)</Dt>
        </ValDt>
        <BkTxCd>
          <Domn>
            <Cd>PMNT</Cd>
            <Fmly>
              <Cd>$(if ($transaction.CreditDebitIndicator -eq "CRDT") { "RCDT" } else { "ICDT" })</Cd>
              <SubFmlyCd>ESCT</SubFmlyCd>
            </Fmly>
          </Domn>
        </BkTxCd>
        <NtryDtls>
          <TxDtls>
            <Refs>
              <TxId>$($transaction.TransactionId)</TxId>
"@
        if ($transaction.EndToEndId) {
            $xml += @"

              <EndToEndId>$($transaction.EndToEndId)</EndToEndId>
"@
        }
        
        $xml += @"

            </Refs>
"@
        
        if ($transaction.DebtorName -or $transaction.CreditorName) {
            $xml += @"

            <RltdPties>
"@
            if ($transaction.DebtorName) {
                $xml += @"

              <Dbtr>
                <Nm>$([System.Security.SecurityElement]::Escape($transaction.DebtorName))</Nm>
              </Dbtr>
"@
            }
            if ($transaction.CreditorName) {
                $xml += @"

              <Cdtr>
                <Nm>$([System.Security.SecurityElement]::Escape($transaction.CreditorName))</Nm>
              </Cdtr>
"@
            }
            $xml += @"

            </RltdPties>
"@
        }
        
        if ($transaction.RemittanceInfo) {
            $xml += @"

            <RmtInf>
              <Ustrd>$([System.Security.SecurityElement]::Escape($transaction.RemittanceInfo))</Ustrd>
            </RmtInf>
"@
        }
        
        $xml += @"

          </TxDtls>
        </NtryDtls>
      </Ntry>
"@
    }

    $xml += @"

    </Stmt>
  </BkToCstmrStmt>
</Document>
"@

    return $xml
}

# Main execution
try {
    $accounts = Get-AvailableAccounts -Path $TestDataPath
    
    if ($accounts.Count -eq 0) {
        Write-Host "Geen JSON testdata gevonden in $TestDataPath" -ForegroundColor Red
        return
    }
    
    Write-Host "Gevonden accounts: $($accounts.Count)" -ForegroundColor Cyan
    foreach ($account in $accounts) {
        Write-Host "  - $account" -ForegroundColor Gray
    }
    Write-Host
    
    $totalGenerated = 0
    
    # Process each account
    foreach ($account in $accounts) {
        Write-Host "Verwerken van account: $account" -ForegroundColor Cyan
        
        $dates = Get-AvailableDates -Account $account -Path $TestDataPath
        
        if ($dates.Count -eq 0) {
            Write-Host "  Geen data gevonden voor account $account" -ForegroundColor Yellow
            continue
        }
        
        Write-Host "  Gevonden datums: $($dates -join ', ')" -ForegroundColor Gray
        
        foreach ($date in $dates) {
            if (Test-CanGenerateStatement -Account $account -Date $date -Path $TestDataPath) {
                if (New-CAMT053Xml -Account $account -Date $date -Path $TestDataPath -OutputPath $OutputPath) {
                    $totalGenerated++
                }
            }
        }
        
        Write-Host
    }
    
    Write-Host "=== CAMT.053 generatie voltooid! ===" -ForegroundColor Green
    Write-Host "Totaal gegenereerde bestanden: $totalGenerated" -ForegroundColor Cyan
    Write-Host "XML bestanden opgeslagen in: $(Resolve-Path $OutputPath)" -ForegroundColor Cyan
}
catch {
    Write-Host "FOUT: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Red
}

Write-Host
Write-Host "Druk op een toets om af te sluiten..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
