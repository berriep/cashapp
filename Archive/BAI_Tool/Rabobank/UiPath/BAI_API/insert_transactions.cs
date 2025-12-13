// UiPath Invoke Code Script - Insert Rabobank Transaction Payloads
// Input Arguments: rawTransactionJson (String, In), audit_id (String, In), attempt_nr (String, In)
// Output Arguments: insertSuccess (Boolean, Out), insertStatements (String, Out), recordsCount (Int32, Out)
// References: Newtonsoft.Json, Newtonsoft.Json.Linq
// NOTE: Use rawTransactionJson instead of jobjTransactions to preserve microsecond precision

try
{
    // Initialize output variables
    insertSuccess = false;
    insertStatements = "";
    recordsCount = 0;

    // Input validation
    if (string.IsNullOrEmpty(rawTransactionJson))
    {
        throw new System.ArgumentNullException("rawTransactionJson", "Transaction JSON string cannot be null or empty");
    }

    if (string.IsNullOrEmpty(audit_id))
    {
        throw new System.ArgumentNullException("audit_id", "Audit ID cannot be null or empty");
    }

    // CRITICAL FIX: Parse raw JSON string with settings to preserve timestamp precision
    // This bypasses UiPath's automatic DateTime conversion that loses microseconds
    Newtonsoft.Json.Linq.JObject jobjTransactions;
    try
    {
        // Use JsonSerializerSettings to prevent automatic DateTime parsing
        var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None  // Keep dates as strings
        };
        
        using (var stringReader = new System.IO.StringReader(rawTransactionJson))
        using (var jsonReader = new Newtonsoft.Json.JsonTextReader(stringReader)
        {
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None  // Critical: Don't auto-parse dates
        })
        {
            var serializer = Newtonsoft.Json.JsonSerializer.Create(jsonSettings);
            jobjTransactions = serializer.Deserialize<Newtonsoft.Json.Linq.JObject>(jsonReader);
        }
        
        System.Console.WriteLine($"[InsertTransactions] ✅ Successfully parsed raw JSON string with preserved timestamps");
    }
    catch (System.Exception ex)
    {
        throw new System.ArgumentException($"Invalid JSON string provided: {ex.Message}", "rawTransactionJson");
    }

    // CRITICAL DEBUG: Check what we receive from raw JSON parsing
    System.Console.WriteLine($"[InsertTransactions] === RECEIVED RAW JSON DEBUG ===");
    System.Console.WriteLine($"[InsertTransactions] rawTransactionJson length: {rawTransactionJson.Length}");
    System.Console.WriteLine($"[InsertTransactions] jobjTransactions type: {jobjTransactions.GetType().Name}");
    
    // Check first transaction's raboBookingDateTime from raw JSON
    if (jobjTransactions["transactions"]?["booked"] is Newtonsoft.Json.Linq.JArray transactions && transactions.Count > 0)
    {
        var firstTx = transactions[0];
        var raboBookingDateTime = firstTx["raboBookingDateTime"];
        System.Console.WriteLine($"[InsertTransactions] First transaction raboBookingDateTime FROM RAW JSON:");
        System.Console.WriteLine($"  Raw Value: '{raboBookingDateTime}'");
        System.Console.WriteLine($"  Token Type: {raboBookingDateTime?.Type}");
        System.Console.WriteLine($"  ToString(): '{raboBookingDateTime?.ToString()}'");
        
        // Check if microseconds are preserved in raw JSON parsing
        string rawValue = raboBookingDateTime?.ToString();
        if (rawValue != null && rawValue.Contains("."))
        {
            string fractionalPart = rawValue.Substring(rawValue.IndexOf(".") + 1);
            if (fractionalPart.Contains("Z")) fractionalPart = fractionalPart.Replace("Z", "");
            System.Console.WriteLine($"  Fractional seconds: '{fractionalPart}' (Length: {fractionalPart.Length})");
            System.Console.WriteLine($"  Has 6-digit microseconds: {fractionalPart.Length == 6}");
            
            if (fractionalPart.Length == 6)
            {
                System.Console.WriteLine($"  ✅ SUCCESS! Raw JSON parsing preserved microsecond precision!");
            }
            else
            {
                System.Console.WriteLine($"  ❌ PRECISION STILL LOST! Expected 6 digits, got {fractionalPart.Length}");
            }
        }
        else
        {
            System.Console.WriteLine($"  ❌ NO fractional seconds found in raw JSON parsing!");
        }
    }
    System.Console.WriteLine($"[InsertTransactions] === END RAW JSON DEBUG ===");

    // Simple parsing without doing any DB work (Option B)
    string accountIban = null;
    string accountCurrency = "EUR";
    Newtonsoft.Json.Linq.JArray bookedArray = null;

    // Helper for SQL escaping
    System.Func<string, string> Esc = s => s == null ? null : s.Replace("'", "''");
    
    // Helper to safely get string value from JToken
    System.Func<Newtonsoft.Json.Linq.JToken, string> GetStr = t => t != null && t.Type != Newtonsoft.Json.Linq.JTokenType.Null ? t.ToString() : null;
    
    // Helper to safely get decimal value from JToken (handles amount strings like "25.00")
    System.Func<Newtonsoft.Json.Linq.JToken, decimal?> GetDecimal = t => {
        if (t == null || t.Type == Newtonsoft.Json.Linq.JTokenType.Null) return null;
        string s = t.ToString().Replace(",", ".");
        decimal result;
        if (decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result))
            return result;
        return null;
    };

    // Parse root structure: account and transactions
    int propIndex = 0;
    foreach (var prop in jobjTransactions.Properties())
    {
        propIndex++;
        if (propIndex == 1) // "account"
        {
            var acct = prop.Value as Newtonsoft.Json.Linq.JObject;
            if (acct != null)
            {
                accountIban = GetStr(acct["iban"]);
                accountCurrency = GetStr(acct["currency"]) ?? "EUR";
            }
        }
        else if (propIndex == 2) // "transactions"
        {
            var txObj = prop.Value as Newtonsoft.Json.Linq.JObject;
            if (txObj != null)
            {
                bookedArray = txObj["booked"] as Newtonsoft.Json.Linq.JArray;
            }
        }
    }

    if (string.IsNullOrEmpty(accountIban))
    {
        throw new System.ArgumentException("Account IBAN not found in JSON");
    }
    if (bookedArray == null || bookedArray.Count == 0)
    {
        // No transactions - this is valid, just return empty result
        insertSuccess = true;
        insertStatements = "";
        recordsCount = 0;
        return;
    }

    // Track all possible column names from ALL transactions first
    var allColumnNames = new System.Collections.Generic.HashSet<string>();
    var allTransactionData = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();

    // FIRST PASS: Collect all possible columns and parse all transactions
    foreach (var item in bookedArray)
    {
        var tx = item as Newtonsoft.Json.Linq.JObject;
        if (tx == null) continue;

        // MANDATORY FIELDS (NOT NULL in database)
        string bookingDate = GetStr(tx["bookingDate"]);
        string entryReference = GetStr(tx["entryReference"]);
        string bankTransactionCode = GetStr(tx["bankTransactionCode"]);
        string raboBookingDateTime = GetStr(tx["raboBookingDateTime"]);
        string raboDetailedTransactionType = GetStr(tx["raboDetailedTransactionType"]);
        
        // Transaction amount (mandatory)
        decimal? transactionAmount = null;
        string transactionCurrency = accountCurrency;
        var txAmountObj = tx["transactionAmount"] as Newtonsoft.Json.Linq.JObject;
        if (txAmountObj != null)
        {
            transactionAmount = GetDecimal(txAmountObj["value"]);
            transactionCurrency = GetStr(txAmountObj["currency"]) ?? transactionCurrency;
        }

        // Skip if mandatory fields are missing
        if (string.IsNullOrEmpty(bookingDate) || 
            string.IsNullOrEmpty(entryReference) || 
            string.IsNullOrEmpty(bankTransactionCode) ||
            string.IsNullOrEmpty(raboBookingDateTime) ||
            string.IsNullOrEmpty(raboDetailedTransactionType) ||
            transactionAmount == null)
        {
            continue;
        }

        // OPTIONAL BASIC FIELDS
        string valueDate = GetStr(tx["valueDate"]);
        string endToEndId = GetStr(tx["endToEndId"]);
        string batchEntryReference = GetStr(tx["batchEntryReference"]);
        string accountServicerReference = GetStr(tx["accountServicerReference"]);
        string instructionId = GetStr(tx["instructionId"]);
        
        // OPTIONAL DATE FIELDS
        string interbankSettlementDate = GetStr(tx["interbankSettlementDate"]);

        // DEBTOR INFORMATION (incoming payments)
        string debtorIban = null;
        string debtorName = GetStr(tx["debtorName"]);
        string debtorAgentBic = GetStr(tx["debtorAgent"]);
        var debtorAcct = tx["debtorAccount"] as Newtonsoft.Json.Linq.JObject;
        if (debtorAcct != null)
        {
            debtorIban = GetStr(debtorAcct["iban"]);
        }

        // CREDITOR INFORMATION (outgoing payments)
        string creditorIban = null;
        string creditorCurrency = null;
        string creditorName = GetStr(tx["creditorName"]);
        string creditorAgentBic = GetStr(tx["creditorAgent"]);
        string creditorId = GetStr(tx["creditorId"]);
        var creditorAcct = tx["creditorAccount"] as Newtonsoft.Json.Linq.JObject;
        if (creditorAcct != null)
        {
            creditorIban = GetStr(creditorAcct["iban"]);
            creditorCurrency = GetStr(creditorAcct["currency"]);
        }

        // ULTIMATE PARTIES
        string ultimateDebtor = GetStr(tx["ultimateDebtor"]);
        string ultimateCreditor = GetStr(tx["ultimateCreditor"]);
        string initiatingPartyName = GetStr(tx["initiatingPartyName"]);

        // SEPA FIELDS
        string mandateId = GetStr(tx["mandateId"]);

        // PAYMENT INFORMATION
        string remittanceInfoUnstructured = GetStr(tx["remittanceInformationUnstructured"]);
        string remittanceInfoStructured = GetStr(tx["remittanceInformationStructured"]);
        string purposeCode = GetStr(tx["purposeCode"]);
        string reasonCode = GetStr(tx["reasonCode"]);

        // BATCH INFORMATION
        string paymentInfoId = GetStr(tx["paymentInformationIdentification"]);
        string numberOfTransactions = GetStr(tx["numberOfTransactions"]);

        // CURRENCY EXCHANGE (multi-currency)
        decimal? currencyExchangeRate = null;
        string currencyExchangeSourceCurrency = null;
        string currencyExchangeTargetCurrency = null;
        var currencyExchangeArray = tx["currencyExchange"] as Newtonsoft.Json.Linq.JArray;
        if (currencyExchangeArray != null && currencyExchangeArray.Count > 0)
        {
            var fxObj = currencyExchangeArray[0] as Newtonsoft.Json.Linq.JObject;
            if (fxObj != null)
            {
                currencyExchangeRate = GetDecimal(fxObj["exchangeRate"]);
                currencyExchangeSourceCurrency = GetStr(fxObj["sourceCurrency"]);
                currencyExchangeTargetCurrency = GetStr(fxObj["targetCurrency"]);
            }
        }

        // INSTRUCTED AMOUNT (original amount before conversion)
        decimal? instructedAmount = null;
        string instructedAmountCurrency = null;
        var instructedAmountObj = tx["instructedAmount"] as Newtonsoft.Json.Linq.JObject;
        if (instructedAmountObj != null)
        {
            instructedAmount = GetDecimal(instructedAmountObj["amount"]);
            instructedAmountCurrency = GetStr(instructedAmountObj["sourceCurrency"]);
        }

        // RABOBANK SPECIFIC
        string raboTransactionTypeName = GetStr(tx["raboTransactionTypeName"]);

        // BALANCE AFTER BOOKING
        decimal? balanceAfterBookingAmount = null;
        string balanceAfterBookingCurrency = null;
        string balanceAfterBookingType = null;
        var balanceAfterObj = tx["balanceAfterBooking"] as Newtonsoft.Json.Linq.JObject;
        if (balanceAfterObj != null)
        {
            balanceAfterBookingType = GetStr(balanceAfterObj["balanceType"]);
            var balAmtObj = balanceAfterObj["balanceAmount"] as Newtonsoft.Json.Linq.JObject;
            if (balAmtObj != null)
            {
                balanceAfterBookingAmount = GetDecimal(balAmtObj["value"]);
                balanceAfterBookingCurrency = GetStr(balAmtObj["currency"]);
            }
        }

        // BUILD DATA DICTIONARY FOR THIS TRANSACTION
        // Store column->value mapping for this row
        var rowData = new System.Collections.Generic.Dictionary<string, string>();

        // Audit fields (always present)
        rowData["audit_id"] = "'" + Esc(audit_id) + "'";
        rowData["attempt_nr"] = attempt_nr != null ? attempt_nr : "1";

        // Account information (mandatory)
        rowData["iban"] = "'" + Esc(accountIban) + "'";
        rowData["currency"] = "'" + Esc(accountCurrency) + "'";

        // Transaction basic data (mandatory)
        // TIMEZONE CONVERSION: Convert raboBookingDateTime (UTC) to local date for booking_date
        // Following Rabobank guidance for timezone consistency in CAMT.053/MT940 exports
        string finalBookingDate = bookingDate; // Default to API value
        if (!string.IsNullOrEmpty(bookingDate) && !string.IsNullOrEmpty(raboBookingDateTime))
        {
            try
            {
                // Convert raboBookingDateTime (UTC) to local date
                DateTime raboBookingDT = DateTime.Parse(raboBookingDateTime);
                DateTime localBookingDT = raboBookingDT.ToLocalTime(); // UTC → Europe/Amsterdam
                string derivedBookingDate = localBookingDT.ToString("yyyy-MM-dd");
                
                if (debugEnabled)
                {
                    System.Console.WriteLine($"[BookingDate] Original API: '{bookingDate}', UTC timestamp: '{raboBookingDateTime}'");
                    System.Console.WriteLine($"[BookingDate] Derived local date: '{derivedBookingDate}' (converted from UTC)");
                    System.Console.WriteLine($"[AUDIT] Data Transformation Applied:");
                    System.Console.WriteLine($"[AUDIT] - Source: Rabobank BAI API");
                    System.Console.WriteLine($"[AUDIT] - Transformation: UTC to Local Date Conversion");
                    System.Console.WriteLine($"[AUDIT] - Justification: Bank guidance for timezone consistency");
                    System.Console.WriteLine($"[AUDIT] - Original bookingDate: '{bookingDate}'");
                    System.Console.WriteLine($"[AUDIT] - Derived bookingDate: '{derivedBookingDate}'");
                    System.Console.WriteLine($"[AUDIT] - raboBookingDateTime: '{raboBookingDateTime}'");
                }
                
                // Use derived date instead of API bookingDate for consistency
                finalBookingDate = derivedBookingDate;
            }
            catch (Exception ex)
            {
                if (debugEnabled)
                {
                    System.Console.WriteLine($"[BookingDate] WARNING: Failed to convert raboBookingDateTime '{raboBookingDateTime}' - using original bookingDate '{bookingDate}'. Error: {ex.Message}");
                }
                // Fallback to original API bookingDate if conversion fails
                finalBookingDate = bookingDate;
            }
        }
        else if (debugEnabled)
        {
            System.Console.WriteLine($"[BookingDate] Using original API bookingDate '{bookingDate}' (raboBookingDateTime not available for conversion)");
        }
        
        rowData["booking_date"] = "'" + Esc(finalBookingDate) + "'";
        rowData["entry_reference"] = "'" + Esc(entryReference) + "'";
        rowData["transaction_amount"] = transactionAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        rowData["transaction_currency"] = "'" + Esc(transactionCurrency) + "'";
        rowData["bank_transaction_code"] = "'" + Esc(bankTransactionCode) + "'";

        // Optional basic fields - valueDate is kept as-is (represents interest date, not timezone dependent)
        if (!string.IsNullOrEmpty(valueDate))
            rowData["value_date"] = "'" + Esc(valueDate) + "'";  // 1-op-1 overnemen (rente datum)
        
        if (!string.IsNullOrEmpty(endToEndId))
            rowData["end_to_end_id"] = "'" + Esc(endToEndId) + "'";
        
        if (!string.IsNullOrEmpty(batchEntryReference))
            rowData["batch_entry_reference"] = "'" + Esc(batchEntryReference) + "'";
        
        if (!string.IsNullOrEmpty(accountServicerReference))
            rowData["acctsvcr_ref"] = "'" + Esc(accountServicerReference) + "'";
        
        if (!string.IsNullOrEmpty(instructionId))
            rowData["instruction_id"] = "'" + Esc(instructionId) + "'";
        
        if (!string.IsNullOrEmpty(interbankSettlementDate))
            rowData["interbank_settlement_date"] = "'" + Esc(interbankSettlementDate) + "'";
        
        // Debtor information
        if (!string.IsNullOrEmpty(debtorIban))
            rowData["debtor_iban"] = "'" + Esc(debtorIban) + "'";
        
        if (!string.IsNullOrEmpty(debtorName))
            rowData["debtor_name"] = "'" + Esc(debtorName) + "'";
        
        if (!string.IsNullOrEmpty(debtorAgentBic))
            rowData["debtor_agent_bic"] = "'" + Esc(debtorAgentBic) + "'";

        // Creditor information
        if (!string.IsNullOrEmpty(creditorIban))
            rowData["creditor_iban"] = "'" + Esc(creditorIban) + "'";
        
        if (!string.IsNullOrEmpty(creditorName))
            rowData["creditor_name"] = "'" + Esc(creditorName) + "'";
        
        if (!string.IsNullOrEmpty(creditorAgentBic))
            rowData["creditor_agent_bic"] = "'" + Esc(creditorAgentBic) + "'";
        
        if (!string.IsNullOrEmpty(creditorCurrency))
            rowData["creditor_currency"] = "'" + Esc(creditorCurrency) + "'";
        
        if (!string.IsNullOrEmpty(creditorId))
            rowData["creditor_id"] = "'" + Esc(creditorId) + "'";

        // Ultimate parties
        if (!string.IsNullOrEmpty(ultimateDebtor))
            rowData["ultimate_debtor"] = "'" + Esc(ultimateDebtor) + "'";
        
        if (!string.IsNullOrEmpty(ultimateCreditor))
            rowData["ultimate_creditor"] = "'" + Esc(ultimateCreditor) + "'";
        
        if (!string.IsNullOrEmpty(initiatingPartyName))
            rowData["initiating_party_name"] = "'" + Esc(initiatingPartyName) + "'";

        // SEPA fields
        if (!string.IsNullOrEmpty(mandateId))
            rowData["mandate_id"] = "'" + Esc(mandateId) + "'";

        // Payment information
        if (!string.IsNullOrEmpty(remittanceInfoUnstructured))
            rowData["remittance_information_unstructured"] = "'" + Esc(remittanceInfoUnstructured) + "'";
        
        if (!string.IsNullOrEmpty(remittanceInfoStructured))
            rowData["remittance_information_structured"] = "'" + Esc(remittanceInfoStructured) + "'";
        
        if (!string.IsNullOrEmpty(purposeCode))
            rowData["purpose_code"] = "'" + Esc(purposeCode) + "'";
        
        if (!string.IsNullOrEmpty(reasonCode))
            rowData["reason_code"] = "'" + Esc(reasonCode) + "'";

        // Batch information
        if (!string.IsNullOrEmpty(paymentInfoId))
            rowData["payment_information_identification"] = "'" + Esc(paymentInfoId) + "'";
        
        if (!string.IsNullOrEmpty(numberOfTransactions))
            rowData["number_of_transactions"] = numberOfTransactions;

        // Currency exchange
        if (currencyExchangeRate.HasValue)
            rowData["currency_exchange_rate"] = currencyExchangeRate.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        
        if (!string.IsNullOrEmpty(currencyExchangeSourceCurrency))
            rowData["currency_exchange_source_currency"] = "'" + Esc(currencyExchangeSourceCurrency) + "'";
        
        if (!string.IsNullOrEmpty(currencyExchangeTargetCurrency))
            rowData["currency_exchange_target_currency"] = "'" + Esc(currencyExchangeTargetCurrency) + "'";

        // Instructed amount
        if (instructedAmount.HasValue)
            rowData["instructed_amount"] = instructedAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        
        if (!string.IsNullOrEmpty(instructedAmountCurrency))
            rowData["instructed_amount_currency"] = "'" + Esc(instructedAmountCurrency) + "'";

        // Rabobank specific (mandatory) - Copy approach from working created_at column
        if (!string.IsNullOrEmpty(raboBookingDateTime))
        {
            // Use same approach as PostgreSQL's NOW() function that works for created_at
            // Simply pass the ISO 8601 string and let PostgreSQL handle it natively
            // Remove the explicit casting that might be causing issues
            
            rowData["rabo_booking_datetime"] = "'" + Esc(raboBookingDateTime) + "'";
        }
        else
            rowData["rabo_booking_datetime"] = "NULL";
        rowData["rabo_detailed_transaction_type"] = "'" + Esc(raboDetailedTransactionType) + "'";
        
        if (!string.IsNullOrEmpty(raboTransactionTypeName))
            rowData["rabo_transaction_type_name"] = "'" + Esc(raboTransactionTypeName) + "'";

        // Balance after booking
        if (balanceAfterBookingAmount.HasValue)
            rowData["balance_after_booking_amount"] = balanceAfterBookingAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        
        if (!string.IsNullOrEmpty(balanceAfterBookingCurrency))
            rowData["balance_after_booking_currency"] = "'" + Esc(balanceAfterBookingCurrency) + "'";
        
        if (!string.IsNullOrEmpty(balanceAfterBookingType))
            rowData["balance_after_booking_type"] = "'" + Esc(balanceAfterBookingType) + "'";
        
        // System metadata (defaults)
        rowData["source_system"] = "'BAI_API'";
        // created_at, updated_at, retrieved_at have DEFAULT now() in database
        
        // Collect all column names used in this transaction
        foreach (var col in rowData.Keys)
        {
            allColumnNames.Add(col);
        }

        // Store this transaction's data
        allTransactionData.Add(rowData);
    }

    // SECOND PASS: Build INSERT with consistent columns for all rows
    if (allTransactionData.Count > 0)
    {
        // Create sorted column list (for consistency)
        var columnList = new System.Collections.Generic.List<string>(allColumnNames);
        columnList.Sort();

        // Build VALUES clauses - each row uses ALL columns (NULL if missing)
        var valueRows = new System.Collections.Generic.List<string>();
        foreach (var rowData in allTransactionData)
        {
            var rowValues = new System.Collections.Generic.List<string>();
            foreach (var col in columnList)
            {
                if (rowData.ContainsKey(col))
                {
                    rowValues.Add(rowData[col]);
                }
                else
                {
                    rowValues.Add("NULL");  // Use NULL for missing columns
                }
            }
            valueRows.Add("(" + string.Join(", ", rowValues) + ")");
        }

        // Build final INSERT statement
        string insertSql = string.Format(
            "INSERT INTO bai_rabobank_transactions ({0}) VALUES {1};",
            string.Join(", ", columnList),
            string.Join(",\n", valueRows)
        );
        
        insertStatements = insertSql;
        recordsCount = allTransactionData.Count;
        insertSuccess = true;
    }
    else
    {
        // No valid transactions found
        insertStatements = "";
        recordsCount = 0;
        insertSuccess = false;  // Changed to false when no data
    }
}
catch (System.Exception ex)
{
    // Set error output
    insertSuccess = false;
    insertStatements = "";
    recordsCount = 0;
    throw;
}
