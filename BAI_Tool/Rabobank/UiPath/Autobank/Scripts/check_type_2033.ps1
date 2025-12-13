# Check type 2033 transaction data
$connString = "Host=localhost;Port=5432;Database=rabobank;Username=postgres;Password=postgres"

try {
    Add-Type -Path "C:\Program Files\PostgreSQL\16\Npgsql.dll"
    $conn = New-Object Npgsql.NpgsqlConnection($connString)
    $conn.Open()
    
    $query = @"
SELECT 
    entry_reference,
    batch_entry_reference,
    instruction_id,
    end_to_end_id,
    payment_information_identification,
    rabo_detailed_transaction_type,
    transaction_amount,
    debtor_iban,
    creditor_iban,
    remittance_information_unstructured
FROM dt_camt053_tx 
WHERE rabo_detailed_transaction_type = '2033'
   OR transaction_amount = 140130.32
ORDER BY entry_reference
LIMIT 10
"@
    
    $cmd = New-Object Npgsql.NpgsqlCommand($query, $conn)
    $reader = $cmd.ExecuteReader()
    
    Write-Host "`nType 2033 Transaction Data:" -ForegroundColor Cyan
    Write-Host "============================`n" -ForegroundColor Cyan
    
    while ($reader.Read()) {
        Write-Host "Entry Ref: $($reader['entry_reference'])" -ForegroundColor Yellow
        Write-Host "  Type: $($reader['rabo_detailed_transaction_type'])"
        Write-Host "  Amount: $($reader['transaction_amount'])"
        Write-Host "  Batch Entry Ref: $($reader['batch_entry_reference'])"
        Write-Host "  Instruction ID: $($reader['instruction_id'])"
        Write-Host "  End-to-End ID: $($reader['end_to_end_id'])"
        Write-Host "  Payment Info ID: $($reader['payment_information_identification'])"
        Write-Host "  Debtor IBAN: $($reader['debtor_iban'])"
        Write-Host "  Creditor IBAN: $($reader['creditor_iban'])"
        Write-Host "  Remittance: $($reader['remittance_information_unstructured'])"
        Write-Host ""
    }
    
    $reader.Close()
    $conn.Close()
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
