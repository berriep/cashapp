// UiPath Invoke Code Script - Get Account Balances (Production - PFX Certificate)
// Input Arguments: jobjTokens (JObject, In), jobjApiSettings (JObject, In), resourceId (String, In), strIban (String, In), strBalanceDate (String, In), debugEnabled (Boolean, In)
// Output Arguments: jobjBalanceResponse (JObject, Out), getBalancesSuccess (Boolean, Out), errorMessage (String, Out), balanceAmount (Decimal, Out), response_status (Int32, Out), response_time_ms (Int32, Out)
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates
// Input Format: ISO 8601 Date string, bijvoorbeeld: "2025-09-01" of "2025-09-01T00:00:00.000"
// IMPORTANT: PfxPassword must be retrieved from KeePass entry named "Rabo_cert pfx"
// Note: Production certificate Serial: 044CB89A91BC353709DAC64E493AD451

try
{
    // Initialize output variables
    response_status = 0;
    response_time_ms = 0;
    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    if (jobjTokens == null || jobjTokens["access_token"] == null)
    {
        throw new Exception("No valid access token available");
    }
    
    if (string.IsNullOrEmpty(strIban))
    {
        throw new Exception("IBAN is required for balance request");
    }
    
    if (string.IsNullOrEmpty(resourceId))
    {
        throw new Exception("resourceId is required for balance request");
    }
    
    // Parse string input naar DateTime, of gebruik default (yesterday)
    DateTime balanceDate;
    
    if (string.IsNullOrEmpty(strBalanceDate))
    {
        balanceDate = DateTime.Today.AddDays(-1);
        if (debugEnabled)
            System.Console.WriteLine($"[GetBalances] Using default date (yesterday): {balanceDate:yyyy-MM-dd}");
    }
    else
    {
        try
        {
            balanceDate = DateTime.Parse(strBalanceDate);
            if (debugEnabled)
                System.Console.WriteLine($"[GetBalances] Parsed balanceDate: {strBalanceDate} -> {balanceDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Invalid balanceDate format: {strBalanceDate}. Expected format: 2025-09-01 or 2025-09-01T00:00:00.000. Error: {ex.Message}");
        }
    }
    
    string balanceDateString = balanceDate.ToString("yyyy-MM-dd");
    
    if (debugEnabled)
        System.Console.WriteLine($"[GetBalances] Using provided resourceId: {resourceId} for IBAN: {strIban}");
    
    string accessToken = jobjTokens["access_token"].ToString();
    
    // Setup HttpClient with mTLS certificate (Production PFX)
    var handler = new System.Net.Http.HttpClientHandler();
    
    // Load PFX certificate for production (production cert Serial: 044CB89A91BC353709DAC64E493AD451)
    if (jobjApiSettings["PfxCertificatePath"] == null || string.IsNullOrEmpty(jobjApiSettings["PfxCertificatePath"].ToString()))
    {
        throw new Exception("PfxCertificatePath is required in jobjApiSettings for production API calls");
    }
    
    if (jobjApiSettings["PfxPassword"] == null || string.IsNullOrEmpty(jobjApiSettings["PfxPassword"].ToString()))
    {
        throw new Exception("PfxPassword is required in jobjApiSettings - retrieve from KeePass entry 'Rabo_cert pfx'");
    }
    
    string pfxPath = jobjApiSettings["PfxCertificatePath"].ToString();
    string pfxPassword = jobjApiSettings["PfxPassword"].ToString();
    
    if (!System.IO.File.Exists(pfxPath))
    {
        throw new Exception($"PFX certificate file not found: {pfxPath}");
    }
    
    // Load PFX certificate (includes both certificate and private key)
    System.Security.Cryptography.X509Certificates.X509Certificate2 cert;
    try
    {
        cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(pfxPath, pfxPassword, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.MachineKeySet | System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.PersistKeySet);
    }
    catch (System.Security.Cryptography.CryptographicException ex)
    {
        throw new Exception($"Failed to load PFX certificate - check that you're using api_rabo_PRODUCTION.pfx (not api_rabo.pfx) and correct password from KeePass 'Rabo_cert pfx': {ex.Message}");
    }
    
    // Extended certificate debugging
    if (debugEnabled)
    {
        System.Console.WriteLine($"[GetBalances] Loaded PFX certificate:");
        System.Console.WriteLine($"  Subject: {cert.Subject}");
        System.Console.WriteLine($"  Issuer: {cert.Issuer}");
        System.Console.WriteLine($"  Serial Number: {cert.SerialNumber}");
        System.Console.WriteLine($"  Valid From: {cert.NotBefore}");
        System.Console.WriteLine($"  Valid To: {cert.NotAfter}");
        System.Console.WriteLine($"  Has Private Key: {cert.HasPrivateKey}");
        System.Console.WriteLine($"  Thumbprint: {cert.Thumbprint}");
    }
    
    // CRITICAL: Verify this is the correct production certificate
    string expectedSerial = "044CB89A91BC353709DAC64E493AD451";
    if (cert.SerialNumber.ToUpper() != expectedSerial)
    {
        System.Console.WriteLine($"[GetBalances] ❌ WRONG CERTIFICATE LOADED!");
        System.Console.WriteLine($"  Expected Serial: {expectedSerial}");
        System.Console.WriteLine($"  Actual Serial:   {cert.SerialNumber}");
        System.Console.WriteLine($"  This certificate is NOT registered in Developer Portal!");
        throw new Exception($"Wrong certificate loaded. Expected production certificate with Serial: {expectedSerial}, but got: {cert.SerialNumber}. Use api_rabo_PRODUCTION.pfx instead of test certificate.");
    }
    else if (debugEnabled)
    {
        System.Console.WriteLine($"[GetBalances] ✅ CORRECT production certificate verified!");
        System.Console.WriteLine($"  Certificate Serial matches registered certificate in Developer Portal");
    }
    
    // Check certificate validity
    if ((System.DateTime.Now < cert.NotBefore || System.DateTime.Now > cert.NotAfter) && debugEnabled)
    {
        System.Console.WriteLine($"[GetBalances] WARNING: Certificate is expired or not yet valid!");
    }
    
    handler.ClientCertificates.Add(cert);
    // Production: Use proper certificate validation (no custom callback)
    
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
    
    // Build API URL for balances with resourceId and date (needs to be defined BEFORE using it for signature)
    string apiBaseUrl = jobjApiSettings["ApiBaseUrl"].ToString();
    string balancesUrl = $"{apiBaseUrl}/accounts/{resourceId}/balances?closingBookedReferenceDate={balanceDateString}";
    
    // Generate signature using private key from PFX certificate
    var uri = new System.Uri(balancesUrl);
    string urlPath = uri.PathAndQuery;
    
    // Build signing string (according to API spec: "date digest x-request-id")
    string signingString = $"date: {dateHeader}\ndigest: {digest}\nx-request-id: {xRequestId}";
    
    // Sign with RSA-SHA512 using PFX certificate private key (Universal method)
    if (!cert.HasPrivateKey)
    {
        throw new Exception("PFX certificate does not contain a private key - check PFX file and password");
    }
    
    byte[] data = System.Text.Encoding.UTF8.GetBytes(signingString);
    byte[] signature;
    
    // Use AsymmetricAlgorithm which works across different .NET versions
    using (var privateKey = cert.PrivateKey)
    {
        if (privateKey == null)
        {
            throw new Exception("Could not extract private key from PFX certificate");
        }
        
        // Create SHA512 hash manually and then sign it
        using (var sha512Signature = System.Security.Cryptography.SHA512.Create())
        {
            byte[] signatureHash = sha512Signature.ComputeHash(data);
            
            // Cast to RSA and sign the hash
            var rsa = privateKey as System.Security.Cryptography.RSA;
            if (rsa != null)
            {
                signature = rsa.SignHash(signatureHash, System.Security.Cryptography.HashAlgorithmName.SHA512, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            }
            else
            {
                throw new Exception("Certificate private key is not RSA type");
            }
        }
    }
    
    string signatureBase64 = System.Convert.ToBase64String(signature);
    
    // Build signature header with production certificate serial number
    // Convert certificate serial number from hex to integer format (required by Rabobank API)
    string hexSerial = cert.SerialNumber;
    System.Numerics.BigInteger serialInt = System.Numerics.BigInteger.Parse(hexSerial, System.Globalization.NumberStyles.HexNumber);
    string keyId = serialInt.ToString(); // Convert to integer format
    string signatureHeader = $"keyId=\"{keyId}\",algorithm=\"rsa-sha512\",headers=\"date digest x-request-id\",signature=\"{signatureBase64}\"";
    
    // Get certificate as base64
    string certificateBase64 = System.Convert.ToBase64String(cert.RawData);
    
    if (debugEnabled)
    {
        System.Console.WriteLine($"[GetBalances] Certificate Serial Number (hex): {hexSerial}");
        System.Console.WriteLine($"[GetBalances] Certificate Serial Number (int): {keyId}");
        System.Console.WriteLine($"[GetBalances] Signature generated successfully");
        System.Console.WriteLine($"[GetBalances] Signing string: {signingString.Replace("\n", "\\n")}");
        System.Console.WriteLine($"[GetBalances] Signature header: {signatureHeader}");
    }
    
    // Add all required headers for production API
    httpClient.DefaultRequestHeaders.Add("X-IBM-Client-Id", jobjApiSettings["ClientId"].ToString());
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    httpClient.DefaultRequestHeaders.Add("Date", dateHeader);
    httpClient.DefaultRequestHeaders.Add("Digest", digest);
    httpClient.DefaultRequestHeaders.Add("X-Request-ID", xRequestId);
    httpClient.DefaultRequestHeaders.Add("Signature", signatureHeader);
    httpClient.DefaultRequestHeaders.Add("Signature-Certificate", certificateBase64);
    
    // Increase timeout to 60 seconds
    httpClient.Timeout = TimeSpan.FromSeconds(60);
    
    if (debugEnabled)
    {
        System.Console.WriteLine($"[GetBalances] All signature headers added successfully");
        System.Console.WriteLine($"[GetBalances] Making request to: {balancesUrl}");
        System.Console.WriteLine($"[GetBalances] Using IBAN: {strIban}");
        System.Console.WriteLine($"[GetBalances] Using date: {balanceDateString}");
        System.Console.WriteLine($"[GetBalances] Using resourceId: {resourceId}");
    }
    
    // Make GET request for balances with full signature headers
    var response = httpClient.GetAsync(balancesUrl).Result;
    string responseBody = response.Content.ReadAsStringAsync().Result;
    
    // Capture response metrics
    stopwatch.Stop();
    response_status = (int)response.StatusCode;
    response_time_ms = (int)stopwatch.ElapsedMilliseconds;
    
    if (debugEnabled)
    {
        System.Console.WriteLine($"[GetBalances] API Response Status: {response.StatusCode} ({response_status})");
        System.Console.WriteLine($"[GetBalances] API Response Time: {response_time_ms}ms");
        System.Console.WriteLine($"[GetBalances] API Response: {responseBody}");
    }
    
    if (response.IsSuccessStatusCode)
    {
        // Parse the JSON response
        jobjBalanceResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
        getBalancesSuccess = true;
        errorMessage = $"Balances successfully retrieved for IBAN: {strIban}";
        
        if (debugEnabled)
            System.Console.WriteLine($"[GetBalances] Success! Response length: {responseBody.Length}");
        
        // Extract closingBooked balance amount
        balanceAmount = 0m; // Default value
        
        // Log balance information if available
        if (jobjBalanceResponse["balances"] != null)
        {
            var balances = jobjBalanceResponse["balances"] as Newtonsoft.Json.Linq.JArray;
            if (debugEnabled)
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
                        // Always show balance amount (important info)
                        System.Console.WriteLine($"[GetBalances] Found closingBooked balance: {balanceAmount:C} EUR");
                        break;
                    }
                }
            }
            
            if (balanceAmount == 0m && debugEnabled)
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
// response_status: Int32 with HTTP response status code (e.g., 200, 400, 500)
// response_time_ms: Int32 with API response time in milliseconds
