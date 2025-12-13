// UiPath Invoke Code Script - Insert Rabobank Balance Payloads
// Input Arguments: jobjBalances (JObject, In), audit_id (String, In), attempt_nr (String, In)
// Output Arguments: insertSuccess (Boolean, Out), insertStatements (String, Out), recordsCount (Int32, Out)
// References: Newtonsoft.Json, Newtonsoft.Json.Linq

try
{
    // Initialize output variables
    insertSuccess = false;
    insertStatements = "";
    recordsCount = 0;

    // Input validation
    if (jobjBalances == null)
    {
        throw new System.ArgumentNullException("jobjBalances", "Balance JSON cannot be null");
    }

    if (string.IsNullOrEmpty(audit_id))
    {
        throw new System.ArgumentNullException("audit_id", "Audit ID cannot be null or empty");
    }

    // Simple parsing without doing any DB work (Option B)
    string iban = null;
    string currency = "EUR";
    Newtonsoft.Json.Linq.JArray balancesArray = null;

    int propIndex = 0;
    foreach (var prop in jobjBalances.Properties())
    {
        propIndex++;
        if (propIndex == 1)
        {
            var acct = prop.Value as Newtonsoft.Json.Linq.JObject;
            if (acct != null)
            {
                foreach (var ap in acct.Properties())
                {
                    var v = ap.Value;
                    if (v != null)
                    {
                        var s = v.ToString();
                        if (s != null && s.StartsWith("NL") && s.Length > 15) iban = s;
                        if (s == "EUR") currency = s;
                    }
                }
            }
        }
        else if (propIndex == 2)
        {
            balancesArray = prop.Value as Newtonsoft.Json.Linq.JArray;
        }
    }

    if (string.IsNullOrEmpty(iban))
    {
        throw new System.ArgumentException("IBAN not found in JSON");
    }
    if (balancesArray == null)
    {
        throw new System.ArgumentException("Balances array not found in JSON");
    }

    var sqlStatements = new System.Collections.Generic.List<string>();

    // Helper for SQL escaping
    System.Func<string, string> Esc = s => s == null ? null : s.Replace("'", "''");

    foreach (var item in balancesArray)
    {
        var bal = item as Newtonsoft.Json.Linq.JObject;
        if (bal == null) continue;

        string balanceType = null;
        double amount = 0.0;
        string refDate = null;
        string lastChange = null;

        foreach (var bp in bal.Properties())
        {
            if (bp.Value is Newtonsoft.Json.Linq.JObject amtObj)
            {
                foreach (var ap in amtObj.Properties())
                {
                    if (ap.Value != null)
                    {
                        string s = ap.Value.ToString().Replace(",", ".");
                        if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsed)) { amount = parsed; break; }
                    }
                }
            }
            else if (bp.Value != null)
            {
                var s = bp.Value.ToString();
                if (s == "interimBooked" || s == "expected" || s == "closingBooked") balanceType = s;
                else if (s.Length == 10 && s.Contains("-")) refDate = s;
                else if (s.Contains("/") && s.Contains(":")) lastChange = s;
            }
        }

        if (string.IsNullOrEmpty(balanceType)) continue;

        // Build a ready-to-run INSERT statement (SQL Server / generic SQL style)
        string q_audit = "'" + Esc(audit_id) + "'";
        string q_attempt = attempt_nr != null ? "'" + Esc(attempt_nr) + "'" : "NULL";
        string q_iban = "'" + Esc(iban) + "'";
        string q_currency = "'" + Esc(currency) + "'";
        string q_type = "'" + Esc(balanceType) + "'";
        string q_amount = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string q_ref = refDate != null ? "'" + Esc(refDate) + "'" : "NULL";
        string q_last = lastChange != null ? "'" + Esc(lastChange) + "'" : "NULL";

        string insertSql = string.Format(
            "INSERT INTO bai_rabobank_balances (audit_id, attempt_nr, iban, currency, balance_type, amount, reference_date, last_change_datetime) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7});",
            q_audit, q_attempt, q_iban, q_currency, q_type, q_amount, q_ref, q_last
        );

        sqlStatements.Add(insertSql);
    }

    insertStatements = string.Join(System.Environment.NewLine, sqlStatements);
    recordsCount = sqlStatements.Count;
    insertSuccess = true;
}
catch (System.Exception ex)
{
    // Set error output
    insertSuccess = false;
    insertStatements = "";
    recordsCount = 0;
    throw;
}
