using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

class Program
{
    static void Main(string[] args)
    {
        var converter = new JsonToCsvConverter();
        converter.ConvertJsonToCsv();
        Console.WriteLine("JSON to CSV conversion complete.");
        Console.ReadKey();
    }
}

public class JsonToCsvConverter
{
    public void ConvertJsonToCsv()
    {
        try
        {
            string inputFile = @"TestDataConverter\Output\transactions_NL31RABO0300087233_20250901_122048.json";
            string outputFile = @"Output\transactions_NL31RABO0300087233_20250901_122048.csv";
            
            if (!File.Exists(inputFile))
            {
                Console.WriteLine("Error: Input file not found: " + inputFile);
                return;
            }

            Console.WriteLine("Converting: " + inputFile);
            
            string jsonContent = File.ReadAllText(inputFile);
            var transactions = ParseTransactions(jsonContent);
            
            Console.WriteLine("Parsed " + transactions.Count + " transactions");
            
            string csvContent = GenerateCsv(transactions);
            
            // Ensure output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
            
            File.WriteAllText(outputFile, csvContent, Encoding.UTF8);
            
            Console.WriteLine("Generated: " + outputFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            Console.WriteLine("Stack trace: " + ex.StackTrace);
        }
    }

    private List<TransactionRecord> ParseTransactions(string json)
    {
        var transactions = new List<TransactionRecord>();
        
        // Parse account info first
        string accountIban = "";
        string accountCurrency = "";
        
        var lines = json.Split('\n');
        bool inBookedArray = false;
        string currentTransaction = "";
        int braceCount = 0;
        
        // Parse account info
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            
            if (line.Contains("\"account\""))
            {
                // Look for IBAN and currency in next few lines
                for (int j = i; j < Math.Min(i + 10, lines.Length); j++)
                {
                    string accountLine = lines[j].Trim().TrimEnd(',');
                    if (accountLine.Contains("\"iban\":"))
                    {
                        accountIban = ExtractJsonStringValue(accountLine);
                    }
                    else if (accountLine.Contains("\"currency\":") && string.IsNullOrEmpty(accountCurrency))
                    {
                        accountCurrency = ExtractJsonStringValue(accountLine);
                    }
                }
            }
            
            if (line.Contains("\"booked\":"))
            {
                inBookedArray = true;
                continue;
            }
            
            if (inBookedArray)
            {
                if (line.StartsWith("{"))
                {
                    currentTransaction = line + "\n";
                    braceCount = 1;
                }
                else if (braceCount > 0)
                {
                    currentTransaction += line + "\n";
                    
                    if (line.Contains("{"))
                        braceCount++;
                    if (line.Contains("}"))
                        braceCount--;
                    
                    if (braceCount == 0)
                    {
                        // Parse this transaction
                        var transaction = ParseSingleTransaction(currentTransaction, accountIban, accountCurrency);
                        if (transaction != null)
                            transactions.Add(transaction);
                        currentTransaction = "";
                    }
                }
                
                if (line.Contains("]") && braceCount == 0)
                {
                    inBookedArray = false;
                }
            }
        }
        
        return transactions;
    }

    private TransactionRecord ParseSingleTransaction(string json, string accountIban, string accountCurrency)
    {
        if (string.IsNullOrEmpty(json)) return null;
        
        var record = new TransactionRecord();
        record.AccountIban = accountIban;
        record.AccountCurrency = accountCurrency;
        
        var lines = json.Split('\n');
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim().TrimEnd(',');
            
            if (trimmedLine.Contains("\"bookingDate\":"))
            {
                record.BookingDate = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"valueDate\":"))
            {
                record.ValueDate = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"raboBookingDateTime\":"))
            {
                record.RaboBookingDateTime = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"entryReference\":"))
            {
                record.EntryReference = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"remittanceInformationUnstructured\":"))
            {
                record.Description = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"raboDetailedTransactionType\":"))
            {
                record.RaboDetailedTransactionType = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"raboTransactionTypeName\":"))
            {
                record.RaboTransactionTypeName = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"reasonCode\":"))
            {
                record.ReasonCode = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"bankTransactionCode\":"))
            {
                record.BankTransactionCode = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"creditorAgent\":"))
            {
                record.CreditorAgent = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"initiatingPartyName\":"))
            {
                record.InitiatingPartyName = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"creditorName\":"))
            {
                record.CreditorName = ExtractJsonStringValue(trimmedLine);
            }
            // Parse nested objects
            else if (trimmedLine.Contains("\"amount\":") && record.TransactionAmount == null)
            {
                record.TransactionAmount = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"currency\":") && record.TransactionCurrency == null)
            {
                record.TransactionCurrency = ExtractJsonStringValue(trimmedLine);
            }
            // Parse debtor account IBAN
            else if (trimmedLine.Contains("\"iban\":") && string.IsNullOrEmpty(record.DebtorAccountIban))
            {
                // Look for context to determine if this is debtor or creditor
                bool isDebtorContext = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] == line)
                    {
                        // Check previous lines for context
                        for (int j = Math.Max(0, i-5); j < i; j++)
                        {
                            if (lines[j].Contains("\"debtorAccount\""))
                            {
                                isDebtorContext = true;
                                break;
                            }
                        }
                        break;
                    }
                }
                
                if (isDebtorContext)
                {
                    record.DebtorAccountIban = ExtractJsonStringValue(trimmedLine);
                }
                else if (string.IsNullOrEmpty(record.CreditorAccountIban))
                {
                    record.CreditorAccountIban = ExtractJsonStringValue(trimmedLine);
                }
            }
            // Parse balance after booking
            else if (trimmedLine.Contains("\"amount\":") && record.BalanceAfterBooking == null)
            {
                // Check if this is in balance context
                bool isBalanceContext = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] == line)
                    {
                        // Check previous lines for balance context
                        for (int j = Math.Max(0, i-5); j < i; j++)
                        {
                            if (lines[j].Contains("\"balanceAfterBooking\"") || lines[j].Contains("\"balanceAmount\""))
                            {
                                isBalanceContext = true;
                                break;
                            }
                        }
                        break;
                    }
                }
                
                if (isBalanceContext)
                {
                    record.BalanceAfterBooking = ExtractJsonStringValue(trimmedLine);
                }
            }
            else if (trimmedLine.Contains("\"balanceType\":"))
            {
                record.BalanceType = ExtractJsonStringValue(trimmedLine);
            }
        }
        
        return record;
    }

    private string ExtractJsonStringValue(string line)
    {
        int colonIndex = line.IndexOf(':');
        if (colonIndex == -1) return "";
        
        string value = line.Substring(colonIndex + 1).Trim();
        if (value.StartsWith("\"") && value.EndsWith("\""))
            return value.Substring(1, value.Length - 2);
        return value;
    }

    private string GenerateCsv(List<TransactionRecord> transactions)
    {
        var sb = new StringBuilder();
        
        // CSV Header
        sb.AppendLine("BookingDate,ValueDate,TransactionAmount,TransactionCurrency,CreditorName,Description,EntryReference,CreditorAccountIban,BankTransactionCode,BalanceAfterBooking,RaboBookingDateTime,ReasonCode,InitiatingPartyName,RaboDetailedTransactionType,RaboTransactionTypeName,CreditorAgent,DebtorAccountIban,BalanceType,AccountIban,AccountCurrency");
        
        // CSV Data
        foreach (var transaction in transactions)
        {
            sb.AppendLine(string.Join(",", 
                EscapeCsvField(transaction.BookingDate),
                EscapeCsvField(transaction.ValueDate),
                EscapeCsvField(transaction.TransactionAmount),
                EscapeCsvField(transaction.TransactionCurrency),
                EscapeCsvField(transaction.CreditorName),
                EscapeCsvField(transaction.Description),
                EscapeCsvField(transaction.EntryReference),
                EscapeCsvField(transaction.CreditorAccountIban),
                EscapeCsvField(transaction.BankTransactionCode),
                EscapeCsvField(transaction.BalanceAfterBooking),
                EscapeCsvField(transaction.RaboBookingDateTime),
                EscapeCsvField(transaction.ReasonCode),
                EscapeCsvField(transaction.InitiatingPartyName),
                EscapeCsvField(transaction.RaboDetailedTransactionType),
                EscapeCsvField(transaction.RaboTransactionTypeName),
                EscapeCsvField(transaction.CreditorAgent),
                EscapeCsvField(transaction.DebtorAccountIban),
                EscapeCsvField(transaction.BalanceType),
                EscapeCsvField(transaction.AccountIban),
                EscapeCsvField(transaction.AccountCurrency)
            ));
        }
        
        return sb.ToString();
    }

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";
        
        // Escape quotes and wrap in quotes if contains comma, quote, or newline
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            field = field.Replace("\"", "\"\"");
            return "\"" + field + "\"";
        }
        
        return field;
    }
}

public class TransactionRecord
{
    // Essential fields
    public string BookingDate { get; set; }
    public string ValueDate { get; set; }
    public string TransactionAmount { get; set; }
    public string TransactionCurrency { get; set; }
    public string CreditorName { get; set; }
    public string Description { get; set; }
    public string EntryReference { get; set; }
    
    // Additional useful fields
    public string CreditorAccountIban { get; set; }
    public string BankTransactionCode { get; set; }
    public string BalanceAfterBooking { get; set; }
    
    // Optional detailed fields
    public string RaboBookingDateTime { get; set; }
    public string ReasonCode { get; set; }
    public string InitiatingPartyName { get; set; }
    public string RaboDetailedTransactionType { get; set; }
    public string RaboTransactionTypeName { get; set; }
    public string CreditorAgent { get; set; }
    public string DebtorAccountIban { get; set; }
    public string BalanceType { get; set; }
    
    // Account level fields
    public string AccountIban { get; set; }
    public string AccountCurrency { get; set; }
}
