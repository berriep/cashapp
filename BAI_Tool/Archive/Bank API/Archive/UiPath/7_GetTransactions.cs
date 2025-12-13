// UiPath Invoke Code Script - Get Account Transactions
// Input Arguments: jobjTokens (JObject, In), jobjApiSettings (JObject, In), jobjAccounts (JObject, In), strIban (String, In), strDateTimeFrom (String, In), strDateTimeTo (String, In)
// Output Arguments: jobjTransactionResponse (JObject, Out), getTransactionsSuccess (Boolean, Out), errorMessage (String, Out)
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates
// Input Format: ISO 8601 DateTime strings, bijvoorbeeld: "2025-09-01T09:30:15.500Z" of "2025-09-01T09:30:15.500"

try
{
    if (jobjTokens == null || jobjTokens["access_token"] == null)
    {
        throw new Exception("No valid access token available");
    }
    
    if (string.IsNullOrEmpty(strIban))
    {
        throw new Exception("IBAN is required for transaction request");
    }
    
    if (jobjAccounts == null || jobjAccounts["AccountIds"] == null)
    {
        throw new Exception("No account tokens available");
    }
    
    // DateTime range is verplicht - geen defaults
    if (string.IsNullOrEmpty(strDateTimeFrom) || string.IsNullOrEmpty(strDateTimeTo))
    {
        throw new Exception("Both strDateTimeFrom and strDateTimeTo are required for transaction request");
    }
    
    // Parse string inputs naar DateTime
    DateTime dateTimeFrom;
    DateTime dateTimeTo;
    
    try
    {
        dateTimeFrom = DateTime.Parse(strDateTimeFrom);
        System.Console.WriteLine($"[GetTransactions] Parsed dateTimeFrom: {strDateTimeFrom} -> {dateTimeFrom:yyyy-MM-ddTHH:mm:ss.fff}");
    }
    catch (Exception ex)
    {
        throw new Exception($"Invalid dateTimeFrom format: {strDateTimeFrom}. Expected format: 2025-09-01T09:30:15.500 or 2025-09-01T09:30:15.500Z. Error: {ex.Message}");
    }
    
    try
    {
        dateTimeTo = DateTime.Parse(strDateTimeTo);
        System.Console.WriteLine($"[GetTransactions] Parsed dateTimeTo: {strDateTimeTo} -> {dateTimeTo:yyyy-MM-ddTHH:mm:ss.fff}");
    }
    catch (Exception ex)
    {
        throw new Exception($"Invalid dateTimeTo format: {strDateTimeTo}. Expected format: 2025-09-01T09:30:15.500 or 2025-09-01T09:30:15.500Z. Error: {ex.Message}");
    }
    
    // Valideer dat de datetime range logisch is
    if (dateTimeFrom > dateTimeTo)
    {
        throw new Exception("DateTimeFrom cannot be later than dateTimeTo");
    }
    
    // Informeer over API limieten, maar pas NIET automatisch aan
    DateTime maxDateBack = DateTime.Now.AddMonths(-15);
    if (dateTimeFrom < maxDateBack)
    {
        System.Console.WriteLine($"[GetTransactions] INFO: dateTimeFrom ({dateTimeFrom:yyyy-MM-dd}) is more than 15 months ago. API limit is: {maxDateBack:yyyy-MM-dd}");
        System.Console.WriteLine($"[GetTransactions] WARNING: This may result in empty results due to API limitations");
    }
    
    if (dateTimeTo > DateTime.Now)
    {
        System.Console.WriteLine($"[GetTransactions] INFO: dateTimeTo ({dateTimeTo:yyyy-MM-dd}) is in the future");
        System.Console.WriteLine($"[GetTransactions] WARNING: This may result in unexpected results");
    }
    
    System.Console.WriteLine($"[GetTransactions] Using datetime range: {dateTimeFrom:yyyy-MM-ddTHH:mm:ss.fff} to {dateTimeTo:yyyy-MM-ddTHH:mm:ss.fff}");
    
    // Build query parameters voor API URL - API verwacht ISO 8601 DateTime format in UTC
    // Converteer naar UTC en format met milliseconden
    string dateTimeFromISO = dateTimeFrom.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    string dateTimeToISO = dateTimeTo.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    string queryParams = $"bookingStatus=booked&dateFrom={dateTimeFromISO}&dateTo={dateTimeToISO}";
    
    System.Console.WriteLine($"[GetTransactions] Using ISO DateTime format (UTC): {dateTimeFromISO} to {dateTimeToISO}");
    
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
    
    // Build API URL for transactions with account-id and date range (needs to be defined BEFORE using it for signature)
    string apiBaseUrl = jobjApiSettings["ApiBaseUrl"].ToString();
    string transactionsUrl = $"{apiBaseUrl}/accounts/{accountToken}/transactions?{queryParams}";
    
    // Generate signature using private key
    var uri = new System.Uri(transactionsUrl);
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
    
    System.Console.WriteLine($"[GetTransactions] Generated signature headers successfully");
    
    System.Console.WriteLine($"[GetTransactions] Making request to: {transactionsUrl}");
    System.Console.WriteLine($"[GetTransactions] Using IBAN: {strIban}");
    System.Console.WriteLine($"[GetTransactions] Using datetime range: {dateTimeFrom:yyyy-MM-ddTHH:mm:ss.fff} to {dateTimeTo:yyyy-MM-ddTHH:mm:ss.fff}");
    System.Console.WriteLine($"[GetTransactions] Using account token: {accountToken.Substring(0, 10)}...");
    
    // Make GET request for transactions
    System.Console.WriteLine($"[GetTransactions] STEP 1: About to call httpClient.GetAsync()");
    var response = httpClient.GetAsync(transactionsUrl).Result;
    System.Console.WriteLine($"[GetTransactions] STEP 2: Received response with status: {response.StatusCode}");
    
    string responseBody = response.Content.ReadAsStringAsync().Result;
    System.Console.WriteLine($"[GetTransactions] STEP 3: Read response body, length: {responseBody.Length}");
    
    if (response.IsSuccessStatusCode)
    {
        System.Console.WriteLine($"[GetTransactions] STEP 4: Response was successful, parsing JSON...");
        
        // Parse the JSON response
        jobjTransactionResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
        System.Console.WriteLine($"[GetTransactions] STEP 5: JSON parsed successfully");
        
        getTransactionsSuccess = true;
        
        errorMessage = $"Transactions successfully retrieved for IBAN: {strIban} from {dateTimeFrom:yyyy-MM-ddTHH:mm:ss.fff} to {dateTimeTo:yyyy-MM-ddTHH:mm:ss.fff}";
        
        System.Console.WriteLine($"[GetTransactions] Success! Response length: {responseBody.Length}");
        
        // VEILIGE logging zonder cast errors
        System.Console.WriteLine($"[GetTransactions] Response keys: {string.Join(", ", jobjTransactionResponse.Properties().Select(p => p.Name))}");
        
        // Log eerste 500 characters van response voor debugging
        string responsePreview = responseBody.Length > 500 ? responseBody.Substring(0, 500) + "..." : responseBody;
        System.Console.WriteLine($"[GetTransactions] Response preview: {responsePreview}");
    }
    else
    {
        getTransactionsSuccess = false;
        errorMessage = $"Get transactions failed: {response.StatusCode} - {responseBody}";
        jobjTransactionResponse = null;
        System.Console.WriteLine($"[GetTransactions] Failed: {errorMessage}");
    }
}
catch (Exception ex)
{
    getTransactionsSuccess = false;
    errorMessage = $"Exception during get transactions: {ex.Message}";
    jobjTransactionResponse = null;
    System.Console.WriteLine($"[GetTransactions] Exception: {ex.ToString()}");
}

// Output variables:
// jobjTransactionResponse: JObject containing transaction data (or null if failed)
// getTransactionsSuccess: Boolean indicating success/failure
// errorMessage: String with result details or error information
