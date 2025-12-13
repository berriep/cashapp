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
/// UiPath Invoke Code wrapper voor CAMT.053 generatie uit database
/// </summary>
public class CAMT053GeneratorWrapper
{
    /// <summary>
    /// Genereert CAMT.053 XML voor een specifieke IBAN en periode
    /// Retourneert null als opening balance van vorige dag niet beschikbaar is
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="iban">IBAN van de rekening</param>
    /// <param name="startDate">Startdatum (YYYY-MM-DD)</param>
    /// <param name="endDate">Einddatum (YYYY-MM-DD)</param>
    /// <returns>CAMT.053 XML als string, of null als opening balance ontbreekt</returns>
    public static string GenerateCAMT053FromDatabase(string connectionString, string iban, string startDate, string endDate)
    {
        try
        {
            var generator = new CAMT053DatabaseGenerator(connectionString);
            return generator.GenerateCAMT053(iban, startDate, endDate);
        }
        catch (Exception ex)
        {
            // Check if it's specifically an opening balance error
            if (ex.Message.Contains("Opening balance niet gevonden"))
            {
                return null; // Return null to indicate missing opening balance
            }

            // Return error XML for other errors
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Document xmlns=""urn:iso:std:iso:20022:tech:xsd:camt.053.001.02"">
    <BkToCstmrStmt>
        <GrpHdr>
            <MsgId>ERROR</MsgId>
            <CreDtTm>{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")}</CreDtTm>
        </GrpHdr>
        <Stmt>
            <Id>ERROR</Id>
            <AddtlStmtInf>
                <AddtlInf>{System.Security.SecurityElement.Escape(ex.Message)}</AddtlInf>
            </AddtlStmtInf>
        </Stmt>
    </BkToCstmrStmt>
</Document>";
        }
    }

    /// <summary>
    /// Controleert of data beschikbaar is voor CAMT.053 generatie
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="iban">IBAN van de rekening</param>
    /// <param name="startDate">Startdatum (YYYY-MM-DD)</param>
    /// <param name="endDate">Einddatum (YYYY-MM-DD)</param>
    /// <returns>true als data beschikbaar is, anders false</returns>
    public static bool CheckDataAvailability(string connectionString, string iban, string startDate, string endDate)
    {
        try
        {
            var start = DateTime.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var end = DateTime.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                // Check balances
                var balanceQuery = @"
                    SELECT COUNT(*) FROM bai_rabobank_balances_payload
                    WHERE iban = @iban
                      AND balance_type = 'closingBooked'
                      AND reference_date BETWEEN @startDate AND @endDate";

                using (var cmd = new NpgsqlCommand(balanceQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@iban", iban);
                    cmd.Parameters.AddWithValue("@startDate", start);
                    cmd.Parameters.AddWithValue("@endDate", end);

                    var balanceCount = (long)cmd.ExecuteScalar();
                    if (balanceCount == 0) return false;
                }

                // Check transactions
                var transactionQuery = @"
                    SELECT COUNT(*) FROM bai_rabobank_transactions_payload
                    WHERE iban = @iban
                      AND booking_date BETWEEN @startDate AND @endDate";

                using (var cmd = new NpgsqlCommand(transactionQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@iban", iban);
                    cmd.Parameters.AddWithValue("@startDate", start);
                    cmd.Parameters.AddWithValue("@endDate", end);

                    var transactionCount = (long)cmd.ExecuteScalar();
                    return transactionCount > 0;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Haalt samenvatting op van beschikbare data
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="iban">IBAN van de rekening</param>
    /// <param name="startDate">Startdatum (YYYY-MM-DD)</param>
    /// <param name="endDate">Einddatum (YYYY-MM-DD)</param>
    /// <returns>JSON string met samenvatting</returns>
    public static string GetDataSummary(string connectionString, string iban, string startDate, string endDate)
    {
        try
        {
            var start = DateTime.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var end = DateTime.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                // Get balance info
                var balanceQuery = @"
                    SELECT COUNT(*), MIN(reference_date), MAX(reference_date)
                    FROM bai_rabobank_balances_payload
                    WHERE iban = @iban
                      AND balance_type = 'closingBooked'
                      AND reference_date BETWEEN @startDate AND @endDate";

                long balanceCount = 0;
                DateTime? minBalanceDate = null;
                DateTime? maxBalanceDate = null;

                using (var cmd = new NpgsqlCommand(balanceQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@iban", iban);
                    cmd.Parameters.AddWithValue("@startDate", start);
                    cmd.Parameters.AddWithValue("@endDate", end);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            balanceCount = reader.GetInt64(0);
                            minBalanceDate = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                            maxBalanceDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                        }
                    }
                }

                // Get transaction info
                var transactionQuery = @"
                    SELECT COUNT(*), MIN(booking_date), MAX(booking_date), SUM(transaction_amount)
                    FROM bai_rabobank_transactions_payload
                    WHERE iban = @iban
                      AND booking_date BETWEEN @startDate AND @endDate";

                long transactionCount = 0;
                DateTime? minTransactionDate = null;
                DateTime? maxTransactionDate = null;
                decimal totalAmount = 0;

                using (var cmd = new NpgsqlCommand(transactionQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@iban", iban);
                    cmd.Parameters.AddWithValue("@startDate", start);
                    cmd.Parameters.AddWithValue("@endDate", end);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            transactionCount = reader.GetInt64(0);
                            minTransactionDate = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                            maxTransactionDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                            totalAmount = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                        }
                    }
                }

                // Return JSON summary
                return $"{{\"iban\":\"{iban}\",\"startDate\":\"{startDate}\",\"endDate\":\"{endDate}\",\"balances\":{balanceCount},\"transactions\":{transactionCount},\"totalAmount\":{totalAmount:F2},\"minBalanceDate\":\"{minBalanceDate?.ToString("yyyy-MM-dd")}\",\"maxBalanceDate\":\"{maxBalanceDate?.ToString("yyyy-MM-dd")}\",\"minTransactionDate\":\"{minTransactionDate?.ToString("yyyy-MM-dd")}\",\"maxTransactionDate\":\"{maxTransactionDate?.ToString("yyyy-MM-dd")}\"}}";
            }
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"{System.Security.SecurityElement.Escape(ex.Message)}\"}}";
        }
    }
}