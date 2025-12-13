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
/// Uitgebreide test class voor CAMT.053 Database Generator
/// Test alle functionaliteiten inclusief Rabobank-specifieke elementen
/// </summary>
public class CAMT053GeneratorTest
{
    /// <summary>
    /// Test de XML generatie met mock data
    /// </summary>
    public static string TestXmlGeneration()
    {
        try
        {
            // Create mock balance data
            var balanceData = new BalanceData
            {
                IBAN = "NL12RABO0330208888",
                Currency = "EUR",
                OpeningBalance = new Balance
                {
                    Amount = 1000.00m,
                    Currency = "EUR",
                    CreditDebitIndicator = "CRDT",
                    ReferenceDate = "2025-10-01"
                },
                ClosingBalance = new Balance
                {
                    Amount = 1250.00m,
                    Currency = "EUR",
                    CreditDebitIndicator = "CRDT",
                    ReferenceDate = "2025-10-31"
                }
            };

            // Create mock transaction data
            var transactions = new List<Transaction>
            {
                new Transaction
                {
                    TransactionId = "TXN001",
                    Amount = 150.00m,
                    Currency = "EUR",
                    CreditDebitIndicator = "CRDT",
                    BookingDate = DateTime.Parse("2025-10-15"),
                    ValueDate = DateTime.Parse("2025-10-15"),
                    DebtorName = "Test Debtor",
                    DebtorIBAN = "NL19RABO0144142392",
                    RemittanceInfo = "Test payment",
                    EndToEndId = "E2E001"
                },
                new Transaction
                {
                    TransactionId = "TXN002",
                    Amount = 100.00m,
                    Currency = "EUR",
                    CreditDebitIndicator = "DBIT",
                    BookingDate = DateTime.Parse("2025-10-20"),
                    ValueDate = DateTime.Parse("2025-10-20"),
                    CreditorName = "Test Creditor",
                    CreditorIBAN = "NL12RABO0330208888",
                    RemittanceInfo = "Test withdrawal",
                    EndToEndId = "E2E002"
                }
            };

            // Create generator instance (without database connection)
            var generator = new CAMT053DatabaseGenerator("mock");

            // Generate XML using reflection to access private method
            var method = typeof(CAMT053DatabaseGenerator).GetMethod("GenerateXmlDocument",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var xmlDoc = (XDocument)method.Invoke(generator,
                new object[] { balanceData, transactions, "NL12RABO0330208888",
                    DateTime.Parse("2025-10-01"), DateTime.Parse("2025-10-31") });

            // Format XML
            var formatMethod = typeof(CAMT053DatabaseGenerator).GetMethod("FormatXml",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (string)formatMethod.Invoke(generator, new object[] { xmlDoc });
        }
        catch (Exception ex)
        {
            return $"Test failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Test database connection (zonder data op te halen)
    /// </summary>
    public static string TestDatabaseConnection(string connectionString)
    {
        try
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                return "Database connection successful";
            }
        }
        catch (Exception ex)
        {
            return $"Database connection failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Test data availability check
    /// </summary>
    public static string TestDataAvailability(string connectionString, string iban, string startDate, string endDate)
    {
        try
        {
            bool available = CAMT053GeneratorWrapper.CheckDataAvailability(connectionString, iban, startDate, endDate);
            return $"Data availability for {iban} ({startDate} to {endDate}): {available}";
        }
        catch (Exception ex)
        {
            return $"Data availability check failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Test data summary
    /// </summary>
    public static string TestDataSummary(string connectionString, string iban, string startDate, string endDate)
    {
        try
        {
            string summary = CAMT053GeneratorWrapper.GetDataSummary(connectionString, iban, startDate, endDate);
            return $"Data summary: {summary}";
        }
        catch (Exception ex)
        {
            return $"Data summary failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Comprehensive test methode voor alle functionaliteiten
    /// </summary>
    public static void RunComprehensiveTests()
    {
        Console.WriteLine("=== CAMT053 Generator Comprehensive Tests ===\n");

        // Test configuratie
        string connectionString = "Host=localhost;Database=rabobank_data;Username=test_user;Password=test_password";
        string testIban = "NL31RABO0300087233";
        string startDate = "2025-10-01";
        string endDate = "2025-10-01";

        try
        {
            // Test 1: Database schema validatie
            Console.WriteLine("Test 1: Database Schema Validation");
            ValidateDatabaseSchema(connectionString);
            Console.WriteLine();

            // Test 2: Mock XML generatie test
            Console.WriteLine("Test 2: Mock XML Generation Test");
            var mockXml = TestXmlGeneration();
            if (!string.IsNullOrEmpty(mockXml) && !mockXml.StartsWith("Test failed"))
            {
                Console.WriteLine("✓ Mock XML generation successful");
                ValidateRabobankSpecificElements(mockXml);
                
                // Sla mock XML op
                File.WriteAllText("mock_camt053_output.xml", mockXml);
                Console.WriteLine("✓ Mock XML saved to mock_camt053_output.xml");
            }
            else
            {
                Console.WriteLine($"✗ Mock XML generation failed: {mockXml}");
            }
            Console.WriteLine();

            // Test 3: Database connectiviteit
            Console.WriteLine("Test 3: Database Connectivity");
            var connectionResult = TestDatabaseConnection(connectionString);
            Console.WriteLine(connectionResult);
            Console.WriteLine();

            // Test 4: Data availability check
            Console.WriteLine("Test 4: Data Availability Check");
            var dataAvailability = TestDataAvailability(connectionString, testIban, startDate, endDate);
            Console.WriteLine(dataAvailability);
            Console.WriteLine();

            // Test 5: Data summary
            Console.WriteLine("Test 5: Data Summary");
            var dataSummary = TestDataSummary(connectionString, testIban, startDate, endDate);
            Console.WriteLine(dataSummary);
            Console.WriteLine();

            // Test 6: Complete workflow test (als data beschikbaar is)
            Console.WriteLine("Test 6: Complete Workflow Test");
            TestCompleteWorkflow(connectionString, testIban, startDate, endDate);
            Console.WriteLine();

            // Test 7: Error handling
            Console.WriteLine("Test 7: Error Handling Tests");
            TestErrorScenarios(connectionString);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test suite failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\n=== Test Suite Completed ===");
    }

    /// <summary>
    /// Valideer database schema
    /// </summary>
    private static void ValidateDatabaseSchema(string connectionString)
    {
        string[] requiredTables = {
            "bai_rabobank_balances_payload",
            "bai_rabobank_transactions_payload", 
            "bai_rabobank_account_info"
        };

        try
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                Console.WriteLine("✓ Database connection established");

                foreach (var table in requiredTables)
                {
                    var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{table}'", conn);
                    var exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    Console.WriteLine($"  {table}: {(exists ? "✓ Exists" : "✗ Missing")}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Database schema validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Valideer Rabobank-specifieke XML elementen
    /// </summary>
    private static void ValidateRabobankSpecificElements(string xml)
    {
        Console.WriteLine("  Validating Rabobank-specific elements:");
        
        string[] rabobankElements = {
            "<NtryRef>",
            "<AcctSvcrRef>", 
            "<RltdAgts>",
            "<RltdDts>",
            "<IntrBkSttlmDt>",
            "<Prtry>",
            "<Issr>RABOBANK</Issr>",
            "<AmtDtls>",
            "<PrtryAmt>",
            "<Tp>IBS</Tp>",
            "urn:iso:std:iso:20022:tech:xsd:camt.053.001.02"
        };

        foreach (var element in rabobankElements)
        {
            if (xml.Contains(element))
            {
                Console.WriteLine($"    ✓ {element}");
            }
            else
            {
                Console.WriteLine($"    ✗ Missing: {element}");
            }
        }
    }

    /// <summary>
    /// Test complete workflow van database naar XML
    /// </summary>
    private static void TestCompleteWorkflow(string connectionString, string iban, string startDate, string endDate)
    {
        try
        {
            var xmlResult = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(
                connectionString, iban, startDate, endDate);

            if (!string.IsNullOrEmpty(xmlResult))
            {
                Console.WriteLine("✓ Complete workflow successful");
                
                string outputPath = $"complete_test_{iban}_{startDate}.xml";
                File.WriteAllText(outputPath, xmlResult);
                Console.WriteLine($"✓ Complete XML saved to: {outputPath}");

                ValidateRabobankSpecificElements(xmlResult);
            }
            else
            {
                Console.WriteLine("ℹ Complete workflow returned null (possibly no data available)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Complete workflow failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Test error handling scenarios
    /// </summary>
    private static void TestErrorScenarios(string connectionString)
    {
        // Test invalid IBAN
        try
        {
            var result = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(
                connectionString, "INVALID_IBAN", "2025-10-01", "2025-10-01");
            Console.WriteLine($"  Invalid IBAN: {(result == null ? "✓ Handled correctly" : "✗ Should return null")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Invalid IBAN: ✓ Exception handled correctly - {ex.Message}");
        }

        // Test invalid date
        try
        {
            var result = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(
                connectionString, "NL31RABO0300087233", "invalid-date", "2025-10-01");
            Console.WriteLine($"  Invalid date: {(result == null ? "✓ Handled correctly" : "✗ Should return null")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Invalid date: ✓ Exception handled correctly - {ex.Message}");
        }

        // Test future dates (no data expected)
        try
        {
            var result = CAMT053GeneratorWrapper.GenerateCAMT053FromDatabase(
                connectionString, "NL31RABO0300087233", "2030-01-01", "2030-01-01");
            Console.WriteLine($"  Future dates: {(result == null ? "✓ No data available (correct)" : "ℹ Unexpected data found")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Future dates: ✓ Exception handled correctly - {ex.Message}");
        }
    }
}