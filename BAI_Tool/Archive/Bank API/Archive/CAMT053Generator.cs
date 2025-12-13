using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// CAMT.053.001.02 Generator voor RaboBank API data
/// Converteert JSON balance en transaction data naar ISO 20022 CAMT.053 XML format
/// </summary>
public class CAMT053Generator
{
    private readonly CAMT053Config _config;
    private readonly CultureInfo _dutchCulture = new CultureInfo("nl-NL");
    
    public CAMT053Generator(CAMT053Config config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }
    
    /// <summary>
    /// Genereert CAMT.053 XML van JSON balance en transaction bestanden
    /// </summary>
    /// <param name="balanceFiles">Balance JSON bestanden (opening en closing)</param>
    /// <param name="transactionFiles">Transaction JSON bestanden</param>
    /// <param name="statementDate">Datum van het afschrift (YYYY-MM-DD)</param>
    /// <returns>CAMT.053 XML string</returns>
    public string GenerateCAMT053(List<string> balanceFiles, List<string> transactionFiles, string statementDate)
    {
        try
        {
            // Parse input data
            var balanceData = ParseBalanceData(balanceFiles, statementDate);
            var transactionData = ParseTransactionData(transactionFiles, statementDate);
            
            // Validate required data
            ValidateInputData(balanceData, transactionData, statementDate);
            
            // Generate XML
            var xmlDoc = GenerateXmlDocument(balanceData, transactionData, statementDate);
            
            // Format and return XML
            return FormatXml(xmlDoc);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fout bij genereren CAMT.053: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Parse balance data van JSON bestanden
    /// </summary>
    private BalanceData ParseBalanceData(List<string> balanceFiles, string statementDate)
    {
        var balanceData = new BalanceData();
        var statementDateTime = DateTime.ParseExact(statementDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        
        foreach (var filePath in balanceFiles)
        {
            if (!File.Exists(filePath))
                continue;
                
            var jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
            var balanceJson = JObject.Parse(jsonContent);
            
            // Extract date from filename: balance_IBAN_YYYYMMDD_HHMMSS.json
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var datePart = fileName.Split('_')[2]; // YYYYMMDD
            var fileDate = DateTime.ParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture);
            
            var account = balanceJson["account"];
            var iban = account["iban"]?.ToString();
            var currency = account["currency"]?.ToString();
            
            var balances = balanceJson["balances"] as JArray;
            if (balances?.Count > 0)
            {
                // Zoek de closingBooked balance
                var closingBalance = balances.FirstOrDefault(b => 
                    b["balanceType"]?.ToString() == "closingBooked");
                
                if (closingBalance != null)
                {
                    var amount = ParseDecimal(closingBalance["balanceAmount"]["amount"]?.ToString());
                    var creditDebitInd = DetermineDebitCreditIndicator(amount);
                    
                    var balance = new Balance
                    {
                        Amount = Math.Abs(amount),
                        Currency = currency,
                        CreditDebitIndicator = creditDebitInd,
                        Date = fileDate,
                        ReferenceDate = fileDate.ToString("yyyy-MM-dd")
                    };
                    
                    // Bepaal of dit opening of closing balance is
                    if (fileDate.Date == statementDateTime.Date.AddDays(-1))
                    {
                        balanceData.ClosingBalance = balance;
                    }
                    else if (fileDate.Date <= statementDateTime.Date.AddDays(-2))
                    {
                        // Neem de meest recente balance als opening balance
                        if (balanceData.OpeningBalance == null || fileDate > balanceData.OpeningBalance.Date)
                        {
                            balanceData.OpeningBalance = balance;
                        }
                    }
                }
            }
            
            // Store account info
            if (!string.IsNullOrEmpty(iban))
            {
                balanceData.IBAN = iban;
                balanceData.Currency = currency;
            }
        }
        
        return balanceData;
    }
    
    /// <summary>
    /// Parse transaction data van JSON bestanden
    /// </summary>
    private List<Transaction> ParseTransactionData(List<string> transactionFiles, string statementDate)
    {
        var transactions = new List<Transaction>();
        var statementDateTime = DateTime.ParseExact(statementDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        
        foreach (var filePath in transactionFiles)
        {
            if (!File.Exists(filePath))
                continue;
                
            // Extract date from filename
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var datePart = fileName.Split('_')[2]; // YYYYMMDD
            var fileDate = DateTime.ParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture);
            
            // Alleen transacties van de statement datum
            if (fileDate.Date != statementDateTime.Date)
                continue;
                
            var jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
            var transactionJson = JObject.Parse(jsonContent);
            
            var transactionArray = transactionJson["transactions"] as JArray;
            if (transactionArray?.Count > 0)
            {
                foreach (var txn in transactionArray)
                {
                    var transaction = ParseSingleTransaction(txn, fileDate);
                    if (transaction != null)
                    {
                        transactions.Add(transaction);
                    }
                }
            }
        }
        
        return transactions.OrderBy(t => t.BookingDate).ThenBy(t => t.TransactionId).ToList();
    }
    
    /// <summary>
    /// Parse een enkele transactie van JSON
    /// </summary>
    private Transaction ParseSingleTransaction(JToken txnJson, DateTime fileDate)
    {
        try
        {
            var amount = ParseDecimal(txnJson["transactionAmount"]?["amount"]?.ToString() ?? 
                                    txnJson["amount"]?.ToString());
            
            var creditDebitInd = txnJson["creditDebitIndicator"]?.ToString();
            if (string.IsNullOrEmpty(creditDebitInd))
            {
                creditDebitInd = DetermineDebitCreditIndicator(amount);
            }
            
            return new Transaction
            {
                TransactionId = txnJson["transactionId"]?.ToString() ?? Guid.NewGuid().ToString(),
                Amount = Math.Abs(amount),
                Currency = txnJson["transactionAmount"]?["currency"]?.ToString() ?? 
                          txnJson["currency"]?.ToString() ?? "EUR",
                CreditDebitIndicator = creditDebitInd,
                BookingDate = ParseDate(txnJson["bookingDate"]?.ToString(), fileDate),
                ValueDate = ParseDate(txnJson["valueDate"]?.ToString(), fileDate),
                RemittanceInfo = SanitizeText(txnJson["remittanceInformationUnstructured"]?.ToString() ?? 
                                            txnJson["description"]?.ToString()),
                EndToEndId = txnJson["endToEndId"]?.ToString(),
                DebtorName = SanitizeText(txnJson["debtorName"]?.ToString()),
                DebtorIBAN = txnJson["debtorAccount"]?["iban"]?.ToString(),
                CreditorName = SanitizeText(txnJson["creditorName"]?.ToString()),
                CreditorIBAN = txnJson["creditorAccount"]?["iban"]?.ToString()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fout bij parsen transactie: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Valideer input data voor CAMT.053 generatie
    /// </summary>
    private void ValidateInputData(BalanceData balanceData, List<Transaction> transactions, string statementDate)
    {
        if (balanceData.OpeningBalance == null)
            throw new Exception("Opening balance ontbreekt - vereist voor CAMT.053");
            
        if (balanceData.ClosingBalance == null)
            throw new Exception("Closing balance ontbreekt - vereist voor CAMT.053");
            
        if (string.IsNullOrEmpty(balanceData.IBAN))
            throw new Exception("IBAN ontbreekt - vereist voor CAMT.053");
            
        if (string.IsNullOrEmpty(balanceData.Currency))
            throw new Exception("Currency ontbreekt - vereist voor CAMT.053");
            
        // Balance reconciliation check
        var openingAmount = balanceData.OpeningBalance.CreditDebitIndicator == "CRDT" ? 
            balanceData.OpeningBalance.Amount : -balanceData.OpeningBalance.Amount;
        var closingAmount = balanceData.ClosingBalance.CreditDebitIndicator == "CRDT" ? 
            balanceData.ClosingBalance.Amount : -balanceData.ClosingBalance.Amount;
        
        var transactionSum = transactions.Sum(t => 
            t.CreditDebitIndicator == "CRDT" ? t.Amount : -t.Amount);
        
        var calculatedClosing = openingAmount + transactionSum;
        var difference = Math.Abs(calculatedClosing - closingAmount);
        
        if (difference > 0.01m) // Tolerance voor rounding
        {
            Console.WriteLine($"WAARSCHUWING: Balance reconciliation verschil van €{difference:F2}");
            Console.WriteLine($"Opening: €{openingAmount:F2}, Transactions: €{transactionSum:F2}, Calculated: €{calculatedClosing:F2}, Actual Closing: €{closingAmount:F2}");
        }
    }
    
    /// <summary>
    /// Genereer CAMT.053 XML document
    /// </summary>
    private XDocument GenerateXmlDocument(BalanceData balanceData, List<Transaction> transactions, string statementDate)
    {
        var statementDateTime = DateTime.ParseExact(statementDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var messageId = GenerateMessageId(statementDate);
        var statementId = GenerateStatementId(statementDate);
        var creationDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
        
        var ns = XNamespace.Get("urn:iso:std:iso:20022:tech:xsd:camt.053.001.02");
        
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Document",
                new XElement(ns + "BkToCstmrStmt",
                    // Group Header
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", messageId),
                        new XElement(ns + "CreDtTm", creationDateTime)
                    ),
                    // Statement
                    new XElement(ns + "Stmt",
                        new XElement(ns + "Id", statementId),
                        new XElement(ns + "ElctrncSeqNb", "1"),
                        new XElement(ns + "CreDtTm", creationDateTime),
                        // Account
                        new XElement(ns + "Acct",
                            new XElement(ns + "Id",
                                new XElement(ns + "IBAN", balanceData.IBAN)
                            ),
                            new XElement(ns + "Ccy", balanceData.Currency),
                            new XElement(ns + "Ownr",
                                new XElement(ns + "Nm", _config.AccountOwnerName)
                            ),
                            new XElement(ns + "Svcr",
                                new XElement(ns + "FinInstnId",
                                    new XElement(ns + "BIC", _config.ServicerBIC)
                                )
                            )
                        ),
                        // From/To Date
                        new XElement(ns + "FrToDt",
                            new XElement(ns + "FrDtTm", statementDate + "T00:00:00"),
                            new XElement(ns + "ToDtTm", statementDate + "T23:59:59")
                        ),
                        // Opening Balance
                        CreateBalanceElement(ns, balanceData.OpeningBalance, "OPBD"),
                        // Closing Balance
                        CreateBalanceElement(ns, balanceData.ClosingBalance, "CLBD"),
                        // Transactions
                        transactions.Select(t => CreateTransactionElement(ns, t))
                    )
                )
            )
        );
        
        return doc;
    }
    
    /// <summary>
    /// Creëer balance XML element
    /// </summary>
    private XElement CreateBalanceElement(XNamespace ns, Balance balance, string balanceTypeCode)
    {
        return new XElement(ns + "Bal",
            new XElement(ns + "Tp",
                new XElement(ns + "CdOrPrtry",
                    new XElement(ns + "Cd", balanceTypeCode)
                )
            ),
            new XElement(ns + "Amt", 
                new XAttribute("Ccy", balance.Currency),
                balance.Amount.ToString("F2", CultureInfo.InvariantCulture)
            ),
            new XElement(ns + "CdtDbtInd", balance.CreditDebitIndicator),
            new XElement(ns + "Dt",
                new XElement(ns + "Dt", balance.ReferenceDate)
            )
        );
    }
    
    /// <summary>
    /// Creëer transaction XML element
    /// </summary>
    private XElement CreateTransactionElement(XNamespace ns, Transaction transaction)
    {
        var entry = new XElement(ns + "Ntry",
            new XElement(ns + "Amt",
                new XAttribute("Ccy", transaction.Currency),
                transaction.Amount.ToString("F2", CultureInfo.InvariantCulture)
            ),
            new XElement(ns + "CdtDbtInd", transaction.CreditDebitIndicator),
            new XElement(ns + "Sts", "BOOK"),
            new XElement(ns + "BookgDt",
                new XElement(ns + "Dt", transaction.BookingDate.ToString("yyyy-MM-dd"))
            ),
            new XElement(ns + "ValDt",
                new XElement(ns + "Dt", transaction.ValueDate.ToString("yyyy-MM-dd"))
            ),
            new XElement(ns + "BkTxCd",
                new XElement(ns + "Domn",
                    new XElement(ns + "Cd", "PMNT"),
                    new XElement(ns + "Fmly",
                        new XElement(ns + "Cd", transaction.CreditDebitIndicator == "CRDT" ? "RCDT" : "ICDT"),
                        new XElement(ns + "SubFmlyCd", "ESCT")
                    )
                )
            )
        );
        
        // Transaction Details
        var entryDetails = new XElement(ns + "NtryDtls",
            new XElement(ns + "TxDtls",
                new XElement(ns + "Refs",
                    new XElement(ns + "TxId", transaction.TransactionId)
                )
            )
        );
        
        // Add EndToEndId if available
        if (!string.IsNullOrEmpty(transaction.EndToEndId))
        {
            entryDetails.Element(ns + "TxDtls").Element(ns + "Refs")
                .Add(new XElement(ns + "EndToEndId", transaction.EndToEndId));
        }
        
        // Add Related Parties if available
        if (!string.IsNullOrEmpty(transaction.DebtorName) || !string.IsNullOrEmpty(transaction.CreditorName))
        {
            var relatedParties = new XElement(ns + "RltdPties");
            
            if (!string.IsNullOrEmpty(transaction.DebtorName))
            {
                var debtor = new XElement(ns + "Dbtr",
                    new XElement(ns + "Nm", transaction.DebtorName)
                );
                if (!string.IsNullOrEmpty(transaction.DebtorIBAN))
                {
                    debtor.Add(new XElement(ns + "DbtrAcct",
                        new XElement(ns + "Id",
                            new XElement(ns + "IBAN", transaction.DebtorIBAN)
                        )
                    ));
                }
                relatedParties.Add(debtor);
            }
            
            if (!string.IsNullOrEmpty(transaction.CreditorName))
            {
                var creditor = new XElement(ns + "Cdtr",
                    new XElement(ns + "Nm", transaction.CreditorName)
                );
                if (!string.IsNullOrEmpty(transaction.CreditorIBAN))
                {
                    creditor.Add(new XElement(ns + "CdtrAcct",
                        new XElement(ns + "Id",
                            new XElement(ns + "IBAN", transaction.CreditorIBAN)
                        )
                    ));
                }
                relatedParties.Add(creditor);
            }
            
            entryDetails.Element(ns + "TxDtls").Add(relatedParties);
        }
        
        // Add Remittance Information if available
        if (!string.IsNullOrEmpty(transaction.RemittanceInfo))
        {
            entryDetails.Element(ns + "TxDtls").Add(
                new XElement(ns + "RmtInf",
                    new XElement(ns + "Ustrd", transaction.RemittanceInfo)
                )
            );
        }
        
        entry.Add(entryDetails);
        return entry;
    }
    
    /// <summary>
    /// Helper methods
    /// </summary>
    private decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        
        // Handle Dutch format: replace comma with dot
        value = value.Replace(",", ".");
        
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result))
            return result;
            
        return 0;
    }
    
    private string DetermineDebitCreditIndicator(decimal amount)
    {
        return amount >= 0 ? "CRDT" : "DBIT";
    }
    
    private DateTime ParseDate(string dateStr, DateTime fallbackDate)
    {
        if (string.IsNullOrEmpty(dateStr))
            return fallbackDate;
            
        if (DateTime.TryParse(dateStr, out DateTime result))
            return result;
            
        return fallbackDate;
    }
    
    private string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        // Remove invalid XML characters and limit length
        var sanitized = new StringBuilder();
        foreach (char c in text)
        {
            if (XmlConvert.IsXmlChar(c))
                sanitized.Append(c);
        }
        
        var result = sanitized.ToString().Trim();
        return result.Length > 140 ? result.Substring(0, 140) : result;
    }
    
    private string GenerateMessageId(string statementDate)
    {
        var dateStr = DateTime.ParseExact(statementDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .ToString("yyyyMMdd");
        return $"STMT{dateStr}001";
    }
    
    private string GenerateStatementId(string statementDate)
    {
        var dateStr = DateTime.ParseExact(statementDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .ToString("yyyyMMdd");
        return $"{dateStr}001";
    }
    
    private string FormatXml(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };
        
        using (var stringWriter = new StringWriter())
        using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
        {
            doc.Save(xmlWriter);
            return stringWriter.ToString();
        }
    }
}

/// <summary>
/// Configuration voor CAMT.053 generator
/// </summary>
public class CAMT053Config
{
    public string AccountOwnerName { get; set; } = "Account Holder";
    public string ServicerBIC { get; set; } = "RABONL2U";
    public string ServicerName { get; set; } = "Rabobank Nederland";
    public string TimeZone { get; set; } = "Europe/Amsterdam";
    public bool ValidationEnabled { get; set; } = true;
}

/// <summary>
/// Balance data model
/// </summary>
public class BalanceData
{
    public Balance OpeningBalance { get; set; }
    public Balance ClosingBalance { get; set; }
    public string IBAN { get; set; }
    public string Currency { get; set; }
}

/// <summary>
/// Balance model
/// </summary>
public class Balance
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CreditDebitIndicator { get; set; }
    public DateTime Date { get; set; }
    public string ReferenceDate { get; set; }
}

/// <summary>
/// Transaction model
/// </summary>
public class Transaction
{
    public string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CreditDebitIndicator { get; set; }
    public DateTime BookingDate { get; set; }
    public DateTime ValueDate { get; set; }
    public string RemittanceInfo { get; set; }
    public string EndToEndId { get; set; }
    public string DebtorName { get; set; }
    public string DebtorIBAN { get; set; }
    public string CreditorName { get; set; }
    public string CreditorIBAN { get; set; }
}
