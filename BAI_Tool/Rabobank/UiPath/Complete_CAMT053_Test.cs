// Complete UiPath Test - Includes Generator Class
// Copy this ENTIRE content into UiPath Invoke Code Activity

// Add these Using statements at the top of your workflow or in Imports:
// System.Data
// System.Xml.Linq  
// Npgsql (add Npgsql NuGet package to project)

#region CAMT053DatabaseGenerator Class
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Npgsql;

public class CAMT053DatabaseGenerator
{
    private readonly string _connectionString;
    private readonly CultureInfo _dutchCulture = new CultureInfo("nl-NL");

    public CAMT053DatabaseGenerator(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public string GenerateCAMT053(string iban, string startDate, string endDate)
    {
        if (string.IsNullOrEmpty(iban))
            throw new ArgumentException("IBAN is verplicht");
        if (string.IsNullOrEmpty(startDate))
            throw new ArgumentException("StartDate is verplicht");
        if (string.IsNullOrEmpty(endDate))
            throw new ArgumentException("EndDate is verplicht");

        try
        {
            var statementStartDate = DateTime.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var statementEndDate = DateTime.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (statementStartDate > statementEndDate)
                throw new ArgumentException("StartDate moet voor EndDate liggen");

            var balanceData = GetBalanceData(iban, statementStartDate, statementEndDate);
            var transactionData = GetTransactionData(iban, statementStartDate, statementEndDate);
            
            ValidateData(balanceData, transactionData, iban, statementStartDate, statementEndDate);
            
            var xmlDoc = GenerateXmlDocument(balanceData, transactionData, iban, statementStartDate, statementEndDate);
            
            return FormatXml(xmlDoc);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fout bij genereren CAMT.053 voor {iban}: {ex.Message}", ex);
        }
    }

    // Simplified methods for testing - add full implementation from CAMT053DatabaseGenerator.cs
    private BalanceData GetBalanceData(string iban, DateTime startDate, DateTime endDate)
    {
        // Simplified for testing
        return new BalanceData 
        { 
            IBAN = iban,
            Currency = "EUR",
            OpeningBalance = new Balance { Amount = 1000.00m, Currency = "EUR", CreditDebitIndicator = "CRDT", ReferenceDate = startDate.AddDays(-1).ToString("yyyy-MM-dd") },
            ClosingBalance = new Balance { Amount = 1500.00m, Currency = "EUR", CreditDebitIndicator = "CRDT", ReferenceDate = endDate.ToString("yyyy-MM-dd") }
        };
    }
    
    private List<Transaction> GetTransactionData(string iban, DateTime startDate, DateTime endDate)
    {
        // Simplified for testing
        return new List<Transaction>
        {
            new Transaction
            {
                TransactionId = "TEST123",
                Amount = 500.00m,
                Currency = "EUR", 
                CreditDebitIndicator = "CRDT",
                BookingDate = startDate,
                ValueDate = startDate,
                BatchEntryReference = "BATCH001",
                RaboDetailedTransactionType = "100",
                DebtorAgent = "RABONL2U"
            }
        };
    }
    
    private void ValidateData(BalanceData balanceData, List<Transaction> transactions, string iban, DateTime startDate, DateTime endDate)
    {
        // Basic validation
        if (balanceData.OpeningBalance == null || balanceData.ClosingBalance == null)
            throw new Exception("Missing balance data");
    }
    
    private XDocument GenerateXmlDocument(BalanceData balanceData, List<Transaction> transactions, string iban, DateTime startDate, DateTime endDate)
    {
        var ns = XNamespace.Get("urn:iso:std:iso:20022:tech:xsd:camt.053.001.02");
        
        return new XDocument(
            new XElement(ns + "Document",
                new XElement(ns + "BkToCstmrStmt",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", "TEST001"),
                        new XElement(ns + "CreDtTm", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"))
                    ),
                    new XElement(ns + "Stmt",
                        new XElement(ns + "Id", "STMT001"),
                        new XElement(ns + "Acct",
                            new XElement(ns + "Id",
                                new XElement(ns + "IBAN", iban)
                            ),
                            new XElement(ns + "Ccy", balanceData.Currency)
                        ),
                        transactions.Select(t => CreateTransactionElement(ns, t))
                    )
                )
            )
        );
    }
    
    private XElement CreateTransactionElement(XNamespace ns, Transaction transaction)
    {
        return new XElement(ns + "Ntry",
            new XElement(ns + "NtryRef", transaction.TransactionId),
            new XElement(ns + "Amt", 
                new XAttribute("Ccy", transaction.Currency),
                transaction.Amount.ToString("F2", CultureInfo.InvariantCulture)
            ),
            new XElement(ns + "CdtDbtInd", transaction.CreditDebitIndicator),
            new XElement(ns + "AcctSvcrRef", transaction.TransactionId),
            new XElement(ns + "BkTxCd",
                new XElement(ns + "Domn",
                    new XElement(ns + "Cd", "PMNT"),
                    new XElement(ns + "Fmly",
                        new XElement(ns + "Cd", "RCDT"),
                        new XElement(ns + "SubFmlyCd", "ESCT")
                    )
                ),
                new XElement(ns + "Prtry",
                    new XElement(ns + "Cd", transaction.RaboDetailedTransactionType),
                    new XElement(ns + "Issr", "RABOBANK")
                )
            ),
            new XElement(ns + "NtryDtls",
                new XElement(ns + "TxDtls",
                    new XElement(ns + "Refs",
                        new XElement(ns + "AcctSvcrRef", transaction.BatchEntryReference),
                        new XElement(ns + "InstrId", transaction.BatchEntryReference)
                    ),
                    new XElement(ns + "RltdAgts",
                        new XElement(ns + "DbtrAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BIC", transaction.DebtorAgent)
                            )
                        )
                    )
                )
            )
        );
    }
    
    private string FormatXml(XDocument doc)
    {
        return doc.ToString();
    }
}

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
    public string ReferenceDate { get; set; }
}

public class Transaction
{
    public string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CreditDebitIndicator { get; set; }
    public DateTime BookingDate { get; set; }
    public DateTime ValueDate { get; set; }
    public string BatchEntryReference { get; set; }
    public string RaboDetailedTransactionType { get; set; }
    public string DebtorAgent { get; set; }
}
#endregion

// TEST CODE - This will test the implementation
var connectionString = "Host=localhost;Database=bai_tool;Username=postgres;Password=yourpassword";
var testIban = "NL48RABO0300002343";
var startDate = "2025-10-06"; 
var endDate = "2025-10-06";

Console.WriteLine("Testing CAMT053DatabaseGenerator - Updated Implementation");
Console.WriteLine($"IBAN: {testIban}, Period: {startDate} to {endDate}");

try 
{
    var generator = new CAMT053DatabaseGenerator(connectionString);
    
    Console.WriteLine("Generating CAMT.053 XML...");
    var xml = generator.GenerateCAMT053(testIban, startDate, endDate);
    
    var outputPath = @"c:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Output\camt053_test_" + 
                     DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml";
    
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
    File.WriteAllText(outputPath, xml);
    
    Console.WriteLine($"Success! XML saved to: {outputPath}");
    
    // Validation checks
    var hasNtryRef = xml.Contains("<NtryRef>");
    var hasAcctSvcrRef = xml.Contains("<AcctSvcrRef>");
    var hasBkTxCd = xml.Contains("<BkTxCd>");
    var hasPrtry = xml.Contains("<Prtry>");
    
    Console.WriteLine("Quick Validation:");
    Console.WriteLine($"NtryRef: {(hasNtryRef ? "FOUND" : "MISSING")}");
    Console.WriteLine($"AcctSvcrRef: {(hasAcctSvcrRef ? "FOUND" : "MISSING")}");
    Console.WriteLine($"BkTxCd: {(hasBkTxCd ? "FOUND" : "MISSING")}");
    Console.WriteLine($"Proprietary Code: {(hasPrtry ? "FOUND" : "MISSING")}");
    
    Console.WriteLine("Test completed! All new elements should be FOUND.");
    Console.WriteLine($"Check XML file: {outputPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Test failed: {ex.Message}");
}