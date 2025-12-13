// Analysis Script: jobjTransactionResponse Structure for raboBookingDateTime
// Purpose: Understand how the Rabobank API response contains timestamp information
// Based on: GetTransactions_auditlog.cs - how jobjTransactionResponse is populated

// IMPORTANT: This script analyzes the structure that GetTransactions_auditlog.cs creates
// The jobjTransactionResponse is created by parsing the raw API response:
// jobjTransactionResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);

// Expected Structure from Rabobank API (based on Business Account Insight Transactions API):
/*
{
    "transactions": {
        "booked": [
            {
                "transactionId": "...",
                "bookingDate": "2025-01-01",  // Date only (YYYY-MM-DD)
                "valueDate": "2025-01-01",    // Date only (YYYY-MM-DD)
                "transactionAmount": {
                    "amount": "100.00",
                    "currency": "EUR"
                },
                "remittanceInformationUnstructured": "...",
                "proprietaryBankTransactionCode": "...",
                "raboBookingDateTime": "2025-01-01T10:30:15.123456Z",  // SHOULD contain microseconds
                "raboTransactionTypeName": "...",
                "raboDetailedTransactionType": "...",
                // ... other fields
            }
        ],
        "_links": {
            "next": "..." // For pagination
        }
    }
}
*/

using System;
using Newtonsoft.Json.Linq;

// Sample analysis function to understand actual structure
static void AnalyzeTransactionResponse(JObject jobjTransactionResponse)
{
    Console.WriteLine("=== jobjTransactionResponse Structure Analysis ===");
    
    if (jobjTransactionResponse == null)
    {
        Console.WriteLine("ERROR: jobjTransactionResponse is null");
        return;
    }
    
    // Analyze top-level structure
    Console.WriteLine("Top-level properties:");
    foreach (var prop in jobjTransactionResponse.Properties())
    {
        Console.WriteLine($"  - {prop.Name}: {prop.Value.Type}");
    }
    
    // Analyze transactions array
    if (jobjTransactionResponse["transactions"] != null)
    {
        Console.WriteLine("\nTransactions section found:");
        var transactions = jobjTransactionResponse["transactions"];
        
        foreach (var prop in ((JObject)transactions).Properties())
        {
            Console.WriteLine($"  - {prop.Name}: {prop.Value.Type}");
        }
        
        // Analyze booked transactions
        if (transactions["booked"] is JArray bookedTransactions)
        {
            Console.WriteLine($"\nBooked transactions: {bookedTransactions.Count} items");
            
            if (bookedTransactions.Count > 0)
            {
                Console.WriteLine("\nFirst transaction structure:");
                var firstTransaction = bookedTransactions[0] as JObject;
                
                foreach (var prop in firstTransaction.Properties())
                {
                    Console.WriteLine($"  - {prop.Name}: {prop.Value.Type}");
                    
                    // Special focus on timestamp fields
                    if (prop.Name.ToLower().Contains("date") || prop.Name.ToLower().Contains("time"))
                    {
                        Console.WriteLine($"    VALUE: '{prop.Value}'");
                        Console.WriteLine($"    TYPE: {prop.Value.Type}");
                        Console.WriteLine($"    STRING: '{prop.Value.ToString()}'");
                    }
                }
                
                // Specifically check for raboBookingDateTime
                Console.WriteLine("\n=== raboBookingDateTime Analysis ===");
                if (firstTransaction["raboBookingDateTime"] != null)
                {
                    var raboBookingDateTime = firstTransaction["raboBookingDateTime"];
                    Console.WriteLine($"raboBookingDateTime found:");
                    Console.WriteLine($"  Value: '{raboBookingDateTime}'");
                    Console.WriteLine($"  Type: {raboBookingDateTime.Type}");
                    Console.WriteLine($"  Raw JSON: '{raboBookingDateTime.ToString()}'");
                    
                    // Check if it contains microseconds
                    string timeStr = raboBookingDateTime.ToString();
                    if (timeStr.Contains("."))
                    {
                        string fractionalPart = timeStr.Substring(timeStr.IndexOf(".") + 1);
                        if (fractionalPart.Contains("Z"))
                            fractionalPart = fractionalPart.Replace("Z", "");
                        if (fractionalPart.Contains("+"))
                            fractionalPart = fractionalPart.Split('+')[0];
                        
                        Console.WriteLine($"  Fractional seconds: '{fractionalPart}'");
                        Console.WriteLine($"  Fractional length: {fractionalPart.Length} digits");
                        Console.WriteLine($"  Has microseconds (6 digits): {fractionalPart.Length == 6}");
                    }
                    else
                    {
                        Console.WriteLine("  NO fractional seconds found!");
                    }
                }
                else
                {
                    Console.WriteLine("❌ raboBookingDateTime field NOT FOUND!");
                    Console.WriteLine("Available timestamp fields:");
                    
                    foreach (var prop in firstTransaction.Properties())
                    {
                        if (prop.Name.ToLower().Contains("date") || prop.Name.ToLower().Contains("time"))
                        {
                            Console.WriteLine($"  - {prop.Name}: '{prop.Value}'");
                        }
                    }
                }
                
                // Check other common timestamp fields
                Console.WriteLine("\n=== Other Timestamp Fields ===");
                string[] timestampFields = { "bookingDate", "valueDate", "transactionDate", "executionDateTime" };
                
                foreach (string field in timestampFields)
                {
                    if (firstTransaction[field] != null)
                    {
                        Console.WriteLine($"{field}: '{firstTransaction[field]}' (Type: {firstTransaction[field].Type})");
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("No booked transactions array found");
        }
    }
    else
    {
        Console.WriteLine("No transactions section found in response");
    }
    
    Console.WriteLine("\n=== Raw JSON Sample (first 500 chars) ===");
    string rawJson = jobjTransactionResponse.ToString();
    Console.WriteLine(rawJson.Length > 500 ? rawJson.Substring(0, 500) + "..." : rawJson);
}

// Example of how GetTransactions_auditlog.cs processes the response:
static void SimulateGetTransactionsProcessing(string responseBody)
{
    Console.WriteLine("\n=== Simulating GetTransactions_auditlog.cs Processing ===");
    
    try
    {
        // This is exactly what GetTransactions_auditlog.cs does:
        var jobjTransactionResponse = JObject.Parse(responseBody);
        
        Console.WriteLine("✅ JSON parsing successful");
        Console.WriteLine($"Response keys: {string.Join(", ", jobjTransactionResponse.Properties().Select(p => p.Name))}");
        
        // Check the transaction collection process
        var allTransactions = new JArray();
        
        if (jobjTransactionResponse["transactions"]?["booked"] is JArray firstPageTransactions)
        {
            Console.WriteLine($"Found {firstPageTransactions.Count} transactions in first page");
            
            foreach (var transaction in firstPageTransactions)
            {
                allTransactions.Add(transaction);
            }
            
            Console.WriteLine($"Collected {allTransactions.Count} transactions total");
            
            // This is the final structure that gets passed to insert_transactions.cs
            jobjTransactionResponse["transactions"]["booked"] = allTransactions;
            
            Console.WriteLine("✅ Final jobjTransactionResponse ready for insert_transactions.cs");
        }
        else
        {
            Console.WriteLine("❌ No booked transactions found in response");
        }
        
        // Analyze the final structure
        AnalyzeTransactionResponse(jobjTransactionResponse);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ JSON parsing failed: {ex.Message}");
    }
}

// Key findings from the code analysis:
Console.WriteLine("=== KEY FINDINGS FROM GetTransactions_auditlog.cs ===");
Console.WriteLine("1. The jobjTransactionResponse is created by: JObject.Parse(responseBody)");
Console.WriteLine("2. It collects transactions from: jobjTransactionResponse[\"transactions\"][\"booked\"]");
Console.WriteLine("3. Handles pagination by collecting ALL transactions into a single array");
Console.WriteLine("4. The final jobjTransactionResponse contains the complete transaction list");
Console.WriteLine("5. This jobjTransactionResponse is passed as output to the next process");
Console.WriteLine("");
Console.WriteLine("❓ QUESTIONS TO INVESTIGATE:");
Console.WriteLine("1. Does the Rabobank API actually return 'raboBookingDateTime' field?");
Console.WriteLine("2. If yes, does it contain microsecond precision (6 digits)?");
Console.WriteLine("3. If no, which field contains the precise timestamp?");
Console.WriteLine("4. What is the exact JSON structure from the API?");
Console.WriteLine("");
Console.WriteLine("🔍 NEXT STEPS:");
Console.WriteLine("1. Run GetTransactions_auditlog.cs with debugEnabled=true");
Console.WriteLine("2. Examine the actual API response body");
Console.WriteLine("3. Check if raboBookingDateTime exists and its precision");
Console.WriteLine("4. Update insert_transactions.cs to handle the correct field");

// To use this analysis:
// 1. Run GetTransactions_auditlog.cs with debugEnabled=true
// 2. Copy the actual jobjTransactionResponse JSON
// 3. Pass it to AnalyzeTransactionResponse() function
// 4. Check the results to understand the actual structure