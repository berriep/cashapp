// UiPath Invoke Code Script - Get Account List (Production - PEM Certificate + Private Key)
// Input Arguments: 
//   jobjTokens (JObject, In) - Token object with access_token
//   jobjApiSettings (JObject, In) - Production API configuration object
// Output Arguments: 
//   jobjAccountResponse (JObject, Out), getAccountsSuccess (Boolean, Out), 
//   errorMessage (String, Out), jobjAccountIds (JObject, Out)
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates
//
// Required jobjApiSettings structure (Production PEM):
// {
//   "ApiBaseUrl": "https://api.rabobank.nl/openapi/payments/insight",
//   "CertificatePath": "C:\\Path\\To\\api_rabobank_centerparcs_nl.pem",
//   "PrivateKeyPath": "C:\\Path\\To\\api_rabobank_centerparcs_nl.key",
//   "ClientId": "b53367ddf9c83b9e0dde68d2fe1d88a1"
// }
// Alternative PFX structure (if PEM extraction not possible):
// {
//   "ApiBaseUrl": "https://api.rabobank.nl/openapi/payments/insight",
//   "PfxCertificatePath": "C:\\Users\\uipath\\BAI\\Certificate Production\\api_rabo.pfx",
//   "PfxPassword": "GET_FROM_KEEPASS_Rabo_cert_pfx",
//   "ClientId": "b53367ddf9c83b9e0dde68d2fe1d88a1"
// }
// Note: Use production certificate (Serial: 044CB89A91BC353709DAC64E493AD451)

try
{
    if (jobjTokens == null || jobjTokens["access_token"] == null)
    {
        throw new Exception("No valid access token available");
    }
    
    string accessToken = jobjTokens["access_token"].ToString();
    
    // Setup HttpClient with mTLS certificate
    var handler = new System.Net.Http.HttpClientHandler();
    
    // Load certificate - try PEM first, fallback to PFX
    System.Security.Cryptography.X509Certificates.X509Certificate2 cert;
    
    if (jobjApiSettings["CertificatePath"] != null && jobjApiSettings["PrivateKeyPath"] != null)
    {
        // PEM Certificate + Private Key method
        string certPath = jobjApiSettings["CertificatePath"].ToString();
        string keyPath = jobjApiSettings["PrivateKeyPath"].ToString();
        
        if (!System.IO.File.Exists(certPath))
        {
            throw new Exception($"Certificate PEM file not found: {certPath}");
        }
        
        if (!System.IO.File.Exists(keyPath))
        {
            throw new Exception($"Private key file not found: {keyPath}");
        }
        
        try
        {
            // Read certificate PEM
            string certPem = System.IO.File.ReadAllText(certPath);
            
            // Read private key PEM
            string keyPem = System.IO.File.ReadAllText(keyPath);
            
            // Combine certificate and private key into single PEM for X509Certificate2
            string combinedPem = certPem + "\n" + keyPem;
            
            // Load certificate with private key
            cert = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(combinedPem);
            
            System.Console.WriteLine($"[GetAccountList] Loaded PEM certificate + private key successfully");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load PEM certificate and private key: {ex.Message}");
        }
    }
    else if (jobjApiSettings["PfxCertificatePath"] != null)
    {
        // PFX Certificate method (fallback)
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
        
        try
        {
            cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(pfxPath, pfxPassword, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.MachineKeySet | System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.PersistKeySet);
            System.Console.WriteLine($"[GetAccountList] Loaded PFX certificate successfully");
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new Exception($"Failed to load PFX certificate - check password from KeePass 'Rabo_cert pfx': {ex.Message}");
        }
    }
    else
    {
        throw new Exception("Either CertificatePath+PrivateKeyPath (PEM) or PfxCertificatePath+PfxPassword (PFX) must be provided in jobjApiSettings");
    }
    
    // Extended certificate debugging
    System.Console.WriteLine($"[GetAccountList] Certificate Details:");
    System.Console.WriteLine($"  Subject: {cert.Subject}");
    System.Console.WriteLine($"  Issuer: {cert.Issuer}");
    System.Console.WriteLine($"  Serial Number: {cert.SerialNumber}");
    System.Console.WriteLine($"  Valid From: {cert.NotBefore}");
    System.Console.WriteLine($"  Valid To: {cert.NotAfter}");
    System.Console.WriteLine($"  Has Private Key: {cert.HasPrivateKey}");
    System.Console.WriteLine($"  Thumbprint: {cert.Thumbprint}");
    
    // Verify this is the production certificate
    if (cert.SerialNumber.ToUpper() != "044CB89A91BC353709DAC64E493AD451")
    {
        System.Console.WriteLine($"[GetAccountList] WARNING: Certificate serial number does not match expected production certificate!");
        System.Console.WriteLine($"  Expected: 044CB89A91BC353709DAC64E493AD451");
        System.Console.WriteLine($"  Actual:   {cert.SerialNumber}");
        System.Console.WriteLine($"  This certificate may not be registered in the Developer Portal!");
    }
    else
    {
        System.Console.WriteLine($"[GetAccountList] ✓ CONFIRMED: Using correct production certificate");
    }
    
    // Check certificate validity
    if (System.DateTime.Now < cert.NotBefore || System.DateTime.Now > cert.NotAfter)
    {
        System.Console.WriteLine($"[GetAccountList] WARNING: Certificate is expired or not yet valid!");
    }
    
    if (!cert.HasPrivateKey)
    {
        throw new Exception("Certificate does not contain a private key - check certificate loading method");
    }
    
    handler.ClientCertificates.Add(cert);
    System.Console.WriteLine($"[GetAccountList] Client certificate added to handler");
    
    using var httpClient = new System.Net.Http.HttpClient(handler);
    
    // Set authorization header
    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    
    // Add required headers from API spec (ALL are required according to documentation)
    string dateHeader = System.DateTime.UtcNow.ToString("R"); // RFC1123 format
    string xRequestId = System.Guid.NewGuid().ToString();
    
    // Generate digest for empty body (GET request)
    using var sha512Digest = System.Security.Cryptography.SHA512.Create();
    byte[] digestHash = sha512Digest.ComputeHash(new byte[0]);
    string digest = "sha-512=" + System.Convert.ToBase64String(digestHash);
    
    // Build API URL for accounts list
    string apiBaseUrl = jobjApiSettings["ApiBaseUrl"].ToString();
    string accountsUrl = $"{apiBaseUrl}/accounts";
    
    // Generate signature using private key from certificate
    var uri = new System.Uri(accountsUrl);
    string urlPath = uri.PathAndQuery;
    
    // Build signing string (according to API spec: "date digest x-request-id")
    string signingString = $"date: {dateHeader}\ndigest: {digest}\nx-request-id: {xRequestId}";
    
    // Sign with RSA-SHA512 using certificate private key
    byte[] data = System.Text.Encoding.UTF8.GetBytes(signingString);
    byte[] signature;
    
    // Use AsymmetricAlgorithm which works across different .NET versions
    using (var privateKey = cert.PrivateKey)
    {
        if (privateKey == null)
        {
            throw new Exception("Could not extract private key from certificate");
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
    
    System.Console.WriteLine($"[GetAccountList] Certificate Serial Number (hex): {hexSerial}");
    System.Console.WriteLine($"[GetAccountList] Certificate Serial Number (int): {keyId}");
    System.Console.WriteLine($"[GetAccountList] Signature generated successfully");
    System.Console.WriteLine($"[GetAccountList] Signing string: {signingString.Replace("\n", "\\n")}");
    System.Console.WriteLine($"[GetAccountList] Signature header: {signatureHeader}");
    
    // DIAGNOSTIC TEST: Try minimal headers first to isolate certificate issue
    System.Console.WriteLine($"[GetAccountList] === DIAGNOSTIC TEST ===");
    System.Console.WriteLine($"[GetAccountList] Testing with minimal headers first...");
    
    httpClient.DefaultRequestHeaders.Add("X-IBM-Client-Id", jobjApiSettings["ClientId"].ToString());
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    
    // Increase timeout to 60 seconds
    httpClient.Timeout = TimeSpan.FromSeconds(60);
    
    System.Console.WriteLine($"[GetAccountList] Making diagnostic request to: {accountsUrl}");
    System.Console.WriteLine($"[GetAccountList] Headers: X-IBM-Client-Id, Accept, Authorization (Bearer)");
    
    // Make GET request for account list (diagnostic test)
    var response = httpClient.GetAsync(accountsUrl).Result;
    string responseBody = response.Content.ReadAsStringAsync().Result;
    
    System.Console.WriteLine($"[GetAccountList] === DIAGNOSTIC RESULT ===");
    System.Console.WriteLine($"[GetAccountList] Status: {response.StatusCode}");
    System.Console.WriteLine($"[GetAccountList] Response: {responseBody}");
    
    // If minimal headers fail with same error, try full signature headers
    if (!response.IsSuccessStatusCode && responseBody.Contains("Invalid client certificate"))
    {
        System.Console.WriteLine($"[GetAccountList] === FULL SIGNATURE TEST ===");
        System.Console.WriteLine($"[GetAccountList] Certificate issue confirmed. Trying full signature headers...");
        
        // Clear existing headers and add full signature headers
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("X-IBM-Client-Id", jobjApiSettings["ClientId"].ToString());
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        httpClient.DefaultRequestHeaders.Add("Date", dateHeader);
        httpClient.DefaultRequestHeaders.Add("Digest", digest);
        httpClient.DefaultRequestHeaders.Add("X-Request-ID", xRequestId);
        httpClient.DefaultRequestHeaders.Add("Signature", signatureHeader);
        httpClient.DefaultRequestHeaders.Add("Signature-Certificate", certificateBase64);
        
        System.Console.WriteLine($"[GetAccountList] Making request with full signature headers...");
        response = httpClient.GetAsync(accountsUrl).Result;
        responseBody = response.Content.ReadAsStringAsync().Result;
        
        System.Console.WriteLine($"[GetAccountList] Full signature result: {response.StatusCode}");
        System.Console.WriteLine($"[GetAccountList] Full signature response: {responseBody}");
    }
    
    if (response.IsSuccessStatusCode)
    {
        // Parse the JSON response
        jobjAccountResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
        getAccountsSuccess = true;
        errorMessage = "Account list successfully retrieved";
        
        System.Console.WriteLine($"[GetAccountList] Success! Response length: {responseBody.Length}");
        
        // Create AccountIds mapping object for easy lookup in other scripts
        jobjAccountIds = new Newtonsoft.Json.Linq.JObject();
        var accountIdsSection = new Newtonsoft.Json.Linq.JObject();
        
        // Extract accounts and create IBAN -> AccountId mapping
        if (jobjAccountResponse["accounts"] != null)
        {
            var accounts = jobjAccountResponse["accounts"] as Newtonsoft.Json.Linq.JArray;
            System.Console.WriteLine($"[GetAccountList] Found {accounts.Count} account(s)");
            
            foreach (var account in accounts)
            {
                string accountId = account["resourceId"]?.ToString();
                string iban = account["iban"]?.ToString();
                string accountName = account["name"]?.ToString();
                string currency = account["currency"]?.ToString();
                
                if (!string.IsNullOrEmpty(accountId) && !string.IsNullOrEmpty(iban))
                {
                    accountIdsSection[iban] = accountId;
                    System.Console.WriteLine($"[GetAccountList] Account mapping: {iban} -> {accountId.Substring(0, 10)}... ({accountName}, {currency})");
                }
                else
                {
                    System.Console.WriteLine($"[GetAccountList] WARNING: Account missing IBAN or resourceId: {account}");
                }
            }
            
            jobjAccountIds["AccountIds"] = accountIdsSection;
            System.Console.WriteLine($"[GetAccountList] Created AccountIds mapping with {accountIdsSection.Count} entries");
        }
        else
        {
            System.Console.WriteLine($"[GetAccountList] WARNING: No 'accounts' array found in response");
            jobjAccountIds["AccountIds"] = new Newtonsoft.Json.Linq.JObject();
        }
        
        // Log response structure for debugging
        System.Console.WriteLine($"[GetAccountList] Response keys: {string.Join(", ", jobjAccountResponse.Properties().Select(p => p.Name))}");
        
        // Log first 500 characters of response for debugging
        string responsePreview = responseBody.Length > 500 ? responseBody.Substring(0, 500) + "..." : responseBody;
        System.Console.WriteLine($"[GetAccountList] Response preview: {responsePreview}");
    }
    else
    {
        getAccountsSuccess = false;
        errorMessage = $"Get account list failed: {response.StatusCode} - {responseBody}";
        jobjAccountResponse = null;
        jobjAccountIds = null;
        System.Console.WriteLine($"[GetAccountList] Failed: {errorMessage}");
    }
}
catch (Exception ex)
{
    getAccountsSuccess = false;
    errorMessage = $"Exception during get account list: {ex.Message}";
    jobjAccountResponse = null;
    jobjAccountIds = null;
    System.Console.WriteLine($"[GetAccountList] Exception: {ex.ToString()}");
}

// Output variables:
// jobjAccountResponse: JObject containing full account list response (or null if failed)
// getAccountsSuccess: Boolean indicating success/failure
// errorMessage: String with result details or error information
// jobjAccountIds: JObject with IBAN -> AccountId mapping for use in other scripts
//   Structure: { "AccountIds": { "NL12RABO0123456789": "account-token-here", ... } }