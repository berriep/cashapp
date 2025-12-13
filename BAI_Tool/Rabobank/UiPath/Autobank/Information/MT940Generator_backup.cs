using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

class Program
{
    static void Main(string[] args)
    {
        var generator = new MT940Generator();
        generator.GenerateFromTestData();
        Console.WriteLine("MT940 generation complete.");
        Console.ReadKey();
    }
}

public class MT940Generator
{
    public void GenerateFromTestData()
    {
        try
        {
            string inputDir = @"TestDataConverter\Output";
            string outputDir = @"Output";
            
            if (!Directory.Exists(inputDir))
            {
                Console.WriteLine("Error: Input directory not found: " + inputDir);
                return;
            }

            var transactionFiles = Directory.GetFiles(inputDir, "transactions_*.json");
            var balanceFiles = Directory.GetFiles(inputDir, "balance_*.json");
            
            Console.WriteLine("Found " + transactionFiles.Length + " transaction files");
            Console.WriteLine("Found " + balanceFiles.Length + " balance files");

            foreach (string transactionFile in transactionFiles)
            {
                string iban = ExtractIbanFromFilename(transactionFile);
                string dateTimeStamp = ExtractDateTimeStampFromFilename(transactionFile);
                
                string balanceFile = Path.Combine(inputDir, "balance_" + iban + "_" + dateTimeStamp + ".json");
                
                Console.WriteLine("  Looking for IBAN: " + iban);
                Console.WriteLine("  Looking for dateTimeStamp: " + dateTimeStamp);
                Console.WriteLine("  Expected balance file: " + balanceFile);

                if (File.Exists(balanceFile))
                {
                    Console.WriteLine("Processing: " + transactionFile);
                    Console.WriteLine("With balance: " + balanceFile);
                    
                    string outputFile = Path.Combine(outputDir, "mt940_" + iban + "_" + dateTimeStamp + ".txt");
                    GenerateMT940(transactionFile, balanceFile, outputFile, iban, dateTimeStamp);
                    
                    Console.WriteLine("Generated: " + outputFile);
                }
                else
                {
                    Console.WriteLine("Warning: Balance file not found for " + transactionFile);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            Console.WriteLine("Stack trace: " + ex.StackTrace);
        }
    }

    private string ExtractIbanFromFilename(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        // transactions_NL08RABO0100929575_20250829_122048.json
        string[] parts = fileName.Split('_');
        if (parts.Length >= 2)
            return parts[1]; // IBAN
        return "UNKNOWN";
    }

    private string ExtractDateTimeStampFromFilename(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        // transactions_NL08RABO0100929575_20250829_122048.json
        string[] parts = fileName.Split('_');
        if (parts.Length >= 4)
            return parts[2] + "_" + parts[3]; // Date + timestamp
        return DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    private string ExtractDateFromTimeStamp(string dateTimeStamp)
    {
        // dateTimeStamp = "20250829_122048", extract "20250829"
        string[] parts = dateTimeStamp.Split('_');
        if (parts.Length >= 1)
            return parts[0];
        return DateTime.Now.ToString("yyyyMMdd");
    }

    private void GenerateMT940(string transactionFile, string balanceFile, string outputFile, string iban, string dateTimeStamp)
    {
        try
        {
            string date = ExtractDateFromTimeStamp(dateTimeStamp);
            
            string transactionJson = File.ReadAllText(transactionFile);
            string balanceJson = File.ReadAllText(balanceFile);
            
            var transactions = ParseTransactions(transactionJson);
            var balance = ParseBalance(balanceJson);
            
            string mt940Content = BuildMT940Content(transactions, balance, iban, date);
            File.WriteAllText(outputFile, mt940Content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error generating MT940 for " + iban + " on " + dateTimeStamp + ": " + ex.Message);
        }
    }

    private List<Transaction> ParseTransactions(string json)
    {
        var transactions = new List<Transaction>();
        
        // Parse Rabobank API structure: {"account": {...}, "transactions": {"booked": [...]}}
        var lines = json.Split('\n');
        bool inBookedArray = false;
        string currentTransaction = "";
        int braceCount = 0;
        
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            
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
                        var transaction = ParseRabobankTransaction(currentTransaction);
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
        
        Console.WriteLine("Parsed " + transactions.Count + " transactions");
        return transactions;
    }

    private Transaction ParseRabobankTransaction(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        
        var transaction = new Transaction();
        var lines = json.Split('\n');
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim().TrimEnd(',');
            
            if (trimmedLine.Contains("\"entryReference\":"))
            {
                transaction.TransactionId = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"bookingDate\":"))
            {
                transaction.BookingDate = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"valueDate\":"))
            {
                transaction.ValueDate = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"remittanceInformationUnstructured\":"))
            {
                transaction.Description = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"debtorName\":"))
            {
                transaction.CounterpartyName = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"initiatingPartyName\":"))
            {
                if (string.IsNullOrEmpty(transaction.CounterpartyName))
                    transaction.CounterpartyName = ExtractJsonStringValue(trimmedLine);
            }
            // Parse transactionAmount nested object
            else if (trimmedLine.Contains("\"currency\":") && transaction.Currency == null)
            {
                transaction.Currency = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"amount\":") && transaction.Amount == 0)
            {
                string amountStr = ExtractJsonStringValue(trimmedLine);
                // Replace comma with dot for decimal parsing
                amountStr = amountStr.Replace(",", ".");
                decimal amount;
                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                {
                    transaction.Amount = amount;
                    // Determine debit/credit based on balanceAfterBooking change
                    // For now, assume positive amounts are credit
                    transaction.DebitCreditIndicator = amount >= 0 ? "CRDT" : "DBIT";
                }
            }
        }
        
        return transaction;
    }

    private Balance ParseBalance(string json)
    {
        var balance = new Balance();
        
        // Parse Rabobank API structure: {"account": {...}, "balances": [...]}
        var lines = json.Split('\n');
        bool inBalancesArray = false;
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim().TrimEnd(',');
            
            // Parse account section
            if (trimmedLine.Contains("\"iban\":"))
            {
                balance.Iban = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"currency\":") && balance.Currency == null)
            {
                balance.Currency = ExtractJsonStringValue(trimmedLine);
            }
            // Parse balances array - look for closingBooked or interimBooked
            else if (trimmedLine.Contains("\"balances\":"))
            {
                inBalancesArray = true;
            }
            else if (inBalancesArray && trimmedLine.Contains("\"balanceType\":"))
            {
                string balanceType = ExtractJsonStringValue(trimmedLine);
                if (balanceType == "closingBooked" || balanceType == "interimBooked")
                {
                    // This is the balance we want, look for amount in next lines
                    for (int j = 0; j < lines.Length; j++)
                    {
                        if (lines[j] == line)
                        {
                            // Look ahead for amount in the balance object
                            for (int k = j; k < lines.Length && k < j + 10; k++)
                            {
                                string nextLine = lines[k].Trim().TrimEnd(',');
                                if (nextLine.Contains("\"amount\":"))
                                {
                                    string amountStr = ExtractJsonStringValue(nextLine);
                                    amountStr = amountStr.Replace(",", ".");
                                    decimal amount;
                                    if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                                    {
                                        balance.Amount = amount;
                                        break;
                                    }
                                }
                                if (nextLine.Contains("\"lastChangeDateTime\":"))
                                {
                                    balance.LastChangeDateTime = ExtractJsonStringValue(nextLine);
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }
        
        Console.WriteLine("Parsed balance: " + balance.Iban + " = " + balance.Amount + " " + balance.Currency);
        return balance;
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

    private string BuildMT940Content(List<Transaction> transactions, Balance balance, string iban, string date)
    {
        var sb = new StringBuilder();
        
        // Parse date for MT940 format (YYMMDD)
        DateTime statementDate;
        if (!DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out statementDate))
        {
            statementDate = DateTime.Now;
        }
        
        string mt940Date = statementDate.ToString("yyMMdd"); // YYMMDD format
        
        // Calculate opening balance from first transaction (if available)
        decimal currentBalance = balance.Amount;
        if (transactions.Count > 0)
        {
            // Use the balance from first transaction as starting point, then subtract first transaction to get opening
            currentBalance = balance.Amount - transactions[0].Amount;
        }
        
        // Process transactions in blocks of 5
        int blockSize = 5;
        int totalBlocks = (int)Math.Ceiling((double)transactions.Count / blockSize);
        
        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            // MT940 Headers {1} {2} {3} {4}
            sb.AppendLine("{1:F01RABONL2UXXXX0000000000}");
            sb.AppendLine("{2:I940RABONL2UXXXXN}");
            sb.AppendLine("{3:{108:MT940TEST}}");
            sb.AppendLine("{4:");
            
            // Generate statement reference (Tag :20:)
            string statementRef = iban;
            
            // Account identification (Tag :25:)
            string accountId = iban;
            
            // Statement number (Tag :28C:)
            string statementNumber = "001" + "/" + (blockIndex + 1).ToString("D3");
            
            // Opening balance for this block
            string indicator = currentBalance >= 0 ? "C" : "D";
            string formattedOpeningBalance = Math.Abs(currentBalance).ToString("0.00", CultureInfo.InvariantCulture).Replace(".", ",");
            string openingBalance = ":60M:" + indicator + mt940Date + balance.Currency + formattedOpeningBalance;
            
            // Build block header
            sb.AppendLine(":20:" + statementRef);
            sb.AppendLine(":25:" + accountId);
            sb.AppendLine(":28C:" + statementNumber);
            sb.AppendLine(openingBalance);
            
            // Add up to 5 transactions for this block
            int startIndex = blockIndex * blockSize;
            int endIndex = Math.Min(startIndex + blockSize, transactions.Count);
            
            for (int i = startIndex; i < endIndex; i++)
            {
                var transaction = transactions[i];
                
                // Update running balance
                currentBalance += transaction.Amount;
                
                string transactionLine = BuildTransactionLine(transaction, mt940Date);
                string informationLine = BuildInformationLine(transaction);
                
                sb.AppendLine(transactionLine);
                if (!string.IsNullOrEmpty(informationLine))
                {
                    sb.AppendLine(informationLine);
                }
            }
            
            // Closing balance for this block
            string closingIndicator = currentBalance >= 0 ? "C" : "D";
            string formattedClosingBalance = Math.Abs(currentBalance).ToString("0.00", CultureInfo.InvariantCulture).Replace(".", ",");
            string closingBalance = ":62M:" + closingIndicator + mt940Date + balance.Currency + formattedClosingBalance;
            
            sb.AppendLine(closingBalance);
            sb.AppendLine("-}");
            
            // Add empty line between blocks (except for last block)
            if (blockIndex < totalBlocks - 1)
            {
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }

    private string FormatIbanForMT940(string iban)
    {
        // MT940 typically uses BIC/Account format, but for simplicity we'll use IBAN
        // In production, this would map to proper bank code + account number
        return iban;
    }

    private string BuildBalanceTag(string tag, Balance balance, string date)
    {
        // Format: :60M:C150701EUR2043,70
        // C/D + Date + Currency + Amount
        
        string indicator = balance.Amount >= 0 ? "C" : "D";
        string formattedAmount = Math.Abs(balance.Amount).ToString("0.00", CultureInfo.InvariantCulture).Replace(".", ",");
        
        return ":" + tag + ":" + indicator + date + balance.Currency + formattedAmount;
    }

    private string BuildTransactionLine(Transaction transaction, string mt940Date)
    {
        // Format: :61:2507020702C1500,00NTRFNONREF//Business ST A
        // Date(YYMMDD) + ValueDate(MMDD) + C/D + Amount + Transaction Type + Reference + //Counterparty
        
        var sb = new StringBuilder();
        sb.Append(":61:");
        
        // Booking date (YYMMDD)
        DateTime bookingDate;
        if (DateTime.TryParseExact(transaction.BookingDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out bookingDate))
        {
            sb.Append(bookingDate.ToString("yyMMdd"));
        }
        else
        {
            sb.Append(mt940Date);
        }
        
        // Credit/Debit indicator
        sb.Append(transaction.DebitCreditIndicator == "DBIT" ? "D" : "C");
        
        // Amount (with comma as decimal separator)
        string formattedAmount = Math.Abs(transaction.Amount).ToString("0.00", CultureInfo.InvariantCulture).Replace(".", ",");
        sb.Append(formattedAmount);
        
        // Transaction type code (simplified)
        sb.Append("NTRF");
        
        // Reference
        if (!string.IsNullOrEmpty(transaction.TransactionId))
        {
            string sanitizedRef = SanitizeReference(transaction.TransactionId);
            sb.Append(sanitizedRef);
        }
        else
        {
            sb.Append("NONREF");
        }
        
        // Counterparty name (after //)
        if (!string.IsNullOrEmpty(transaction.CounterpartyName))
        {
            sb.Append("//");
            sb.Append(SanitizeText(transaction.CounterpartyName));
        }
        
        return sb.ToString();
    }

    private string BuildInformationLine(Transaction transaction)
    {
        // Format: :86:Description text
        
        var sb = new StringBuilder();
        
        if (!string.IsNullOrEmpty(transaction.Description) || !string.IsNullOrEmpty(transaction.CounterpartyName))
        {
            sb.Append(":86:");
            
            if (!string.IsNullOrEmpty(transaction.CounterpartyName))
            {
                sb.Append(SanitizeText(transaction.CounterpartyName));
                if (!string.IsNullOrEmpty(transaction.Description))
                {
                    sb.Append(" ");
                }
            }
            
            if (!string.IsNullOrEmpty(transaction.Description))
            {
                sb.Append(SanitizeText(transaction.Description));
            }
        }
        
        return sb.ToString();
    }

    private string SanitizeReference(string reference)
    {
        if (string.IsNullOrEmpty(reference)) return "";
        
        // Remove invalid characters for MT940 reference
        var sanitized = new StringBuilder();
        foreach (char c in reference)
        {
            if (char.IsLetterOrDigit(c) || c == '/' || c == '-')
            {
                sanitized.Append(c);
            }
        }
        
        // Limit length
        string result = sanitized.ToString();
        if (result.Length > 16)
            result = result.Substring(0, 16);
            
        return result;
    }

    private string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        
        // Remove invalid characters for MT940 text fields
        var sanitized = new StringBuilder();
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '/' || c == '-' || c == '.' || c == ',')
            {
                sanitized.Append(c);
            }
        }
        
        // Limit length
        string result = sanitized.ToString().Trim();
        if (result.Length > 65)
            result = result.Substring(0, 65);
            
        return result;
    }
}

public class Transaction
{
    public string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string DebitCreditIndicator { get; set; }
    public string BookingDate { get; set; }
    public string ValueDate { get; set; }
    public string Description { get; set; }
    public string CounterpartyName { get; set; }
    public string CounterpartyIban { get; set; }
}

public class Balance
{
    public string Iban { get; set; }
    public string Currency { get; set; }
    public decimal Amount { get; set; }
    public string LastChangeDateTime { get; set; }
}
