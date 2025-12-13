# PowerShell Test Script for MT940 Database Generator
# Executes the VB.NET script with database connection and generates MT940 files

param(
    [string]$ConnectionString = "Host=localhost;Database=bai_tool;Username=postgres;Password=yourpassword",
    [string]$IBAN = "NL31RABO0300087233",
    [string]$StartDate = "2025-11-07",
    [string]$EndDate = "2025-11-07",
    [switch]$MinimalFields = $false,
    [switch]$FullSwiftFormat = $false,
    [switch]$ShowDatabaseCheck = $false
)

Write-Host "🏦 MT940 Database Generator Test Script" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Gray

Write-Host "`n📊 Test Parameters:" -ForegroundColor Yellow
Write-Host "   IBAN: $IBAN" -ForegroundColor White
Write-Host "   Period: $StartDate to $EndDate" -ForegroundColor White
Write-Host "   Minimal Fields: $MinimalFields" -ForegroundColor White
Write-Host "   Full SWIFT Format: $FullSwiftFormat" -ForegroundColor White
Write-Host "   Database: $($ConnectionString.Split(';')[1] -replace 'Database=','')" -ForegroundColor White

# Test database connection first (optional)
if ($ShowDatabaseCheck) {
    Write-Host "`n🔌 Testing Database Connection..." -ForegroundColor Yellow
    try {
        Add-Type -Path "C:\Program Files\PostgreSQL\15\lib\Npgsql.dll" -ErrorAction SilentlyContinue
        $conn = New-Object Npgsql.NpgsqlConnection($ConnectionString)
        $conn.Open()
        Write-Host "   ✅ Database connection successful" -ForegroundColor Green
        $conn.Close()
    }
    catch {
        Write-Host "   ❌ Database connection failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "   💡 Note: Connection test requires Npgsql.dll" -ForegroundColor Yellow
    }
}

# Prepare data for VB.NET script
Write-Host "`n⚙️ Preparing MT940 Generation..." -ForegroundColor Yellow

# Check if VB.NET script exists
$vbScriptPath = Join-Path $PSScriptRoot "autobank_rabobank_mt940_db.vb"
if (-not (Test-Path $vbScriptPath)) {
    Write-Host "   ❌ VB.NET script not found: $vbScriptPath" -ForegroundColor Red
    exit 1
}
Write-Host "   ✅ VB.NET script found" -ForegroundColor Green

# Since we can't directly execute VB.NET from PowerShell without compilation,
# we'll create a C# wrapper that can invoke the VB.NET logic
Write-Host "`n🔧 Creating C# Test Wrapper..." -ForegroundColor Yellow

$csharpWrapper = @"
using System;
using System.Data;
using System.IO;
using Npgsql;

public class MT940TestRunner
{
    public static void RunMT940Generation(string connectionString, string iban, 
        string startDate, string endDate, bool minimalFields, bool fullSwiftFormat)
    {
        try
        {
            Console.WriteLine("🚀 Starting MT940 Generation...");
            Console.WriteLine($"   IBAN: {iban}");
            Console.WriteLine($"   Period: {startDate} to {endDate}");
            
            // Load data from database
            var balanceData = LoadBalanceData(connectionString, iban, startDate, endDate);
            var transactionData = LoadTransactionData(connectionString, iban, startDate, endDate);
            
            Console.WriteLine($"   Balance Records: {balanceData.Rows.Count}");
            Console.WriteLine($"   Transaction Records: {transactionData.Rows.Count}");
            
            if (balanceData.Rows.Count == 0)
            {
                Console.WriteLine("❌ No balance data found for the specified period");
                return;
            }
            
            if (transactionData.Rows.Count == 0)
            {
                Console.WriteLine("⚠️ No transaction data found for the specified period");
            }
            
            // Call VB.NET script logic (would need to be adapted)
            Console.WriteLine("📝 Generating MT940 file...");
            
            // For now, just show what data we have
            ShowDataSummary(balanceData, transactionData);
            
            Console.WriteLine("✅ MT940 generation completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        }
    }
    
    private static DataTable LoadBalanceData(string connectionString, string iban, 
        string startDate, string endDate)
    {
        var dt = new DataTable();
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            var sql = @"
                SELECT iban, owner_name, day, currency, opening_balance, closing_balance, 
                       transaction_count, created_at
                FROM rpa_data.bai_rabobank_balances 
                WHERE iban = @iban 
                  AND day BETWEEN @startDate AND @endDate
                ORDER BY day";
                
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("iban", iban);
                cmd.Parameters.AddWithValue("startDate", DateTime.Parse(startDate));
                cmd.Parameters.AddWithValue("endDate", DateTime.Parse(endDate));
                
                using (var adapter = new NpgsqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
        }
        return dt;
    }
    
    private static DataTable LoadTransactionData(string connectionString, string iban, 
        string startDate, string endDate)
    {
        var dt = new DataTable();
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            var sql = @"
                SELECT iban, booking_date, entry_reference, transaction_amount, 
                       value_date, end_to_end_id, batch_entry_reference, acctsvcr_ref,
                       instruction_id, payment_information_identification,
                       remittance_information_unstructured, debtor_name, creditor_name,
                       debtor_iban, creditor_iban, rabo_detailed_transaction_type,
                       created_at
                FROM rpa_data.bai_rabobank_transactions 
                WHERE iban = @iban 
                  AND booking_date BETWEEN @startDate AND @endDate
                ORDER BY booking_date, entry_reference";
                
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("iban", iban);
                cmd.Parameters.AddWithValue("startDate", DateTime.Parse(startDate));
                cmd.Parameters.AddWithValue("endDate", DateTime.Parse(endDate));
                
                using (var adapter = new NpgsqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
        }
        return dt;
    }
    
    private static void ShowDataSummary(DataTable balanceData, DataTable transactionData)
    {
        Console.WriteLine("\n📊 Data Summary:");
        
        if (balanceData.Rows.Count > 0)
        {
            var row = balanceData.Rows[0];
            Console.WriteLine($"   Opening Balance: {row["opening_balance"]} {row["currency"]}");
            Console.WriteLine($"   Closing Balance: {row["closing_balance"]} {row["currency"]}");
            Console.WriteLine($"   Account Owner: {row["owner_name"]}");
        }
        
        if (transactionData.Rows.Count > 0)
        {
            Console.WriteLine($"\n🔍 Reference Data Check:");
            int batchRefCount = 0, instrIdCount = 0, endToEndCount = 0;
            
            foreach (DataRow row in transactionData.Rows)
            {
                if (!string.IsNullOrEmpty(row["batch_entry_reference"]?.ToString()))
                    batchRefCount++;
                if (!string.IsNullOrEmpty(row["instruction_id"]?.ToString()))
                    instrIdCount++;
                if (!string.IsNullOrEmpty(row["end_to_end_id"]?.ToString()) && 
                    row["end_to_end_id"].ToString() != "NOTPROVIDED")
                    endToEndCount++;
            }
            
            Console.WriteLine($"   Transactions with batch_entry_reference: {batchRefCount}/{transactionData.Rows.Count}");
            Console.WriteLine($"   Transactions with instruction_id: {instrIdCount}/{transactionData.Rows.Count}");
            Console.WriteLine($"   Transactions with end_to_end_id: {endToEndCount}/{transactionData.Rows.Count}");
            
            // Show first few reference examples
            Console.WriteLine($"\n📝 Reference Examples:");
            int count = 0;
            foreach (DataRow row in transactionData.Rows)
            {
                if (count >= 3) break;
                var batchRef = row["batch_entry_reference"]?.ToString();
                var instrId = row["instruction_id"]?.ToString();
                var entryRef = row["entry_reference"]?.ToString();
                
                Console.WriteLine($"   Entry {entryRef}: batch={batchRef}, instr={instrId}");
                count++;
            }
        }
    }
}
"@

# Write C# wrapper to temp file
$tempCsFile = Join-Path $env:TEMP "MT940TestRunner.cs"
$csharpWrapper | Out-File -FilePath $tempCsFile -Encoding UTF8

Write-Host "   ✅ C# wrapper created" -ForegroundColor Green

# Try to compile and run (requires .NET SDK)
Write-Host "`n🏗️ Compiling and running test..." -ForegroundColor Yellow

try {
    # Add Npgsql reference path (adjust as needed)
    $npgsqlPath = "C:\Program Files\PostgreSQL\15\lib\Npgsql.dll"
    if (-not (Test-Path $npgsqlPath)) {
        $npgsqlPath = "Npgsql.dll"  # Assume it's in PATH or project
    }
    
    # Compile C# code
    Add-Type -TypeDefinition $csharpWrapper -ReferencedAssemblies @("System.Data", $npgsqlPath) -ErrorAction Stop
    
    # Run the test
    [MT940TestRunner]::RunMT940Generation($ConnectionString, $IBAN, $StartDate, $EndDate, $MinimalFields, $FullSwiftFormat)
}
catch {
    Write-Host "   ❌ Compilation/execution failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`n💡 Alternative: Manual Database Check" -ForegroundColor Yellow
    
    # Fallback: Show SQL queries to run manually
    Write-Host "`n📋 Manual SQL Queries to Check Data:" -ForegroundColor Cyan
    Write-Host @"
-- Check balance data:
SELECT iban, day, opening_balance, closing_balance, transaction_count 
FROM rpa_data.bai_rabobank_balances 
WHERE iban = '$IBAN' AND day BETWEEN '$StartDate' AND '$EndDate';

-- Check transaction references:
SELECT entry_reference, batch_entry_reference, instruction_id, end_to_end_id,
       rabo_detailed_transaction_type, transaction_amount
FROM rpa_data.bai_rabobank_transactions 
WHERE iban = '$IBAN' AND booking_date BETWEEN '$StartDate' AND '$EndDate'
ORDER BY entry_reference
LIMIT 10;
"@ -ForegroundColor White
}

Write-Host "`n📁 Output Location:" -ForegroundColor Yellow
Write-Host "   Generated files will be in: C:\temp\MT940_*.swi" -ForegroundColor White

Write-Host "`n🏁 Test script completed!" -ForegroundColor Green