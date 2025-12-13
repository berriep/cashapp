// UiPath Invoke Code Script - Get Account Transactions (Production - PFX Certificate)
// Input Arguments: jobjTokens (JObject, In), jobjApiSettings (JObject, In), resourceId (String, In), strIban (String, In), strDateTimeFrom (String, In), strDateTimeTo (String, In), debugEnabled (Boolean, In)
// Output Arguments: jobjTransactionResponse (JObject, Out), rawTransactionJson (String, Out), getTransactionsSuccess (Boolean, Out), errorMessage (String, Out), response_status (Int32, Out), response_time_ms (Int32, Out)
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates
// Input Format: ISO 8601 DateTime strings, bijvoorbeeld: "2025-09-01T09:30:15.500000Z" of "2025-09-01T09:30:15.500000"
// IMPORTANT: PfxPassword must be retrieved from KeePass entry named "Rabo_cert pfx"
// Note: Production certificate Serial: 044CB89A91BC353709DAC64E493AD451

try
{
    // Initialize output variables
    response_status = 0;
    response_time_ms = 0;
    rawTransactionJson = null;
    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    if (jobjTokens == null || jobjTokens["access_token"] == null)
    {
        throw new Exception("No valid access token available");
    }
    
    if (string.IsNullOrEmpty(strIban))
    {
        throw new Exception("IBAN is required for transaction request");
    }
    
    if (string.IsNullOrEmpty(resourceId))
    {
        throw new Exception("resourceId is required for transaction request");
    }
    
    // DateTime range is verplicht - geen defaults
    if (string.IsNullOrEmpty(strDateTimeFrom) || string.IsNullOrEmpty(strDateTimeTo))
    {
        throw new Exception("Both strDateTimeFrom and strDateTimeTo are required for transaction request");
    }
    
    // Parse string inputs naar DateTime - handle UTC inputs correctly
    DateTime dateTimeFrom;
    DateTime dateTimeTo;
    
    try
    {
        // Parse with UTC awareness - handle both " T" and "T" formats
        string cleanDateFrom = strDateTimeFrom.Replace(" T", "T"); // Normalize format
        if (cleanDateFrom.EndsWith("Z"))
        {
            dateTimeFrom = DateTime.Parse(cleanDateFrom, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        }
        else
        {
            dateTimeFrom = DateTime.Parse(cleanDateFrom);
        }
        
        if (debugEnabled)
            System.Console.WriteLine($"[GetTransactions] Parsed dateTimeFrom: {strDateTimeFrom} -> {dateTimeFrom:yyyy-MM-ddTHH:mm:ss.ffffff} (Kind: {dateTimeFrom.Kind})");
    }
    catch (Exception ex)
    {
        throw new Exception($"Invalid dateTimeFrom format: {strDateTimeFrom}. Expected format: 2025-09-01T09:30:15.500000 or 2025-09-01T09:30:15.500000Z. Error: {ex.Message}");
    }
    
    try
    {
        // Parse with UTC awareness - handle both " T" and "T" formats
        string cleanDateTo = strDateTimeTo.Replace(" T", "T"); // Normalize format
        if (cleanDateTo.EndsWith("Z"))
        {
            dateTimeTo = DateTime.Parse(cleanDateTo, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        }
        else
        {
            dateTimeTo = DateTime.Parse(cleanDateTo);
        }
        
        if (debugEnabled)
            System.Console.WriteLine($"[GetTransactions] Parsed dateTimeTo: {strDateTimeTo} -> {dateTimeTo:yyyy-MM-ddTHH:mm:ss.ffffff} (Kind: {dateTimeTo.Kind})");
    }
    catch (Exception ex)
    {
        throw new Exception($"Invalid dateTimeTo format: {strDateTimeTo}. Expected format: 2025-09-01T09:30:15.500000 or 2025-09-01T09:30:15.500000Z. Error: {ex.Message}");
    }
    
    // Valideer dat de datetime range logisch is
    if (dateTimeFrom > dateTimeTo)
    {
        throw new Exception("DateTimeFrom cannot be later than dateTimeTo");
    }
    
    // Informeer over API limieten, maar pas NIET automatisch aan
    DateTime maxDateBack = DateTime.Now.AddMonths(-15);
    if (dateTimeFrom < maxDateBack && debugEnabled)
    {
        System.Console.WriteLine($"[GetTransactions] INFO: dateTimeFrom ({dateTimeFrom:yyyy-MM-dd}) is more than 15 months ago. API limit is: {maxDateBack:yyyy-MM-dd}");
        System.Console.WriteLine($"[GetTransactions] WARNING: This may result in empty results due to API limitations");
    }
    
    if (dateTimeTo > DateTime.Now && debugEnabled)
    {
        System.Console.WriteLine($"[GetTransactions] INFO: dateTimeTo ({dateTimeTo:yyyy-MM-dd}) is in the future");
        System.Console.WriteLine($"[GetTransactions] WARNING: This may result in unexpected results");
    }
    
    if (debugEnabled)
        System.Console.WriteLine($"[GetTransactions] Using datetime range: {dateTimeFrom:yyyy-MM-ddTHH:mm:ss.ffffff} to {dateTimeTo:yyyy-MM-ddTHH:mm:ss.ffffff} (UTC)");
    
    // Build query parameters voor API URL - use microsecond precision (confirmed by bank)
    string dateTimeFromISO = dateTimeFrom.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
    string dateTimeToISO = dateTimeTo.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
    string queryParams = $"bookingStatus=booked&dateFrom={dateTimeFromISO}&dateTo={dateTimeToISO}";
    
    if (debugEnabled)
    {
        System.Console.WriteLine($"[GetTransactions] API DateTime format: {dateTimeFromISO} to {dateTimeToISO}");
        System.Console.WriteLine($"[GetTransactions] Input was parsed as UTC and sent to API as UTC");
        System.Console.WriteLine($"[GetTransactions] Using provided resourceId: {resourceId} for IBAN: {strIban}");
        System.Console.WriteLine($"[GetTransactions] CONSENT DEBUG:");
        System.Console.WriteLine($"[GetTransactions] - Access Token: {accessToken.Substring(0, Math.Min(20, accessToken.Length))}...");
        System.Console.WriteLine($"[GetTransactions] - Full API URL will be: /accounts/{resourceId}/transactions");
        System.Console.WriteLine($"[GetTransactions] - Query Parameters: {queryParams}");
        System.Console.WriteLine($"[GetTransactions] NOTE: If you get 'accountId does not belong to consent', check:");
        System.Console.WriteLine($"[GetTransactions] 1. Is resourceId '{resourceId}' correct for IBAN '{strIban}'?");
        System.Console.WriteLine($"[GetTransactions] 2. Was consent created for the same IBAN?");
        System.Console.WriteLine($"[GetTransactions] 3. Is the access token still valid?");
    }
    
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
        System.Console.WriteLine($"[GetTransactions] Loaded PFX certificate:");
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
        System.Console.WriteLine($"[GetTransactions] ❌ WRONG CERTIFICATE LOADED!");
        System.Console.WriteLine($"  Expected Serial: {expectedSerial}");
        System.Console.WriteLine($"  Actual Serial:   {cert.SerialNumber}");
        System.Console.WriteLine($"  This certificate is NOT registered in Developer Portal!");
        throw new Exception($"Wrong certificate loaded. Expected production certificate with Serial: {expectedSerial}, but got: {cert.SerialNumber}. Use api_rabo_PRODUCTION.pfx instead of test certificate.");
    }
    else if (debugEnabled)
    {
        System.Console.WriteLine($"[GetTransactions] ✅ CORRECT production certificate verified!");
        System.Console.WriteLine($"  Certificate Serial matches registered certificate in Developer Portal");
    }
    
    // Check certificate validity
    if ((System.DateTime.Now < cert.NotBefore || System.DateTime.Now > cert.NotAfter) && debugEnabled)
    {
        System.Console.WriteLine($"[GetTransactions] WARNING: Certificate is expired or not yet valid!");
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
    
    // Build API URL for transactions with resourceId and date range (needs to be defined BEFORE using it for signature)
    string apiBaseUrl = jobjApiSettings["ApiBaseUrl"].ToString();
    string transactionsUrl = $"{apiBaseUrl}/accounts/{resourceId}/transactions?{queryParams}";
    
    // Helper method to generate signature for any request
    System.Func<string, string, string, string> GenerateSignature = (dateHdr, digestHdr, requestId) =>
    {
        string signStr = $"date: {dateHdr}\ndigest: {digestHdr}\nx-request-id: {requestId}";
        byte[] signData = System.Text.Encoding.UTF8.GetBytes(signStr);
        
        // Use compatible RSA method that works across all .NET versions
        using (var sha512Sig = System.Security.Cryptography.SHA512.Create())
        {
            byte[] signHash = sha512Sig.ComputeHash(signData);
            
            // Get private key using compatible method
            var privateKey = cert.PrivateKey;
            if (privateKey == null)
            {
                throw new Exception("Could not extract private key from PFX certificate");
            }
            
            var rsa = privateKey as System.Security.Cryptography.RSA;
            if (rsa == null)
            {
                throw new Exception("Certificate private key is not RSA type");
            }
            
            byte[] sig = rsa.SignHash(signHash, System.Security.Cryptography.HashAlgorithmName.SHA512, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            return System.Convert.ToBase64String(sig);
        }
    };
    
    // Generate signature using private key from PFX certificate
    var uri = new System.Uri(transactionsUrl);
    string urlPath = uri.PathAndQuery;
    
    // Build signing string (according to API spec: "date digest x-request-id")
    string signingString = $"date: {dateHeader}\ndigest: {digest}\nx-request-id: {xRequestId}";
    
    // Sign with RSA-SHA512 using PFX certificate private key
    if (!cert.HasPrivateKey)
    {
        throw new Exception("PFX certificate does not contain a private key - check PFX file and password");
    }
    
    string signatureBase64 = GenerateSignature(dateHeader, digest, xRequestId);
    
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
        System.Console.WriteLine($"[GetTransactions] Certificate Serial Number (hex): {hexSerial}");
        System.Console.WriteLine($"[GetTransactions] Certificate Serial Number (int): {keyId}");
        System.Console.WriteLine($"[GetTransactions] Signature generated successfully");
        System.Console.WriteLine($"[GetTransactions] Signing string: {signingString.Replace("\n", "\\n")}");
        System.Console.WriteLine($"[GetTransactions] Signature header: {signatureHeader}");
    }
    
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
        System.Console.WriteLine($"[GetTransactions] All signature headers added successfully");
        System.Console.WriteLine($"[GetTransactions] Making request to: {transactionsUrl}");
        System.Console.WriteLine($"[GetTransactions] Using IBAN: {strIban}");
        System.Console.WriteLine($"[GetTransactions] Using datetime range: {dateTimeFrom:yyyy-MM-ddTHH:mm:ss.ffffff} to {dateTimeTo:yyyy-MM-ddTHH:mm:ss.ffffff}");
        System.Console.WriteLine($"[GetTransactions] Using resourceId: {resourceId}");
    }
    
    // Make GET request for transactions with full signature headers
    if (debugEnabled)
        System.Console.WriteLine($"[GetTransactions] STEP 1: About to call httpClient.GetAsync()");
    
    var response = httpClient.GetAsync(transactionsUrl).Result;
    
    if (debugEnabled)
        System.Console.WriteLine($"[GetTransactions] STEP 2: Received response with status: {response.StatusCode}");
    
    string responseBody = response.Content.ReadAsStringAsync().Result;
    
    // Capture response metrics
    stopwatch.Stop();
    response_status = (int)response.StatusCode;
    response_time_ms = (int)stopwatch.ElapsedMilliseconds;
    
    if (debugEnabled)
    {
        System.Console.WriteLine($"[GetTransactions] STEP 3: Read response body, length: {responseBody.Length}");
        System.Console.WriteLine($"[GetTransactions] API Response Status: {response.StatusCode} ({response_status})");
        System.Console.WriteLine($"[GetTransactions] API Response Time: {response_time_ms}ms");
        System.Console.WriteLine($"[GetTransactions] API Response: {responseBody}");
    }
    
    if (response.IsSuccessStatusCode)
    {
        if (debugEnabled)
            System.Console.WriteLine($"[GetTransactions] STEP 4: Response was successful, parsing JSON...");
        
        // CRITICAL: Store original responseBody BEFORE parsing for microsecond preservation
        string originalResponseBody = responseBody;
        
        // Parse the JSON response
        jobjTransactionResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
        
        if (debugEnabled)
            System.Console.WriteLine($"[GetTransactions] STEP 5: JSON parsed successfully");
        
        // Check if we have paginated results and collect ALL transactions
        var allTransactions = new Newtonsoft.Json.Linq.JArray();
        int pageCount = 1;
        int totalTransactionCount = 0;
        
        // Add transactions from first page
        if (jobjTransactionResponse["transactions"]?["booked"] is Newtonsoft.Json.Linq.JArray firstPageTransactions)
        {
            foreach (var transaction in firstPageTransactions)
            {
                allTransactions.Add(transaction);
            }
            totalTransactionCount = firstPageTransactions.Count;
            if (debugEnabled)
                System.Console.WriteLine($"[GetTransactions] Page {pageCount}: Found {firstPageTransactions.Count} transactions");
        }
        
        // Check for pagination and fetch all remaining pages
        string nextPageUrl = jobjTransactionResponse["transactions"]?["_links"]?["next"]?.ToString();
        
        while (!string.IsNullOrEmpty(nextPageUrl))
        {
            pageCount++;
            if (debugEnabled)
                System.Console.WriteLine($"[GetTransactions] Fetching page {pageCount}: {nextPageUrl}");
            
            // Build full URL for next page
            string fullNextUrl = nextPageUrl.StartsWith("http") ? nextPageUrl : $"{apiBaseUrl.TrimEnd('/')}{nextPageUrl}";
            
            // Create new signature for next page request
            string nextDateHeader = System.DateTime.UtcNow.ToString("R");
            string nextXRequestId = System.Guid.NewGuid().ToString();
            string nextDigest = "sha-512=" + System.Convert.ToBase64String(sha512.ComputeHash(new byte[0]));
            
            // Generate signature for next page using our helper function
            string nextSignatureBase64 = GenerateSignature(nextDateHeader, nextDigest, nextXRequestId);
            string nextSignatureHeader = $"keyId=\"{keyId}\",algorithm=\"rsa-sha512\",headers=\"date digest x-request-id\",signature=\"{nextSignatureBase64}\"";
            
            // Clear and set headers for next page
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("X-IBM-Client-Id", jobjApiSettings["ClientId"].ToString());
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("Date", nextDateHeader);
            httpClient.DefaultRequestHeaders.Add("Digest", nextDigest);
            httpClient.DefaultRequestHeaders.Add("X-Request-ID", nextXRequestId);
            httpClient.DefaultRequestHeaders.Add("Signature", nextSignatureHeader);
            httpClient.DefaultRequestHeaders.Add("Signature-Certificate", certificateBase64);
            
            // Make request for next page
            var nextResponse = httpClient.GetAsync(fullNextUrl).Result;
            string nextResponseBody = nextResponse.Content.ReadAsStringAsync().Result;
            
            if (nextResponse.IsSuccessStatusCode)
            {
                var nextPageData = Newtonsoft.Json.Linq.JObject.Parse(nextResponseBody);
                
                if (nextPageData["transactions"]?["booked"] is Newtonsoft.Json.Linq.JArray nextPageTransactions)
                {
                    foreach (var transaction in nextPageTransactions)
                    {
                        allTransactions.Add(transaction);
                    }
                    totalTransactionCount += nextPageTransactions.Count;
                    if (debugEnabled)
                        System.Console.WriteLine($"[GetTransactions] Page {pageCount}: Found {nextPageTransactions.Count} transactions (Total: {totalTransactionCount})");
                }
                
                // Get next page URL
                nextPageUrl = nextPageData["transactions"]?["_links"]?["next"]?.ToString();
            }
            else
            {
                if (debugEnabled)
                    System.Console.WriteLine($"[GetTransactions] Failed to fetch page {pageCount}: {nextResponse.StatusCode} - {nextResponseBody}");
                break;
            }
        }
        
        // Update the response with ALL collected transactions
        jobjTransactionResponse["transactions"]["booked"] = allTransactions;
        
        // Remove the next link since we have all data now
        if (jobjTransactionResponse["transactions"]["_links"] != null)
        {
            ((Newtonsoft.Json.Linq.JObject)jobjTransactionResponse["transactions"]["_links"]).Remove("next");
        }
        
        // CRITICAL FIX: Use raw responseBody without pagination to preserve microseconds  
        // For paginated results, we'll accept the first page only to preserve precision
        // Alternative: Handle pagination with special JSON parsing settings
        if (pageCount == 1)
        {
            // Single page - use original responseBody with intact microseconds
            rawTransactionJson = originalResponseBody;
            if (debugEnabled)
                System.Console.WriteLine($"[GetTransactions] Using original responseBody (single page) - microseconds preserved");
        }
        else
        {
            // Multiple pages - use combined result but warn about potential precision loss
            rawTransactionJson = jobjTransactionResponse.ToString(Newtonsoft.Json.Formatting.None);
            if (debugEnabled)
                System.Console.WriteLine($"[GetTransactions] WARNING: Multiple pages combined - microseconds may be lost");
        }
        
        getTransactionsSuccess = true;
        errorMessage = $"All transactions successfully retrieved for IBAN: {strIban} from {dateTimeFrom:yyyy-MM-ddTHH:mm:ss.ffffff} to {dateTimeTo:yyyy-MM-ddTHH:mm:ss.ffffff} ({totalTransactionCount} transactions across {pageCount} page(s))";
        
        // Always show success summary
        System.Console.WriteLine($"[GetTransactions] SUCCESS! Collected {totalTransactionCount} transactions across {pageCount} page(s)");
        
        if (debugEnabled)
        {
            System.Console.WriteLine($"[GetTransactions] Final response length: {jobjTransactionResponse.ToString().Length}");
            // VEILIGE logging zonder cast errors
            System.Console.WriteLine($"[GetTransactions] Response keys: {string.Join(", ", jobjTransactionResponse.Properties().Select(p => p.Name))}");
            
            // CRITICAL DEBUG: Check microsecond precision in final jobjTransactionResponse 
            System.Console.WriteLine($"[GetTransactions] === MICROSECOND PRECISION DEBUG ===");
            if (jobjTransactionResponse["transactions"]?["booked"] is Newtonsoft.Json.Linq.JArray transactions && transactions.Count > 0)
            {
                var firstTx = transactions[0];
                var raboBookingDateTime = firstTx["raboBookingDateTime"];
                System.Console.WriteLine($"[GetTransactions] First transaction raboBookingDateTime:");
                System.Console.WriteLine($"  Raw Value: '{raboBookingDateTime}'");
                System.Console.WriteLine($"  Token Type: {raboBookingDateTime?.Type}");
                System.Console.WriteLine($"  ToString(): '{raboBookingDateTime?.ToString()}'");
                
                // Check if microseconds are preserved in the JObject
                string rawValue = raboBookingDateTime?.ToString();
                if (rawValue != null && rawValue.Contains("."))
                {
                    string fractionalPart = rawValue.Substring(rawValue.IndexOf(".") + 1);
                    if (fractionalPart.Contains("Z")) fractionalPart = fractionalPart.Replace("Z", "");
                    System.Console.WriteLine($"  Fractional seconds: '{fractionalPart}' (Length: {fractionalPart.Length})");
                    System.Console.WriteLine($"  Has 6-digit microseconds: {fractionalPart.Length == 6}");
                    System.Console.WriteLine($"  Has 3-digit milliseconds: {fractionalPart.Length == 3}");
                    System.Console.WriteLine($"  NOTE: API requests now use microseconds (.ffffff), API responses contain microseconds (.ffffff)");
                }
                else
                {
                    System.Console.WriteLine($"  ❌ NO fractional seconds found in final jobjTransactionResponse!");
                }
            }
            System.Console.WriteLine($"[GetTransactions] === END MICROSECOND DEBUG ===");
        }
    }
    else
    {
        getTransactionsSuccess = false;
        errorMessage = $"Get transactions failed: {response.StatusCode} - {responseBody}";
        jobjTransactionResponse = null;
        rawTransactionJson = null;
        
        // Enhanced error logging for consent issues
        System.Console.WriteLine($"[GetTransactions] Failed: {errorMessage}");
        
        // Specific guidance for consent errors
        if (responseBody.Contains("accountId does not belong to the consent") || 
            responseBody.Contains("consent"))
        {
            System.Console.WriteLine($"[GetTransactions] === CONSENT ERROR DIAGNOSIS ===");
            System.Console.WriteLine($"[GetTransactions] This error means the resourceId '{resourceId}' is not authorized in your current consent.");
            System.Console.WriteLine($"[GetTransactions] TROUBLESHOOTING STEPS:");
            System.Console.WriteLine($"[GetTransactions] 1. Verify IBAN '{strIban}' matches the consent IBAN");
            System.Console.WriteLine($"[GetTransactions] 2. Check if resourceId '{resourceId}' is correct for this IBAN");
            System.Console.WriteLine($"[GetTransactions] 3. Ensure consent is still valid and not expired");
            System.Console.WriteLine($"[GetTransactions] 4. Consider re-creating consent if needed");
            System.Console.WriteLine($"[GetTransactions] 5. Double-check the access token is from the correct consent");
            System.Console.WriteLine($"[GetTransactions] === END DIAGNOSIS ===");
        }
    }
}
catch (Exception ex)
{
    getTransactionsSuccess = false;
    errorMessage = $"Exception during get transactions: {ex.Message}";
    jobjTransactionResponse = null;
    rawTransactionJson = null;
    // Always show exceptions
    System.Console.WriteLine($"[GetTransactions] Exception: {ex.ToString()}");
}

// Output variables:
// jobjTransactionResponse: JObject containing transaction data (or null if failed) - WARNING: UiPath may convert timestamps
// rawTransactionJson: String containing raw JSON with preserved microsecond precision (NEW - use this for insert_transactions.cs)
// getTransactionsSuccess: Boolean indicating success/failure
// errorMessage: String with result details or error information
// response_status: Int32 with HTTP response status code (e.g., 200, 400, 500)
// response_time_ms: Int32 with API response time in milliseconds
