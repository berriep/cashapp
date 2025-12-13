// UiPath Invoke Code Script - Test Daily Closing with Mock Data
// Input Arguments: strTestScenario (String, In), strTestDataPath (String, In)
// Output Arguments: testResults (String, Out), allTestsPassed (Boolean, Out)
// References: System.IO, Newtonsoft.Json

try
{
    string testDataPath = strTestDataPath ?? @"C:\Users\bpeijmen\Downloads\Zero\Zero\UiPath\TestData\";
    string scenario = strTestScenario ?? "perfect";
    
    System.Console.WriteLine($"[TestDailyClosing] Starting test scenario: {scenario}");
    
    // Load test data files
    string balanceFile = System.IO.Path.Combine(testDataPath, $"balance_{scenario}_day.json");
    string transactionFile = System.IO.Path.Combine(testDataPath, $"transactions_{scenario}_day.json");
    
    if (!System.IO.File.Exists(balanceFile))
        throw new Exception($"Balance test file not found: {balanceFile}");
    if (!System.IO.File.Exists(transactionFile))
        throw new Exception($"Transaction test file not found: {transactionFile}");
    
    System.Console.WriteLine($"[TestDailyClosing] Loading files:");
    System.Console.WriteLine($"  Balance: {balanceFile}");
    System.Console.WriteLine($"  Transactions: {transactionFile}");
    
    // Load JSON data
    string balanceJson = System.IO.File.ReadAllText(balanceFile);
    string transactionJson = System.IO.File.ReadAllText(transactionFile);
    
    var jobjBalance = Newtonsoft.Json.Linq.JObject.Parse(balanceJson);
    var jobjTransaction = Newtonsoft.Json.Linq.JObject.Parse(transactionJson);
    
    // Extract balance amount
    decimal balanceAmount = 0m;
    if (jobjBalance["balances"] != null)
    {
        var balances = jobjBalance["balances"] as Newtonsoft.Json.Linq.JArray;
        foreach (var balance in balances)
        {
            if (balance["balanceType"]?.ToString() == "closingBooked")
            {
                balanceAmount = decimal.Parse(balance["balanceAmount"]["amount"].ToString());
                break;
            }
        }
    }
    
    // Extract test metadata
    string testDate = "";
    string testIban = jobjBalance["account"]["iban"].ToString();
    
    // Determine test date based on scenario
    switch (scenario.ToLower())
    {
        case "perfect":
            testDate = "2025-08-31";
            break;
        case "issues":
            testDate = "2025-08-30";
            break;
        case "highactivity":
            testDate = "2025-08-29";
            break;
        default:
            testDate = "2025-08-31";
            break;
    }
    
    // Count transactions for summary
    int transactionCount = 0;
    decimal totalIncoming = 0m;
    decimal totalOutgoing = 0m;
    
    if (jobjTransaction["transactions"]?["booked"] != null)
    {
        var transactions = jobjTransaction["transactions"]["booked"] as Newtonsoft.Json.Linq.JArray;
        transactionCount = transactions.Count;
        
        foreach (var transaction in transactions)
        {
            if (transaction["transactionAmount"]?["amount"] != null)
            {
                decimal amount = decimal.Parse(transaction["transactionAmount"]["amount"].ToString());
                if (amount > 0)
                    totalIncoming += amount;
                else
                    totalOutgoing += Math.Abs(amount);
            }
        }
    }
    
    decimal netMovement = totalIncoming - totalOutgoing;
    
    // Build test results
    var results = new System.Text.StringBuilder();
    results.AppendLine($"=== TEST RESULTS - {scenario.ToUpper()} SCENARIO ===");
    results.AppendLine($"Test Date: {testDate}");
    results.AppendLine($"IBAN: {testIban}");
    results.AppendLine();
    results.AppendLine("BALANCE SUMMARY:");
    results.AppendLine($"  Closing Balance: €{balanceAmount:F2}");
    results.AppendLine();
    results.AppendLine("TRANSACTION SUMMARY:");
    results.AppendLine($"  Total Transactions: {transactionCount}");
    results.AppendLine($"  Total Incoming: €{totalIncoming:F2}");
    results.AppendLine($"  Total Outgoing: €{totalOutgoing:F2}");
    results.AppendLine($"  Net Movement: €{netMovement:F2}");
    results.AppendLine();
    results.AppendLine("EXPECTED OUTCOMES:");
    
    // Add scenario-specific expectations
    switch (scenario.ToLower())
    {
        case "perfect":
            results.AppendLine("  ✅ Should be RECONCILED");
            results.AppendLine("  🟡 May show HIGH_SAVINGS_RATIO warning");
            results.AppendLine("  💰 Positive net movement from salary");
            break;
        case "issues":
            results.AppendLine("  ❌ Should show ZERO_CLOSING_BALANCE");
            results.AppendLine("  ❌ Should show PENDING_LARGE_INCOMING");
            results.AppendLine("  🟡 Should show HIGH_SAVINGS_RATIO warning");
            results.AppendLine("  📉 Negative net movement");
            break;
        case "highactivity":
            results.AppendLine("  ✅ Should be RECONCILED");
            results.AppendLine("  📊 Many transactions (12+)");
            results.AppendLine("  💰 Large positive net movement");
            results.AppendLine("  🔄 High transaction volume");
            break;
    }
    
    results.AppendLine();
    results.AppendLine("FILES LOADED:");
    results.AppendLine($"  ✅ {System.IO.Path.GetFileName(balanceFile)}");
    results.AppendLine($"  ✅ {System.IO.Path.GetFileName(transactionFile)}");
    results.AppendLine();
    results.AppendLine("STATUS: Ready for Daily Closing script testing");
    
    testResults = results.ToString();
    allTestsPassed = true;
    
    System.Console.WriteLine($"[TestDailyClosing] SUCCESS: Test data loaded for scenario '{scenario}'");
    System.Console.WriteLine($"[TestDailyClosing] Balance: €{balanceAmount:F2}, Transactions: {transactionCount}, Net: €{netMovement:F2}");
}
catch (Exception ex)
{
    testResults = $"Test failed: {ex.Message}";
    allTestsPassed = false;
    System.Console.WriteLine($"[TestDailyClosing] ERROR: {ex.Message}");
}

// Output variables:
// testResults: String with detailed test scenario summary
// allTestsPassed: Boolean indicating if test data loaded successfully
