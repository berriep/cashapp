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

/// <summary>
/// CAMT.053.001.02 Generator voor Rabobank database data
/// Converteert data uit bai_rabobank_balances_payload en bai_rabobank_transactions_payload naar ISO 20022 CAMT.053 XML format
/// </summary>
public class CAMT053DatabaseGenerator
{
    private readonly string _connectionString;
    private readonly CultureInfo _dutchCulture = new CultureInfo("nl-NL");

    public CAMT053DatabaseGenerator(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Genereert CAMT.053 XML van database data voor een specifieke IBAN en periode
    /// </summary>
    /// <param name="iban">IBAN van de rekening</param>
    /// <param name="startDate">Startdatum (YYYY-MM-DD)</param>
    /// <param name="endDate">Einddatum (YYYY-MM-DD)</param>
    /// <returns>CAMT.053 XML string</returns>
    public string GenerateCAMT053(string iban, string startDate, string endDate)
    {
        // Input validatie
        if (string.IsNullOrEmpty(iban))
            throw new ArgumentException("IBAN is verplicht");
        if (string.IsNullOrEmpty(startDate))
            throw new ArgumentException("StartDate is verplicht");
        if (string.IsNullOrEmpty(endDate))
            throw new ArgumentException("EndDate is verplicht");

        try
        {
            // Parse datums
            var statementStartDate = DateTime.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var statementEndDate = DateTime.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Valideer datum range
            if (statementStartDate > statementEndDate)
                throw new ArgumentException("StartDate moet voor EndDate liggen");

            // Haal data op uit database
            var balanceData = GetBalanceData(iban, statementStartDate, statementEndDate);
            var transactionData = GetTransactionData(iban, statementStartDate, statementEndDate);

            // Valideer data
            ValidateData(balanceData, transactionData, iban, statementStartDate, statementEndDate);

            // Generate XML
            var xmlDoc = GenerateXmlDocument(balanceData, transactionData, iban, statementStartDate, statementEndDate);

            // Format and return XML
            return FormatXml(xmlDoc);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fout bij genereren CAMT.053 voor {iban}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Haal balance data op uit database
    /// </summary>
    private BalanceData GetBalanceData(string iban, DateTime startDate, DateTime endDate)
    {
        var balanceData = new BalanceData { IBAN = iban };

        using (var conn = new NpgsqlConnection(_connectionString))
        {
            conn.Open();

            // Haal opening balance (closingBooked van de dag voor startDate)
            var openingQuery = @"
                SELECT amount, currency, reference_date
                FROM bai_rabobank_balances_payload
                WHERE iban = @iban
                  AND balance_type = 'closingBooked'
                  AND reference_date = @startDate - INTERVAL '1 day'
                ORDER BY retrieved_at DESC
                LIMIT 1";

            using (var cmd = new NpgsqlCommand(openingQuery, conn))
            {
                cmd.Parameters.AddWithValue("@iban", iban);
                cmd.Parameters.AddWithValue("@startDate", startDate.Date);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        balanceData.OpeningBalance = new Balance
                        {
                            Amount = Math.Abs(reader.GetDecimal(0)),
                            Currency = reader.GetString(1),
                            CreditDebitIndicator = reader.GetDecimal(0) >= 0 ? "CRDT" : "DBIT",
                            ReferenceDate = reader.GetDateTime(2).ToString("yyyy-MM-dd")
                        };
                        balanceData.Currency = reader.GetString(1);
                    }
                    // Geen fallback - als opening balance van vorige dag niet bestaat, kan geen geldig CAMT.053 gemaakt worden
                }
            }

            // Haal closing balance (closingBooked van endDate)
            var closingQuery = @"
                SELECT amount, currency, reference_date
                FROM bai_rabobank_balances_payload
                WHERE iban = @iban
                  AND balance_type = 'closingBooked'
                  AND reference_date = @endDate
                ORDER BY retrieved_at DESC
                LIMIT 1";

            using (var cmd = new NpgsqlCommand(closingQuery, conn))
            {
                cmd.Parameters.AddWithValue("@iban", iban);
                cmd.Parameters.AddWithValue("@endDate", endDate.Date);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        balanceData.ClosingBalance = new Balance
                        {
                            Amount = Math.Abs(reader.GetDecimal(0)),
                            Currency = reader.GetString(1),
                            CreditDebitIndicator = reader.GetDecimal(0) >= 0 ? "CRDT" : "DBIT",
                            ReferenceDate = reader.GetDateTime(2).ToString("yyyy-MM-dd")
                        };
                        if (string.IsNullOrEmpty(balanceData.Currency))
                            balanceData.Currency = reader.GetString(1);
                    }
                }
            }
        }

        return balanceData;
    }

    /// <summary>
    /// Haal transaction data op uit database
    /// </summary>
    private List<Transaction> GetTransactionData(string iban, DateTime startDate, DateTime endDate)
    {
        var transactions = new List<Transaction>();

        using (var conn = new NpgsqlConnection(_connectionString))
        {
            conn.Open();

            var query = @"
                SELECT
                    entry_reference,
                    booking_date,
                    value_date,
                    transaction_amount,
                    transaction_currency,
                    debtor_name,
                    debtor_iban,
                    creditor_name,
                    creditor_iban,
                    remittance_information_unstructured,
                    end_to_end_id,
                    rabo_booking_datetime,
                    bank_transaction_code,
                    purpose_code,
                    mandate_id,
                    ultimate_debtor,
                    ultimate_creditor,
                    batch_entry_reference,
                    rabo_detailed_transaction_type,
                    debtor_agent
                FROM bai_rabobank_transactions_payload
                WHERE iban = @iban
                  AND DATE(rabo_booking_datetime AT TIME ZONE 'UTC' AT TIME ZONE 'Europe/Amsterdam') BETWEEN @startDate AND @endDate
                ORDER BY DATE(rabo_booking_datetime AT TIME ZONE 'UTC' AT TIME ZONE 'Europe/Amsterdam'), rabo_booking_datetime";

            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@iban", iban);
                cmd.Parameters.AddWithValue("@startDate", startDate.Date);
                cmd.Parameters.AddWithValue("@endDate", endDate.Date);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var amount = reader.GetDecimal(3);
                        var raboBookingDateTime = reader.GetDateTime(11); // rabo_booking_datetime kolom
                        
                        var transaction = new Transaction
                        {
                            TransactionId = reader.GetString(0), // entry_reference -> NtryRef
                            Amount = Math.Abs(amount),
                            Currency = reader.GetString(4),
                            CreditDebitIndicator = amount >= 0 ? "CRDT" : "DBIT",
                            // Gebruik rabo_booking_datetime geconverteerd naar Amsterdam timezone
                            BookingDate = TimeZoneInfo.ConvertTimeFromUtc(
                                DateTime.SpecifyKind(raboBookingDateTime, DateTimeKind.Utc),
                                TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time")
                            ).Date,
                            ValueDate = reader.IsDBNull(2) ? 
                                TimeZoneInfo.ConvertTimeFromUtc(
                                    DateTime.SpecifyKind(raboBookingDateTime, DateTimeKind.Utc),
                                    TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time")
                                ).Date : reader.GetDateTime(2),
                            DebtorName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            DebtorIBAN = reader.IsDBNull(6) ? null : reader.GetString(6),
                            CreditorName = reader.IsDBNull(7) ? null : reader.GetString(7),
                            CreditorIBAN = reader.IsDBNull(8) ? null : reader.GetString(8),
                            RemittanceInfo = reader.IsDBNull(9) ? null : SanitizeText(reader.GetString(9)),
                            EndToEndId = reader.IsDBNull(10) ? null : reader.GetString(10),
                            BankTransactionCode = reader.IsDBNull(12) ? "PMNT-RCDT-ESCT" : reader.GetString(12),
                            PurposeCode = reader.IsDBNull(13) ? null : reader.GetString(13),
                            MandateId = reader.IsDBNull(14) ? null : reader.GetString(14),
                            UltimateDebtorName = reader.IsDBNull(15) ? null : reader.GetString(15),
                            UltimateCreditorName = reader.IsDBNull(16) ? null : reader.GetString(16),
                            // Nieuwe mappings gebaseerd op reference.xml
                            BatchEntryReference = reader.IsDBNull(17) ? null : reader.GetString(17), // batch_entry_reference
                            RaboDetailedTransactionType = reader.IsDBNull(18) ? "100" : reader.GetString(18), // rabo_detailed_transaction_type
                            DebtorAgent = reader.IsDBNull(19) ? "RABONL2U" : reader.GetString(19) // debtor_agent
                        };

                        transactions.Add(transaction);
                    }
                }
            }
        }

        // Sorteer op booking date (afgeleid van rabo_booking_datetime) en dan op transaction ID
        return transactions.OrderBy(t => t.BookingDate).ThenBy(t => t.TransactionId).ToList();
    }

    /// <summary>
    /// Valideer opgehaalde data
    /// </summary>
    private void ValidateData(BalanceData balanceData, List<Transaction> transactions, string iban, DateTime startDate, DateTime endDate)
    {
        if (balanceData.OpeningBalance == null)
            throw new Exception("Opening balance niet gevonden voor periode");

        if (balanceData.ClosingBalance == null)
            throw new Exception("Closing balance niet gevonden voor periode");

        if (string.IsNullOrEmpty(balanceData.Currency))
            throw new Exception("Currency niet gevonden");

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
        }
    }

    /// <summary>
    /// Genereer CAMT.053 XML document
    /// </summary>
    private XDocument GenerateXmlDocument(BalanceData balanceData, List<Transaction> transactions, string iban, DateTime startDate, DateTime endDate)
    {
        var messageId = GenerateMessageId(startDate, endDate);
        var statementId = GenerateStatementId(iban, startDate, endDate);
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
                        new XElement(ns + "ElctrncSeqNb", GenerateSequenceNumber()),
                        new XElement(ns + "CreDtTm", creationDateTime),
                        // Account
                        new XElement(ns + "Acct",
                            new XElement(ns + "Id",
                                new XElement(ns + "IBAN", iban)
                            ),
                            new XElement(ns + "Ccy", balanceData.Currency),
                            new XElement(ns + "Nm", GetAccountName(iban)),
                            new XElement(ns + "Ownr",
                                new XElement(ns + "Nm", GetAccountName(iban))
                            ),
                            new XElement(ns + "Svcr",
                                new XElement(ns + "FinInstnId",
                                    new XElement(ns + "BIC", "RABONL2U")
                                )
                            )
                        ),
                        // From/To Date
                        new XElement(ns + "FrToDt",
                            new XElement(ns + "FrDtTm", startDate.ToString("yyyy-MM-dd") + "T00:00:00"),
                            new XElement(ns + "ToDtTm", endDate.ToString("yyyy-MM-dd") + "T23:59:59")
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
            // NtryRef - entry_reference uit database
            new XElement(ns + "NtryRef", transaction.TransactionId),
            // Amount met currency
            new XElement(ns + "Amt",
                new XAttribute("Ccy", transaction.Currency),
                transaction.Amount.ToString("F2", CultureInfo.InvariantCulture)
            ),
            // Credit/Debit Indicator
            new XElement(ns + "CdtDbtInd", transaction.CreditDebitIndicator),
            // Status - always BOOK for booked transactions
            new XElement(ns + "Sts", "BOOK"),
            // Booking Date
            new XElement(ns + "BookgDt",
                new XElement(ns + "Dt", transaction.BookingDate.ToString("yyyy-MM-dd"))
            ),
            // Value Date
            new XElement(ns + "ValDt",
                new XElement(ns + "Dt", transaction.ValueDate.ToString("yyyy-MM-dd"))
            ),
            // AcctSvcrRef - Generate format like "43011075189:CI49CT" (need to research this pattern)
            new XElement(ns + "AcctSvcrRef", GenerateAccountServicerReference(transaction)),
            // BkTxCd - Bank Transaction Code per reference.xml
            new XElement(ns + "BkTxCd",
                new XElement(ns + "Domn",
                    new XElement(ns + "Cd", "PMNT"), // Payment domain
                    new XElement(ns + "Fmly",
                        new XElement(ns + "Cd", "RCDT"), // Received Credit Transfer
                        new XElement(ns + "SubFmlyCd", "ESCT") // SEPA Credit Transfer
                    )
                ),
                // Proprietary Bank Transaction Code - gebruik rabo_detailed_transaction_type
                new XElement(ns + "Prtry",
                    new XElement(ns + "Cd", transaction.RaboDetailedTransactionType), // uit database
                    new XElement(ns + "Issr", "RABOBANK")
                )
            )
        );

        // Transaction Details
        var entryDetails = new XElement(ns + "NtryDtls",
            new XElement(ns + "TxDtls",
                CreateRefsElement(ns, transaction),
                // AmtDtls - Amount Details (Rabobank specifiek)
                new XElement(ns + "AmtDtls",
                    new XElement(ns + "TxAmt",
                        new XElement(ns + "Amt",
                            new XAttribute("Ccy", transaction.Currency),
                            transaction.Amount.ToString("F2", CultureInfo.InvariantCulture)
                        )
                    ),
                    new XElement(ns + "PrtryAmt",
                        new XElement(ns + "Tp", "IBS"), // Internal Banking System
                        new XElement(ns + "Amt",
                            new XAttribute("Ccy", transaction.Currency),
                            transaction.Amount.ToString("F2", CultureInfo.InvariantCulture)
                        )
                    )
                ),
                // BkTxCd - Bank Transaction Code op transaction details niveau
                new XElement(ns + "BkTxCd",
                    new XElement(ns + "Domn",
                        new XElement(ns + "Cd", "PMNT"), // Payment domain
                        new XElement(ns + "Fmly",
                            new XElement(ns + "Cd", "RCDT"), // Received Credit Transfer  
                            new XElement(ns + "SubFmlyCd", "ESCT") // SEPA Credit Transfer
                        )
                    ),
                    new XElement(ns + "Prtry",
                        new XElement(ns + "Cd", transaction.RaboDetailedTransactionType), // rabo_detailed_transaction_type
                        new XElement(ns + "Issr", "RABOBANK")
                    )
                )
            )
        );



        // Add Related Parties if available
        if (!string.IsNullOrEmpty(transaction.DebtorName) || !string.IsNullOrEmpty(transaction.CreditorName) ||
            !string.IsNullOrEmpty(transaction.UltimateDebtorName) || !string.IsNullOrEmpty(transaction.UltimateCreditorName))
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

            if (!string.IsNullOrEmpty(transaction.UltimateDebtorName))
            {
                relatedParties.Add(new XElement(ns + "UltmtDbtr",
                    new XElement(ns + "Nm", transaction.UltimateDebtorName)
                ));
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

            if (!string.IsNullOrEmpty(transaction.UltimateCreditorName))
            {
                relatedParties.Add(new XElement(ns + "UltmtCdtr",
                    new XElement(ns + "Nm", transaction.UltimateCreditorName)
                ));
            }

            entryDetails.Element(ns + "TxDtls").Add(relatedParties);
        }

        // Add Related Agents (RltdAgts) - per reference.xml
        var relatedAgents = new XElement(ns + "RltdAgts");
        if (!string.IsNullOrEmpty(transaction.DebtorIBAN))
        {
            relatedAgents.Add(new XElement(ns + "DbtrAgt",
                new XElement(ns + "FinInstnId",
                    new XElement(ns + "BIC", transaction.DebtorAgent) // debtor_agent uit database
                )
            ));
        }
        if (!string.IsNullOrEmpty(transaction.CreditorIBAN))
        {
            relatedAgents.Add(new XElement(ns + "CdtrAgt",
                new XElement(ns + "FinInstnId",
                    new XElement(ns + "BIC", "RABONL2U") // Rabobank als creditor agent
                )
            ));
        }
        if (relatedAgents.HasElements)
        {
            entryDetails.Element(ns + "TxDtls").Add(relatedAgents);
        }

        // Add Purpose Code if available
        if (!string.IsNullOrEmpty(transaction.PurposeCode))
        {
            entryDetails.Element(ns + "TxDtls").Add(
                new XElement(ns + "Purp",
                    new XElement(ns + "Cd", transaction.PurposeCode)
                )
            );
        }

        // Add Mandate ID if available (SEPA Direct Debit)
        if (!string.IsNullOrEmpty(transaction.MandateId))
        {
            entryDetails.Element(ns + "TxDtls").Add(
                new XElement(ns + "MndtRltdInf",
                    new XElement(ns + "MndtId", transaction.MandateId)
                )
            );
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

        // Add Related Dates (RltdDts) - Rabobank specifiek
        entryDetails.Element(ns + "TxDtls").Add(
            new XElement(ns + "RltdDts",
                new XElement(ns + "IntrBkSttlmDt", transaction.ValueDate.ToString("yyyy-MM-dd"))
            )
        );

        entry.Add(entryDetails);
        return entry;
    }

    /// <summary>
    /// Haal account naam op uit database op basis van IBAN
    /// </summary>
    private string GetAccountName(string iban)
    {
        try
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                var query = @"
                    SELECT owner_name 
                    FROM bai_rabobank_account_info 
                    WHERE iban = @iban 
                    LIMIT 1";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@iban", iban);

                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "Onbekende Rekening";
                }
            }
        }
        catch
        {
            return "Onbekende Rekening";
        }
    }

    /// <summary>
    /// Helper methods
    /// </summary>
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

    private string GenerateMessageId(DateTime startDate, DateTime endDate)
    {
        var dateStr = startDate.ToString("yyyyMMdd") + "_" + endDate.ToString("yyyyMMdd");
        return $"STMT{dateStr}001";
    }

    private string GenerateStatementId(string iban, DateTime startDate, DateTime endDate)
    {
        var dateStr = startDate.ToString("yyyyMMdd") + "_" + endDate.ToString("yyyyMMdd");
        return $"{iban.Replace(" ", "")}_{dateStr}";
    }

    private string GenerateSequenceNumber()
    {
        // Genereer een sequence number gebaseerd op huidige tijd
        var now = DateTime.Now;
        return now.ToString("yyyyMMdd") + now.Ticks.ToString().Substring(8); // Laatste 10 digits van ticks
    }

    /// <summary>
    /// Genereer Account Servicer Reference format zoals "43011075189:CI49CT"
    /// Pattern nog te onderzoeken - voor nu gebruik TransactionId
    /// </summary>
    private string GenerateAccountServicerReference(Transaction transaction)
    {
        // TODO: Onderzoek het exacte patroon voor AcctSvcrRef
        // Bijvoorbeeld: transactie sequence nummer + transactie type code
        // Voor nu gebruiken we de TransactionId als fallback
        return transaction.TransactionId;
    }

    /// <summary>
    /// Creëer Refs element met alle references per reference.xml mapping
    /// </summary>
    private XElement CreateRefsElement(XNamespace ns, Transaction transaction)
    {
        var refs = new XElement(ns + "Refs");
        
        // AcctSvcrRef - batch_entry_reference uit database 
        refs.Add(new XElement(ns + "AcctSvcrRef", transaction.BatchEntryReference ?? transaction.TransactionId));
        
        // InstrId - batch_entry_reference (same as AcctSvcrRef per reference.xml)
        refs.Add(new XElement(ns + "InstrId", transaction.BatchEntryReference ?? transaction.TransactionId));
        
        // EndToEndId - end_to_end_id uit database (only if available)
        if (!string.IsNullOrEmpty(transaction.EndToEndId))
        {
            refs.Add(new XElement(ns + "EndToEndId", transaction.EndToEndId));
        }
        
        return refs;
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
    public string ReferenceDate { get; set; }
}

public class Transaction
{
    public string TransactionId { get; set; } // entry_reference -> NtryRef
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
    public string BankTransactionCode { get; set; }
    public string PurposeCode { get; set; }
    public string MandateId { get; set; }
    public string UltimateDebtorName { get; set; }
    public string UltimateCreditorName { get; set; }
    
    // Nieuwe mappings gebaseerd op reference.xml
    public string BatchEntryReference { get; set; } // batch_entry_reference -> AcctSvcrRef in TxDtls
    public string RaboDetailedTransactionType { get; set; } // rabo_detailed_transaction_type -> BkTxCd Prtry Cd
    public string DebtorAgent { get; set; } // debtor_agent -> RltdAgts DbtrAgt BIC
}