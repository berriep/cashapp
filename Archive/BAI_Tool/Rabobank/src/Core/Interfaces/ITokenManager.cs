using RabobankBAI.Models;

namespace RabobankBAI.Core;

/// <summary>
/// Interface for OAuth2 token management
/// </summary>
public interface ITokenManager
{
    /// <summary>
    /// Ensures a valid access token is available, refreshing if necessary
    /// </summary>
    Task<TokenResult> EnsureValidTokenAsync(ClientConfiguration clientConfig, bool forceRefresh = false);

    /// <summary>
    /// Exchanges an authorization code for fresh tokens
    /// </summary>
    Task<TokenResult> ExchangeAuthorizationCodeAsync(ClientConfiguration clientConfig, string authorizationCode);

    /// <summary>
    /// Refreshes tokens using the current refresh token
    /// </summary>
    Task<TokenResult> RefreshTokenAsync(ClientConfiguration clientConfig, TokenData currentTokens, bool forceFullRefresh = false);

    /// <summary>
    /// Validates if the current tokens are still valid
    /// </summary>
    TokenValidationResult ValidateTokens(TokenData tokens);

    /// <summary>
    /// Loads tokens from storage for a specific client
    /// </summary>
    Task<TokenData?> LoadTokensAsync(string clientName);

    /// <summary>
    /// Saves tokens to storage for a specific client
    /// </summary>
    Task SaveTokensAsync(string clientName, TokenData tokens);
}