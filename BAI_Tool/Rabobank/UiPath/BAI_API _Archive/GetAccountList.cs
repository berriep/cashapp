// UiPath Invoke Code Script - Get Account List (Production - Flexible Certificate Loading)
// Input Arguments: 
//   jobjTokens (JObject, In) - Token object with access_token
//   jobjApiSettings (JObject, In) - Production API configuration object
// Output Arguments: 
//   jobjAccountResponse (JObject, Out), getAccountsSuccess (Boolean, Out), 
//   errorMessage (String, Out), jobjAccountIds (JObject, Out)
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates
//
// Required jobjApiSettings structure (Production PFX - RECOMMENDED):
// {
//   "ApiBaseUrl": "https://api.rabobank.nl/openapi/payments/insight",
//   "PfxCertificatePath": "C:\\Users\\uipath\\BAI\\Certificate Production\\api_rabo_PRODUCTION.pfx",
//   "PfxPassword": "EPsBGVecfWa9jGb6Bd3B",
//   "ClientId": "b53367ddf9c83b9e0dde68d2fe1d88a1"
// }
// NOTE: Use api_rabo_PRODUCTION.pfx (exported from server with correct certificate)
// Alternative PEM structure (if separate private key available):
// {
//   "ApiBaseUrl": "https://api.rabobank.nl/openapi/payments/insight", 
//   "CertificatePath": "C:\\Path\\To\\api_rabobank_centerparcs_nl.pem",
//   "PrivateKeyPath": "C:\\Path\\To\\api_rabobank_centerparcs_nl.key",
//   "ClientId": "b53367ddf9c83b9e0dde68d2fe1d88a1"
// }
// IMPORTANT: PfxPassword must be retrieved from KeePass entry named "Rabo_cert pfx"
// Note: Production certificate Serial: 044CB89A91BC353709DAC64E493AD451
//
// This script retrieves all consented payment accounts for the authenticated customer

try
{
    if (jobjTokens == null || jobjTokens["access_token"] == null)
    {
        throw new Exception("No valid access token available");
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
    System.Console.WriteLine($"[GetAccountList] Loaded PFX certificate:");
    System.Console.WriteLine($"  Subject: {cert.Subject}");
    System.Console.WriteLine($"  Issuer: {cert.Issuer}");
    System.Console.WriteLine($"  Serial Number: {cert.SerialNumber}");
    System.Console.WriteLine($"  Valid From: {cert.NotBefore}");
    System.Console.WriteLine($"  Valid To: {cert.NotAfter}");
    System.Console.WriteLine($"  Has Private Key: {cert.HasPrivateKey}");
    System.Console.WriteLine($"  Thumbprint: {cert.Thumbprint}");
    
    // CRITICAL: Verify this is the correct production certificate
    string expectedSerial = "044CB89A91BC353709DAC64E493AD451";
    if (cert.SerialNumber.ToUpper() != expectedSerial)
    {
        System.Console.WriteLine($"[GetAccountList] ❌ WRONG CERTIFICATE LOADED!");
        System.Console.WriteLine($"  Expected Serial: {expectedSerial}");
        System.Console.WriteLine($"  Actual Serial:   {cert.SerialNumber}");
        System.Console.WriteLine($"  This certificate is NOT registered in Developer Portal!");
        throw new Exception($"Wrong certificate loaded. Expected production certificate with Serial: {expectedSerial}, but got: {cert.SerialNumber}. Use api_rabo_PRODUCTION.pfx instead of test certificate.");
    }
    else
    {
        System.Console.WriteLine($"[GetAccountList] ✅ CORRECT production certificate verified!");
        System.Console.WriteLine($"  Certificate Serial matches registered certificate in Developer Portal");
    }
    
    // Check certificate validity
    if (System.DateTime.Now < cert.NotBefore || System.DateTime.Now > cert.NotAfter)
    {
        System.Console.WriteLine($"[GetAccountList] WARNING: Certificate is expired or not yet valid!");
    }
    
    handler.ClientCertificates.Add(cert);
    // Production: Use proper certificate validation (no custom callback)
    
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
    
    // Generate signature using private key from PFX certificate
    var uri = new System.Uri(accountsUrl);
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
    
    System.Console.WriteLine($"[GetAccountList] Certificate Serial Number (hex): {hexSerial}");
    System.Console.WriteLine($"[GetAccountList] Certificate Serial Number (int): {keyId}");
    System.Console.WriteLine($"[GetAccountList] Signature generated successfully");
    System.Console.WriteLine($"[GetAccountList] Signing string: {signingString.Replace("\n", "\\n")}");
    System.Console.WriteLine($"[GetAccountList] Signature header: {signatureHeader}");
    
    // Add all required headers for production API (no more diagnostic testing needed)
    httpClient.DefaultRequestHeaders.Add("X-IBM-Client-Id", jobjApiSettings["ClientId"].ToString());
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    httpClient.DefaultRequestHeaders.Add("Date", dateHeader);
    httpClient.DefaultRequestHeaders.Add("Digest", digest);
    httpClient.DefaultRequestHeaders.Add("X-Request-ID", xRequestId);
    httpClient.DefaultRequestHeaders.Add("Signature", signatureHeader);
    httpClient.DefaultRequestHeaders.Add("Signature-Certificate", certificateBase64);
    
    // Increase timeout to 60 seconds
    httpClient.Timeout = TimeSpan.FromSeconds(60);
    
    System.Console.WriteLine($"[GetAccountList] Making production API request to: {accountsUrl}");
    System.Console.WriteLine($"[GetAccountList] Headers: X-IBM-Client-Id, Accept, Authorization (Bearer), Date, Digest, X-Request-ID, Signature, Signature-Certificate");
    
    // Make GET request for account list with full signature headers
    var response = httpClient.GetAsync(accountsUrl).Result;
    string responseBody = response.Content.ReadAsStringAsync().Result;
    
    System.Console.WriteLine($"[GetAccountList] API Response Status: {response.StatusCode}");
    System.Console.WriteLine($"[GetAccountList] API Response: {responseBody}");
    
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