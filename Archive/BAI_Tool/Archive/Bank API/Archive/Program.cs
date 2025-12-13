using System;
using System.Globalization;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using RabobankZero;

// Parse command line arguments for date range
if (args.Length != 2)
{
    Console.WriteLine("Usage: dotnet run <dateFrom> <dateTo>");
    Console.WriteLine("Example: dotnet run 2020-08-01 2020-08-31");
    return;
}

string dateFromStr = args[0];
string dateToStr = args[1];

// Validate date format
if (!DateTime.TryParseExact(dateFromStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateFrom) ||
    !DateTime.TryParseExact(dateToStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTo))
{
    Console.WriteLine("Error: Invalid date format. Please use yyyy-MM-dd format.");
    return;
}

// Validate date range
if (dateFrom > dateTo)
{
    Console.WriteLine("Error: 'dateFrom' cannot be later than 'dateTo'.");
    return;
}

// Balance API 15-month limitation validation
DateTime fifteenMonthsAgo = DateTime.Now.AddMonths(-15);
if (dateFrom < fifteenMonthsAgo)
{
    Console.WriteLine($"[WARNING] Balance API limitation: Historical balance data is only available for the past 15 months.");
    Console.WriteLine($"[WARNING] Your requested start date ({dateFromStr}) is before {fifteenMonthsAgo:yyyy-MM-dd}.");
    Console.WriteLine($"[WARNING] Balance API may return incomplete or no data for dates before {fifteenMonthsAgo:yyyy-MM-dd}.");
    Console.WriteLine($"[WARNING] Consider adjusting your date range to start from {fifteenMonthsAgo:yyyy-MM-dd} or later for complete balance data.");
    Console.WriteLine();
}

try
{
    // Load configuration
    string configPath = "config.json";
    if (!File.Exists(configPath))
    {
        Console.WriteLine($"Error: Configuration file '{configPath}' not found.");
        return;
    }

    string configContent = await File.ReadAllTextAsync(configPath);
    var config = JsonConvert.DeserializeObject<Config>(configContent);

    if (config?.AccountIds == null || !config.AccountIds.Any())
    {
        Console.WriteLine("Error: No accounts configured in config.json");
        return;
    }

    Console.WriteLine($"[INFO] Processing {config.AccountIds.Count} account(s):");
    foreach (var account in config.AccountIds)
    {
        Console.WriteLine($"[INFO] - {account.Key} -> {account.Value}");
    }
    Console.WriteLine();

    // Process each account
    foreach (var account in config.AccountIds)
    {
        string iban = account.Key;
        string accountId = account.Value;
        
        Console.WriteLine($"[INFO] Starting processing for account {iban}...");
        
        try
        {
            await ProcessAccount(iban, accountId, dateFrom, dateTo, config);
            Console.WriteLine($"[SUCCESS] Completed processing for account {iban}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to process account {iban}: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine(); // Empty line between accounts
    }

    Console.WriteLine("[SUCCESS] All accounts processed!");
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] Application error: {ex.Message}");
    Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
}

async Task ProcessAccount(string iban, string accountId, DateTime dateFrom, DateTime dateTo, Config config)
{
    // Create API client and token manager
    var apiClient = new RabobankApiClient(config);
    var tokenManager = new TokenManager(config);

    // Get access token
    Console.WriteLine($"[{iban}] Loading access token...");
    var tokens = await tokenManager.LoadTokens();
    
    if (tokens == null)
    {
        throw new Exception("Failed to get access token");
    }

    Console.WriteLine($"[{iban}] Access token obtained successfully");

    // Get CAMT dataset with Balance API integration
    Console.WriteLine($"[{iban}] Retrieving CAMT dataset with Balance API integration...");
    Console.WriteLine($"[{iban}] Date range: {dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}");
    
    var camtDataSet = await apiClient.GetCamtDataSet(tokens, dateFrom, dateTo, accountId, iban);
    
    if (camtDataSet == null)
    {
        throw new Exception("Failed to retrieve CAMT dataset");
    }

    // Create Output directory if it doesn't exist
    string outputDir = Path.Combine(Environment.CurrentDirectory, "Output");
    Directory.CreateDirectory(outputDir);

    // Create a short identifier from IBAN for filename
    string ibanShort = iban.Replace("NL", "").Substring(0, 8);
    
    // Save to file with account identifier in Output folder
    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    string fileName = $"camt_dataset_{ibanShort}_{timestamp}.json";
    string outputPath = Path.Combine(outputDir, fileName);

    Console.WriteLine($"[{iban}] Writing CAMT dataset to: {outputPath}");
    
    // Convert to JSON
    string camtJson = JsonConvert.SerializeObject(camtDataSet, Formatting.Indented);
    Console.WriteLine($"[{iban}] CAMT JSON size: {camtJson.Length} characters");

    try
    {
        // Write to system temp directory first to avoid macOS permission issues
        string tempDir = Path.GetTempPath();
        string tempPath = Path.Combine(tempDir, fileName);
        
        await File.WriteAllTextAsync(tempPath, camtJson);
        
        // Copy to Output folder 
        File.Copy(tempPath, outputPath, true);
        
        // Clean up temp file
        File.Delete(tempPath);
        
        Console.WriteLine($"[{iban}] CAMT dataset written successfully to Output folder");
    }
    catch (Exception ex)
    {
        throw new Exception($"Failed to write CAMT dataset: {ex.Message}");
    }

    Console.WriteLine($"[{iban}] CAMT dataset generation completed!");
    Console.WriteLine($"[{iban}] File created: {outputPath}");
}

public class TokenResponse
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = "";
    
    [JsonProperty("token_type")]
    public string TokenType { get; set; } = "";
    
    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = "";
}

public class TransactionResponse
{
    [JsonProperty("transactions")]
    public List<Transaction> Transactions { get; set; } = new List<Transaction>();
    
    [JsonProperty("moreTransactionsAvailable")]
    public bool MoreTransactionsAvailable { get; set; }
}

public class Transaction
{
    [JsonProperty("transactionId")]
    public string TransactionId { get; set; } = "";
    
    [JsonProperty("status")]
    public string Status { get; set; } = "";
    
    [JsonProperty("transactionTimestamp")]
    public DateTime TransactionTimestamp { get; set; }
    
    [JsonProperty("amount")]
    public decimal Amount { get; set; }
    
    [JsonProperty("currency")]
    public string Currency { get; set; } = "";
    
    [JsonProperty("description")]
    public string Description { get; set; } = "";
    
    [JsonProperty("debtorName")]
    public string DebtorName { get; set; } = "";
    
    [JsonProperty("debtorIban")]
    public string DebtorIban { get; set; } = "";
    
    [JsonProperty("creditorName")]
    public string CreditorName { get; set; } = "";
    
    [JsonProperty("creditorIban")]
    public string CreditorIban { get; set; } = "";
    
    [JsonProperty("transactionType")]
    public string TransactionType { get; set; } = "";
}

public class CamtDataSet
{
    [JsonProperty("account_iban")]
    public string AccountIban { get; set; } = "";
    
    [JsonProperty("account_name")]
    public string AccountName { get; set; } = "";
    
    [JsonProperty("opening_balance")]
    public BalanceInfo OpeningBalance { get; set; } = new BalanceInfo();
    
    [JsonProperty("closing_balance")]
    public BalanceInfo ClosingBalance { get; set; } = new BalanceInfo();
    
    [JsonProperty("transactions")]
    public List<Transaction> Transactions { get; set; } = new List<Transaction>();
    
    [JsonProperty("date_from")]
    public string DateFrom { get; set; } = "";
    
    [JsonProperty("date_to")]
    public string DateTo { get; set; } = "";
    
    [JsonProperty("generation_timestamp")]
    public string GenerationTimestamp { get; set; } = "";
    
    [JsonProperty("total_transaction_count")]
    public int TotalTransactionCount { get; set; }
    
    [JsonProperty("debit_transaction_count")]
    public int DebitTransactionCount { get; set; }
    
    [JsonProperty("credit_transaction_count")]
    public int CreditTransactionCount { get; set; }
    
    [JsonProperty("total_debit_amount")]
    public decimal TotalDebitAmount { get; set; }
    
    [JsonProperty("total_credit_amount")]
    public decimal TotalCreditAmount { get; set; }
}

public class BalanceInfo
{
    [JsonProperty("amount")]
    public decimal Amount { get; set; }
    
    [JsonProperty("currency")]
    public string Currency { get; set; } = "EUR";
    
    [JsonProperty("date")]
    public string Date { get; set; } = "";
    
    [JsonProperty("balance_type")]
    public string BalanceType { get; set; } = "";
}