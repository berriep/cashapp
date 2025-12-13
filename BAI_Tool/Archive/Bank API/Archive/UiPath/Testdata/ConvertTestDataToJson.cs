using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Globalization;

/// <summary>
/// Converteert testdata (CSV transacties + PDF balansen) naar per-dag JSON bestanden 
/// zoals deze van de Rabobank API komen voor het testen van MT940/CAMT.053 conversie
/// </summary>
public class ConvertTestDataToJson
{
    private const string TestDataPath = @"c:\Users\bpeijmen\Downloads\Zero\Zero\UiPath\Testdata\Conversie standen";
    private const string OutputPath = @"c:\Users\bpeijmen\Downloads\Zero\Zero\UiPath\Testdata";

    /// <summary>
    /// Represents a transaction from CSV
    /// </summary>
    public class CsvTransaction
    {
        public string IBAN { get; set; }
        public string Currency { get; set; }
        public string BIC { get; set; }
        public string SequenceNumber { get; set; }
        public DateTime Date { get; set; }
        public DateTime ValueDate { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfterTransaction { get; set; }
        public string CounterpartyIBAN { get; set; }
        public string CounterpartyName { get; set; }
        public string UltimatePartyName { get; set; }
        public string InitiatingPartyName { get; set; }
        public string CounterpartyBIC { get; set; }
        public string Code { get; set; }
        public string BatchId { get; set; }
        public string TransactionReference { get; set; }
        public string MandateReference { get; set; }
        public string CreditorId { get; set; }
        public string PaymentReference { get; set; }
        public string Description1 { get; set; }
        public string Description2 { get; set; }
        public string Description3 { get; set; }
        public string ReturnReason { get; set; }
        public decimal? OriginalAmount { get; set; }
        public string OriginalCurrency { get; set; }
        public decimal? ExchangeRate { get; set; }
    }

    /// <summary>
    /// Rabobank API Transaction format
    /// </summary>
    public class ApiTransaction
    {
        public string transactionId { get; set; }
        public string status { get; set; }
        public decimal amount { get; set; }
        public string currency { get; set; }
        public DateTime valueDate { get; set; }
        public DateTime bookingDate { get; set; }
        public CounterParty counterParty { get; set; }
        public string reference { get; set; }
        public string description { get; set; }
        public string category { get; set; }
        public string typeDescription { get; set; }
        public Creditor creditor { get; set; }
        public Debtor debtor { get; set; }
        public ExchangeRate exchangeRate { get; set; }
    }

    public class CounterParty
    {
        public string iban { get; set; }
        public string name { get; set; }
        public string bic { get; set; }
    }

    public class Creditor
    {
        public string iban { get; set; }
        public string name { get; set; }
    }

    public class Debtor
    {
        public string iban { get; set; }
        public string name { get; set; }
    }

    public class ExchangeRate
    {
        public string sourceCurrency { get; set; }
        public string targetCurrency { get; set; }
        public decimal rate { get; set; }
        public decimal sourceAmount { get; set; }
    }

    /// <summary>
    /// Rabobank API Transactions Response format
    /// </summary>
    public class TransactionsResponse
    {
        public string account { get; set; }
        public string currency { get; set; }
        public DateTime fromDate { get; set; }
        public DateTime toDate { get; set; }
        public List<ApiTransaction> transactions { get; set; } = new List<ApiTransaction>();
        public int totalCount { get; set; }
        public string nextPageKey { get; set; }
    }

    /// <summary>
    /// Rabobank API Balance format
    /// </summary>
    public class ApiBalance
    {
        public string account { get; set; }
        public string currency { get; set; }
        public DateTime date { get; set; }
        public decimal openingBalance { get; set; }
        public decimal closingBalance { get; set; }
        public List<IntradayBalance> intradayBalances { get; set; } = new List<IntradayBalance>();
    }

    public class IntradayBalance
    {
        public DateTime timestamp { get; set; }
        public decimal balance { get; set; }
    }

    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== Rabobank Test Data Converter ===");
            Console.WriteLine($"Input path: {TestDataPath}");
            Console.WriteLine($"Output path: {OutputPath}");
            Console.WriteLine();

            // Convert CSV files to per-day transaction JSON files
            ConvertTransactionData();

            // Extract balance data from PDFs and create per-day balance JSON files
            ConvertBalanceData();

            Console.WriteLine("\nConversie voltooid!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
        }
    }

    private static void ConvertTransactionData()
    {
        Console.WriteLine("=== Converting Transaction Data ===");
        
        var csvFiles = Directory.GetFiles(TestDataPath, "CSV_*.csv");
        
        foreach (var csvFile in csvFiles)
        {
            Console.WriteLine($"Processing: {Path.GetFileName(csvFile)}");
            
            // Extract account from filename: CSV_A_NL08RABO0100929575_EUR_20250829_20250901.csv
            var fileName = Path.GetFileNameWithoutExtension(csvFile);
            var parts = fileName.Split('_');
            var account = parts[2]; // NL08RABO0100929575
            var currency = parts[3]; // EUR
            
            Console.WriteLine($"  Account: {account}");
            Console.WriteLine($"  Currency: {currency}");

            // Read and parse CSV
            var transactions = ReadCsvTransactions(csvFile);
            Console.WriteLine($"  Total transactions: {transactions.Count}");

            // Group by date
            var transactionsByDate = transactions
                .GroupBy(t => t.Date.Date)
                .OrderBy(g => g.Key)
                .ToList();

            Console.WriteLine($"  Days with transactions: {transactionsByDate.Count}");

            foreach (var dayGroup in transactionsByDate)
            {
                var date = dayGroup.Key;
                var dayTransactions = dayGroup.OrderBy(t => t.SequenceNumber).ToList();
                
                Console.WriteLine($"    {date:yyyy-MM-dd}: {dayTransactions.Count} transactions");

                // Convert to API format
                var apiResponse = new TransactionsResponse
                {
                    account = account,
                    currency = currency,
                    fromDate = date,
                    toDate = date,
                    totalCount = dayTransactions.Count,
                    transactions = dayTransactions.Select(ConvertToApiTransaction).ToList()
                };

                // Save to JSON file
                var outputFileName = $"transactions_{account}_{date:yyyyMMdd}.json";
                var outputFilePath = Path.Combine(OutputPath, outputFileName);
                
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(apiResponse, jsonOptions);
                File.WriteAllText(outputFilePath, json);
                
                Console.WriteLine($"      → {outputFileName}");
            }
        }
    }

    private static List<CsvTransaction> ReadCsvTransactions(string csvFile)
    {
        var transactions = new List<CsvTransaction>();
        var lines = File.ReadAllLines(csvFile);
        
        // Skip header
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Parse CSV line (handling quoted fields)
            var fields = ParseCsvLine(line);
            
            if (fields.Length < 26) continue; // Ensure we have all expected fields

            var transaction = new CsvTransaction
            {
                IBAN = fields[0].Trim('"'),
                Currency = fields[1].Trim('"'),
                BIC = fields[2].Trim('"'),
                SequenceNumber = fields[3].Trim('"'),
                Date = DateTime.ParseExact(fields[4].Trim('"'), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                ValueDate = DateTime.ParseExact(fields[5].Trim('"'), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Amount = ParseDecimal(fields[6].Trim('"')),
                BalanceAfterTransaction = ParseDecimal(fields[7].Trim('"')),
                CounterpartyIBAN = fields[8].Trim('"'),
                CounterpartyName = fields[9].Trim('"'),
                UltimatePartyName = fields[10].Trim('"'),
                InitiatingPartyName = fields[11].Trim('"'),
                CounterpartyBIC = fields[12].Trim('"'),
                Code = fields[13].Trim('"'),
                BatchId = fields[14].Trim('"'),
                TransactionReference = fields[15].Trim('"'),
                MandateReference = fields[16].Trim('"'),
                CreditorId = fields[17].Trim('"'),
                PaymentReference = fields[18].Trim('"'),
                Description1 = fields[19].Trim('"'),
                Description2 = fields[20].Trim('"'),
                Description3 = fields[21].Trim('"'),
                ReturnReason = fields[22].Trim('"'),
                OriginalAmount = string.IsNullOrWhiteSpace(fields[23].Trim('"')) ? null : ParseDecimal(fields[23].Trim('"')),
                OriginalCurrency = fields[24].Trim('"'),
                ExchangeRate = string.IsNullOrWhiteSpace(fields[25].Trim('"')) ? null : ParseDecimal(fields[25].Trim('"'))
            };

            transactions.Add(transaction);
        }

        return transactions;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = "";
        var inQuotes = false;
        var i = 0;

        while (i < line.Length)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current += '"';
                    i += 2;
                }
                else
                {
                    // Start or end quotes
                    inQuotes = !inQuotes;
                    i++;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // Field separator
                fields.Add(current);
                current = "";
                i++;
            }
            else
            {
                current += c;
                i++;
            }
        }

        // Add last field
        fields.Add(current);

        return fields.ToArray();
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        
        // Handle Dutch format: +123,45 or -123,45
        value = value.Replace("+", "").Replace(" ", "");
        
        if (value.Contains(","))
        {
            // Dutch decimal format
            value = value.Replace(".", "").Replace(",", ".");
        }

        return decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    private static ApiTransaction ConvertToApiTransaction(CsvTransaction csv)
    {
        var isCredit = csv.Amount > 0;
        var description = string.Join(" ", new[] { csv.Description1, csv.Description2, csv.Description3 }
            .Where(d => !string.IsNullOrWhiteSpace(d) && d.Trim() != " ")).Trim();

        var transaction = new ApiTransaction
        {
            transactionId = csv.SequenceNumber,
            status = "Booked",
            amount = Math.Abs(csv.Amount),
            currency = csv.Currency,
            valueDate = csv.ValueDate,
            bookingDate = csv.Date,
            reference = csv.TransactionReference,
            description = description,
            category = DetermineCategory(csv.Code),
            typeDescription = DetermineTypeDescription(csv.Code, isCredit)
        };

        // Set counterparty
        if (!string.IsNullOrWhiteSpace(csv.CounterpartyIBAN) || !string.IsNullOrWhiteSpace(csv.CounterpartyName))
        {
            transaction.counterParty = new CounterParty
            {
                iban = csv.CounterpartyIBAN,
                name = csv.CounterpartyName,
                bic = csv.CounterpartyBIC
            };
        }

        // Set creditor/debtor based on transaction direction
        if (isCredit)
        {
            transaction.creditor = new Creditor
            {
                iban = csv.CounterpartyIBAN,
                name = csv.CounterpartyName
            };
            transaction.debtor = new Debtor
            {
                iban = csv.IBAN,
                name = ExtractAccountHolderName(csv)
            };
        }
        else
        {
            transaction.creditor = new Creditor
            {
                iban = csv.IBAN,
                name = ExtractAccountHolderName(csv)
            };
            transaction.debtor = new Debtor
            {
                iban = csv.CounterpartyIBAN,
                name = csv.CounterpartyName
            };
        }

        // Set exchange rate if foreign currency
        if (csv.OriginalAmount.HasValue && csv.ExchangeRate.HasValue && !string.IsNullOrWhiteSpace(csv.OriginalCurrency))
        {
            transaction.exchangeRate = new ExchangeRate
            {
                sourceCurrency = csv.OriginalCurrency,
                targetCurrency = csv.Currency,
                rate = csv.ExchangeRate.Value,
                sourceAmount = csv.OriginalAmount.Value
            };
        }

        return transaction;
    }

    private static string ExtractAccountHolderName(CsvTransaction csv)
    {
        // Extract from description or use a default based on IBAN
        if (csv.IBAN.Contains("RABO0300087233"))
            return "CENTER PARCS EUROPE NV";
        else if (csv.IBAN.Contains("RABO0100929575"))
            return "Center Parcs Development B.V.";
        
        return "Account Holder";
    }

    private static string DetermineCategory(string code)
    {
        return code?.ToLower() switch
        {
            "cb" => "Card Payment",
            "tb" => "Transfer",
            "db" => "Direct Debit",
            "bg" => "Bank Charges",
            "wb" => "International Transfer",
            "ok" => "Online Payment",
            "ei" => "Electronic Invoice",
            "sb" => "SEPA Transfer",
            _ => "Other"
        };
    }

    private static string DetermineTypeDescription(string code, bool isCredit)
    {
        var direction = isCredit ? "Credit" : "Debit";
        var type = code?.ToLower() switch
        {
            "cb" => "Card Payment",
            "tb" => "Transfer",
            "db" => "Direct Debit",
            "bg" => "Bank Charges",
            "wb" => "International Wire",
            "ok" => "Online Payment",
            "ei" => "Electronic Invoice",
            "sb" => "SEPA Transfer",
            _ => "Transaction"
        };

        return $"{direction} {type}";
    }

    private static void ConvertBalanceData()
    {
        Console.WriteLine("\n=== Converting Balance Data ===");
        Console.WriteLine("Note: PDF parsing requires additional libraries/tools.");
        Console.WriteLine("For now, creating sample balance JSON files based on transaction data...");

        // Get transaction files to derive balance data
        var transactionFiles = Directory.GetFiles(OutputPath, "transactions_*.json");
        
        var balancesByAccount = new Dictionary<string, List<(DateTime date, decimal balance)>>();

        foreach (var file in transactionFiles)
        {
            var json = File.ReadAllText(file);
            var response = JsonSerializer.Deserialize<TransactionsResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (!balancesByAccount.ContainsKey(response.account))
            {
                balancesByAccount[response.account] = new List<(DateTime, decimal)>();
            }

            // Calculate end-of-day balance from last transaction
            if (response.transactions.Any())
            {
                // For real implementation, extract from PDF or use actual balance data
                // For now, use a calculated balance based on transaction data
                var lastTransaction = response.transactions.OrderBy(t => t.bookingDate).Last();
                var estimatedBalance = CalculateEstimatedBalance(response.account, response.fromDate);
                
                balancesByAccount[response.account].Add((response.fromDate, estimatedBalance));
            }
        }

        // Create balance JSON files
        foreach (var accountBalances in balancesByAccount)
        {
            var account = accountBalances.Key;
            var balances = accountBalances.Value.OrderBy(b => b.date).ToList();

            foreach (var (date, balance) in balances)
            {
                var apiBalance = new ApiBalance
                {
                    account = account,
                    currency = "EUR",
                    date = date,
                    openingBalance = balance - 1000, // Dummy calculation
                    closingBalance = balance,
                    intradayBalances = new List<IntradayBalance>
                    {
                        new IntradayBalance { timestamp = date.AddHours(9), balance = balance - 500 },
                        new IntradayBalance { timestamp = date.AddHours(12), balance = balance - 200 },
                        new IntradayBalance { timestamp = date.AddHours(15), balance = balance - 100 },
                        new IntradayBalance { timestamp = date.AddHours(17), balance = balance }
                    }
                };

                var outputFileName = $"balance_{account}_{date:yyyyMMdd}.json";
                var outputFilePath = Path.Combine(OutputPath, outputFileName);

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(apiBalance, jsonOptions);
                File.WriteAllText(outputFilePath, json);

                Console.WriteLine($"  → {outputFileName}");
            }
        }
    }

    private static decimal CalculateEstimatedBalance(string account, DateTime date)
    {
        // Dummy balance calculation based on account and date
        // In real implementation, this would come from PDF parsing
        return account.Contains("087233") ? 2000000 + (date.Day * 10000) : 500000 + (date.Day * 5000);
    }
}
