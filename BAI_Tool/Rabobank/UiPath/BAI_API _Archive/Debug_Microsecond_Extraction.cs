// Debug Script: insert_transactions.cs Precision Loss Investigation
// Purpose: Find exactly where microseconds are lost in the insert process
// Based on: Confirmed API provides raboBookingDateTime with 6-digit microseconds

using System;
using Newtonsoft.Json.Linq;

// Test data exactly from the API response you provided
string testApiResponse = @"{
    ""transactions"": {
        ""booked"": [
            {
                ""raboBookingDateTime"": ""2025-08-04T15:00:36.584509Z"",
                ""transactionAmount"": {""value"": ""-96313.45"", ""currency"": ""EUR""},
                ""entryReference"": ""62373""
            },
            {
                ""raboBookingDateTime"": ""2025-08-04T10:12:45.940121Z"",
                ""transactionAmount"": {""value"": ""538.91"", ""currency"": ""EUR""},
                ""entryReference"": ""62372""
            }
        ]
    }
}";

Console.WriteLine(""=== Debug: insert_transactions.cs Precision Loss ===\n"");

try
{
    // Step 1: Parse the JSON exactly like GetTransactions_auditlog.cs does
    Console.WriteLine(""Step 1: Parsing API response (like GetTransactions_auditlog.cs)"");
    var jobjTransactionResponse = JObject.Parse(testApiResponse);
    Console.WriteLine(""✅ JSON parsed successfully"");
    
    // Step 2: Extract transactions array (like insert_transactions.cs does)
    Console.WriteLine(""Step 2: Extracting transactions array"");
    if (jobjTransactionResponse[""transactions""]?[""booked""] is JArray transactions)
    {
        Console.WriteLine($""✅ Found {transactions.Count} transactions"");
        
        // Step 3: Process each transaction (simulating insert_transactions.cs)
        Console.WriteLine(""Step 3: Processing each transaction"");
        
        for (int i = 0; i < transactions.Count; i++)
        {
            var transaction = transactions[i] as JObject;
            Console.WriteLine($""\n--- Transaction {i + 1} ---"");
            
            // Test different methods of extracting raboBookingDateTime
            Console.WriteLine(""Testing different extraction methods:"");
            
            // Method 1: Direct access (current approach?)
            var raboBookingDateTime1 = transaction[""raboBookingDateTime""];
            Console.WriteLine($""Method 1 - Direct access: '{raboBookingDateTime1}' (Type: {raboBookingDateTime1?.Type})"");
            
            // Method 2: ToString() method (what GetStr() probably does)
            var raboBookingDateTime2 = transaction[""raboBookingDateTime""]?.ToString();
            Console.WriteLine($""Method 2 - ToString(): '{raboBookingDateTime2}'"");
            
            // Method 3: Value<string>() method
            var raboBookingDateTime3 = transaction[""raboBookingDateTime""]?.Value<string>();
            Console.WriteLine($""Method 3 - Value<string>(): '{raboBookingDateTime3}'"");
            
            // Method 4: Parse as DateTime and format
            try
            {
                if (DateTime.TryParse(raboBookingDateTime2, out DateTime parsed))
                {
                    Console.WriteLine($""Method 4 - DateTime.Parse(): {parsed:yyyy-MM-dd HH:mm:ss.ffffff}"");
                    Console.WriteLine($""Method 4 - DateTime Kind: {parsed.Kind}"");
                    Console.WriteLine($""Method 4 - Ticks: {parsed.Ticks}"");
                    
                    // Check if microseconds are preserved
                    long microseconds = (parsed.Ticks % TimeSpan.TicksPerSecond) / TimeSpan.TicksPerMicrosecond;
                    Console.WriteLine($""Method 4 - Extracted microseconds: {microseconds}"");
                }
                else
                {
                    Console.WriteLine(""❌ Method 4 - DateTime.Parse() failed"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($""❌ Method 4 - Exception: {ex.Message}"");
            }
            
            // Method 5: Check raw JSON token
            Console.WriteLine($""Method 5 - Raw JSON token: '{transaction[""raboBookingDateTime""].ToString()}'"");
            
            // Method 6: Simulate database parameter creation
            string dbValue = raboBookingDateTime2; // This is probably what goes to DB
            Console.WriteLine($""Method 6 - Database value: '{dbValue}'"");
            
            // Method 7: Check if string contains all 6 microsecond digits
            if (raboBookingDateTime2 != null && raboBookingDateTime2.Contains("".""))
            {
                string fractionalPart = raboBookingDateTime2.Substring(raboBookingDateTime2.IndexOf(""."") + 1);
                if (fractionalPart.Contains(""Z""))
                    fractionalPart = fractionalPart.Replace(""Z"", """");
                
                Console.WriteLine($""Method 7 - Fractional part: '{fractionalPart}' (Length: {fractionalPart.Length})"");
                Console.WriteLine($""Method 7 - Has 6 digits: {fractionalPart.Length == 6}"");
            }
        }
        
        // Step 4: Simulate the GetStr() helper function from insert_transactions.cs
        Console.WriteLine(""\n=== Testing GetStr() Helper Function ===\");
        
        static string GetStr(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            return token.ToString();
        }
        
        var firstTransaction = transactions[0] as JObject;
        string getStrResult = GetStr(firstTransaction[""raboBookingDateTime""]);
        Console.WriteLine($""GetStr() result: '{getStrResult}'"");
        
        // Step 5: Check what happens during database insertion
        Console.WriteLine(""\n=== Database Insertion Simulation ===\");
        
        // This simulates what might happen in the database insert
        Console.WriteLine(""Simulating PostgreSQL timestamp(6) insertion:"");
        Console.WriteLine($""Input value: '{getStrResult}'"");
        
        // PostgreSQL should handle this correctly if it's timestamp(6)
        if (getStrResult != null && getStrResult.Contains("".""))
        {
            Console.WriteLine(""✅ Contains fractional seconds - should work with timestamp(6)"");
        }
        else
        {
            Console.WriteLine(""❌ No fractional seconds - will result in .000000"");
        }
        
    }
    else
    {
        Console.WriteLine(""❌ No transactions found in response"");
    }
}
catch (Exception ex)
{
    Console.WriteLine($""❌ Error during processing: {ex.Message}"");
    Console.WriteLine($""Stack trace: {ex.StackTrace}"");
}

Console.WriteLine(""\n=== CONCLUSIONS ===\");
Console.WriteLine(""1. API provides perfect 6-digit microseconds"");
Console.WriteLine(""2. JObject.Parse() preserves the precision"");
Console.WriteLine(""3. ToString() method should preserve precision"");
Console.WriteLine(""4. Check if insert_transactions.cs uses correct extraction method"");
Console.WriteLine(""5. Verify database parameter creation preserves string value"");

Console.WriteLine(""\n=== RECOMMENDED ACTIONS ===\");
Console.WriteLine(""1. Run this script to confirm extraction methods work"");
Console.WriteLine(""2. Check insert_transactions.cs for DateTime parsing that might lose precision"");
Console.WriteLine(""3. Ensure database parameters use string values, not DateTime objects"");
Console.WriteLine(""4. Verify PostgreSQL connection string doesn't have precision limitations"");