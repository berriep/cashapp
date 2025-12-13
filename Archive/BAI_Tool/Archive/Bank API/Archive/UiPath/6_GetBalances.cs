// UiPath Invoke Code Script - Get Account Balances
// Input Arguments: jobjTokens (JObject, In), jobjApiSettings (JObject, In), jobjAccounts (JObject, In), strIban (String, In), strBalanceDate (String, In)
// Output Arguments: jobjBalanceResponse (JObject, Out), getBalancesSuccess (Boolean, Out), errorMessage (String, Out), balanceAmount (Decimal, Out)
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates
// Input Format: ISO 8601 Date string, bijvoorbeeld: "2025-09-01" of "2025-09-01T00:00:00.000"

try
{
    if (jobjTokens == null || jobjTokens["access_token"] == null)
    {
        throw new Exception("No valid access token available");
    }
    
    if (string.IsNullOrEmpty(strIban))
    {
        throw new Exception("IBAN is required for balance request");
    }
    
    if (jobjAccounts == null || jobjAccounts["AccountIds"] == null)
    {
        throw new Exception("No account tokens available");
    }
    
    // Parse string input naar DateTime, of gebruik default (yesterday)
    DateTime balanceDate;
    
    if (string.IsNullOrEmpty(strBalanceDate))
    {
        balanceDate = DateTime.Today.AddDays(-1);
        System.Console.WriteLine($"[GetBalances] Using default date (yesterday): {balanceDate:yyyy-MM-dd}");
    }
    else
    {
        try
        {
            balanceDate = DateTime.Parse(strBalanceDate);
            System.Console.WriteLine($"[GetBalances] Parsed balanceDate: {strBalanceDate} -> {balanceDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Invalid balanceDate format: {strBalanceDate}. Expected format: 2025-09-01 or 2025-09-01T00:00:00.000. Error: {ex.Message}");
        }
    }
    
    string balanceDateString = balanceDate.ToString("yyyy-MM-dd");
    
    // Get the account token for this IBAN
    string accountToken = jobjAccounts["AccountIds"][strIban]?.ToString();
    if (string.IsNullOrEmpty(accountToken))
    {
        throw new Exception($"No account token found for IBAN: {strIban}");
    }
    
    string accessToken = jobjTokens["access_token"].ToString();
    
    // Setup HttpClient with mTLS certificate
    var handler = new System.Net.Http.HttpClientHandler();
    
    string certPath = jobjApiSettings["CertificatePath"].ToString();
    string keyPath = jobjApiSettings["PrivateKeyPath"].ToString();
    
    // Check if certificate files exist
    if (!System.IO.File.Exists(certPath))
        throw new Exception($"Certificate file not found: {certPath}");
    if (!System.IO.File.Exists(keyPath))
        throw new Exception($"Private key file not found: {keyPath}");
    
    string certPem = System.IO.File.ReadAllText(certPath);
    string keyPem = System.IO.File.ReadAllText(keyPath);
    
    // Create certificate with private key and mark as exportable
    var cert = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(certPem, keyPem);
    var certWithKey = new System.Security.Cryptography.X509Certificates.X509Certificate2(cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));
    
    handler.ClientCertificates.Add(certWithKey);
    handler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    
    using var httpClient = new System.Net.Http.HttpClient(handler);
    
    // Set authorization and required headers (like working RabobankApiClient)
    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    
    // Add required headers from API spec
    string dateHeader = System.DateTime.UtcNow.ToString("R"); // RFC1123 format
    string xRequestId = System.Guid.NewGuid().ToString();
    
    // Generate digest for empty body (GET request)
    using var sha512 = System.Security.Cryptography.SHA512.Create();
    byte[] hash = sha512.ComputeHash(new byte[0]);
    string digest = "sha-512=" + System.Convert.ToBase64String(hash);
    
    // Build API URL for balances with account-id and date (needs to be defined BEFORE using it for signature)
    string apiBaseUrl = jobjApiSettings["ApiBaseUrl"].ToString();
    string balancesUrl = $"{apiBaseUrl}/accounts/{accountToken}/balances?closingBookedReferenceDate={balanceDateString}";
    
    // Generate signature using private key
    var uri = new System.Uri(balancesUrl);
    string urlPath = uri.PathAndQuery;
    
    // Build signing string (according to API spec: "date digest x-request-id")
    string signingString = $"date: {dateHeader}\ndigest: {digest}\nx-request-id: {xRequestId}";
    
    // Sign with RSA-SHA512
    using var rsa = System.Security.Cryptography.RSA.Create();
    rsa.ImportFromPem(keyPem.ToCharArray());
    byte[] data = System.Text.Encoding.UTF8.GetBytes(signingString);
    byte[] signature = rsa.SignData(data, System.Security.Cryptography.HashAlgorithmName.SHA512, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    string signatureBase64 = System.Convert.ToBase64String(signature);
    
    // Build signature header
    string keyId = "41703392498275823274478450484290741484992002829"; // Certificate serial number
    string signatureHeader = $"keyId=\"{keyId}\",algorithm=\"rsa-sha512\",headers=\"date digest x-request-id\",signature=\"{signatureBase64}\"";
    
    // Get certificate as base64
    string certificateBase64 = System.Convert.ToBase64String(cert.RawData);
    
    httpClient.DefaultRequestHeaders.Add("X-IBM-Client-Id", jobjApiSettings["ClientId"].ToString());
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    httpClient.DefaultRequestHeaders.Add("Date", dateHeader);
    httpClient.DefaultRequestHeaders.Add("Digest", digest);
    httpClient.DefaultRequestHeaders.Add("X-Request-ID", xRequestId);
    httpClient.DefaultRequestHeaders.Add("Signature", signatureHeader);
    httpClient.DefaultRequestHeaders.Add("Signature-Certificate", certificateBase64);
    
    // Increase timeout to 60 seconds
    httpClient.Timeout = TimeSpan.FromSeconds(60);
    
    System.Console.WriteLine($"[GetBalances] Generated signature headers successfully");
    
    System.Console.WriteLine($"[GetBalances] Making request to: {balancesUrl}");
    System.Console.WriteLine($"[GetBalances] Using IBAN: {strIban}");
    System.Console.WriteLine($"[GetBalances] Using date: {balanceDateString}");
    System.Console.WriteLine($"[GetBalances] Using account token: {accountToken.Substring(0, 10)}...");
    
    // Make GET request for balances
    var response = httpClient.GetAsync(balancesUrl).Result;
    string responseBody = response.Content.ReadAsStringAsync().Result;
    
    if (response.IsSuccessStatusCode)
    {
        // Parse the JSON response
        jobjBalanceResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
        getBalancesSuccess = true;
        errorMessage = $"Balances successfully retrieved for IBAN: {strIban}";
        
        System.Console.WriteLine($"[GetBalances] Success! Response length: {responseBody.Length}");
        
        // Extract closingBooked balance amount
        balanceAmount = 0m; // Default value
        
        // Log balance information if available
        if (jobjBalanceResponse["balances"] != null)
        {
            var balances = jobjBalanceResponse["balances"] as Newtonsoft.Json.Linq.JArray;
            System.Console.WriteLine($"[GetBalances] Found {balances.Count} balance(s)");
            
            // Find closingBooked balance for the requested date
            foreach (var balance in balances)
            {
                string balanceType = balance["balanceType"]?.ToString();
                if (balanceType == "closingBooked")
                {
                    string amountStr = balance["balanceAmount"]["amount"]?.ToString();
                    if (decimal.TryParse(amountStr, out decimal amount))
                    {
                        balanceAmount = amount;
                        System.Console.WriteLine($"[GetBalances] Found closingBooked balance: {balanceAmount:C} EUR");
                        break;
                    }
                }
            }
            
            if (balanceAmount == 0m)
            {
                System.Console.WriteLine($"[GetBalances] WARNING: No closingBooked balance found, using 0");
            }
        }
    }
    else
    {
        getBalancesSuccess = false;
        errorMessage = $"Get balances failed: {response.StatusCode} - {responseBody}";
        jobjBalanceResponse = null;
        balanceAmount = 0m; // Set default on failure
        System.Console.WriteLine($"[GetBalances] Failed: {errorMessage}");
    }
}
catch (Exception ex)
{
    getBalancesSuccess = false;
    errorMessage = $"Exception during get balances: {ex.Message}";
    jobjBalanceResponse = null;
    balanceAmount = 0m; // Set default on exception
    System.Console.WriteLine($"[GetBalances] Exception: {ex.ToString()}");
}

// Output variables:
// jobjBalanceResponse: JObject containing balance data (or null if failed)
// getBalancesSuccess: Boolean indicating success/failure
// errorMessage: String with result details or error information
// balanceAmount: Decimal with closingBooked balance amount (0 if not found/failed)
