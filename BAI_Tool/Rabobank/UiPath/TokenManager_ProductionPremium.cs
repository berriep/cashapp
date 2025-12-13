// UiPath Invoke Code Script - Complete Token Manager (Refresh + Exchange)
// Input Arguments: 
//   jobjTokens (JObject, In)
//   jobjApiSettings (JObject, In) - Must contain PfxCertificatePath, PfxPassword for production
//   forceRefreshToken (Boolean, In) - True = use auth code, False = refresh existing tokens
//   strAuthCode (String, In)
//   cacheMinutes (Int32, In) - Minutes to cache tokens before refresh (default: 1200 = 20 hours, 0 = always refresh)
// Output Arguments: 
//   refreshSuccess (Boolean, Out)
//   accessToken (String, Out)
//   errorMessage (String, Out)
//   jobjNewTokens (JObject, Out)
//   refreshType (String, Out)
//   statusCode (Int32, Out)
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates
//
// PRODUCTION NOTES:
// - PfxCertificatePath and PfxPassword are REQUIRED in jobjApiSettings
// - TokenUrl should be: https://oauth.rabobank.nl/openapi/oauth2-premium/token (not sandbox)
// - ApiBaseUrl should be: https://api.rabobank.nl/openapi/payments/insight (not sandbox)
// - DangerousAcceptAnyServerCertificateValidator is removed for production security
// - PEM certificate fallback is removed - only PFX certificates supported

try
{
    // Initialize output variables
    refreshSuccess = false;
    accessToken = "";
    errorMessage = "";
    jobjNewTokens = null;
    refreshType = "NO_ACTION";
    statusCode = 0;   // <--- Added
    
    // Set default cache minutes if not provided or invalid
    // Default: 20 hours (1200 minutes) - suitable for daily refresh patterns
    int cacheMinutesToUse = (cacheMinutes <= 0) ? 1200 : cacheMinutes;
    
    // === TOKEN CACHING LOGIC ===
    bool hasValidTokens = (jobjTokens != null && jobjTokens["refresh_token"] != null && !string.IsNullOrEmpty(jobjTokens["refresh_token"].ToString()));
    bool hasAuthCode = !string.IsNullOrEmpty(strAuthCode);
    bool canReuseTokens = false;
    
    // Check if we can reuse existing tokens (skip API call for efficiency)
    if (!forceRefreshToken && hasValidTokens && cacheMinutesToUse > 0)
    {
        // Check if tokens are still fresh enough to reuse
        if (jobjTokens["access_token"] != null && !string.IsNullOrEmpty(jobjTokens["access_token"].ToString()) &&
            jobjTokens["retrieved_at"] != null && jobjTokens["expires_in"] != null)
        {
            try
            {
                long retrievedAt = jobjTokens["retrieved_at"].Value<long>();
                int expiresIn = jobjTokens["expires_in"].Value<int>();
                
                DateTime tokenRetrievedTime = DateTimeOffset.FromUnixTimeSeconds(retrievedAt).DateTime;
                DateTime tokenExpiryTime = tokenRetrievedTime.AddSeconds(expiresIn);
                DateTime now = DateTime.UtcNow;
                
                // Calculate how many minutes until token expires
                double minutesUntilExpiry = (tokenExpiryTime - now).TotalMinutes;
                double minutesSinceRetrieved = (now - tokenRetrievedTime).TotalMinutes;
                
                System.Console.WriteLine($"[TOKEN_CACHE] Token retrieved: {tokenRetrievedTime:yyyy-MM-dd HH:mm:ss} UTC");
                System.Console.WriteLine($"[TOKEN_CACHE] Token expires: {tokenExpiryTime:yyyy-MM-dd HH:mm:ss} UTC");
                System.Console.WriteLine($"[TOKEN_CACHE] Minutes since retrieved: {minutesSinceRetrieved:F1}");
                System.Console.WriteLine($"[TOKEN_CACHE] Minutes until expiry: {minutesUntilExpiry:F1}");
                System.Console.WriteLine($"[TOKEN_CACHE] Cache threshold: {cacheMinutesToUse} minutes");
                
                // Reuse tokens if: 
                // 1. Token was retrieved less than cacheMinutes ago, AND
                // 2. Token still has at least 5 minutes before expiry (safety margin)
                if (minutesSinceRetrieved < cacheMinutesToUse && minutesUntilExpiry > 5)
                {
                    canReuseTokens = true;
                    System.Console.WriteLine($"[TOKEN_CACHE] Reusing existing tokens (fresh enough)");
                    System.Console.WriteLine($"[TOKEN_CACHE] CRITICAL: NO API CALL - returning exact same tokens");
                    System.Console.WriteLine($"[TOKEN_CACHE] Input retrieved_at: {retrievedAt} ({tokenRetrievedTime:yyyy-MM-dd HH:mm:ss})");
                    
                    // Return existing tokens without API call - preserve ALL original values
                    jobjNewTokens = new Newtonsoft.Json.Linq.JObject();
                    foreach (var property in jobjTokens.Properties())
                        jobjNewTokens[property.Name] = property.Value;
                    
                    // Verify the timestamp is preserved
                    System.Console.WriteLine($"[TOKEN_CACHE] Output retrieved_at: {jobjNewTokens["retrieved_at"]} (should be identical!)");
                    
                    accessToken = jobjTokens["access_token"].ToString();
                    refreshSuccess = true;
                    refreshType = "REUSE_EXISTING";
                    errorMessage = $"Reused existing tokens (retrieved {minutesSinceRetrieved:F1} minutes ago, expires in {minutesUntilExpiry:F1} minutes)";
                    statusCode = 200; // Success without API call
                }
                else
                {
                    System.Console.WriteLine($"[TOKEN_CACHE] Tokens need refresh (too old or close to expiry)");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[TOKEN_CACHE] Warning: Could not parse token timestamps: {ex.Message}");
                // Continue with normal refresh logic
            }
        }
        else
        {
            System.Console.WriteLine($"[TOKEN_CACHE] Missing required token fields for caching");
        }
    }
    else if (forceRefreshToken)
    {
        System.Console.WriteLine($"[TOKEN_CACHE] Forced refresh - skipping cache");
    }
    else if (cacheMinutesToUse == 0)
    {
        System.Console.WriteLine($"[TOKEN_CACHE] Caching disabled (cacheMinutes=0) - always refresh");
    }
    
    // If we can reuse tokens, return early
    if (canReuseTokens)
    {
        return; // Exit early with reused tokens
    }
    
    // === REFRESH LOGIC (only executed if tokens cannot be reused) ===
    System.Console.WriteLine($"[TOKEN_REFRESH] Proceeding with token refresh...");
    
    string tokenStrategy = "";
    string strategyReason = "";
    
    // SCENARIO 1: forceRefreshToken = True → Use Authorization Code (fresh start)
    if (forceRefreshToken)
    {
        if (!hasAuthCode)
            throw new Exception("forceRefreshToken=true but no authorization code provided. Cannot proceed.");
        
        tokenStrategy = "EXCHANGE_AUTH_CODE";
        strategyReason = "forceRefreshToken=true - using authorization code for fresh tokens";
    }
    // SCENARIO 2: forceRefreshToken = False → Regular Token Refresh
    else
    {
        if (!hasValidTokens)
            throw new Exception("forceRefreshToken=false but no valid refresh tokens available. Cannot proceed.");
        
        tokenStrategy = "REFRESH_TOKENS";
        strategyReason = "forceRefreshToken=false - regular refresh of existing tokens";
    }
    
    // === STEP 2: EXECUTE STRATEGY ===
    if (tokenStrategy == "EXCHANGE_AUTH_CODE")
    {
        var handler = new System.Net.Http.HttpClientHandler();
        
        // Load PFX certificate (required for production)
        if (jobjApiSettings["PfxCertificatePath"] == null || string.IsNullOrEmpty(jobjApiSettings["PfxCertificatePath"].ToString()))
        {
            throw new Exception("PfxCertificatePath is required in jobjApiSettings for production use");
        }
        
        string pfxPath = jobjApiSettings["PfxCertificatePath"].ToString();
        string pfxPassword = jobjApiSettings["PfxPassword"]?.ToString() ?? "";
        
        if (!System.IO.File.Exists(pfxPath))
        {
            throw new Exception($"PFX certificate file not found: {pfxPath}");
        }
        
        var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(pfxPath, pfxPassword, 
            System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable | 
            System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.PersistKeySet);
        
        handler.ClientCertificates.Add(cert);
        // Production: Remove DangerousAcceptAnyServerCertificateValidator for proper certificate validation

        using var httpClient = new System.Net.Http.HttpClient(handler);
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", strAuthCode),
            new("client_id", jobjApiSettings["ClientId"].ToString()),
            new("redirect_uri", jobjApiSettings["RedirectUri"]?.ToString() ?? "http://localhost:8080/callback")
        };
        if (jobjApiSettings["ClientSecret"] != null && !string.IsNullOrEmpty(jobjApiSettings["ClientSecret"].ToString()))
            formData.Add(new("client_secret", jobjApiSettings["ClientSecret"].ToString()));

        var content = new System.Net.Http.FormUrlEncodedContent(formData);
        string tokenUrl = jobjApiSettings["TokenUrl"].ToString();
        
        // Debug logging
        System.Console.WriteLine($"[DEBUG] Token URL: {tokenUrl}");
        System.Console.WriteLine($"[DEBUG] Grant Type: authorization_code");
        System.Console.WriteLine($"[DEBUG] Client ID: {jobjApiSettings["ClientId"]}");
        System.Console.WriteLine($"[DEBUG] Auth Code Length: {strAuthCode?.Length ?? 0}");
        System.Console.WriteLine($"[DEBUG] Auth Code First 50 chars: {(string.IsNullOrEmpty(strAuthCode) ? "NULL" : strAuthCode.Substring(0, Math.Min(50, strAuthCode.Length)))}");
        System.Console.WriteLine($"[DEBUG] Redirect URI: {jobjApiSettings["RedirectUri"]?.ToString() ?? "http://localhost:8080/callback"}");
        System.Console.WriteLine($"[DEBUG] Certificate Subject: {cert.Subject}");
        System.Console.WriteLine($"[DEBUG] Certificate Valid From: {cert.NotBefore}");
        System.Console.WriteLine($"[DEBUG] Certificate Valid To: {cert.NotAfter}");
        
        var response = httpClient.PostAsync(tokenUrl, content).Result;
        string responseBody = response.Content.ReadAsStringAsync().Result;
        statusCode = (int)response.StatusCode;   // <--- Added
        
        // Debug response
        System.Console.WriteLine($"[DEBUG] Response Status: {response.StatusCode}");
        System.Console.WriteLine($"[DEBUG] Response Body: {responseBody}");

        if (response.IsSuccessStatusCode)
        {
            var jobjTokenResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
            jobjNewTokens = new Newtonsoft.Json.Linq.JObject();

            if (jobjTokenResponse["access_token"] != null)
            {
                jobjNewTokens["access_token"] = jobjTokenResponse["access_token"];
                accessToken = jobjTokenResponse["access_token"].ToString();
            }
            if (jobjTokenResponse["refresh_token"] != null)
                jobjNewTokens["refresh_token"] = jobjTokenResponse["refresh_token"];
            if (jobjTokenResponse["expires_in"] != null)
                jobjNewTokens["expires_in"] = jobjTokenResponse["expires_in"];
            if (jobjTokenResponse["refresh_token_expires_in"] != null)
                jobjNewTokens["refresh_token_expires_in"] = jobjTokenResponse["refresh_token_expires_in"];
            if (jobjTokenResponse["token_type"] != null)
                jobjNewTokens["token_type"] = jobjTokenResponse["token_type"];
            if (jobjTokenResponse["scope"] != null)
                jobjNewTokens["scope"] = jobjTokenResponse["scope"];
            if (jobjTokenResponse["consented_on"] != null)
                jobjNewTokens["consented_on"] = jobjTokenResponse["consented_on"];
            if (jobjTokenResponse["metadata"] != null)
                jobjNewTokens["metadata"] = jobjTokenResponse["metadata"];

            jobjNewTokens["retrieved_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            refreshSuccess = true;
            refreshType = "EXCHANGE_AUTH_CODE";
            errorMessage = "Fresh tokens obtained via authorization code exchange";
        }
        else
        {
            refreshSuccess = false;
            refreshType = "EXCHANGE_FAILED";
            errorMessage = $"Authorization code exchange failed: {response.StatusCode} - {responseBody}";
        }
    }
    else if (tokenStrategy == "REFRESH_TOKENS")
    {
        string refreshToken = jobjTokens["refresh_token"].ToString();
        var handler = new System.Net.Http.HttpClientHandler();
        
        // Load PFX certificate (required for production)
        if (jobjApiSettings["PfxCertificatePath"] == null || string.IsNullOrEmpty(jobjApiSettings["PfxCertificatePath"].ToString()))
        {
            throw new Exception("PfxCertificatePath is required in jobjApiSettings for production use");
        }
        
        string pfxPath = jobjApiSettings["PfxCertificatePath"].ToString();
        string pfxPassword = jobjApiSettings["PfxPassword"]?.ToString() ?? "";
        
        if (!System.IO.File.Exists(pfxPath))
        {
            throw new Exception($"PFX certificate file not found: {pfxPath}");
        }
        
        var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(pfxPath, pfxPassword, 
            System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable | 
            System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.PersistKeySet);
        
        handler.ClientCertificates.Add(cert);
        // Production: Remove DangerousAcceptAnyServerCertificateValidator for proper certificate validation

        using var httpClient = new System.Net.Http.HttpClient(handler);
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken),
            new("client_id", jobjApiSettings["ClientId"].ToString())
        };
        if (jobjApiSettings["ClientSecret"] != null && !string.IsNullOrEmpty(jobjApiSettings["ClientSecret"].ToString()))
            formData.Add(new("client_secret", jobjApiSettings["ClientSecret"].ToString()));

        var content = new System.Net.Http.FormUrlEncodedContent(formData);
        string tokenUrl = jobjApiSettings["TokenUrl"].ToString();

        var response = httpClient.PostAsync(tokenUrl, content).Result;
        string responseBody = response.Content.ReadAsStringAsync().Result;
        statusCode = (int)response.StatusCode;   // <--- Added

        if (response.IsSuccessStatusCode)
        {
            var jobjRefreshResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
            jobjNewTokens = new Newtonsoft.Json.Linq.JObject();
            foreach (var property in jobjTokens.Properties())
                jobjNewTokens[property.Name] = property.Value;

            if (jobjRefreshResponse["access_token"] != null)
            {
                jobjNewTokens["access_token"] = jobjRefreshResponse["access_token"];
                accessToken = jobjRefreshResponse["access_token"].ToString();
            }
            if (jobjRefreshResponse["expires_in"] != null)
                jobjNewTokens["expires_in"] = jobjRefreshResponse["expires_in"];
            if (jobjRefreshResponse["token_type"] != null)
                jobjNewTokens["token_type"] = jobjRefreshResponse["token_type"];
            if (jobjRefreshResponse["refresh_token"] != null)
                jobjNewTokens["refresh_token"] = jobjRefreshResponse["refresh_token"];
            if (jobjRefreshResponse["refresh_token_expires_in"] != null)
                jobjNewTokens["refresh_token_expires_in"] = jobjRefreshResponse["refresh_token_expires_in"];

            jobjNewTokens["retrieved_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            refreshSuccess = true;
            refreshType = "REFRESH_TOKENS";
            errorMessage = "Token refresh successful";
        }
        else
        {
            refreshSuccess = false;
            refreshType = "REFRESH_FAILED";
            errorMessage = $"Refresh failed: {response.StatusCode} - {responseBody}";
            statusCode = (int)response.StatusCode;   // <--- Added

            // Example fallback logic
            if (hasAuthCode)
            {
                var fallbackHandler = new System.Net.Http.HttpClientHandler();
                
                // Load PFX certificate for fallback (reuse existing variables)
                var fallbackCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    jobjApiSettings["PfxCertificatePath"].ToString(), 
                    jobjApiSettings["PfxPassword"]?.ToString() ?? "", 
                    System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable | 
                    System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.PersistKeySet);
                
                fallbackHandler.ClientCertificates.Add(fallbackCert);
                // Production: Remove DangerousAcceptAnyServerCertificateValidator for proper certificate validation

                using var fallbackHttpClient = new System.Net.Http.HttpClient(fallbackHandler);
                fallbackHttpClient.Timeout = TimeSpan.FromSeconds(30);

                var fallbackFormData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "authorization_code"),
                    new("code", strAuthCode),
                    new("client_id", jobjApiSettings["ClientId"].ToString()),
                    new("redirect_uri", jobjApiSettings["RedirectUri"]?.ToString() ?? "http://localhost:8080/callback")
                };
                if (jobjApiSettings["ClientSecret"] != null && !string.IsNullOrEmpty(jobjApiSettings["ClientSecret"].ToString()))
                    fallbackFormData.Add(new("client_secret", jobjApiSettings["ClientSecret"].ToString()));

                var fallbackContent = new System.Net.Http.FormUrlEncodedContent(fallbackFormData);
                string fallbackTokenUrl = jobjApiSettings["TokenUrl"].ToString();

                var fallbackResponse = fallbackHttpClient.PostAsync(fallbackTokenUrl, fallbackContent).Result;
                string fallbackResponseBody = fallbackResponse.Content.ReadAsStringAsync().Result;
                statusCode = (int)fallbackResponse.StatusCode;   // <--- Added

                if (fallbackResponse.IsSuccessStatusCode)
                {
                    var jobjFallbackResponse = Newtonsoft.Json.Linq.JObject.Parse(fallbackResponseBody);
                    jobjNewTokens = new Newtonsoft.Json.Linq.JObject();

                    if (jobjFallbackResponse["access_token"] != null)
                    {
                        jobjNewTokens["access_token"] = jobjFallbackResponse["access_token"];
                        accessToken = jobjFallbackResponse["access_token"].ToString();
                    }
                    if (jobjFallbackResponse["refresh_token"] != null)
                        jobjNewTokens["refresh_token"] = jobjFallbackResponse["refresh_token"];
                    if (jobjFallbackResponse["expires_in"] != null)
                        jobjNewTokens["expires_in"] = jobjFallbackResponse["expires_in"];
                    if (jobjFallbackResponse["refresh_token_expires_in"] != null)
                        jobjNewTokens["refresh_token_expires_in"] = jobjFallbackResponse["refresh_token_expires_in"];
                    if (jobjFallbackResponse["token_type"] != null)
                        jobjNewTokens["token_type"] = jobjFallbackResponse["token_type"];
                    if (jobjFallbackResponse["scope"] != null)
                        jobjNewTokens["scope"] = jobjFallbackResponse["scope"];

                    jobjNewTokens["retrieved_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    refreshSuccess = true;
                    refreshType = "FALLBACK_EXCHANGE_AUTH_CODE";
                    errorMessage = "Fallback auth code exchange successful";
                }
                else
                {
                    errorMessage = $"Fallback auth code exchange failed: {fallbackResponse.StatusCode} - {fallbackResponseBody}";
                }
            }
        }
    }
}
catch (Exception ex)
{
    refreshSuccess = false;
    errorMessage = $"Exception during token management: {ex.Message}";
    accessToken = "";
    jobjNewTokens = null;
    refreshType = "ERROR";
    statusCode = -1;   // <--- Added
}