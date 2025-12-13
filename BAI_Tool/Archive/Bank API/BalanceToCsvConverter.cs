using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

class Program
{
    static void Main(string[] args)
    {
        var converter = new BalanceToCsvConverter();
        converter.ConvertBalanceToCsv();
        Console.WriteLine("Balance to CSV conversion complete.");
        Console.ReadKey();
    }
}

public class BalanceToCsvConverter
{
    public void ConvertBalanceToCsv()
    {
        try
        {
            string inputFile = @"TestDataConverter\Output\balance_NL31RABO0300087233_20250901_122048.json";
            string outputFileBalances = @"Output\balance_NL31RABO0300087233_20250901_122048.csv";
            string outputFilePiggyBanks = @"Output\piggybank_NL31RABO0300087233_20250901_122048.csv";
            
            if (!File.Exists(inputFile))
            {
                Console.WriteLine("Error: Input file not found: " + inputFile);
                return;
            }

            Console.WriteLine("Converting: " + inputFile);
            
            string jsonContent = File.ReadAllText(inputFile);
            var balanceData = ParseBalanceData(jsonContent);
            
            Console.WriteLine("Parsed " + balanceData.Balances.Count + " balances and " + balanceData.PiggyBanks.Count + " piggy banks");
            
            // Generate balance CSV
            string balanceCsvContent = GenerateBalanceCsv(balanceData);
            
            // Generate piggy bank CSV
            string piggyBankCsvContent = GeneratePiggyBankCsv(balanceData);
            
            // Ensure output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputFileBalances));
            
            File.WriteAllText(outputFileBalances, balanceCsvContent, Encoding.UTF8);
            File.WriteAllText(outputFilePiggyBanks, piggyBankCsvContent, Encoding.UTF8);
            
            Console.WriteLine("Generated: " + outputFileBalances);
            Console.WriteLine("Generated: " + outputFilePiggyBanks);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            Console.WriteLine("Stack trace: " + ex.StackTrace);
        }
    }

    private BalanceData ParseBalanceData(string json)
    {
        var balanceData = new BalanceData();
        
        var lines = json.Split('\n');
        bool inBalancesArray = false;
        bool inPiggyBanksArray = false;
        string currentBalance = "";
        string currentPiggyBank = "";
        int braceCount = 0;
        
        // Parse account info first
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
                        balanceData.AccountIban = ExtractJsonStringValue(accountLine);
                    }
                    else if (accountLine.Contains("\"currency\":"))
                    {
                        balanceData.AccountCurrency = ExtractJsonStringValue(accountLine);
                    }
                }
            }
            
            // Parse piggyBanks array
            if (line.Contains("\"piggyBanks\":"))
            {
                inPiggyBanksArray = true;
                continue;
            }
            
            if (inPiggyBanksArray)
            {
                if (line.StartsWith("{"))
                {
                    currentPiggyBank = line + "\n";
                    braceCount = 1;
                }
                else if (braceCount > 0)
                {
                    currentPiggyBank += line + "\n";
                    
                    if (line.Contains("{"))
                        braceCount++;
                    if (line.Contains("}"))
                        braceCount--;
                    
                    if (braceCount == 0)
                    {
                        // Parse this piggy bank
                        var piggyBank = ParseSinglePiggyBank(currentPiggyBank, balanceData.AccountIban, balanceData.AccountCurrency);
                        if (piggyBank != null)
                            balanceData.PiggyBanks.Add(piggyBank);
                        currentPiggyBank = "";
                    }
                }
                
                if (line.Contains("]") && braceCount == 0)
                {
                    inPiggyBanksArray = false;
                }
            }
            
            // Parse balances array
            if (line.Contains("\"balances\":"))
            {
                inBalancesArray = true;
                continue;
            }
            
            if (inBalancesArray)
            {
                if (line.StartsWith("{"))
                {
                    currentBalance = line + "\n";
                    braceCount = 1;
                }
                else if (braceCount > 0)
                {
                    currentBalance += line + "\n";
                    
                    if (line.Contains("{"))
                        braceCount++;
                    if (line.Contains("}"))
                        braceCount--;
                    
                    if (braceCount == 0)
                    {
                        // Parse this balance
                        var balance = ParseSingleBalance(currentBalance, balanceData.AccountIban, balanceData.AccountCurrency);
                        if (balance != null)
                            balanceData.Balances.Add(balance);
                        currentBalance = "";
                    }
                }
                
                if (line.Contains("]") && braceCount == 0)
                {
                    inBalancesArray = false;
                }
            }
        }
        
        return balanceData;
    }

    private PiggyBankRecord ParseSinglePiggyBank(string json, string accountIban, string accountCurrency)
    {
        if (string.IsNullOrEmpty(json)) return null;
        
        var record = new PiggyBankRecord();
        record.AccountIban = accountIban;
        record.AccountCurrency = accountCurrency;
        
        var lines = json.Split('\n');
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim().TrimEnd(',');
            
            if (trimmedLine.Contains("\"piggyBankBalance\":"))
            {
                record.PiggyBankBalance = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"piggyBankName\":"))
            {
                record.PiggyBankName = ExtractJsonStringValue(trimmedLine);
            }
        }
        
        return record;
    }

    private BalanceRecord ParseSingleBalance(string json, string accountIban, string accountCurrency)
    {
        if (string.IsNullOrEmpty(json)) return null;
        
        var record = new BalanceRecord();
        record.AccountIban = accountIban;
        record.AccountCurrency = accountCurrency;
        
        var lines = json.Split('\n');
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim().TrimEnd(',');
            
            if (trimmedLine.Contains("\"balanceType\":"))
            {
                record.BalanceType = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"lastChangeDateTime\":"))
            {
                record.LastChangeDateTime = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"referenceDate\":"))
            {
                record.ReferenceDate = ExtractJsonStringValue(trimmedLine);
            }
            // Parse nested balanceAmount object
            else if (trimmedLine.Contains("\"amount\":") && record.BalanceAmount == null)
            {
                record.BalanceAmount = ExtractJsonStringValue(trimmedLine);
            }
            else if (trimmedLine.Contains("\"currency\":") && record.BalanceCurrency == null)
            {
                record.BalanceCurrency = ExtractJsonStringValue(trimmedLine);
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

    private string GenerateBalanceCsv(BalanceData balanceData)
    {
        var sb = new StringBuilder();
        
        // CSV Header
        sb.AppendLine("AccountIban,AccountCurrency,BalanceType,BalanceAmount,BalanceCurrency,LastChangeDateTime,ReferenceDate");
        
        // CSV Data
        foreach (var balance in balanceData.Balances)
        {
            sb.AppendLine(string.Join(",", 
                EscapeCsvField(balance.AccountIban),
                EscapeCsvField(balance.AccountCurrency),
                EscapeCsvField(balance.BalanceType),
                EscapeCsvField(balance.BalanceAmount),
                EscapeCsvField(balance.BalanceCurrency),
                EscapeCsvField(balance.LastChangeDateTime),
                EscapeCsvField(balance.ReferenceDate)
            ));
        }
        
        return sb.ToString();
    }

    private string GeneratePiggyBankCsv(BalanceData balanceData)
    {
        var sb = new StringBuilder();
        
        // CSV Header
        sb.AppendLine("AccountIban,AccountCurrency,PiggyBankName,PiggyBankBalance");
        
        // CSV Data
        foreach (var piggyBank in balanceData.PiggyBanks)
        {
            sb.AppendLine(string.Join(",", 
                EscapeCsvField(piggyBank.AccountIban),
                EscapeCsvField(piggyBank.AccountCurrency),
                EscapeCsvField(piggyBank.PiggyBankName),
                EscapeCsvField(piggyBank.PiggyBankBalance)
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

public class BalanceData
{
    public string AccountIban { get; set; }
    public string AccountCurrency { get; set; }
    public List<BalanceRecord> Balances { get; set; }
    public List<PiggyBankRecord> PiggyBanks { get; set; }
    
    public BalanceData()
    {
        Balances = new List<BalanceRecord>();
        PiggyBanks = new List<PiggyBankRecord>();
    }
}

public class BalanceRecord
{
    public string AccountIban { get; set; }
    public string AccountCurrency { get; set; }
    public string BalanceType { get; set; }
    public string BalanceAmount { get; set; }
    public string BalanceCurrency { get; set; }
    public string LastChangeDateTime { get; set; }
    public string ReferenceDate { get; set; }
}

public class PiggyBankRecord
{
    public string AccountIban { get; set; }
    public string AccountCurrency { get; set; }
    public string PiggyBankName { get; set; }
    public string PiggyBankBalance { get; set; }
}
