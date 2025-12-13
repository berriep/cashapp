using System;
using System.IO;

/// <summary>
/// Test script voor de geüpdateerde CAMT053DatabaseGenerator met alle database field mappings
/// </summary>
public class CAMT053_Test_Updated
{
    public static void Main(string[] args)
    {
        Console.WriteLine("🔬 CAMT053DatabaseGenerator Test - Updated Implementation");
        Console.WriteLine("=" + new string('=', 60));
        
        try
        {
            // Database connection string - pas aan naar jouw setup
            var connectionString = "Host=localhost;Database=bai_tool;Username=postgres;Password=yourpassword";
            
            // Test parameters
            var testIban = "NL48RABO0300002343"; // Gebruik IBAN uit jouw reference.xml
            var startDate = "2025-10-06";
            var endDate = "2025-10-06";
            
            Console.WriteLine($"📊 Test Parameters:");
            Console.WriteLine($"   IBAN: {testIban}");
            Console.WriteLine($"   Periode: {startDate} tot {endDate}");
            Console.WriteLine($"   Database: {connectionString.Split(';')[1]}");
            Console.WriteLine();
            
            // Initialize generator
            var generator = new CAMT053DatabaseGenerator(connectionString);
            
            Console.WriteLine("🚀 Genereren CAMT.053 XML...");
            
            // Generate CAMT.053
            var xml = generator.GenerateCAMT053(testIban, startDate, endDate);
            
            // Save to file
            var outputPath = Path.Combine(
                @"c:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Output",
                $"camt053_updated_test_{testIban}_{startDate}.xml"
            );
            
            File.WriteAllText(outputPath, xml);
            
            Console.WriteLine("✅ CAMT.053 XML succesvol gegenereerd!");
            Console.WriteLine($"📄 Bestand opgeslagen: {outputPath}");
            Console.WriteLine();
            
            // Analyze generated XML
            AnalyzeGeneratedXML(xml);
            
            Console.WriteLine("🎯 Test voltooid - controleer het gegenereerde XML bestand!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fout tijdens test: {ex.Message}");
            Console.WriteLine($"📍 Details: {ex}");
        }
        
        Console.WriteLine("\nDruk op een toets om te sluiten...");
        Console.ReadKey();
    }
    
    private static void AnalyzeGeneratedXML(string xml)
    {
        Console.WriteLine("🔍 XML Analysis:");
        Console.WriteLine("-" + new string('-', 40));
        
        // Check for critical elements
        var checks = new[]
        {
            ("<NtryRef>", "Entry Reference"),
            ("<AcctSvcrRef>", "Account Servicer Reference"),  
            ("<BkTxCd>", "Bank Transaction Code"),
            ("<Domn>", "Domain Code"),
            ("<Prtry>", "Proprietary Code"),
            ("<Refs>", "References Section"),
            ("<RltdAgts>", "Related Agents"),
            ("<RltdDts>", "Related Dates"),
            ("<EndToEndId>", "End to End ID"),
            ("<InstrId>", "Instruction ID")
        };
        
        foreach (var (element, description) in checks)
        {
            var found = xml.Contains(element);
            var status = found ? "✅" : "❌";
            Console.WriteLine($"   {status} {description}: {(found ? "FOUND" : "MISSING")}");
        }
        
        // Count entries
        var entryCount = CountOccurrences(xml, "<Ntry>");
        Console.WriteLine($"   📊 Transaction Entries: {entryCount}");
        
        // Check for new database fields
        Console.WriteLine("\n🗃️ Database Field Integration:");
        Console.WriteLine("-" + new string('-', 40));
        
        var fieldChecks = new[]
        {
            ("entry_reference → NtryRef", xml.Contains("<NtryRef>")),
            ("batch_entry_reference → AcctSvcrRef", xml.Contains("<AcctSvcrRef>")),
            ("rabo_detailed_transaction_type → BkTxCd", xml.Contains("<Prtry>")),
            ("debtor_agent → DbtrAgt BIC", xml.Contains("<DbtrAgt>")),
            ("end_to_end_id → EndToEndId", xml.Contains("<EndToEndId>"))
        };
        
        foreach (var (mapping, found) in fieldChecks)
        {
            var status = found ? "✅" : "❌";
            Console.WriteLine($"   {status} {mapping}");
        }
        
        Console.WriteLine($"\n📏 XML Size: {xml.Length:N0} characters");
    }
    
    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}