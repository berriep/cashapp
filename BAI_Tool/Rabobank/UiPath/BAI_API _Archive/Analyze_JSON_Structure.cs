// DEBUG Script - Analyze the ACTUAL JSON structure we're receiving
// Input: jobjTransactions (JObject, In)
// Output: structureAnalysis (String, Out)

try 
{
    var results = new System.Text.StringBuilder();
    results.AppendLine("JSON STRUCTURE ANALYSIS:");
    results.AppendLine("=======================");
    
    if (jobjTransactions == null)
    {
        structureAnalysis = "ERROR: jobjTransactions is null";
        return;
    }

    // Analyze the entire JSON structure
    results.AppendLine("ROOT LEVEL PROPERTIES:");
    foreach (var prop in jobjTransactions.Properties())
    {
        results.AppendLine($"- {prop.Name}: {prop.Value.Type}");
    }
    results.AppendLine();

    // Navigate to transactions
    var transactionsObj = jobjTransactions["transactions"] as Newtonsoft.Json.Linq.JObject;
    if (transactionsObj != null)
    {
        results.AppendLine("TRANSACTIONS OBJECT PROPERTIES:");
        foreach (var prop in transactionsObj.Properties())
        {
            results.AppendLine($"- {prop.Name}: {prop.Value.Type}");
        }
        results.AppendLine();

        // Get booked array
        var bookedArray = transactionsObj["booked"] as Newtonsoft.Json.Linq.JArray;
        if (bookedArray != null && bookedArray.Count > 0)
        {
            results.AppendLine($"BOOKED ARRAY: {bookedArray.Count} transactions");
            results.AppendLine();
            
            // Analyze first transaction in detail
            var firstTx = bookedArray[0] as Newtonsoft.Json.Linq.JObject;
            if (firstTx != null)
            {
                results.AppendLine("FIRST TRANSACTION PROPERTIES:");
                foreach (var prop in firstTx.Properties())
                {
                    string value = prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.String ? 
                                   $"'{prop.Value}'" : prop.Value.ToString();
                    results.AppendLine($"- {prop.Name}: {prop.Value.Type} = {value}");
                }
                results.AppendLine();
                
                // Specifically look for timestamp fields
                results.AppendLine("TIMESTAMP FIELDS FOUND:");
                foreach (var prop in firstTx.Properties())
                {
                    if (prop.Name.ToLower().Contains("date") || 
                        prop.Name.ToLower().Contains("time") ||
                        prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.String)
                    {
                        string stringValue = prop.Value.ToString();
                        if (stringValue.Contains("T") || stringValue.Contains("/") || stringValue.Contains("-"))
                        {
                            results.AppendLine($"- {prop.Name}: '{stringValue}'");
                            
                            // Check if it has microseconds
                            if (stringValue.Contains("."))
                            {
                                string[] parts = stringValue.Split('.');
                                if (parts.Length >= 2)
                                {
                                    string fractional = parts[1].Replace("Z", "");
                                    results.AppendLine($"  → Has fractional seconds: '{fractional}' (length: {fractional.Length})");
                                }
                            }
                            else
                            {
                                results.AppendLine($"  → NO fractional seconds");
                            }
                        }
                    }
                }
                
                // Check if raboBookingDateTime exists
                var raboDateTime = firstTx["raboBookingDateTime"];
                if (raboDateTime != null)
                {
                    results.AppendLine();
                    results.AppendLine($"✓ raboBookingDateTime FOUND: '{raboDateTime}'");
                }
                else
                {
                    results.AppendLine();
                    results.AppendLine("✗ raboBookingDateTime NOT FOUND!");
                    results.AppendLine("This explains why microseconds are missing!");
                }
            }
        }
        else
        {
            results.AppendLine("No booked transactions found in array");
        }
    }
    else
    {
        results.AppendLine("No 'transactions' object found");
    }
    
    structureAnalysis = results.ToString();
}
catch (System.Exception ex)
{
    structureAnalysis = $"EXCEPTION: {ex.Message}\n{ex.StackTrace}";
}