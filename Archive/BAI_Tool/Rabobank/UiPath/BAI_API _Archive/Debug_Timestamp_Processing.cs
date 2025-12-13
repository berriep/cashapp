// DEBUG Script - Analyze exactly what happens to timestamps in C# processing
// Input: jobjTransactions (JObject, In)
// Output: debugResults (String, Out)

try 
{
    var results = new System.Text.StringBuilder();
    results.AppendLine("TIMESTAMP PROCESSING DEBUG RESULTS:");
    results.AppendLine("=====================================");
    
    // Helper to safely get string value from JToken
    System.Func<Newtonsoft.Json.Linq.JToken, string> GetStr = t => t != null && t.Type != Newtonsoft.Json.Linq.JTokenType.Null ? t.ToString() : null;
    
    if (jobjTransactions == null)
    {
        debugResults = "ERROR: jobjTransactions is null";
        return;
    }

    // Parse root structure to get to transactions
    Newtonsoft.Json.Linq.JArray bookedArray = null;
    
    foreach (var prop in jobjTransactions.Properties())
    {
        if (prop.Name == "transactions")
        {
            var txObj = prop.Value as Newtonsoft.Json.Linq.JObject;
            if (txObj != null)
            {
                bookedArray = txObj["booked"] as Newtonsoft.Json.Linq.JArray;
            }
            break;
        }
    }
    
    if (bookedArray == null)
    {
        debugResults = "ERROR: No booked transactions found";
        return;
    }
    
    results.AppendLine($"Found {bookedArray.Count} transactions to analyze");
    results.AppendLine();
    
    // Analyze first few transactions
    int count = 0;
    foreach (var item in bookedArray)
    {
        if (count >= 3) break; // Only first 3 for debugging
        count++;
        
        var tx = item as Newtonsoft.Json.Linq.JObject;
        if (tx == null) continue;
        
        results.AppendLine($"TRANSACTION {count}:");
        results.AppendLine("================");
        
        // Get entry reference for identification
        string entryRef = GetStr(tx["entryReference"]);
        results.AppendLine($"Entry Reference: {entryRef}");
        
        // Analyze raboBookingDateTime processing step by step
        var raboDateTimeToken = tx["raboBookingDateTime"];
        results.AppendLine($"1. Raw JToken: {raboDateTimeToken}");
        results.AppendLine($"2. JToken Type: {raboDateTimeToken?.Type}");
        
        string raboBookingDateTime = GetStr(raboDateTimeToken);
        results.AppendLine($"3. GetStr() result: '{raboBookingDateTime}'");
        results.AppendLine($"4. String length: {raboBookingDateTime?.Length ?? 0}");
        
        // Check if it contains microseconds
        if (!string.IsNullOrEmpty(raboBookingDateTime))
        {
            if (raboBookingDateTime.Contains("."))
            {
                string[] parts = raboBookingDateTime.Split('.');
                if (parts.Length >= 2)
                {
                    string fractionalPart = parts[1].Replace("Z", "");
                    results.AppendLine($"5. Fractional part: '{fractionalPart}' (length: {fractionalPart.Length})");
                    
                    if (fractionalPart.Length == 6)
                    {
                        results.AppendLine("   ✓ HAS 6-digit microseconds!");
                    }
                    else if (fractionalPart.Length == 0)
                    {
                        results.AppendLine("   ✗ NO fractional seconds!");
                    }
                    else
                    {
                        results.AppendLine($"   ⚠ Has {fractionalPart.Length} digits (not 6)");
                    }
                }
                else
                {
                    results.AppendLine("5. ERROR: Split on '.' failed");
                }
            }
            else
            {
                results.AppendLine("5. ✗ NO decimal point found in timestamp");
            }
            
            // Test what would be sent to database
            string sqlValue = "'" + raboBookingDateTime.Replace("'", "''") + "'";
            results.AppendLine($"6. SQL string that would be sent: {sqlValue}");
            
            // Test DateTime parsing (what .NET sees)
            try
            {
                var dt = System.DateTime.Parse(raboBookingDateTime);
                results.AppendLine($"7. .NET DateTime.Parse result: {dt:yyyy-MM-ddTHH:mm:ss.ffffff}Z");
                results.AppendLine($"8. Millisecond: {dt.Millisecond}");
                var ticks = dt.Ticks % 10000000; // Get sub-second ticks
                var microseconds = ticks / 10; // Convert to microseconds
                results.AppendLine($"9. Calculated microseconds: {microseconds}");
            }
            catch (System.Exception ex)
            {
                results.AppendLine($"7. DateTime.Parse FAILED: {ex.Message}");
            }
        }
        
        results.AppendLine();
    }
    
    debugResults = results.ToString();
}
catch (System.Exception ex)
{
    debugResults = $"EXCEPTION: {ex.Message}\n{ex.StackTrace}";
}