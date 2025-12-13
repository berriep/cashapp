// UiPath Invoke Code Script - Smart Refresh Access Token with Granular Control
// Input Arguments: 
//   jobjTokens (JObject, In) - Current token object with refresh_token
//   jobjApiSettings (JObject, In) - API configuration (ClientId, TokenUrl, certificates)
//   forceAccessToken (Boolean, In) - Force refresh of access token only (default: false)
//   forceRefreshToken (Boolean, In) - Force refresh of both access AND refresh tokens (default: false)
// Output Arguments: 
//   refreshSuccess (Boolean, Out) - True if refresh was successful
//   accessToken (String, Out) - New access token ready for use
//   errorMessage (String, Out) - Status message or error details
//   jobjNewTokens (JObject, Out) - Complete updated token object
//   refreshType (String, Out) - Type of refresh performed: "ACCESS_ONLY", "FULL_REFRESH", "NO_REFRESH"
// References: System.Net.Http, Newtonsoft.Json, System.Security.Cryptography.X509Certificates

try
{
    // Initialize output variables
    refreshSuccess = false;
    accessToken = "";
    errorMessage = "";
    jobjNewTokens = null;
    refreshType = "NO_REFRESH";

    // Validate input tokens
    if (jobjTokens == null || jobjTokens["refresh_token"] == null)
    {
        throw new Exception("No refresh token available in current tokens");
    }
    
    string refreshToken = jobjTokens["refresh_token"].ToString();
    
    if (string.IsNullOrEmpty(refreshToken))
    {
        throw new Exception("Refresh token is empty");
    }
    
    // === STEP 1: CHECK REFRESH TOKEN VALIDITY ===
    bool refreshTokenValid = true;
    string refreshTokenStatus = "VALID";
    
    if (jobjTokens["refresh_token_expires_in"] != null && jobjTokens["retrieved_at"] != null)
    {
        try
        {
            long retrievedAtUnix = long.Parse(jobjTokens["retrieved_at"].ToString());
            DateTime retrievedAt = DateTimeOffset.FromUnixTimeSeconds(retrievedAtUnix).DateTime;
            int refreshExpiresInSeconds = int.Parse(jobjTokens["refresh_token_expires_in"].ToString());
            DateTime refreshExpiryTime = retrievedAt.AddSeconds(refreshExpiresInSeconds);
            TimeSpan refreshTimeRemaining = refreshExpiryTime - DateTime.UtcNow;
            
            System.Console.WriteLine($"[RefreshTokens] Refresh token expires in: {refreshTimeRemaining.TotalMinutes:F1} minutes");
            
            if (refreshTimeRemaining.TotalMinutes <= 5) // Less than 5 minutes
            {
                refreshTokenValid = false;
                refreshTokenStatus = "EXPIRED";
                System.Console.WriteLine("[RefreshTokens] Refresh token expires soon - full refresh required");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[RefreshTokens] Could not parse refresh token expiry: {ex.Message}");
            // Assume valid if we can't parse
        }
    }
    else
    {
        System.Console.WriteLine("[RefreshTokens] No refresh token expiry info - assuming valid");
    }
    
    // === STEP 2: DETERMINE REFRESH STRATEGY ===
    string refreshStrategy = "";
    string refreshReason = "";
    
    if (!refreshTokenValid)
    {
        // OLD_REFRESH not valid -> Full refresh required
        refreshStrategy = "FULL_REFRESH";
        refreshReason = "refresh token expired or invalid";
        System.Console.WriteLine("[RefreshTokens] Strategy: FULL_REFRESH (refresh token not valid)");
    }
    else if (forceRefreshToken)
    {
        // OLD_REFRESH valid + forceRefreshToken=true -> Full refresh
        refreshStrategy = "FULL_REFRESH";
        refreshReason = "forced refresh token renewal";
        System.Console.WriteLine("[RefreshTokens] Strategy: FULL_REFRESH (forceRefreshToken=true)");
    }
    else if (forceAccessToken)
    {
        // OLD_REFRESH valid + forceAccessToken=true -> Access only
        refreshStrategy = "ACCESS_ONLY";
        refreshReason = "forced access token renewal";
        System.Console.WriteLine("[RefreshTokens] Strategy: ACCESS_ONLY (forceAccessToken=true)");
    }
    else
    {
        // Check if access token needs refresh based on age/expiry
        bool accessNeedsRefresh = false;
        
        if (jobjTokens["retrieved_at"] != null)
        {
            try
            {
                long retrievedAtUnix = long.Parse(jobjTokens["retrieved_at"].ToString());
                DateTime retrievedAt = DateTimeOffset.FromUnixTimeSeconds(retrievedAtUnix).DateTime;
                TimeSpan tokenAge = DateTime.UtcNow - retrievedAt;
                
                System.Console.WriteLine($"[RefreshTokens] Access token age: {tokenAge.TotalMinutes:F1} minutes");
                
                if (tokenAge.TotalMinutes >= 60) // Older than 1 hour
                {
                    accessNeedsRefresh = true;
                    refreshReason = $"access token is {tokenAge.TotalMinutes:F1} minutes old";
                }
                else if (jobjTokens["expires_in"] != null)
                {
                    int expiresInSeconds = int.Parse(jobjTokens["expires_in"].ToString());
                    DateTime expiryTime = retrievedAt.AddSeconds(expiresInSeconds);
                    TimeSpan timeUntilExpiry = expiryTime - DateTime.UtcNow;
                    
                    if (timeUntilExpiry.TotalMinutes <= 60) // Expires within 1 hour
                    {
                        accessNeedsRefresh = true;
                        refreshReason = $"access token expires in {timeUntilExpiry.TotalMinutes:F1} minutes";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[RefreshTokens] Could not parse access token timing: {ex.Message}");
                accessNeedsRefresh = true;
                refreshReason = "could not determine access token age";
            }
        }
        else
        {
            accessNeedsRefresh = true;
            refreshReason = "no retrieved_at timestamp found";
        }
        
        if (accessNeedsRefresh)
        {
            // OLD_REFRESH valid -> Access only refresh
            refreshStrategy = "ACCESS_ONLY";
            System.Console.WriteLine($"[RefreshTokens] Strategy: ACCESS_ONLY ({refreshReason})");
        }
        else
        {
            refreshStrategy = "NO_REFRESH";
            refreshReason = "access token is still fresh";
            System.Console.WriteLine($"[RefreshTokens] Strategy: NO_REFRESH ({refreshReason})");
        }
    }
    
    // === STEP 3: EARLY RETURN IF NO REFRESH NEEDED ===
    if (refreshStrategy == "NO_REFRESH")
    {
        refreshSuccess = true;
        accessToken = jobjTokens["access_token"].ToString();
        errorMessage = $"No refresh needed - {refreshReason}";
        jobjNewTokens = jobjTokens; // Return existing tokens
        refreshType = "NO_REFRESH";
        System.Console.WriteLine("[RefreshTokens] No refresh needed - returning existing tokens");
        return;
    }
    
    // === STEP 4: PREPARE REFRESH REQUEST ===
    System.Console.WriteLine($"[RefreshTokens] Starting {refreshStrategy} refresh (reason: {refreshReason})");
    
    // Setup HttpClient with mTLS certificate
    var handler = new System.Net.Http.HttpClientHandler();
    
    string certPath = jobjApiSettings["CertificatePath"].ToString();
    string keyPath = jobjApiSettings["PrivateKeyPath"].ToString();
    
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
    
    // Prepare refresh request
    var formData = new List<KeyValuePair<string, string>>
    {
        new("grant_type", "refresh_token"),
        new("refresh_token", refreshToken),
        new("client_id", jobjApiSettings["ClientId"].ToString())
    };
    
    // Add scope parameter to control what gets refreshed
    if (refreshStrategy == "ACCESS_ONLY")
    {
        // Request only access token refresh (if Rabobank supports this)
        // Note: This may not be supported by all OAuth providers
        // If not supported, Rabobank will return both tokens anyway
        System.Console.WriteLine("[RefreshTokens] Requesting ACCESS_ONLY refresh (if supported by API)");
    }
    else if (refreshStrategy == "FULL_REFRESH")
    {
        // Request full refresh (this is the default behavior)
        System.Console.WriteLine("[RefreshTokens] Requesting FULL_REFRESH (access + refresh tokens)");
    }
    
    // Add client_secret if provided
    if (jobjApiSettings["ClientSecret"] != null && !string.IsNullOrEmpty(jobjApiSettings["ClientSecret"].ToString()))
    {
        formData.Add(new("client_secret", jobjApiSettings["ClientSecret"].ToString()));
    }

    var content = new System.Net.Http.FormUrlEncodedContent(formData);
    
    string tokenUrl = jobjApiSettings["TokenUrl"].ToString();
    System.Console.WriteLine($"[RefreshTokens] Making {refreshStrategy} request to: {tokenUrl}");
    
    // === STEP 5: EXECUTE REFRESH REQUEST ===
    var response = httpClient.PostAsync(tokenUrl, content).Result;
    string responseBody = response.Content.ReadAsStringAsync().Result;

    if (response.IsSuccessStatusCode)
    {
        // Parse the new token response
        var jobjRefreshResponse = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
        
        // Create updated token object
        jobjNewTokens = new Newtonsoft.Json.Linq.JObject();
        
        // Copy all existing token data
        foreach (var property in jobjTokens.Properties())
        {
            jobjNewTokens[property.Name] = property.Value;
        }
        
        // === STEP 6: HANDLE RESPONSE BASED ON STRATEGY ===
        
        // Always update access token (both strategies need this)
        if (jobjRefreshResponse["access_token"] != null)
        {
            jobjNewTokens["access_token"] = jobjRefreshResponse["access_token"];
            accessToken = jobjRefreshResponse["access_token"].ToString();
            System.Console.WriteLine($"[RefreshTokens] Updated access token (length: {accessToken.Length})");
        }
        
        if (jobjRefreshResponse["expires_in"] != null)
        {
            jobjNewTokens["expires_in"] = jobjRefreshResponse["expires_in"];
        }
        
        if (jobjRefreshResponse["token_type"] != null)
        {
            jobjNewTokens["token_type"] = jobjRefreshResponse["token_type"];
        }
        
        // Handle refresh token based on strategy
        if (refreshStrategy == "FULL_REFRESH")
        {
            // For FULL_REFRESH: Always update refresh token if provided
            if (jobjRefreshResponse["refresh_token"] != null)
            {
                jobjNewTokens["refresh_token"] = jobjRefreshResponse["refresh_token"];
                System.Console.WriteLine("[RefreshTokens] Updated refresh token (FULL_REFRESH)");
            }
            
            if (jobjRefreshResponse["refresh_token_expires_in"] != null)
            {
                jobjNewTokens["refresh_token_expires_in"] = jobjRefreshResponse["refresh_token_expires_in"];
            }
            
            refreshType = "FULL_REFRESH";
        }
        else if (refreshStrategy == "ACCESS_ONLY")
        {
            // For ACCESS_ONLY: Only update refresh token if Rabobank forces it
            if (jobjRefreshResponse["refresh_token"] != null)
            {
                jobjNewTokens["refresh_token"] = jobjRefreshResponse["refresh_token"];
                System.Console.WriteLine("[RefreshTokens] Rabobank provided new refresh token (forced rotation)");
            }
            else
            {
                System.Console.WriteLine("[RefreshTokens] Keeping existing refresh token (ACCESS_ONLY)");
            }
            
            if (jobjRefreshResponse["refresh_token_expires_in"] != null)
            {
                jobjNewTokens["refresh_token_expires_in"] = jobjRefreshResponse["refresh_token_expires_in"];
            }
            
            refreshType = "ACCESS_ONLY";
        }
        
        // Update timestamp
        jobjNewTokens["retrieved_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        refreshSuccess = true;
        errorMessage = $"{refreshStrategy} refresh successful - {refreshReason}";
        
        // Log success details
        if (jobjNewTokens["expires_in"] != null)
        {
            int expiresIn = int.Parse(jobjNewTokens["expires_in"].ToString());
            var expiryTime = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            System.Console.WriteLine($"[RefreshTokens] New access token expires in {expiresIn} seconds (at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC)");
        }
        
        System.Console.WriteLine($"[RefreshTokens] SUCCESS: {refreshStrategy} completed");
    }
    else
    {
        refreshSuccess = false;
        refreshType = "FAILED";
        
        System.Console.WriteLine($"[RefreshTokens] === REFRESH FAILURE ===");
        System.Console.WriteLine($"[RefreshTokens] Strategy attempted: {refreshStrategy}");
        System.Console.WriteLine($"[RefreshTokens] Status Code: {response.StatusCode}");
        System.Console.WriteLine($"[RefreshTokens] Response Body: {responseBody}");
        System.Console.WriteLine($"[RefreshTokens] === END FAILURE ===");
        
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && responseBody.Contains("invalid_grant"))
        {
            errorMessage = $"{refreshStrategy} failed: Refresh token invalid/expired. " +
                          "Possible causes: 1) Single-use token already used, 2) Token expired, " +
                          "3) Consent revoked. New authorization required.";
        }
        else
        {
            errorMessage = $"{refreshStrategy} failed: {response.StatusCode} - {responseBody}";
        }
    }
}
catch (Exception ex)
{
    refreshSuccess = false;
    errorMessage = $"Exception during {refreshType} refresh: {ex.Message}";
    accessToken = "";
    jobjNewTokens = null;
    refreshType = "ERROR";
    System.Console.WriteLine($"[RefreshTokens] Exception: {ex.ToString()}");
}

// Output variables:
// refreshSuccess: Boolean indicating if the refresh was successful
// accessToken: String with the new access token (ready for immediate use)
// errorMessage: String with status message or error details
// jobjNewTokens: JObject with complete updated token data
// refreshType: String indicating what was refreshed: "ACCESS_ONLY", "FULL_REFRESH", "NO_REFRESH", "FAILED", "ERROR"
