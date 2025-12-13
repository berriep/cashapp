using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Test programma voor CAMT.053 generatie van JSON testdata
/// </summary>
public class CAMT053TestProgram
{
    private static readonly string TestDataPath = @".\TestDataConverter\Output";
    private static readonly string OutputPath = @".\Output";
    
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== CAMT.053 Generator Test ===");
            Console.WriteLine("Converteert JSON testdata naar CAMT.053 XML format");
            Console.WriteLine();
            
            // Ensure output directory exists
            Directory.CreateDirectory(OutputPath);
            
            // Find available accounts and dates
            var accounts = FindAvailableAccounts();
            
            if (accounts.Count == 0)
            {
                Console.WriteLine("Geen JSON testdata gevonden in " + TestDataPath);
                return;
            }
            
            Console.WriteLine($"Gevonden accounts: {accounts.Count}");
            foreach (var account in accounts)
            {
                Console.WriteLine($"  - {account}");
            }
            Console.WriteLine();
            
            // Process each account
            foreach (var account in accounts)
            {
                ProcessAccount(account);
            }
            
            Console.WriteLine("=== CAMT.053 generatie voltooid! ===");
            Console.WriteLine($"XML bestanden opgeslagen in: {Path.GetFullPath(OutputPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FOUT: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Details: {ex.InnerException.Message}");
            }
        }
        
        Console.WriteLine("\nDruk op een toets om af te sluiten...");
        Console.ReadKey();
    }
    
    /// <summary>
    /// Zoek beschikbare accounts in testdata
    /// </summary>
    private static List<string> FindAvailableAccounts()
    {
        if (!Directory.Exists(TestDataPath))
            return new List<string>();
            
        var accounts = new HashSet<string>();
        
        // Zoek in balance bestanden
        var balanceFiles = Directory.GetFiles(TestDataPath, "balance_*.json");
        foreach (var file in balanceFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var parts = fileName.Split('_');
            if (parts.Length >= 2)
            {
                accounts.Add(parts[1]); // IBAN
            }
        }
        
        return accounts.OrderBy(a => a).ToList();
    }
    
    /// <summary>
    /// Verwerk een specifiek account
    /// </summary>
    private static void ProcessAccount(string account)
    {
        Console.WriteLine($"Verwerken van account: {account}");
        
        try
        {
            // Find available dates for this account
            var dates = FindAvailableDates(account);
            
            if (dates.Count == 0)
            {
                Console.WriteLine($"  Geen data gevonden voor account {account}");
                return;
            }
            
            Console.WriteLine($"  Gevonden datums: {string.Join(", ", dates)}");
            
            // Process each date that has both balance and transaction data
            foreach (var date in dates)
            {
                if (CanGenerateStatement(account, date))
                {
                    GenerateCAMT053ForDate(account, date);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FOUT bij verwerken account {account}: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Zoek beschikbare datums voor een account
    /// </summary>
    private static List<string> FindAvailableDates(string account)
    {
        var dates = new HashSet<string>();
        
        // Zoek in balance bestanden
        var balancePattern = $"balance_{account}_*.json";
        var balanceFiles = Directory.GetFiles(TestDataPath, balancePattern);
        
        foreach (var file in balanceFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var parts = fileName.Split('_');
            if (parts.Length >= 3)
            {
                var dateStr = parts[2]; // YYYYMMDD
                if (dateStr.Length == 8)
                {
                    // Convert to YYYY-MM-DD format
                    var formattedDate = $"{dateStr.Substring(0, 4)}-{dateStr.Substring(4, 2)}-{dateStr.Substring(6, 2)}";
                    dates.Add(formattedDate);
                }
            }
        }
        
        return dates.OrderBy(d => d).ToList();
    }
    
    /// <summary>
    /// Controleer of we een statement kunnen genereren voor een specifieke datum
    /// </summary>
    private static bool CanGenerateStatement(string account, string date)
    {
        var dateObj = DateTime.ParseExact(date, "yyyy-MM-dd", null);
        var dateStr = dateObj.ToString("yyyyMMdd");
        var prevDateStr = dateObj.AddDays(-1).ToString("yyyyMMdd");
        
        // We hebben nodig:
        // - Balance van de statement datum (closing balance)
        // - Balance van de vorige dag (opening balance) 
        // - Transactions van de statement datum
        
        var currentBalanceFile = Path.Combine(TestDataPath, $"balance_{account}_{dateStr}_122048.json");
        var transactionFile = Path.Combine(TestDataPath, $"transactions_{account}_{dateStr}_122048.json");
        
        var hasCurrentBalance = File.Exists(currentBalanceFile);
        var hasTransactions = File.Exists(transactionFile);
        
        // Zoek een opening balance (van eerdere datum)
        var hasOpeningBalance = false;
        for (int i = 1; i <= 7; i++) // Zoek tot 7 dagen terug
        {
            var checkDate = dateObj.AddDays(-i).ToString("yyyyMMdd");
            var openingBalanceFile = Path.Combine(TestDataPath, $"balance_{account}_{checkDate}_122048.json");
            if (File.Exists(openingBalanceFile))
            {
                hasOpeningBalance = true;
                break;
            }
        }
        
        if (!hasCurrentBalance)
        {
            Console.WriteLine($"    {date}: Geen closing balance bestand gevonden");
            return false;
        }
        
        if (!hasOpeningBalance)
        {
            Console.WriteLine($"    {date}: Geen opening balance gevonden");
            return false;
        }
        
        if (!hasTransactions)
        {
            Console.WriteLine($"    {date}: Geen transactie bestand gevonden (wel toegestaan voor balance-only statement)");
        }
        
        return true;
    }
    
    /// <summary>
    /// Genereer CAMT.053 voor specifieke datum
    /// </summary>
    private static void GenerateCAMT053ForDate(string account, string date)
    {
        try
        {
            Console.WriteLine($"    Genereren CAMT.053 voor {date}...");
            
            // Collect balance files
            var balanceFiles = GetBalanceFiles(account, date);
            var transactionFiles = GetTransactionFiles(account, date);
            
            Console.WriteLine($"      Balance bestanden: {balanceFiles.Count}");
            Console.WriteLine($"      Transaction bestanden: {transactionFiles.Count}");
            
            // Configure generator
            var config = new CAMT053Config
            {
                AccountOwnerName = "Test Account Holder",
                ServicerBIC = "RABONL2U",
                ServicerName = "Rabobank Nederland",
                TimeZone = "Europe/Amsterdam",
                ValidationEnabled = true
            };
            
            // Generate CAMT.053
            var generator = new CAMT053Generator(config);
            var camt053Xml = generator.GenerateCAMT053(balanceFiles, transactionFiles, date);
            
            // Save to file
            var outputFileName = $"camt053_{account}_{date.Replace("-", "")}.xml";
            var outputFilePath = Path.Combine(OutputPath, outputFileName);
            
            File.WriteAllText(outputFilePath, camt053Xml, System.Text.Encoding.UTF8);
            
            Console.WriteLine($"      ✓ Opgeslagen: {outputFileName}");
            
            // Show file size
            var fileInfo = new FileInfo(outputFilePath);
            Console.WriteLine($"        Bestand grootte: {fileInfo.Length:N0} bytes");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      ✗ FOUT: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Verzamel balance bestanden voor een datum
    /// </summary>
    private static List<string> GetBalanceFiles(string account, string date)
    {
        var files = new List<string>();
        var dateObj = DateTime.ParseExact(date, "yyyy-MM-dd", null);
        
        // Voeg current balance toe (closing balance)
        var currentDateStr = dateObj.ToString("yyyyMMdd");
        var currentBalanceFile = Path.Combine(TestDataPath, $"balance_{account}_{currentDateStr}_122048.json");
        if (File.Exists(currentBalanceFile))
        {
            files.Add(currentBalanceFile);
        }
        
        // Zoek opening balance (eerdere datum)
        for (int i = 1; i <= 7; i++)
        {
            var checkDate = dateObj.AddDays(-i).ToString("yyyyMMdd");
            var openingBalanceFile = Path.Combine(TestDataPath, $"balance_{account}_{checkDate}_122048.json");
            if (File.Exists(openingBalanceFile))
            {
                files.Add(openingBalanceFile);
                break; // Neem de meest recente eerdere balance
            }
        }
        
        return files;
    }
    
    /// <summary>
    /// Verzamel transaction bestanden voor een datum
    /// </summary>
    private static List<string> GetTransactionFiles(string account, string date)
    {
        var files = new List<string>();
        var dateObj = DateTime.ParseExact(date, "yyyy-MM-dd", null);
        var dateStr = dateObj.ToString("yyyyMMdd");
        
        var transactionFile = Path.Combine(TestDataPath, $"transactions_{account}_{dateStr}_122048.json");
        if (File.Exists(transactionFile))
        {
            files.Add(transactionFile);
        }
        
        return files;
    }
}
