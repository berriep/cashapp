using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// CAMT.053 Generator Console Application (Standalone versie)
/// Converteert JSON testdata naar ISO 20022 CAMT.053 XML format
/// Gebruikt geen externe dependencies
/// </summary>
class CAMT053Standalone
{
    private static readonly string TestDataPath = @".\TestDataConverter\Output";
    private static readonly string OutputPath = @".\Output";
    
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== CAMT.053 Generator (Standalone) ===");
            Console.WriteLine("Converteert JSON testdata naar CAMT.053 XML format");
            Console.WriteLine();
            
            // Ensure output directory exists
            Directory.CreateDirectory(OutputPath);
            
            // Find available accounts and dates
            var accounts = FindAvailableAccounts();
            
            if (accounts.Count == 0)
            {
                Console.WriteLine($"Geen JSON testdata gevonden in {TestDataPath}");
                return;
            }
            
            Console.WriteLine($"Gevonden accounts: {accounts.Count}");
            foreach (var account in accounts)
            {
                Console.WriteLine($"  - {account}");
            }
            Console.WriteLine();
            
            int totalGenerated = 0;
            
            // Process each account
            foreach (var account in accounts)
            {
                totalGenerated += ProcessAccount(account);
            }
            
            Console.WriteLine("=== CAMT.053 generatie voltooid! ===");
            Console.WriteLine($"Totaal gegenereerde bestanden: {totalGenerated}");
            Console.WriteLine($"XML bestanden opgeslagen in: {Path.GetFullPath(OutputPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FOUT: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
    private static int ProcessAccount(string account)
    {
        Console.WriteLine($"Verwerken van account: {account}");
        int generatedCount = 0;
        
        try
        {
            // Find available dates for this account
            var dates = FindAvailableDates(account);
            
            if (dates.Count == 0)
            {
                Console.WriteLine($"  Geen data gevonden voor account {account}");
                return 0;
            }
            
            Console.WriteLine($"  Gevonden datums: {string.Join(", ", dates.ToArray())}");
            
            // Process each date that has both balance and transaction data
            foreach (var date in dates)
            {
                if (CanGenerateStatement(account, date))
                {
                    if (GenerateCAMT053ForDate(account, date))
                    {
                        generatedCount++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FOUT bij verwerken account {account}: {ex.Message}");
        }
        
        Console.WriteLine();
        return generatedCount;
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
        var dateObj = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dateStr = dateObj.ToString("yyyyMMdd");
        
        // We hebben nodig:
        // - Balance van de statement datum (closing balance)
        // - Balance van een eerdere dag (opening balance) 
        // - Transactions van de statement datum (optioneel)
        
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
    private static bool GenerateCAMT053ForDate(string account, string date)
    {
        try
        {
            Console.WriteLine($"    Genereren CAMT.053 voor {date}...");
            
            // Collect balance and transaction data
            var balanceData = GetBalanceData(account, date);
            var transactionData = GetTransactionData(account, date);
            
            Console.WriteLine($"      Opening balance: €{balanceData.OpeningBalance.Amount:F2} ({balanceData.OpeningBalance.CreditDebitIndicator})");
            Console.WriteLine($"      Closing balance: €{balanceData.ClosingBalance.Amount:F2} ({balanceData.ClosingBalance.CreditDebitIndicator})");
            Console.WriteLine($"      Transacties: {transactionData.Count}");
            
            // Generate CAMT.053 XML
            var camt053Xml = GenerateCAMT053XML(balanceData, transactionData, date);
            
            // Save to file
            var outputFileName = $"camt053_{account}_{date.Replace("-", "")}.xml";
            var outputFilePath = Path.Combine(OutputPath, outputFileName);
            
            File.WriteAllText(outputFilePath, camt053Xml, Encoding.UTF8);
            
            Console.WriteLine($"      ✓ Opgeslagen: {outputFileName}");
            
            // Show file size
            var fileInfo = new FileInfo(outputFilePath);
            Console.WriteLine($"        Bestand grootte: {fileInfo.Length:N0} bytes");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      ✗ FOUT: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Verzamel balance data voor een datum
    /// </summary>
    private static BalanceData GetBalanceData(string account, string date)
    {
        var dateObj = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dateStr = dateObj.ToString("yyyyMMdd");
        
        // Current balance (closing)
        var currentBalanceFile = Path.Combine(TestDataPath, $"balance_{account}_{dateStr}_122048.json");
        Balance closingBalance = null;
        string iban = null;
        string currency = "EUR";
        
        if (File.Exists(currentBalanceFile))
        {
            var jsonContent = File.ReadAllText(currentBalanceFile);
            iban = ExtractJsonValue(jsonContent, "iban");
            var currencyFromJson = ExtractJsonValue(jsonContent, "currency");
            if (!string.IsNullOrEmpty(currencyFromJson))
                currency = currencyFromJson;
            
            // Find closingBooked balance
            var closingBookedAmount = ExtractClosingBookedAmount(jsonContent);
            if (closingBookedAmount.HasValue)
            {
                var amount = closingBookedAmount.Value;
                closingBalance = new Balance
                {
                    Amount = Math.Abs(amount),
                    Currency = currency,
                    CreditDebitIndicator = amount >= 0 ? "CRDT" : "DBIT",
                    Date = date,
                    IBAN = iban
                };
            }
        }
        
        // Opening balance (previous day)
        Balance openingBalance = null;
        for (int i = 1; i <= 7; i++)
        {
            var checkDate = dateObj.AddDays(-i).ToString("yyyyMMdd");
            var openingBalanceFile = Path.Combine(TestDataPath, $"balance_{account}_{checkDate}_122048.json");
            if (File.Exists(openingBalanceFile))
            {
                var jsonContent = File.ReadAllText(openingBalanceFile);
                var openingBookedAmount = ExtractClosingBookedAmount(jsonContent);
                
                if (openingBookedAmount.HasValue)
                {
                    var amount = openingBookedAmount.Value;
                    openingBalance = new Balance
                    {
                        Amount = Math.Abs(amount),
                        Currency = currency,
                        CreditDebitIndicator = amount >= 0 ? "CRDT" : "DBIT",
                        Date = dateObj.AddDays(-i).ToString("yyyy-MM-dd"),
                        IBAN = iban
                    };
                    break;
                }
            }
        }
        
        if (openingBalance == null || closingBalance == null)
        {
            throw new Exception("Kan opening of closing balance niet vinden");
        }
        
        return new BalanceData
        {
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            IBAN = iban,
            Currency = currency
        };
    }
    
    /// <summary>
    /// Verzamel transaction data voor een datum
    /// </summary>
    private static List<Transaction> GetTransactionData(string account, string date)
    {
        var transactions = new List<Transaction>();
        var dateObj = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dateStr = dateObj.ToString("yyyyMMdd");
        
        var transactionFile = Path.Combine(TestDataPath, $"transactions_{account}_{dateStr}_122048.json");
        
        if (!File.Exists(transactionFile))
        {
            return transactions;
        }
        
        var jsonContent = File.ReadAllText(transactionFile);
        var transactionMatches = ExtractTransactions(jsonContent);
        
        foreach (var txnMatch in transactionMatches)
        {
            var transaction = new Transaction
            {
                TransactionId = txnMatch.TransactionId ?? Guid.NewGuid().ToString(),
                Amount = Math.Abs(txnMatch.Amount),
                Currency = txnMatch.Currency ?? "EUR",
                CreditDebitIndicator = txnMatch.CreditDebitIndicator ?? (txnMatch.Amount >= 0 ? "CRDT" : "DBIT"),
                BookingDate = date,
                ValueDate = date,
                RemittanceInfo = SanitizeText(txnMatch.RemittanceInfo),
                EndToEndId = txnMatch.EndToEndId,
                DebtorName = SanitizeText(txnMatch.DebtorName),
                CreditorName = SanitizeText(txnMatch.CreditorName)
            };
            
            transactions.Add(transaction);
        }
        
        return transactions.OrderBy(t => t.TransactionId).ToList();
    }
    
    /// <summary>
    /// Extract JSON values using simple regex parsing
    /// </summary>
    private static string ExtractJsonValue(string json, string key)
    {
        var pattern = $"\"{key}\"\\s*:\\s*\"([^\"]+)\"";
        var match = Regex.Match(json, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }
    
    private static decimal? ExtractClosingBookedAmount(string json)
    {
        // Look for closingBooked balance
        var pattern = @"""balanceType""\s*:\s*""closingBooked""[^}]+""amount""\s*:\s*""([^""]+)""";
        var match = Regex.Match(json, pattern);
        
        if (match.Success)
        {
            var amountStr = match.Groups[1].Value.Replace(",", ".");
            if (decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
            {
                return amount;
            }
        }
        
        return null;
    }
    
    private static List<TransactionMatch> ExtractTransactions(string json)
    {
        var transactions = new List<TransactionMatch>();
        
        // Find all transaction objects in the JSON
        var transactionPattern = @"""transactionId""\s*:\s*""([^""]+)""[^}]+""transactionAmount""\s*:\s*\{[^}]*""amount""\s*:\s*""([^""]+)""[^}]*""currency""\s*:\s*""([^""]+)""[^}]*\}[^}]*(?:""creditDebitIndicator""\s*:\s*""([^""]+)"")?[^}]*(?:""remittanceInformationUnstructured""\s*:\s*""([^""]+)"")?";
        
        var matches = Regex.Matches(json, transactionPattern);
        
        foreach (Match match in matches)
        {
            var amountStr = match.Groups[2].Value.Replace(",", ".");
            if (decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
            {
                transactions.Add(new TransactionMatch
                {
                    TransactionId = match.Groups[1].Value,
                    Amount = amount,
                    Currency = match.Groups[3].Value,
                    CreditDebitIndicator = match.Groups[4].Success ? match.Groups[4].Value : null,
                    RemittanceInfo = match.Groups[5].Success ? match.Groups[5].Value : null
                });
            }
        }
        
        return transactions;
    }
    
    /// <summary>
    /// Genereer CAMT.053 XML
    /// </summary>
    private static string GenerateCAMT053XML(BalanceData balanceData, List<Transaction> transactions, string date)
    {
        var dateObj = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var messageId = $"STMT{dateObj:yyyyMMdd}001";
        var statementId = $"{dateObj:yyyyMMdd}001";
        var creationDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
        
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:camt.053.001.02\">");
        xml.AppendLine("  <BkToCstmrStmt>");
        xml.AppendLine("    <GrpHdr>");
        xml.AppendLine($"      <MsgId>{messageId}</MsgId>");
        xml.AppendLine($"      <CreDtTm>{creationDateTime}</CreDtTm>");
        xml.AppendLine("    </GrpHdr>");
        xml.AppendLine("    <Stmt>");
        xml.AppendLine($"      <Id>{statementId}</Id>");
        xml.AppendLine("      <ElctrncSeqNb>1</ElctrncSeqNb>");
        xml.AppendLine($"      <CreDtTm>{creationDateTime}</CreDtTm>");
        xml.AppendLine("      <Acct>");
        xml.AppendLine("        <Id>");
        xml.AppendLine($"          <IBAN>{balanceData.IBAN}</IBAN>");
        xml.AppendLine("        </Id>");
        xml.AppendLine($"        <Ccy>{balanceData.Currency}</Ccy>");
        xml.AppendLine("        <Ownr>");
        xml.AppendLine("          <Nm>Test Account Holder</Nm>");
        xml.AppendLine("        </Ownr>");
        xml.AppendLine("        <Svcr>");
        xml.AppendLine("          <FinInstnId>");
        xml.AppendLine("            <BIC>RABONL2U</BIC>");
        xml.AppendLine("          </FinInstnId>");
        xml.AppendLine("        </Svcr>");
        xml.AppendLine("      </Acct>");
        xml.AppendLine("      <FrToDt>");
        xml.AppendLine($"        <FrDtTm>{date}T00:00:00</FrDtTm>");
        xml.AppendLine($"        <ToDtTm>{date}T23:59:59</ToDtTm>");
        xml.AppendLine("      </FrToDt>");
        
        // Opening Balance
        xml.AppendLine("      <Bal>");
        xml.AppendLine("        <Tp>");
        xml.AppendLine("          <CdOrPrtry>");
        xml.AppendLine("            <Cd>OPBD</Cd>");
        xml.AppendLine("          </CdOrPrtry>");
        xml.AppendLine("        </Tp>");
        xml.AppendLine($"        <Amt Ccy=\"{balanceData.OpeningBalance.Currency}\">{balanceData.OpeningBalance.Amount:F2}</Amt>");
        xml.AppendLine($"        <CdtDbtInd>{balanceData.OpeningBalance.CreditDebitIndicator}</CdtDbtInd>");
        xml.AppendLine("        <Dt>");
        xml.AppendLine($"          <Dt>{balanceData.OpeningBalance.Date}</Dt>");
        xml.AppendLine("        </Dt>");
        xml.AppendLine("      </Bal>");
        
        // Closing Balance
        xml.AppendLine("      <Bal>");
        xml.AppendLine("        <Tp>");
        xml.AppendLine("          <CdOrPrtry>");
        xml.AppendLine("            <Cd>CLBD</Cd>");
        xml.AppendLine("          </CdOrPrtry>");
        xml.AppendLine("        </Tp>");
        xml.AppendLine($"        <Amt Ccy=\"{balanceData.ClosingBalance.Currency}\">{balanceData.ClosingBalance.Amount:F2}</Amt>");
        xml.AppendLine($"        <CdtDbtInd>{balanceData.ClosingBalance.CreditDebitIndicator}</CdtDbtInd>");
        xml.AppendLine("        <Dt>");
        xml.AppendLine($"          <Dt>{balanceData.ClosingBalance.Date}</Dt>");
        xml.AppendLine("        </Dt>");
        xml.AppendLine("      </Bal>");
        
        // Transactions
        foreach (var transaction in transactions)
        {
            xml.AppendLine("      <Ntry>");
            xml.AppendLine($"        <Amt Ccy=\"{transaction.Currency}\">{transaction.Amount:F2}</Amt>");
            xml.AppendLine($"        <CdtDbtInd>{transaction.CreditDebitIndicator}</CdtDbtInd>");
            xml.AppendLine("        <Sts>BOOK</Sts>");
            xml.AppendLine("        <BookgDt>");
            xml.AppendLine($"          <Dt>{transaction.BookingDate}</Dt>");
            xml.AppendLine("        </BookgDt>");
            xml.AppendLine("        <ValDt>");
            xml.AppendLine($"          <Dt>{transaction.ValueDate}</Dt>");
            xml.AppendLine("        </ValDt>");
            xml.AppendLine("        <BkTxCd>");
            xml.AppendLine("          <Domn>");
            xml.AppendLine("            <Cd>PMNT</Cd>");
            xml.AppendLine("            <Fmly>");
            xml.AppendLine($"              <Cd>{(transaction.CreditDebitIndicator == "CRDT" ? "RCDT" : "ICDT")}</Cd>");
            xml.AppendLine("              <SubFmlyCd>ESCT</SubFmlyCd>");
            xml.AppendLine("            </Fmly>");
            xml.AppendLine("          </Domn>");
            xml.AppendLine("        </BkTxCd>");
            xml.AppendLine("        <NtryDtls>");
            xml.AppendLine("          <TxDtls>");
            xml.AppendLine("            <Refs>");
            xml.AppendLine($"              <TxId>{transaction.TransactionId}</TxId>");
            
            if (!string.IsNullOrEmpty(transaction.EndToEndId))
            {
                xml.AppendLine($"              <EndToEndId>{transaction.EndToEndId}</EndToEndId>");
            }
            
            xml.AppendLine("            </Refs>");
            
            if (!string.IsNullOrEmpty(transaction.DebtorName) || !string.IsNullOrEmpty(transaction.CreditorName))
            {
                xml.AppendLine("            <RltdPties>");
                if (!string.IsNullOrEmpty(transaction.DebtorName))
                {
                    xml.AppendLine("              <Dbtr>");
                    xml.AppendLine($"                <Nm>{XmlEscape(transaction.DebtorName)}</Nm>");
                    xml.AppendLine("              </Dbtr>");
                }
                if (!string.IsNullOrEmpty(transaction.CreditorName))
                {
                    xml.AppendLine("              <Cdtr>");
                    xml.AppendLine($"                <Nm>{XmlEscape(transaction.CreditorName)}</Nm>");
                    xml.AppendLine("              </Cdtr>");
                }
                xml.AppendLine("            </RltdPties>");
            }
            
            if (!string.IsNullOrEmpty(transaction.RemittanceInfo))
            {
                xml.AppendLine("            <RmtInf>");
                xml.AppendLine($"              <Ustrd>{XmlEscape(transaction.RemittanceInfo)}</Ustrd>");
                xml.AppendLine("            </RmtInf>");
            }
            
            xml.AppendLine("          </TxDtls>");
            xml.AppendLine("        </NtryDtls>");
            xml.AppendLine("      </Ntry>");
        }
        
        xml.AppendLine("    </Stmt>");
        xml.AppendLine("  </BkToCstmrStmt>");
        xml.AppendLine("</Document>");
        
        return xml.ToString();
    }
    
    /// <summary>
    /// Helper methods
    /// </summary>
    private static string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        return text.Trim().Length > 140 ? text.Trim().Substring(0, 140) : text.Trim();
    }
    
    private static string XmlEscape(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&apos;");
    }
}

/// <summary>
/// Data models
/// </summary>
public class BalanceData
{
    public Balance OpeningBalance { get; set; }
    public Balance ClosingBalance { get; set; }
    public string IBAN { get; set; }
    public string Currency { get; set; }
}

public class Balance
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CreditDebitIndicator { get; set; }
    public string Date { get; set; }
    public string IBAN { get; set; }
}

public class Transaction
{
    public string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CreditDebitIndicator { get; set; }
    public string BookingDate { get; set; }
    public string ValueDate { get; set; }
    public string RemittanceInfo { get; set; }
    public string EndToEndId { get; set; }
    public string DebtorName { get; set; }
    public string CreditorName { get; set; }
}

public class TransactionMatch
{
    public string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CreditDebitIndicator { get; set; }
    public string RemittanceInfo { get; set; }
    public string EndToEndId { get; set; }
    public string DebtorName { get; set; }
    public string CreditorName { get; set; }
}
