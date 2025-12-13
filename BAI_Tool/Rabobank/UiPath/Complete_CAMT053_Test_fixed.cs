// Complete, compilable test program for CAMT053 generation
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Simple data models
public class Balance
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CreditDebitIndicator { get; set; }
    public string ReferenceDate { get; set; }
}

public class BalanceData
{
    public Balance OpeningBalance { get; set; }
    public Balance ClosingBalance { get; set; }
    public string IBAN { get; set; }
    public string Currency { get; set; }
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
        if (string.IsNullOrEmpty(iban)) throw new ArgumentException("IBAN is verplicht");
        if (string.IsNullOrEmpty(startDate)) throw new ArgumentException("StartDate is verplicht");
        if (string.IsNullOrEmpty(endDate)) throw new ArgumentException("EndDate is verplicht");

        var statementStartDate = DateTime.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var statementEndDate = DateTime.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (statementStartDate > statementEndDate) throw new ArgumentException("StartDate moet voor EndDate liggen");

        // For this test we use simplified in-memory data; replace with DB calls if needed
        var balanceData = GetBalanceData(iban, statementStartDate, statementEndDate);
        var transactions = GetTransactionData(iban, statementStartDate, statementEndDate);

        var doc = GenerateXmlDocument(balanceData, transactions, iban, statementStartDate, statementEndDate);
        return doc.ToString();
    }

    private BalanceData GetBalanceData(string iban, DateTime startDate, DateTime endDate)
    {
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

    private XElement CreateTransactionElement(XNamespace ns, Transaction t)
    {
        return new XElement(ns + "Ntry",
            new XElement(ns + "NtryRef", t.TransactionId),
            new XElement(ns + "Amt", new XAttribute("Ccy", t.Currency), t.Amount.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(ns + "CdtDbtInd", t.CreditDebitIndicator),
            new XElement(ns + "AcctSvcrRef", t.TransactionId),
            new XElement(ns + "BkTxCd",
                new XElement(ns + "Domn",
                    new XElement(ns + "Cd", "PMNT"),
                    new XElement(ns + "Fmly",
                        new XElement(ns + "Cd", "RCDT"),
                        new XElement(ns + "SubFmlyCd", "ESCT")
                    )
                ),
                new XElement(ns + "Prtry",
                    new XElement(ns + "Cd", t.RaboDetailedTransactionType),
                    new XElement(ns + "Issr", "RABOBANK")
                )
            ),
            new XElement(ns + "NtryDtls",
                new XElement(ns + "TxDtls",
                    new XElement(ns + "Refs",
                        new XElement(ns + "AcctSvcrRef", t.BatchEntryReference),
                        new XElement(ns + "InstrId", t.BatchEntryReference)
                    ),
                    new XElement(ns + "RltdAgts",
                        new XElement(ns + "DbtrAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BIC", t.DebtorAgent)
                            )
                        )
                    )
                )
            )
        );
    }
}

public static class Program
{
    public static void Main()
    {
        var connectionString = "Host=localhost;Database=bai_tool;Username=postgres;Password=yourpassword";
        var testIban = "NL48RABO0300002343";
        var startDate = "2025-10-06";
        var endDate = "2025-10-06";

        Console.WriteLine("Testing CAMT053DatabaseGenerator - Updated Implementation");
        Console.WriteLine($"IBAN: {testIban}, Period: {startDate} to {endDate}");

        try
        {
            var generator = new CAMT053DatabaseGenerator(connectionString);
            var xml = generator.GenerateCAMT053(testIban, startDate, endDate);

            var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "camt053_test_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, xml);

            Console.WriteLine($"Success! XML saved to: {outputPath}");

            // Quick validation
            Console.WriteLine("Quick Validation:");
            Console.WriteLine($"NtryRef: {(xml.Contains("<NtryRef>") ? "FOUND" : "MISSING")}");
            Console.WriteLine($"AcctSvcrRef: {(xml.Contains("<AcctSvcrRef>") ? "FOUND" : "MISSING")}");
            Console.WriteLine($"BkTxCd: {(xml.Contains("<BkTxCd>") ? "FOUND" : "MISSING")}");
            Console.WriteLine($"Prtry: {(xml.Contains("<Prtry>") ? "FOUND" : "MISSING")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
        }
    }
}
