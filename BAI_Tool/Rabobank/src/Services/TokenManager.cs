using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabobankBAI.Configuration;
using RabobankBAI.Core;
using RabobankBAI.Models;
using RabobankBAI.Utils;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace RabobankBAI.Services;

/// <summary>
/// Robust OAuth2 token manager for Rabobank BAI API
/// </summary>
public class TokenManager : ITokenManager
{
    private readonly ILogger<TokenManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly ICertificateManager _certificateManager;

    public TokenManager(
        ILogger<TokenManager> logger,
        HttpClient httpClient,
        ICertificateManager certificateManager)
    {
        _logger = logger;
        _httpClient = httpClient;
        _certificateManager = certificateManager;
    }

    /// <summary>
    /// Ensures a valid access token is available, refreshing if necessary
    /// </summary>
    public async Task<TokenResult> EnsureValidTokenAsync(ClientConfiguration clientConfig, bool forceRefresh = false)
    {
        try
        {
            _logger.LogInformation("Ensuring valid token for client: {ClientName}", clientConfig.ClientName);

            // Load existing tokens
            var existingTokens = await LoadTokensAsync(clientConfig.ClientName);
            
            if (existingTokens == null)
            {
                _logger.LogWarning("No existing tokens found for client: {ClientName}", clientConfig.ClientName);
                return TokenResult.Failed("No tokens available. Authorization code exchange required.", TokenOperationType.Error);
            }

            // Validate existing tokens
            var validation = ValidateTokens(existingTokens);
            
            if (!forceRefresh && validation.IsValid && !validation.NeedsRefresh)
            {
                _logger.LogInformation("Existing tokens are valid, no refresh needed");
                return TokenResult.Successful(existingTokens.AccessToken!, existingTokens, TokenOperationType.NoAction);
            }

            // Determine refresh strategy
            if (validation.NeedsNewTokens || string.IsNullOrEmpty(existingTokens.RefreshToken))
            {
                _logger.LogWarning("Refresh token invalid or missing. New authorization required.");
                return TokenResult.Failed("Refresh token invalid. New authorization code required.", TokenOperationType.Error);
            }

            // Perform token refresh
            var refreshResult = await RefreshTokenAsync(clientConfig, existingTokens, forceRefresh);
            
            if (refreshResult.Success && refreshResult.TokenData != null)
            {
                await SaveTokensAsync(clientConfig.ClientName, refreshResult.TokenData);
            }

            return refreshResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring valid token for client: {ClientName}", clientConfig.ClientName);
            return TokenResult.Failed($"Exception during token validation: {ex.Message}", TokenOperationType.Error, ex);
        }
    }

    /// <summary>
    /// Exchanges an authorization code for fresh tokens
    /// </summary>
    public async Task<TokenResult> ExchangeAuthorizationCodeAsync(ClientConfiguration clientConfig, string authorizationCode)
    {
        try
        {
            _logger.LogInformation("Exchanging authorization code for client: {ClientName}", clientConfig.ClientName);

            if (string.IsNullOrEmpty(authorizationCode))
            {
                return TokenResult.Failed("Authorization code is required", TokenOperationType.AuthorizationCodeExchange);
            }

            // Setup HTTP client with mTLS
            using var httpClient = await CreateHttpClientAsync(clientConfig);

            // Prepare token request
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = authorizationCode,
                ["client_id"] = clientConfig.ApiConfig.ClientId,
                ["redirect_uri"] = clientConfig.ApiConfig.RedirectUri
            };

            if (!string.IsNullOrEmpty(clientConfig.ApiConfig.ClientSecret))
            {
                tokenRequest["client_secret"] = clientConfig.ApiConfig.ClientSecret;
            }

            var content = new FormUrlEncodedContent(tokenRequest);
            
            _logger.LogInformation("Making token exchange request to: {TokenUrl}", clientConfig.ApiConfig.TokenUrl);

            var response = await httpClient.PostAsync(clientConfig.ApiConfig.TokenUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent);
                
                if (tokenResponse?.AccessToken == null)
                {
                    return TokenResult.Failed("Invalid token response: missing access token", TokenOperationType.AuthorizationCodeExchange);
                }

                var tokenData = CreateTokenData(tokenResponse);
                await SaveTokensAsync(clientConfig.ClientName, tokenData);

                _logger.LogInformation("Authorization code exchange successful. Access token length: {Length}", 
                    tokenResponse.AccessToken.Length);

                return TokenResult.Successful(tokenResponse.AccessToken, tokenData, TokenOperationType.AuthorizationCodeExchange);
            }
            else
            {
                _logger.LogError("Authorization code exchange failed: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);
                
                return TokenResult.Failed($"Token exchange failed: {response.StatusCode} - {responseContent}", 
                    TokenOperationType.AuthorizationCodeExchange);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during authorization code exchange for client: {ClientName}", clientConfig.ClientName);
            return TokenResult.Failed($"Exception during authorization code exchange: {ex.Message}", 
                TokenOperationType.AuthorizationCodeExchange, ex);
        }
    }

    /// <summary>
    /// Refreshes tokens using the current refresh token
    /// </summary>
    public async Task<TokenResult> RefreshTokenAsync(ClientConfiguration clientConfig, TokenData currentTokens, bool forceFullRefresh = false)
    {
        try
        {
            _logger.LogInformation("Refreshing tokens for client: {ClientName}, Force full refresh: {ForceFullRefresh}", 
                clientConfig.ClientName, forceFullRefresh);

            if (string.IsNullOrEmpty(currentTokens.RefreshToken))
            {
                return TokenResult.Failed("Refresh token is missing", TokenOperationType.FullRefresh);
            }

            // Check refresh token validity
            if (currentTokens.IsRefreshTokenExpired())
            {
                _logger.LogWarning("Refresh token has expired for client: {ClientName}", clientConfig.ClientName);
                return TokenResult.Failed("Refresh token has expired. New authorization required.", TokenOperationType.FullRefresh);
            }

            // Setup HTTP client with mTLS
            using var httpClient = await CreateHttpClientAsync(clientConfig);

            // Prepare refresh request
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = currentTokens.RefreshToken,
                ["client_id"] = clientConfig.ApiConfig.ClientId
            };

            if (!string.IsNullOrEmpty(clientConfig.ApiConfig.ClientSecret))
            {
                tokenRequest["client_secret"] = clientConfig.ApiConfig.ClientSecret;
            }

            var content = new FormUrlEncodedContent(tokenRequest);
            
            _logger.LogInformation("Making token refresh request to: {TokenUrl}", clientConfig.ApiConfig.TokenUrl);

            var response = await httpClient.PostAsync(clientConfig.ApiConfig.TokenUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent);
                
                if (tokenResponse?.AccessToken == null)
                {
                    return TokenResult.Failed("Invalid refresh response: missing access token", TokenOperationType.FullRefresh);
                }

                // Create updated token data
                var newTokenData = UpdateTokenData(currentTokens, tokenResponse, forceFullRefresh);
                
                var operationType = forceFullRefresh || !string.IsNullOrEmpty(tokenResponse.RefreshToken) 
                    ? TokenOperationType.FullRefresh 
                    : TokenOperationType.AccessTokenRefresh;

                _logger.LogInformation("Token refresh successful. Operation type: {OperationType}, Access token length: {Length}", 
                    operationType, tokenResponse.AccessToken.Length);

                return TokenResult.Successful(tokenResponse.AccessToken, newTokenData, operationType);
            }
            else
            {
                _logger.LogError("Token refresh failed: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);

                // Check if it's an invalid_grant error - might need new authorization
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && 
                    responseContent.Contains("invalid_grant"))
                {
                    return TokenResult.Failed("Refresh token is invalid or expired. New authorization required.", 
                        TokenOperationType.FullRefresh);
                }

                return TokenResult.Failed($"Token refresh failed: {response.StatusCode} - {responseContent}", 
                    TokenOperationType.FullRefresh);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during token refresh for client: {ClientName}", clientConfig.ClientName);
            return TokenResult.Failed($"Exception during token refresh: {ex.Message}", 
                TokenOperationType.FullRefresh, ex);
        }
    }

    /// <summary>
    /// Validates if the current tokens are still valid
    /// </summary>
    public TokenValidationResult ValidateTokens(TokenData tokens)
    {
        var result = new TokenValidationResult();

        if (tokens == null)
        {
            result.IsValid = false;
            result.NeedsNewTokens = true;
            result.Reason = "Tokens are null";
            return result;
        }

        if (string.IsNullOrEmpty(tokens.AccessToken))
        {
            result.IsValid = false;
            result.NeedsNewTokens = true;
            result.Reason = "Access token is missing";
            return result;
        }

        // Check refresh token validity
        if (string.IsNullOrEmpty(tokens.RefreshToken))
        {
            result.IsValid = false;
            result.NeedsNewTokens = true;
            result.Reason = "Refresh token is missing";
            return result;
        }

        if (tokens.IsRefreshTokenExpired())
        {
            result.IsValid = false;
            result.NeedsNewTokens = true;
            result.Reason = "Refresh token has expired";
            return result;
        }

        // Check access token validity
        if (tokens.IsAccessTokenExpired(0)) // Already expired
        {
            result.IsValid = false;
            result.NeedsRefresh = true;
            result.Reason = "Access token has expired";
            return result;
        }

        if (tokens.IsAccessTokenExpired(5)) // Expires within 5 minutes
        {
            result.IsValid = true;
            result.NeedsRefresh = true;
            result.Reason = "Access token expires soon";
            
            var expiryTime = tokens.AccessTokenExpiryTime;
            if (expiryTime.HasValue)
            {
                result.TimeUntilExpiry = expiryTime.Value - DateTime.UtcNow;
            }
            
            return result;
        }

        // Tokens are valid
        result.IsValid = true;
        result.Reason = "Tokens are valid";
        
        var expiryTime2 = tokens.AccessTokenExpiryTime;
        if (expiryTime2.HasValue)
        {
            result.TimeUntilExpiry = expiryTime2.Value - DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Loads tokens from storage for a specific client
    /// </summary>
    public async Task<TokenData?> LoadTokensAsync(string clientName)
    {
        try
        {
            var tokenPath = GetTokenFilePath(clientName);
            
            if (!File.Exists(tokenPath))
            {
                _logger.LogInformation("No token file found for client: {ClientName}", clientName);
                return null;
            }

            var json = await File.ReadAllTextAsync(tokenPath);
            var tokenData = JsonConvert.DeserializeObject<TokenData>(json);
            
            _logger.LogInformation("Loaded tokens for client: {ClientName}", clientName);
            return tokenData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tokens for client: {ClientName}", clientName);
            return null;
        }
    }

    /// <summary>
    /// Saves tokens to storage for a specific client
    /// </summary>
    public async Task SaveTokensAsync(string clientName, TokenData tokens)
    {
        try
        {
            var tokenPath = GetTokenFilePath(clientName);
            var directory = Path.GetDirectoryName(tokenPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(tokens, Formatting.Indented);
            await File.WriteAllTextAsync(tokenPath, json);
            
            _logger.LogInformation("Saved tokens for client: {ClientName}", clientName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving tokens for client: {ClientName}", clientName);
            throw;
        }
    }

    /// <summary>
    /// Creates an HTTP client with mTLS configuration
    /// </summary>
    private async Task<HttpClient> CreateHttpClientAsync(ClientConfiguration clientConfig)
    {
        var handler = new HttpClientHandler();
        
        // Load client certificate
        var certificate = await _certificateManager.LoadCertificateAsync(
            clientConfig.Certificates.CertificatePath,
            clientConfig.Certificates.PrivateKeyPath);
        
        handler.ClientCertificates.Add(certificate);
        
        // Configure certificate validation
        if (!clientConfig.Certificates.ValidateServerCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = 
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        var httpClient = new HttpClient(handler);
        httpClient.Timeout = TimeSpan.FromSeconds(clientConfig.Settings.TimeoutSeconds);
        
        // Add custom headers
        foreach (var header in clientConfig.ApiConfig.CustomHeaders)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        return httpClient;
    }

    /// <summary>
    /// Creates TokenData from TokenResponse
    /// </summary>
    private static TokenData CreateTokenData(TokenResponse tokenResponse)
    {
        return new TokenData
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            TokenType = tokenResponse.TokenType,
            ExpiresIn = tokenResponse.ExpiresIn,
            RefreshTokenExpiresIn = tokenResponse.RefreshTokenExpiresIn,
            Scope = tokenResponse.Scope,
            ConsentedOn = tokenResponse.ConsentedOn,
            Metadata = tokenResponse.Metadata,
            RetrievedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    /// <summary>
    /// Updates existing TokenData with new TokenResponse
    /// </summary>
    private static TokenData UpdateTokenData(TokenData existing, TokenResponse tokenResponse, bool forceFullRefresh)
    {
        var updated = new TokenData
        {
            // Always update access token
            AccessToken = tokenResponse.AccessToken,
            TokenType = tokenResponse.TokenType ?? existing.TokenType,
            ExpiresIn = tokenResponse.ExpiresIn ?? existing.ExpiresIn,
            Scope = tokenResponse.Scope ?? existing.Scope,
            RetrievedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Handle refresh token based on strategy and response
        if (forceFullRefresh || !string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            // Use new refresh token if provided, otherwise keep existing
            updated.RefreshToken = tokenResponse.RefreshToken ?? existing.RefreshToken;
            updated.RefreshTokenExpiresIn = tokenResponse.RefreshTokenExpiresIn ?? existing.RefreshTokenExpiresIn;
        }
        else
        {
            // Keep existing refresh token for access-only refresh
            updated.RefreshToken = existing.RefreshToken;
            updated.RefreshTokenExpiresIn = existing.RefreshTokenExpiresIn;
        }

        // Keep other metadata
        updated.ConsentedOn = tokenResponse.ConsentedOn ?? existing.ConsentedOn;
        updated.Metadata = tokenResponse.Metadata ?? existing.Metadata;

        return updated;
    }

    /// <summary>
    /// Gets the token file path for a client
    /// </summary>
    private static string GetTokenFilePath(string clientName)
    {
        var tokensDirectory = Path.Combine(Directory.GetCurrentDirectory(), "tokens");
        return Path.Combine(tokensDirectory, $"{clientName}_tokens.json");
    }
}