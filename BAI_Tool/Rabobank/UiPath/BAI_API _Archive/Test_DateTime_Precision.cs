// Test Script voor Datetime Precision - UiPath Invoke Code
// Input: geen
// Output: testResults (String, Out)

try 
{
    var results = new System.Text.StringBuilder();
    
    // Test verschillende raboBookingDateTime waarden uit de API
    string[] testValues = {
        "2025-10-06T08:12:49.282952Z",  // 6 decimalen
        "2025-10-06T13:33:05.89122Z",   // 5 decimalen  
        "2025-10-06T09:06:33.97313Z",   // 5 decimalen
        "2025-10-06T14:55:32.000321Z"   // 6 decimalen met nullen
    };
    
    results.AppendLine("PRECISION TEST RESULTS:");
    results.AppendLine("======================");
    
    foreach (string apiValue in testValues)
    {
        results.AppendLine($"\nOriginal API value: {apiValue}");
        
        // Test 1: Direct string opslag (zoals GetStr() doet)
        results.AppendLine($"GetStr() result: {apiValue}");
        
        // Test 2: Parse naar DateTime dan terug naar string
        try 
        {
            DateTime dt = DateTime.Parse(apiValue);
            results.AppendLine($"DateTime.Parse(): {dt:yyyy-MM-ddTHH:mm:ss.ffffffZ}");
        }
        catch (System.Exception ex)
        {
            results.AppendLine($"DateTime.Parse() failed: {ex.Message}");
        }
        
        // Test 3: Parse naar DateTimeOffset dan terug naar string
        try 
        {
            DateTimeOffset dto = DateTimeOffset.Parse(apiValue);
            results.AppendLine($"DateTimeOffset.Parse(): {dto:yyyy-MM-ddTHH:mm:ss.ffffffZ}");
        }
        catch (System.Exception ex)
        {
            results.AppendLine($"DateTimeOffset.Parse() failed: {ex.Message}");
        }
        
        // Test 4: Check microsecond extractie
        if (apiValue.Contains("."))
        {
            string fractionalPart = apiValue.Split('.')[1].Replace("Z", "");
            results.AppendLine($"Fractional seconds: '{fractionalPart}' (length: {fractionalPart.Length})");
        }
        
        results.AppendLine("---");
    }
    
    // Test SQL string formatting
    results.AppendLine("\nSQL INSERT STRING SIMULATION:");
    results.AppendLine("=============================");
    
    string testApiValue = "2025-10-06T08:12:49.282952Z";
    string sqlValue = testApiValue; // Zoals GetStr() het doorgeeft
    string sqlInsert = $"INSERT INTO rpa_data.transactions (rabo_booking_datetime) VALUES ('{sqlValue}');";
    results.AppendLine($"Generated SQL: {sqlInsert}");
    
    testResults = results.ToString();
}
catch (System.Exception ex)
{
    testResults = $"ERROR: {ex.Message}\n{ex.StackTrace}";
}