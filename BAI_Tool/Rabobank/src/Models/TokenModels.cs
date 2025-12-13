using Newtonsoft.Json;

namespace RabobankBAI.Models;

/// <summary>
/// OAuth2 token data model
/// </summary>
public class TokenData
{
    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonProperty("token_type")]
    public string? TokenType { get; set; }

    [JsonProperty("expires_in")]
    public int? ExpiresIn { get; set; }

    [JsonProperty("refresh_token_expires_in")]
    public int? RefreshTokenExpiresIn { get; set; }

    [JsonProperty("scope")]
    public string? Scope { get; set; }

    [JsonProperty("consented_on")]
    public long? ConsentedOn { get; set; }

    [JsonProperty("metadata")]
    public object? Metadata { get; set; }

    [JsonProperty("retrieved_at")]
    public long RetrievedAt { get; set; }

    /// <summary>
    /// Gets the access token expiry time
    /// </summary>
    public DateTime? AccessTokenExpiryTime
    {
        get
        {
            if (ExpiresIn.HasValue && RetrievedAt > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(RetrievedAt).DateTime.AddSeconds(ExpiresIn.Value);
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the refresh token expiry time
    /// </summary>
    public DateTime? RefreshTokenExpiryTime
    {
        get
        {
            if (RefreshTokenExpiresIn.HasValue && RetrievedAt > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(RetrievedAt).DateTime.AddSeconds(RefreshTokenExpiresIn.Value);
            }
            return null;
        }
    }

    /// <summary>
    /// Checks if the access token is expired or close to expiry
    /// </summary>
    public bool IsAccessTokenExpired(int bufferMinutes = 5)
    {
        var expiryTime = AccessTokenExpiryTime;
        if (expiryTime.HasValue)
        {
            return DateTime.UtcNow.AddMinutes(bufferMinutes) >= expiryTime.Value;
        }
        
        // If no expiry info, consider expired after 1 hour
        var retrievedTime = DateTimeOffset.FromUnixTimeSeconds(RetrievedAt).DateTime;
        return DateTime.UtcNow >= retrievedTime.AddHours(1);
    }

    /// <summary>
    /// Checks if the refresh token is expired or close to expiry
    /// </summary>
    public bool IsRefreshTokenExpired(int bufferMinutes = 5)
    {
        var expiryTime = RefreshTokenExpiryTime;
        if (expiryTime.HasValue)
        {
            return DateTime.UtcNow.AddMinutes(bufferMinutes) >= expiryTime.Value;
        }
        
        // If no expiry info, assume it doesn't expire (or has long expiry)
        return false;
    }
}

/// <summary>
/// Token operation result
/// </summary>
public class TokenResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public TokenData? TokenData { get; set; }
    public string? ErrorMessage { get; set; }
    public TokenOperationType OperationType { get; set; }
    public Exception? Exception { get; set; }

    public static TokenResult Successful(string accessToken, TokenData tokenData, TokenOperationType operationType)
    {
        return new TokenResult
        {
            Success = true,
            AccessToken = accessToken,
            TokenData = tokenData,
            OperationType = operationType
        };
    }

    public static TokenResult Failed(string errorMessage, TokenOperationType operationType, Exception? exception = null)
    {
        return new TokenResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            OperationType = operationType,
            Exception = exception
        };
    }
}

/// <summary>
/// Token validation result
/// </summary>
public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public bool NeedsRefresh { get; set; }
    public bool NeedsNewTokens { get; set; }
    public string? Reason { get; set; }
    public TimeSpan? TimeUntilExpiry { get; set; }
}

/// <summary>
/// Types of token operations
/// </summary>
public enum TokenOperationType
{
    NoAction,
    AccessTokenRefresh,
    FullRefresh,
    AuthorizationCodeExchange,
    FallbackExchange,
    ValidationOnly,
    Error
}

/// <summary>
/// OAuth2 token request models
/// </summary>
public class TokenRequest
{
    public string GrantType { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    public string? Code { get; set; }
    public string? RefreshToken { get; set; }
    public string? RedirectUri { get; set; }
}

/// <summary>
/// OAuth2 token response model
/// </summary>
public class TokenResponse
{
    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonProperty("token_type")]
    public string? TokenType { get; set; }

    [JsonProperty("expires_in")]
    public int? ExpiresIn { get; set; }

    [JsonProperty("refresh_token_expires_in")]
    public int? RefreshTokenExpiresIn { get; set; }

    [JsonProperty("scope")]
    public string? Scope { get; set; }

    [JsonProperty("consented_on")]
    public long? ConsentedOn { get; set; }

    [JsonProperty("metadata")]
    public object? Metadata { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("error_description")]
    public string? ErrorDescription { get; set; }
}