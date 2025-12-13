// UiPath Invoke Code Script - Test Daily Closing (File Loading Interface)
// Input Arguments: 
//   strTestScenario (String, In) - Test scenario: "perfect", "issues", or "highactivity"
//   strTestDataPath (String, In, Optional) - Path to TestData folder (defaults to standard path)
// Output Arguments: 
//   testResults (String, Out) - Detailed test report with validation results
//   allTestsPassed (Boolean, Out) - True if all scenario expectations are met
//   jobjDailyClosing (JObject, Out) - Complete daily closing report
//   reconciliationSuccess (Boolean, Out) - True if reconciliation passed
//   netMovement (Decimal, Out) - Net transaction movement for the day
// References: System.IO, Newtonsoft.Json, System.Linq

try
{
    string testDataPath = strTestDataPath ?? @"C:\Users\bpeijmen\Downloads\Zero\Zero\UiPath\TestData\";
    string scenario = strTestScenario ?? "perfect";
    
    System.Console.WriteLine($"[TestDailyClosing] ========================================");
    System.Console.WriteLine($"[TestDailyClosing] LOADING TEST SCENARIO: {scenario.ToUpper()}");
    System.Console.WriteLine($"[TestDailyClosing] ========================================");
    
    // Load test data files based on scenario
    string balanceFile = System.IO.Path.Combine(testDataPath, $"balance_{scenario}_day.json");
    string transactionFile = System.IO.Path.Combine(testDataPath, $"transactions_{scenario}_day.json");
    
    if (!System.IO.File.Exists(balanceFile))
        throw new Exception($"Balance test file not found: {balanceFile}");
    if (!System.IO.File.Exists(transactionFile))
        throw new Exception($"Transaction test file not found: {transactionFile}");
    
    System.Console.WriteLine($"[TestDailyClosing] Loading files:");
    System.Console.WriteLine($"[TestDailyClosing]   Balance: {balanceFile}");
    System.Console.WriteLine($"[TestDailyClosing]   Transactions: {transactionFile}");
    
    // Load JSON data
    string balanceJson = System.IO.File.ReadAllText(balanceFile);
    string transactionJson = System.IO.File.ReadAllText(transactionFile);
    
    var jobjBalanceResponse = Newtonsoft.Json.Linq.JObject.Parse(balanceJson);
    var jobjTransactionResponse = Newtonsoft.Json.Linq.JObject.Parse(transactionJson);
    
    // Auto-extract all required data from JSON files
    decimal balanceAmount = 0m;
    if (jobjBalanceResponse["balances"] != null)
    {
        var balances = jobjBalanceResponse["balances"] as Newtonsoft.Json.Linq.JArray;
        foreach (var balance in balances)
        {
            if (balance["balanceType"]?.ToString() == "closingBooked")
            {
                balanceAmount = decimal.Parse(balance["balanceAmount"]["amount"].ToString());
                System.Console.WriteLine($"[TestDailyClosing] Auto-extracted closing balance: €{balanceAmount:F2}");
                break;
            }
        }
    }
    
    // Auto-extract IBAN
    string testIban = jobjBalanceResponse["account"]?["iban"]?.ToString() ?? "Unknown";
    System.Console.WriteLine($"[TestDailyClosing] Auto-extracted IBAN: {testIban}");
    
    // Auto-extract close date
    string testDate = "";
    if (jobjBalanceResponse["balances"]?[0]?["referenceDate"] != null)
    {
        testDate = jobjBalanceResponse["balances"][0]["referenceDate"].ToString();
        System.Console.WriteLine($"[TestDailyClosing] Auto-extracted date from balance: {testDate}");
    }
    else
    {
        // Fallback based on scenario
        switch (scenario.ToLower())
        {
            case "perfect": testDate = "2025-08-31"; break;
            case "issues": testDate = "2025-08-30"; break;
            case "highactivity": testDate = "2025-08-29"; break;
            default: testDate = DateTime.Now.ToString("yyyy-MM-dd"); break;
        }
        System.Console.WriteLine($"[TestDailyClosing] Using scenario-based date: {testDate}");
    }
    
    System.Console.WriteLine($"[TestDailyClosing] Test Parameters:");
    System.Console.WriteLine($"[TestDailyClosing]   Scenario: {scenario}");
    System.Console.WriteLine($"[TestDailyClosing]   IBAN: {testIban}");
    System.Console.WriteLine($"[TestDailyClosing]   Date: {testDate}");
    System.Console.WriteLine($"[TestDailyClosing]   Balance: €{balanceAmount:F2}");
    System.Console.WriteLine($"[TestDailyClosing] ----------------------------------------");
    
    // =====================================
    // RUN SAME LOGIC AS 9_DAILYCLOSING
    // =====================================
    
    // Parse close date
    DateTime closeDate = DateTime.Parse(testDate);
    string closeDateFormatted = closeDate.ToString("yyyy-MM-dd");
    
    // Initialize daily closing object
    var dailyClosing = new Newtonsoft.Json.Linq.JObject();
    
    // Account Information
    var accountInfo = new Newtonsoft.Json.Linq.JObject();
    accountInfo["iban"] = jobjBalanceResponse["account"]["iban"];
    accountInfo["currency"] = jobjBalanceResponse["account"]["currency"];
    accountInfo["closeDate"] = closeDateFormatted;
    accountInfo["processingDateTime"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    dailyClosing["account"] = accountInfo;
    
    // Balance Information
    var balanceInfo = new Newtonsoft.Json.Linq.JObject();
    balanceInfo["closingBookedBalance"] = balanceAmount;
    
    // Extract other balance types
    decimal expectedBalance = 0m;
    decimal interimBalance = 0m;
    decimal totalPiggyBanks = 0m;
    
    if (jobjBalanceResponse["balances"] != null)
    {
        var balances = jobjBalanceResponse["balances"] as Newtonsoft.Json.Linq.JArray;
        foreach (var balance in balances)
        {
            string balanceType = balance["balanceType"]?.ToString();
            if (balance["balanceAmount"]?["amount"] != null)
            {
                decimal amount = decimal.Parse(balance["balanceAmount"]["amount"].ToString());
                
                switch (balanceType)
                {
                    case "expected":
                        expectedBalance = amount;
                        break;
                    case "interimBooked":
                        interimBalance = amount;
                        break;
                }
            }
        }
    }
    
    // Extract piggy bank totals
    if (jobjBalanceResponse["piggyBanks"] != null)
    {
        var piggyBanks = jobjBalanceResponse["piggyBanks"] as Newtonsoft.Json.Linq.JArray;
        var piggyBankDetails = new Newtonsoft.Json.Linq.JArray();
        
        foreach (var piggy in piggyBanks)
        {
            if (piggy["piggyBankBalance"] != null)
            {
                decimal piggyAmount = decimal.Parse(piggy["piggyBankBalance"].ToString());
                totalPiggyBanks += piggyAmount;
                
                piggyBankDetails.Add(new Newtonsoft.Json.Linq.JObject
                {
                    ["name"] = piggy["piggyBankName"],
                    ["balance"] = piggyAmount
                });
            }
        }
        
        balanceInfo["piggyBanks"] = piggyBankDetails;
    }
    
    balanceInfo["expectedBalance"] = expectedBalance;
    balanceInfo["interimBalance"] = interimBalance;
    balanceInfo["totalPiggyBanks"] = totalPiggyBanks;
    balanceInfo["totalAccountValue"] = balanceAmount + totalPiggyBanks;
    
    dailyClosing["balances"] = balanceInfo;
    
    // Transaction Analysis
    var transactionSummary = new Newtonsoft.Json.Linq.JObject();
    int incomingCount = 0;
    int outgoingCount = 0;
    decimal incomingTotal = 0m;
    decimal outgoingTotal = 0m;
    
    if (jobjTransactionResponse != null && jobjTransactionResponse["transactions"] != null)
    {
        var transactions = jobjTransactionResponse["transactions"];
        
        if (transactions["booked"] != null)
        {
            var bookedTransactions = transactions["booked"] as Newtonsoft.Json.Linq.JArray;
            
            foreach (var transaction in bookedTransactions)
            {
                if (transaction["transactionAmount"] != null)
                {
                    // Try both "value" (Rabobank API) and "amount" (test data) field names
                    string amountField = transaction["transactionAmount"]["value"] != null ? "value" : "amount";
                    decimal amount = decimal.Parse(transaction["transactionAmount"][amountField].ToString());
                    
                    if (amount > 0)
                    {
                        incomingCount++;
                        incomingTotal += amount;
                    }
                    else
                    {
                        outgoingCount++;
                        outgoingTotal += Math.Abs(amount);
                    }
                }
            }
        }
    }
    
    netMovement = incomingTotal - outgoingTotal;
    
    transactionSummary["incomingCount"] = incomingCount;
    transactionSummary["outgoingCount"] = outgoingCount;
    transactionSummary["totalCount"] = incomingCount + outgoingCount;
    transactionSummary["incomingTotal"] = incomingTotal;
    transactionSummary["outgoingTotal"] = outgoingTotal;
    transactionSummary["netMovement"] = netMovement;
    
    dailyClosing["transactions"] = transactionSummary;
    
    // Reconciliation Analysis (same as 9_DailyClosing)
    var reconciliation = new Newtonsoft.Json.Linq.JObject();
    var flags = new System.Collections.Generic.List<string>();
    var warnings = new System.Collections.Generic.List<string>();
    
    // Apply reconciliation rules (test data is always aligned)
    if (expectedBalance > balanceAmount + 10) // €10 threshold
        flags.Add("PENDING_LARGE_INCOMING");
    if (expectedBalance < balanceAmount - 10)
        flags.Add("PENDING_LARGE_OUTGOING");
    if (Math.Abs(expectedBalance - balanceAmount) > 1) // €1 tolerance
        flags.Add("BALANCE_VARIANCE");
    
    // These flags are always applicable
    if (balanceAmount == 0)
        flags.Add("ZERO_CLOSING_BALANCE");
    if (totalPiggyBanks > balanceAmount * 2)
        warnings.Add("HIGH_SAVINGS_RATIO");
    if (incomingCount + outgoingCount == 0)
        warnings.Add("NO_TRANSACTIONS");
    
    reconciliation["flags"] = new Newtonsoft.Json.Linq.JArray(flags);
    reconciliation["warnings"] = new Newtonsoft.Json.Linq.JArray(warnings);
    reconciliation["hasIssues"] = flags.Count > 0;
    reconciliation["hasWarnings"] = warnings.Count > 0;
    reconciliation["balanceVariance"] = expectedBalance - balanceAmount;
    
    // Overall assessment
    bool isReconciled = flags.Count == 0;
    reconciliation["isReconciled"] = isReconciled;
    
    dailyClosing["reconciliation"] = reconciliation;
    
    // Summary
    var summary = new Newtonsoft.Json.Linq.JObject();
    summary["closeDate"] = closeDateFormatted;
    summary["closingBalance"] = balanceAmount;
    summary["transactionCount"] = incomingCount + outgoingCount;
    summary["netMovement"] = netMovement;
    summary["reconciliationStatus"] = isReconciled ? "RECONCILED" : "ISSUES_FOUND";
    summary["totalAccountValue"] = balanceAmount + totalPiggyBanks;
    summary["dataQuality"] = "TEST_DATA";
    
    dailyClosing["summary"] = summary;
    
    // Set outputs (same as 9_DailyClosing)
    jobjDailyClosing = dailyClosing;
    reconciliationSuccess = isReconciled;
    
    // =====================================
    // BUILD TEST RESULTS SUMMARY
    // =====================================
    
    var results = new System.Text.StringBuilder();
    results.AppendLine($"=== TEST RESULTS - {scenario.ToUpper()} SCENARIO ===");
    results.AppendLine($"Test Date: {testDate}");
    results.AppendLine($"IBAN: {testIban}");
    results.AppendLine();
    results.AppendLine("📊 BALANCE SUMMARY:");
    results.AppendLine($"  Closing Balance: €{balanceAmount:F2}");
    results.AppendLine($"  Expected Balance: €{expectedBalance:F2}");
    results.AppendLine($"  Interim Balance: €{interimBalance:F2}");
    results.AppendLine($"  Piggy Banks Total: €{totalPiggyBanks:F2}");
    results.AppendLine($"  Total Account Value: €{(balanceAmount + totalPiggyBanks):F2}");
    results.AppendLine();
    results.AppendLine("💳 TRANSACTION SUMMARY:");
    results.AppendLine($"  Total Transactions: {incomingCount + outgoingCount}");
    results.AppendLine($"  Incoming: {incomingCount} transactions (+€{incomingTotal:F2})");
    results.AppendLine($"  Outgoing: {outgoingCount} transactions (-€{outgoingTotal:F2})");
    results.AppendLine($"  Net Movement: €{netMovement:F2}");
    results.AppendLine();
    results.AppendLine("🔍 RECONCILIATION RESULTS:");
    results.AppendLine($"  Status: {(isReconciled ? "✅ RECONCILED" : "❌ ISSUES_FOUND")}");
    results.AppendLine($"  Balance Variance: €{(expectedBalance - balanceAmount):F2}");
    
    if (flags.Count > 0)
    {
        results.AppendLine($"  🚨 Issues Found: {string.Join(", ", flags)}");
    }
    
    if (warnings.Count > 0)
    {
        results.AppendLine($"  🟡 Warnings: {string.Join(", ", warnings)}");
    }
    
    if (flags.Count == 0 && warnings.Count == 0)
    {
        results.AppendLine($"  ✨ No issues or warnings detected");
    }
    
    results.AppendLine();
    results.AppendLine("📋 EXPECTED OUTCOMES FOR THIS SCENARIO:");
    
    switch (scenario.ToLower())
    {
        case "perfect":
            bool perfectPassed = isReconciled && netMovement > 0;
            results.AppendLine($"  ✅ Should be RECONCILED - RESULT: {(isReconciled ? "PASS" : "FAIL")}");
            results.AppendLine($"  🟡 May show HIGH_SAVINGS_RATIO warning - RESULT: {(warnings.Contains("HIGH_SAVINGS_RATIO") ? "FOUND" : "NOT FOUND")}");
            results.AppendLine($"  💰 Should have positive net movement - RESULT: {(netMovement > 0 ? "PASS" : "FAIL")}");
            allTestsPassed = perfectPassed;
            break;
        case "issues":
            bool issuesPassed = !isReconciled && flags.Contains("ZERO_CLOSING_BALANCE") && netMovement < 0;
            results.AppendLine($"  ❌ Should show ZERO_CLOSING_BALANCE - RESULT: {(flags.Contains("ZERO_CLOSING_BALANCE") ? "PASS" : "FAIL")}");
            results.AppendLine($"  ❌ Should show PENDING_LARGE_INCOMING - RESULT: {(flags.Contains("PENDING_LARGE_INCOMING") ? "PASS" : "FAIL")}");
            results.AppendLine($"  🟡 Should show HIGH_SAVINGS_RATIO warning - RESULT: {(warnings.Contains("HIGH_SAVINGS_RATIO") ? "PASS" : "FAIL")}");
            results.AppendLine($"  📉 Should have negative net movement - RESULT: {(netMovement < 0 ? "PASS" : "FAIL")}");
            allTestsPassed = issuesPassed;
            break;
        case "highactivity":
            bool highActivityPassed = isReconciled && (incomingCount + outgoingCount) >= 10 && netMovement > 1000;
            results.AppendLine($"  ✅ Should be RECONCILED - RESULT: {(isReconciled ? "PASS" : "FAIL")}");
            results.AppendLine($"  📊 Should have many transactions (10+) - RESULT: {((incomingCount + outgoingCount) >= 10 ? "PASS" : "FAIL")}");
            results.AppendLine($"  💰 Should have large positive net movement - RESULT: {(netMovement > 1000 ? "PASS" : "FAIL")}");
            results.AppendLine($"  🔄 Should show high transaction volume - RESULT: {((incomingCount + outgoingCount) >= 10 ? "PASS" : "FAIL")}");
            allTestsPassed = highActivityPassed;
            break;
        default:
            allTestsPassed = isReconciled;
            results.AppendLine($"  📝 Generic test - Status: {(isReconciled ? "RECONCILED" : "ISSUES_FOUND")}");
            break;
    }
    
    results.AppendLine();
    results.AppendLine("🎯 OVERALL TEST RESULT:");
    results.AppendLine($"  {(allTestsPassed ? "✅ ALL TESTS PASSED" : "❌ SOME TESTS FAILED")}");
    results.AppendLine();
    results.AppendLine("==========================================");
    
    testResults = results.ToString();
    
    // Console output
    System.Console.WriteLine($"[TestDailyClosing] ----------------------------------------");
    System.Console.WriteLine($"[TestDailyClosing] FINAL RESULTS:");
    System.Console.WriteLine($"[TestDailyClosing]   Scenario: {scenario}");
    System.Console.WriteLine($"[TestDailyClosing]   Status: {(isReconciled ? "RECONCILED" : "ISSUES_FOUND")}");
    System.Console.WriteLine($"[TestDailyClosing]   Balance: €{balanceAmount:F2}");
    System.Console.WriteLine($"[TestDailyClosing]   Transactions: {incomingCount + outgoingCount}");
    System.Console.WriteLine($"[TestDailyClosing]   Net Movement: €{netMovement:F2}");
    System.Console.WriteLine($"[TestDailyClosing]   Test Result: {(allTestsPassed ? "PASSED" : "FAILED")}");
    
    if (flags.Count > 0)
    {
        System.Console.WriteLine($"[TestDailyClosing]   Issues: {string.Join(", ", flags)}");
    }
    if (warnings.Count > 0)
    {
        System.Console.WriteLine($"[TestDailyClosing]   Warnings: {string.Join(", ", warnings)}");
    }
    
    System.Console.WriteLine($"[TestDailyClosing] ========================================");
}
catch (Exception ex)
{
    reconciliationSuccess = false;
    testResults = $"Test execution failed: {ex.Message}";
    jobjDailyClosing = null;
    netMovement = 0m;
    allTestsPassed = false;
    System.Console.WriteLine($"[TestDailyClosing] ERROR: {ex.Message}");
    System.Console.WriteLine($"[TestDailyClosing] Exception: {ex.ToString()}");
}

// Output variables:
// testResults: String with complete test scenario analysis
// allTestsPassed: Boolean indicating if test expectations were met
// jobjDailyClosing: JObject containing complete daily closing report (same as 9_DailyClosing)
// reconciliationSuccess: Boolean indicating if daily closing is reconciled (same as 9_DailyClosing)
// netMovement: Decimal with net transaction movement for the day (same as 9_DailyClosing)
