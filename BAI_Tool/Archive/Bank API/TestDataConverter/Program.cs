using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Globalization;

namespace TestDataConverter
{
    /// <summary>
    /// Converteert testdata (CSV transacties + PDF balansen) naar per-dag JSON bestanden 
    /// zoals deze van de Rabobank API komen voor het testen van MT940/CAMT.053 conversie
    /// </summary>
    class Program
    {
        private static readonly string TestDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        private static readonly string OutputPath = Path.Combine(Directory.GetCurrentDirectory(), "Output");

        static void Main(string[] args)
        {
            Console.WriteLine("=== Rabobank Test Data Converter ===");
            Console.WriteLine("Converteert CSV transacties naar per-dag JSON bestanden");
            Console.WriteLine("in Rabobank API formaat voor MT940/CAMT.053 test doeleinden");
            Console.WriteLine();

            try
            {
                // Ensure directories exist
                Directory.CreateDirectory(TestDataPath);
                Directory.CreateDirectory(OutputPath);

                Console.WriteLine($"Input directory: {TestDataPath}");
                Console.WriteLine($"Output directory: {OutputPath}");
                Console.WriteLine();

                // Check if test data exists
                if (!Directory.Exists(TestDataPath))
                {
                    Console.WriteLine($"FOUT: TestData directory niet gevonden: {TestDataPath}");
                    Console.WriteLine("Plaats CSV bestanden in de TestData folder en probeer opnieuw.");
                    return;
                }

                var csvFiles = Directory.GetFiles(TestDataPath, "*.csv");
                if (csvFiles.Length == 0)
                {
                    Console.WriteLine("FOUT: Geen CSV bestanden gevonden in TestData folder.");
                    Console.WriteLine("Verwachte bestandsnamen: CSV_A_[IBAN]_EUR_[YYYYMMDD]_[YYYYMMDD].csv");
                    return;
                }

                Console.WriteLine($"Gevonden CSV bestanden: {csvFiles.Length}");
                foreach (var file in csvFiles)
                {
                    Console.WriteLine($"  - {Path.GetFileName(file)}");
                }
                Console.WriteLine();

                // Convert transaction data
                ConvertTransactionData();

                // Generate balance data (simulated based on transactions)
                GenerateBalanceData();

                Console.WriteLine();
                Console.WriteLine("=== Conversie voltooid! ===");
                Console.WriteLine($"JSON bestanden zijn opgeslagen in: {OutputPath}");
                Console.WriteLine();
                Console.WriteLine("Gebruik deze bestanden om MT940/CAMT.053 conversie te testen.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FOUT: {ex.Message}");
                Console.WriteLine($"Details: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("Druk op een toets om af te sluiten...");
            Console.ReadKey();
        }

        private static void ConvertTransactionData()
        {
            Console.WriteLine("=== Converting Transaction Data ===");
            
            var csvFiles = Directory.GetFiles(TestDataPath, "*.csv");
            
            foreach (var csvFile in csvFiles)
            {
                Console.WriteLine($"Processing: {Path.GetFileName(csvFile)}");
                
                try
                {
                    // Extract account from filename
                    var fileName = Path.GetFileNameWithoutExtension(csvFile);
                    var account = ExtractAccountFromFilename(fileName);
                    
                    if (string.IsNullOrEmpty(account))
                    {
                        Console.WriteLine($"  WAARSCHUWING: Kan IBAN niet extraheren uit bestandsnaam: {fileName}");
                        account = "UNKNOWN_ACCOUNT";
                    }

                    Console.WriteLine($"  Account: {account}");

                    // Read and parse CSV
                    var transactions = ReadCsvTransactions(csvFile);
                    Console.WriteLine($"  Totaal transacties: {transactions.Count}");

                    if (transactions.Count == 0)
                    {
                        Console.WriteLine("  Geen transacties gevonden, overslaan...");
                        continue;
                    }

                    // Group by date
                    var transactionsByDate = transactions
                        .GroupBy(t => t.Date.Date)
                        .OrderBy(g => g.Key)
                        .ToList();

                    Console.WriteLine($"  Dagen met transacties: {transactionsByDate.Count}");

                    foreach (var dayGroup in transactionsByDate)
                    {
                        var date = dayGroup.Key;
                        var dayTransactions = dayGroup.OrderBy(t => t.SequenceNumber).ToList();
                        
                        Console.WriteLine($"    {date:yyyy-MM-dd}: {dayTransactions.Count} transacties");

                        // Convert to API format
                        var apiResponse = new TransactionsResponse
                        {
                            Account = account,
                            Currency = "EUR",
                            FromDate = date,
                            ToDate = date,
                            TotalCount = dayTransactions.Count,
                            Transactions = dayTransactions.Select(ConvertToApiTransaction).ToList()
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
                catch (Exception ex)
                {
                    Console.WriteLine($"  FOUT bij verwerken van {Path.GetFileName(csvFile)}: {ex.Message}");
                }
            }
        }

        private static string ExtractAccountFromFilename(string fileName)
        {
            // Expected format: CSV_A_NL08RABO0100929575_EUR_20250829_20250901
            var parts = fileName.Split('_');
            
            if (parts.Length >= 3)
            {
                return parts[2]; // Should be the IBAN
            }

            // Fallback: look for IBAN pattern in filename
            var tokens = fileName.Split('_', '-', ' ');
            foreach (var token in tokens)
            {
                if (token.StartsWith("NL") && token.Length >= 15)
                {
                    return token;
                }
            }

            return "";
        }

        private static List<CsvTransaction> ReadCsvTransactions(string csvFile)
        {
            var transactions = new List<CsvTransaction>();
            
            try
            {
                var lines = File.ReadAllLines(csvFile);
                
                if (lines.Length <= 1)
                {
                    Console.WriteLine("    CSV bestand is leeg of heeft alleen een header");
                    return transactions;
                }

                // Skip header
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var fields = ParseCsvLine(line);
                        
                        if (fields.Length < 8) 
                        {
                            Console.WriteLine($"    Regel {i+1}: onvoldoende velden ({fields.Length})");
                            continue;
                        }

                        var transaction = new CsvTransaction
                        {
                            IBAN = GetField(fields, 0),
                            Currency = GetField(fields, 1),
                            BIC = GetField(fields, 2),
                            SequenceNumber = GetField(fields, 3),
                            Date = ParseDate(GetField(fields, 4)),
                            ValueDate = ParseDate(GetField(fields, 5)),
                            Amount = ParseDecimal(GetField(fields, 6)),
                            BalanceAfterTransaction = ParseDecimal(GetField(fields, 7)),
                            CounterpartyIBAN = GetField(fields, 8),
                            CounterpartyName = GetField(fields, 9),
                            UltimatePartyName = GetField(fields, 10),
                            InitiatingPartyName = GetField(fields, 11),
                            CounterpartyBIC = GetField(fields, 12),
                            Code = GetField(fields, 13),
                            BatchId = GetField(fields, 14),
                            TransactionReference = GetField(fields, 15),
                            MandateReference = GetField(fields, 16),
                            CreditorId = GetField(fields, 17),
                            PaymentReference = GetField(fields, 18),
                            Description1 = GetField(fields, 19),
                            Description2 = GetField(fields, 20),
                            Description3 = GetField(fields, 21),
                            ReturnReason = GetField(fields, 22),
                            OriginalAmount = ParseOptionalDecimal(GetField(fields, 23)),
                            OriginalCurrency = GetField(fields, 24),
                            ExchangeRate = ParseOptionalDecimal(GetField(fields, 25))
                        };

                        transactions.Add(transaction);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Fout bij regel {i+1}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FOUT bij lezen CSV: {ex.Message}");
            }

            return transactions;
        }

        private static string GetField(string[] fields, int index)
        {
            if (index >= fields.Length) return "";
            return fields[index].Trim('"', ' ');
        }

        private static DateTime ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DateTime.MinValue;
            
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                return result;
            
            if (DateTime.TryParse(value, out result))
                return result;
                
            return DateTime.MinValue;
        }

        private static decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            
            // Handle Dutch format: +123,45 or -123,45
            value = value.Replace("+", "").Replace(" ", "");
            
            if (value.Contains(","))
            {
                // Handle European format: 1.234.567,89 -> 1234567.89
                var lastCommaIndex = value.LastIndexOf(',');
                if (lastCommaIndex > 0)
                {
                    var integerPart = value.Substring(0, lastCommaIndex).Replace(".", "");
                    var decimalPart = value.Substring(lastCommaIndex + 1);
                    value = integerPart + "." + decimalPart;
                }
                else
                {
                    // Simple comma as decimal separator
                    value = value.Replace(",", ".");
                }
            }

            if (decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal result))
                return result;
                
            return 0;
        }

        private static decimal? ParseOptionalDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var result = ParseDecimal(value);
            return result == 0 ? null : result;
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

        private static ApiTransaction ConvertToApiTransaction(CsvTransaction csv)
        {
            var isCredit = csv.Amount > 0;
            var description = string.Join(" ", new[] { csv.Description1, csv.Description2, csv.Description3 }
                .Where(d => !string.IsNullOrWhiteSpace(d) && d.Trim() != " ")).Trim();

            var transaction = new ApiTransaction
            {
                TransactionId = csv.SequenceNumber,
                Status = "Booked",
                Amount = Math.Abs(csv.Amount),
                Currency = csv.Currency,
                ValueDate = csv.ValueDate,
                BookingDate = csv.Date,
                Reference = csv.TransactionReference,
                Description = description,
                Category = DetermineCategory(csv.Code),
                TypeDescription = DetermineTypeDescription(csv.Code, isCredit)
            };

            // Set counterparty
            if (!string.IsNullOrWhiteSpace(csv.CounterpartyIBAN) || !string.IsNullOrWhiteSpace(csv.CounterpartyName))
            {
                transaction.CounterParty = new CounterParty
                {
                    Iban = csv.CounterpartyIBAN,
                    Name = csv.CounterpartyName,
                    Bic = csv.CounterpartyBIC
                };
            }

            // Set creditor/debtor based on transaction direction
            if (isCredit)
            {
                transaction.Creditor = new Creditor
                {
                    Iban = csv.CounterpartyIBAN,
                    Name = csv.CounterpartyName
                };
                transaction.Debtor = new Debtor
                {
                    Iban = csv.IBAN,
                    Name = ExtractAccountHolderName(csv)
                };
            }
            else
            {
                transaction.Creditor = new Creditor
                {
                    Iban = csv.IBAN,
                    Name = ExtractAccountHolderName(csv)
                };
                transaction.Debtor = new Debtor
                {
                    Iban = csv.CounterpartyIBAN,
                    Name = csv.CounterpartyName
                };
            }

            // Set exchange rate if foreign currency
            if (csv.OriginalAmount.HasValue && csv.ExchangeRate.HasValue && !string.IsNullOrWhiteSpace(csv.OriginalCurrency))
            {
                transaction.ExchangeRate = new ExchangeRate
                {
                    SourceCurrency = csv.OriginalCurrency,
                    TargetCurrency = csv.Currency,
                    Rate = csv.ExchangeRate.Value,
                    SourceAmount = csv.OriginalAmount.Value
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

        private static void GenerateBalanceData()
        {
            Console.WriteLine("\n=== Generating Balance Data ===");
            Console.WriteLine("Creëren van balans JSON bestanden gebaseerd op transactie data...");

            // Get transaction files to derive balance data
            var transactionFiles = Directory.GetFiles(OutputPath, "transactions_*.json");
            
            var balancesByAccount = new Dictionary<string, List<(DateTime date, decimal balance)>>();

            foreach (var file in transactionFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var response = JsonSerializer.Deserialize<TransactionsResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    if (response == null) continue;

                    if (!balancesByAccount.ContainsKey(response.Account))
                    {
                        balancesByAccount[response.Account] = new List<(DateTime, decimal)>();
                    }

                    // Calculate balance based on last transaction in day
                    if (response.Transactions.Any())
                    {
                        var lastTransaction = response.Transactions.OrderBy(t => t.BookingDate).Last();
                        var estimatedBalance = CalculateEstimatedBalance(response.Account, response.FromDate, response.Transactions);
                        
                        balancesByAccount[response.Account].Add((response.FromDate, estimatedBalance));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error processing {file}: {ex.Message}");
                }
            }

            // Create balance JSON files
            foreach (var accountBalances in balancesByAccount)
            {
                var account = accountBalances.Key;
                var balances = accountBalances.Value.OrderBy(b => b.date).ToList();

                foreach (var (date, balance) in balances)
                {
                    var random = new Random(date.GetHashCode()); // Consistent random for same date
                    var dailyVariation = (decimal)(random.NextDouble() * 100000);

                    var apiBalance = new ApiBalance
                    {
                        Account = account,
                        Currency = "EUR",
                        Date = date,
                        OpeningBalance = balance - dailyVariation,
                        ClosingBalance = balance,
                        IntradayBalances = new List<IntradayBalance>
                        {
                            new IntradayBalance { Timestamp = date.AddHours(9), Balance = balance - (dailyVariation * 0.8m) },
                            new IntradayBalance { Timestamp = date.AddHours(12), Balance = balance - (dailyVariation * 0.5m) },
                            new IntradayBalance { Timestamp = date.AddHours(15), Balance = balance - (dailyVariation * 0.2m) },
                            new IntradayBalance { Timestamp = date.AddHours(17), Balance = balance }
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

        private static decimal CalculateEstimatedBalance(string account, DateTime date, List<ApiTransaction> transactions)
        {
            // Use a base balance and add up transactions to estimate closing balance
            decimal baseBalance = account.Contains("087233") ? 6000000 : 1000000;
            
            // Add transaction effects
            decimal transactionSum = transactions.Sum(t => t.Amount);
            
            return baseBalance + transactionSum + (date.Day * 1000); // Add some date-based variation
        }
    }

    // Model classes for CSV data
    public class CsvTransaction
    {
        public string IBAN { get; set; } = "";
        public string Currency { get; set; } = "";
        public string BIC { get; set; } = "";
        public string SequenceNumber { get; set; } = "";
        public DateTime Date { get; set; }
        public DateTime ValueDate { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfterTransaction { get; set; }
        public string CounterpartyIBAN { get; set; } = "";
        public string CounterpartyName { get; set; } = "";
        public string UltimatePartyName { get; set; } = "";
        public string InitiatingPartyName { get; set; } = "";
        public string CounterpartyBIC { get; set; } = "";
        public string Code { get; set; } = "";
        public string BatchId { get; set; } = "";
        public string TransactionReference { get; set; } = "";
        public string MandateReference { get; set; } = "";
        public string CreditorId { get; set; } = "";
        public string PaymentReference { get; set; } = "";
        public string Description1 { get; set; } = "";
        public string Description2 { get; set; } = "";
        public string Description3 { get; set; } = "";
        public string ReturnReason { get; set; } = "";
        public decimal? OriginalAmount { get; set; }
        public string OriginalCurrency { get; set; } = "";
        public decimal? ExchangeRate { get; set; }
    }

    // Model classes for API JSON format
    public class TransactionsResponse
    {
        public string Account { get; set; } = "";
        public string Currency { get; set; } = "";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<ApiTransaction> Transactions { get; set; } = new List<ApiTransaction>();
        public int TotalCount { get; set; }
        public string? NextPageKey { get; set; }
    }

    public class ApiTransaction
    {
        public string TransactionId { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "";
        public DateTime ValueDate { get; set; }
        public DateTime BookingDate { get; set; }
        public CounterParty? CounterParty { get; set; }
        public string Reference { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string TypeDescription { get; set; } = "";
        public Creditor? Creditor { get; set; }
        public Debtor? Debtor { get; set; }
        public ExchangeRate? ExchangeRate { get; set; }
    }

    public class CounterParty
    {
        public string Iban { get; set; } = "";
        public string Name { get; set; } = "";
        public string Bic { get; set; } = "";
    }

    public class Creditor
    {
        public string Iban { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class Debtor
    {
        public string Iban { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class ExchangeRate
    {
        public string SourceCurrency { get; set; } = "";
        public string TargetCurrency { get; set; } = "";
        public decimal Rate { get; set; }
        public decimal SourceAmount { get; set; }
    }

    public class ApiBalance
    {
        public string Account { get; set; } = "";
        public string Currency { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<IntradayBalance> IntradayBalances { get; set; } = new List<IntradayBalance>();
    }

    public class IntradayBalance
    {
        public DateTime Timestamp { get; set; }
        public decimal Balance { get; set; }
    }
}
