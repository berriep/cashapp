// UiPath Invoke Code Script - Enhanced Token Validation with Refresh Strategy
// Input Arguments: jobjTokens (JObject, In)
// Output Arguments: isTokenValid (Boolean, Out), errorMessage (String, Out), accessTokenExpired (Boolean, Out), refreshTokenExpired (Boolean, Out), actionNeeded (String, Out)
// References: Newtonsoft.Json

try 
{
    accessTokenExpired = false;
    refreshTokenExpired = false;
    actionNeeded = "NONE";
    
    // Check if tokens object exists
    if (jobjTokens == null || jobjTokens["access_token"] == null)
    {
        isTokenValid = false;
        errorMessage = "No tokens found or access_token missing";
        actionNeeded = "NEW_AUTH_REQUIRED";
        System.Console.WriteLine($"[CheckTokenValidity] No tokens found");
        return;
    }
    
    string accessToken = jobjTokens["access_token"].ToString();
    DateTime currentTime = DateTime.UtcNow;
    
    // Basic access token validation
    if (string.IsNullOrEmpty(accessToken) || accessToken.Trim().Length <= 10)
    {
        isTokenValid = false;
        errorMessage = "Access token is empty or too short";
        actionNeeded = "NEW_AUTH_REQUIRED";
        return;
    }
    
    // Check if we have refresh token
    if (jobjTokens["refresh_token"] == null || string.IsNullOrEmpty(jobjTokens["refresh_token"].ToString()))
    {
        isTokenValid = false;
        errorMessage = "Access token exists but no refresh token found";
        actionNeeded = "NEW_AUTH_REQUIRED";
        return;
    }
    
    // ===== ACCESS TOKEN EXPIRY CHECK =====
    bool accessNeedsRefresh = false;
    
    if (jobjTokens["expires_in"] != null)
    {
        try
        {
            int expiresInSeconds = int.Parse(jobjTokens["expires_in"].ToString());
            
            // If we have token_created_at, use it. Otherwise, assume token was just created
            DateTime tokenCreatedAt;
            if (jobjTokens["token_created_at"] != null)
            {
                tokenCreatedAt = DateTime.Parse(jobjTokens["token_created_at"].ToString());
            }
            else
            {
                // Assume token was created now (this happens on fresh tokens)
                tokenCreatedAt = DateTime.UtcNow;
                System.Console.WriteLine($"[CheckTokenValidity] No token_created_at found - assuming token is fresh");
            }
            
            DateTime accessTokenExpiryTime = tokenCreatedAt.AddSeconds(expiresInSeconds);
            TimeSpan accessTimeRemaining = accessTokenExpiryTime - currentTime;
            
            System.Console.WriteLine($"[CheckTokenValidity] Access token created: {tokenCreatedAt:yyyy-MM-dd HH:mm:ss}");
            System.Console.WriteLine($"[CheckTokenValidity] Access token expires: {accessTokenExpiryTime:yyyy-MM-dd HH:mm:ss}");
            System.Console.WriteLine($"[CheckTokenValidity] Access time remaining: {accessTimeRemaining.TotalMinutes:F1} minutes");
            
            // Access token is expired or expires soon (within 5 minutes)
            if (currentTime >= accessTokenExpiryTime || accessTimeRemaining.TotalMinutes <= 5)
            {
                accessTokenExpired = true;
                accessNeedsRefresh = true;
                System.Console.WriteLine($"[CheckTokenValidity] Access token expired or expires soon - refresh needed");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[CheckTokenValidity] Warning: Could not parse access token expiry: {ex.Message}");
            // For fresh tokens without timestamp, assume they're valid
            accessNeedsRefresh = false;
            accessTokenExpired = false;
        }
    }
    else
    {
        System.Console.WriteLine($"[CheckTokenValidity] No expires_in found - assuming token is valid");
        accessNeedsRefresh = false;
        accessTokenExpired = false;
    }
    
    // ===== REFRESH TOKEN EXPIRY CHECK =====
    bool refreshTokenValid = true;
    
    // Check refresh token expiry using refresh_token_expires_in (Rabobank field)
    if (jobjTokens["refresh_token_expires_in"] != null)
    {
        try
        {
            int refreshExpiresInSeconds = int.Parse(jobjTokens["refresh_token_expires_in"].ToString());
            
            // If we have token_created_at, use it. Otherwise, assume token was just created
            DateTime tokenCreatedAt;
            if (jobjTokens["token_created_at"] != null)
            {
                tokenCreatedAt = DateTime.Parse(jobjTokens["token_created_at"].ToString());
            }
            else
            {
                // Assume token was created now (this happens on fresh tokens)
                tokenCreatedAt = DateTime.UtcNow;
                System.Console.WriteLine($"[CheckTokenValidity] No token_created_at for refresh - assuming token is fresh");
            }
            
            DateTime refreshTokenExpiryTime = tokenCreatedAt.AddSeconds(refreshExpiresInSeconds);
            TimeSpan refreshTimeRemaining = refreshTokenExpiryTime - currentTime;
            
            System.Console.WriteLine($"[CheckTokenValidity] Refresh token expires: {refreshTokenExpiryTime:yyyy-MM-dd HH:mm:ss}");
            System.Console.WriteLine($"[CheckTokenValidity] Refresh time remaining: {refreshTimeRemaining.TotalDays:F1} days");
            
            // Refresh token is expired
            if (currentTime >= refreshTokenExpiryTime)
            {
                refreshTokenExpired = true;
                refreshTokenValid = false;
                System.Console.WriteLine($"[CheckTokenValidity] Refresh token has expired - new authentication required");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[CheckTokenValidity] Warning: Could not parse refresh token expiry: {ex.Message}");
            // Assume refresh token is still valid if we can't parse
        }
    }
    else
    {
        System.Console.WriteLine($"[CheckTokenValidity] No refresh_token_expires_in found - assuming refresh token is valid");
    }
    
    // Check consent expiry using consented_on (Unix timestamp)
    if (jobjTokens["consented_on"] != null)
    {
        try
        {
            // consented_on is Unix timestamp - convert to DateTime
            long consentedOnUnix = long.Parse(jobjTokens["consented_on"].ToString());
            DateTime consentedOnTime = DateTimeOffset.FromUnixTimeSeconds(consentedOnUnix).DateTime;
            
            // Rabobank consents typically expire after 90 days
            DateTime consentExpiryTime = consentedOnTime.AddDays(90);
            TimeSpan consentTimeRemaining = consentExpiryTime - currentTime;
            
            System.Console.WriteLine($"[CheckTokenValidity] Consented on: {consentedOnTime:yyyy-MM-dd HH:mm:ss}");
            System.Console.WriteLine($"[CheckTokenValidity] Consent expires: {consentExpiryTime:yyyy-MM-dd HH:mm:ss}");
            System.Console.WriteLine($"[CheckTokenValidity] Consent time remaining: {consentTimeRemaining.TotalDays:F1} days");
            
            // Consent is expired - both tokens are invalid
            if (currentTime >= consentExpiryTime)
            {
                isTokenValid = false;
                refreshTokenExpired = true;
                errorMessage = $"Consent has expired {Math.Abs(consentTimeRemaining.TotalDays):F1} days ago";
                actionNeeded = "NEW_AUTH_REQUIRED";
                return;
            }
            
            // Warn if consent expires soon (within 7 days)
            if (consentTimeRemaining.TotalDays <= 7)
            {
                System.Console.WriteLine($"[CheckTokenValidity] Warning: Consent expires in {consentTimeRemaining.TotalDays:F1} days");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[CheckTokenValidity] Warning: Could not parse consented_on timestamp: {ex.Message}");
        }
    }
    
    // ===== DETERMINE ACTION NEEDED =====
    
    if (refreshTokenExpired || !refreshTokenValid)
    {
        // Refresh token is expired - need full re-authentication
        isTokenValid = false;
        errorMessage = "Refresh token expired - new authentication required";
        actionNeeded = "NEW_AUTH_REQUIRED";
    }
    else if (accessNeedsRefresh)
    {
        // Access token needs refresh, but refresh token is still valid
        isTokenValid = false; // Not currently valid, but refreshable
        errorMessage = "Access token expired or expires soon - refresh with refresh_token";
        actionNeeded = "REFRESH_ACCESS_TOKEN";
    }
    else
    {
        // Both tokens are currently valid
        isTokenValid = true;
        errorMessage = "Both access and refresh tokens are currently valid";
        actionNeeded = "NONE";
    }
    
    // Final logging
    System.Console.WriteLine($"[CheckTokenValidity] Final Assessment:");
    System.Console.WriteLine($"  - isTokenValid: {isTokenValid}");
    System.Console.WriteLine($"  - accessTokenExpired: {accessTokenExpired}");
    System.Console.WriteLine($"  - refreshTokenExpired: {refreshTokenExpired}");
    System.Console.WriteLine($"  - actionNeeded: {actionNeeded}");
    System.Console.WriteLine($"  - Message: {errorMessage}");
}
catch (Exception ex)
{
    isTokenValid = false;
    accessTokenExpired = true;
    refreshTokenExpired = false;
    actionNeeded = "ERROR";
    errorMessage = $"Token validation error: {ex.Message}";
    System.Console.WriteLine($"[CheckTokenValidity] Exception: {ex.ToString()}");
}

// Output variables:
// isTokenValid: Boolean - true if both tokens are currently valid
// errorMessage: String - detailed status message
// accessTokenExpired: Boolean - true if access token needs refresh
// refreshTokenExpired: Boolean - true if refresh token is expired
// actionNeeded: String - "NONE", "REFRESH_ACCESS_TOKEN", "NEW_AUTH_REQUIRED", or "ERROR"