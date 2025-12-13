using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

class Program
{
    static void Main(string[] args)
    {
        var generator = new CAMT053Generator();
        generator.GenerateFromTestData();
        Console.WriteLine("CAMT.053 generation complete.");
        Console.ReadKey();
    }
}

public class CAMT053Generator
{
    public void GenerateFromTestData()
    {
        try
        {
            string inputDir = @"TestDataConverter\Output";
            string outputDir = @"Output";
            
            if (!Directory.Exists(inputDir))
            {
                Console.WriteLine("Error: Output directory not found: " + inputDir);
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
                    
                    string outputFile = Path.Combine(outputDir, "camt053_" + iban + "_" + dateTimeStamp + ".xml");
                    GenerateCAMT053(transactionFile, balanceFile, outputFile, iban, dateTimeStamp);
                    
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

    private string ExtractDateFromFilename(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        // transactions_NL08RABO0100929575_20250829_122048.json
        string[] parts = fileName.Split('_');
        if (parts.Length >= 3)
            return parts[2]; // Date part
        return DateTime.Now.ToString("yyyyMMdd");
    }

    private void GenerateCAMT053(string transactionFile, string balanceFile, string outputFile, string iban, string dateTimeStamp)
    {
        try
        {
            string date = ExtractDateFromTimeStamp(dateTimeStamp);
            
            string transactionJson = File.ReadAllText(transactionFile);
            string balanceJson = File.ReadAllText(balanceFile);
            
            var transactions = ParseTransactions(transactionJson);
            var balance = ParseBalance(balanceJson);
            
            string camt053Xml = BuildCAMT053Xml(transactions, balance, iban, date);
            File.WriteAllText(outputFile, camt053Xml, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error generating CAMT.053 for " + iban + " on " + dateTimeStamp + ": " + ex.Message);
        }
    }

    private string ExtractDateFromTimeStamp(string dateTimeStamp)
    {
        // dateTimeStamp = "20250829_122048", extract "20250829"
        string[] parts = dateTimeStamp.Split('_');
        if (parts.Length >= 1)
            return parts[0];
        return DateTime.Now.ToString("yyyyMMdd");
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
            else if (trimmedLine.Contains("\"initiatingPartyName\":"))
            {
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
                    // Note: this is simplified - in real parsing we'd track the object properly
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

    private Transaction ParseSingleTransaction(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        
        var transaction = new Transaction();
        var lines = json.Split('\n');
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim().TrimEnd(',');
            
            if (trimmedLine.Contains("\"transactionId\":"))
            {
                transaction.TransactionId = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"amount\":"))
            {
                transaction.Amount = ExtractJsonDecimalValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"currency\":"))
            {
                transaction.Currency = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"debitCreditIndicator\":"))
            {
                transaction.DebitCreditIndicator = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"bookingDate\":"))
            {
                transaction.BookingDate = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"valueDate\":"))
            {
                transaction.ValueDate = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"description\":"))
            {
                transaction.Description = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"counterpartyName\":"))
            {
                transaction.CounterpartyName = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"counterpartyIban\":"))
            {
                transaction.CounterpartyIban = ExtractJsonStringValue(trimmedLine);
            }
        }
        
        return transaction;
    }

    private List<string> SplitJsonArray(string json)
    {
        var result = new List<string>();
        int braceCount = 0;
        int start = 0;
        
        for (int i = 0; i < json.Length; i++)
        {
            if (json[i] == '{')
                braceCount++;
            else if (json[i] == '}')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    result.Add(json.Substring(start, i - start + 1));
                    start = i + 1;
                    // Skip comma and whitespace
                    while (start < json.Length && (json[start] == ',' || char.IsWhiteSpace(json[start])))
                        start++;
                    i = start - 1; // Will be incremented by for loop
                }
            }
        }
        
        return result;
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

    private decimal ExtractJsonDecimalValue(string line)
    {
        int colonIndex = line.IndexOf(':');
        if (colonIndex == -1) return 0;
        
        string value = line.Substring(colonIndex + 1).Trim();
        value = value.Trim('"', ',');
        
        decimal result;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return result;
        return 0;
    }

    private string BuildCAMT053Xml(List<Transaction> transactions, Balance balance, string iban, string date)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartDocument();
            
            // Root element
            writer.WriteStartElement("Document", "urn:iso:std:iso:20022:tech:xsd:camt.053.001.02");
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

            writer.WriteStartElement("BkToCstmrStmt");

            // Group Header
            writer.WriteStartElement("GrpHdr");
            writer.WriteElementString("MsgId", "STMT" + date + "001");
            writer.WriteElementString("CreDtTm", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
            writer.WriteEndElement(); // GrpHdr

            // Statement
            writer.WriteStartElement("Stmt");
            writer.WriteElementString("Id", "STMT" + date);
            writer.WriteElementString("CreDtTm", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));

            // Account
            writer.WriteStartElement("Acct");
            writer.WriteStartElement("Id");
            writer.WriteElementString("IBAN", iban);
            writer.WriteEndElement(); // Id
            writer.WriteElementString("Ccy", balance.Currency);
            writer.WriteStartElement("Ownr");
            writer.WriteElementString("Nm", "Account Holder");
            writer.WriteEndElement(); // Ownr
            writer.WriteEndElement(); // Acct

            // Balance - Opening
            writer.WriteStartElement("Bal");
            writer.WriteElementString("Tp", "OPBD");
            writer.WriteStartElement("Amt");
            writer.WriteAttributeString("Ccy", balance.Currency);
            writer.WriteValue(balance.Amount.ToString("F2", CultureInfo.InvariantCulture));
            writer.WriteEndElement(); // Amt
            writer.WriteElementString("CdtDbtInd", balance.Amount >= 0 ? "CRDT" : "DBIT");
            writer.WriteElementString("Dt", DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"));
            writer.WriteEndElement(); // Bal

            // Transactions
            foreach (var transaction in transactions)
            {
                writer.WriteStartElement("Ntry");
                
                writer.WriteStartElement("Amt");
                writer.WriteAttributeString("Ccy", transaction.Currency);
                writer.WriteValue(Math.Abs(transaction.Amount).ToString("F2", CultureInfo.InvariantCulture));
                writer.WriteEndElement(); // Amt
                
                writer.WriteElementString("CdtDbtInd", transaction.DebitCreditIndicator == "DBIT" ? "DBIT" : "CRDT");
                writer.WriteElementString("Sts", "BOOK");
                
                if (!string.IsNullOrEmpty(transaction.BookingDate))
                {
                    DateTime bookingDate;
                    if (DateTime.TryParseExact(transaction.BookingDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out bookingDate))
                    {
                        writer.WriteElementString("BookgDt", bookingDate.ToString("yyyy-MM-dd"));
                    }
                }
                
                if (!string.IsNullOrEmpty(transaction.ValueDate))
                {
                    DateTime valueDate;
                    if (DateTime.TryParseExact(transaction.ValueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out valueDate))
                    {
                        writer.WriteElementString("ValDt", valueDate.ToString("yyyy-MM-dd"));
                    }
                }

                // Transaction Details
                writer.WriteStartElement("NtryDtls");
                writer.WriteStartElement("TxDtls");
                
                if (!string.IsNullOrEmpty(transaction.TransactionId))
                {
                    writer.WriteStartElement("Refs");
                    writer.WriteElementString("EndToEndId", transaction.TransactionId);
                    writer.WriteEndElement(); // Refs
                }

                if (!string.IsNullOrEmpty(transaction.CounterpartyName) || !string.IsNullOrEmpty(transaction.CounterpartyIban))
                {
                    writer.WriteStartElement("RltdPties");
                    if (!string.IsNullOrEmpty(transaction.CounterpartyName))
                    {
                        writer.WriteStartElement("Cdtr");
                        writer.WriteElementString("Nm", transaction.CounterpartyName);
                        writer.WriteEndElement(); // Cdtr
                    }
                    if (!string.IsNullOrEmpty(transaction.CounterpartyIban))
                    {
                        writer.WriteStartElement("CdtrAcct");
                        writer.WriteStartElement("Id");
                        writer.WriteElementString("IBAN", transaction.CounterpartyIban);
                        writer.WriteEndElement(); // Id
                        writer.WriteEndElement(); // CdtrAcct
                    }
                    writer.WriteEndElement(); // RltdPties
                }

                if (!string.IsNullOrEmpty(transaction.Description))
                {
                    writer.WriteStartElement("RmtInf");
                    writer.WriteElementString("Ustrd", transaction.Description);
                    writer.WriteEndElement(); // RmtInf
                }

                writer.WriteEndElement(); // TxDtls
                writer.WriteEndElement(); // NtryDtls
                writer.WriteEndElement(); // Ntry
            }

            // Balance - Closing (same as opening for now)
            writer.WriteStartElement("Bal");
            writer.WriteElementString("Tp", "CLBD");
            writer.WriteStartElement("Amt");
            writer.WriteAttributeString("Ccy", balance.Currency);
            writer.WriteValue(balance.Amount.ToString("F2", CultureInfo.InvariantCulture));
            writer.WriteEndElement(); // Amt
            writer.WriteElementString("CdtDbtInd", balance.Amount >= 0 ? "CRDT" : "DBIT");
            writer.WriteElementString("Dt", DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"));
            writer.WriteEndElement(); // Bal

            writer.WriteEndElement(); // Stmt
            writer.WriteEndElement(); // BkToCstmrStmt
            writer.WriteEndElement(); // Document
        }

        return sb.ToString();
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
