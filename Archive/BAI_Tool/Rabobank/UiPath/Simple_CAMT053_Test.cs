// Simple UiPath CAMT053 Test - Copy into Invoke Code Activity
// Update connection string and test parameters below

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
    
    System.IO.File.WriteAllText(outputPath, xml);
    
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
    
    var entryCount = xml.Split(new string[] {"<Ntry>"}, StringSplitOptions.None).Length - 1;
    Console.WriteLine($"Transaction Count: {entryCount}");
    
    Console.WriteLine("Test completed! Check the XML file for detailed results.");
}
catch (Exception ex)
{
    Console.WriteLine($"Test failed: {ex.Message}");
    Console.WriteLine($"Details: {ex.ToString()}");
}