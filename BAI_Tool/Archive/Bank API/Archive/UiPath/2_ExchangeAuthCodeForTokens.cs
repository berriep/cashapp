// UiPath Invoke Code Script - Exchange Auth Code for Tokens
// Input Arguments: strAuthCode (String, In), jobjApiSettings (JObject, In)
// Output Arguments: jobjTokens (JObject, Out), tokenExchangeSuccess (Boolean, Out), errorMessage (String, Out)
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates

try
{
    if (string.IsNullOrEmpty(strAuthCode?.Trim()))
    {
        throw new Exception("Auth code is empty or null");
    }

    // Setup HttpClient with mTLS certificate (like TokenManager constructor)
    var handler = new System.Net.Http.HttpClientHandler();
    
    // Load certificate from PEM files
    string certPath = jobjApiSettings["CertificatePath"].ToString();
    string keyPath = jobjApiSettings["PrivateKeyPath"].ToString();
    
    // Check if certificate files exist
    if (!System.IO.File.Exists(certPath))
        throw new Exception($"Certificate file not found: {certPath}");
    if (!System.IO.File.Exists(keyPath))
        throw new Exception($"Private key file not found: {keyPath}");
    
    string certPem = System.IO.File.ReadAllText(certPath);
    string keyPem = System.IO.File.ReadAllText(keyPath);
    var cert = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(certPem, keyPem);
    
    handler.ClientCertificates.Add(cert);
    handler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    
    using var httpClient = new System.Net.Http.HttpClient(handler);
    httpClient.Timeout = TimeSpan.FromSeconds(30);
    
    // Prepare form data (exactly like ExchangeAuthCodeForTokens method)
    var formData = new List<KeyValuePair<string, string>>
    {
        new("grant_type", "authorization_code"),
        new("code", strAuthCode.Trim()),
        new("client_id", jobjApiSettings["ClientId"].ToString()),
        new("client_secret", jobjApiSettings["ClientSecret"].ToString()),
        new("redirect_uri", "http://localhost:8080/callback")
    };

    var content = new System.Net.Http.FormUrlEncodedContent(formData);
    
    // Make token request
    string tokenUrl = jobjApiSettings["TokenUrl"].ToString();
    System.Console.WriteLine($"[ExchangeAuthCode] Making request to: {tokenUrl}");
    
    var response = httpClient.PostAsync(tokenUrl, content).Result;
    string responseBody = response.Content.ReadAsStringAsync().Result;

    if (response.IsSuccessStatusCode)
    {
        // Parse the JSON response
        jobjTokens = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
        tokenExchangeSuccess = true;
        errorMessage = "Auth code successfully exchanged for tokens";
        
        System.Console.WriteLine($"[ExchangeAuthCode] Success! Access token length: {jobjTokens["access_token"]?.ToString()?.Length ?? 0}");
        System.Console.WriteLine($"[ExchangeAuthCode] NEW TOKENS READY FOR SAVING!");
    }
    else
    {
        tokenExchangeSuccess = false;
        errorMessage = $"Token exchange failed: {response.StatusCode} - {responseBody}";
        System.Console.WriteLine($"[ExchangeAuthCode] Failed: {errorMessage}");
    }
}
catch (Exception ex)
{
    tokenExchangeSuccess = false;
    errorMessage = $"Exception during token exchange: {ex.Message}";
    System.Console.WriteLine($"[ExchangeAuthCode] Exception: {ex.ToString()}");
}

// Output variables:
// jobjTokens: JObject containing the new tokens (or null if failed)
// tokenExchangeSuccess: Boolean indicating success/failure
// errorMessage: String with result details or error information
