using RabobankBAI.Configuration;

namespace RabobankBAI.Core;

/// <summary>
/// Interface for client configuration management
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Loads client configuration by name
    /// </summary>
    Task<ClientConfiguration?> LoadClientConfigurationAsync(string clientName);

    /// <summary>
    /// Saves client configuration
    /// </summary>
    Task SaveClientConfigurationAsync(string clientName, ClientConfiguration configuration);

    /// <summary>
    /// Lists all available client configurations
    /// </summary>
    Task<IEnumerable<string>> ListClientConfigurationsAsync();

    /// <summary>
    /// Validates client configuration
    /// </summary>
    ConfigurationValidationResult ValidateConfiguration(ClientConfiguration configuration);
}

/// <summary>
/// Interface for Rabobank API client
/// </summary>
public interface IRabobankApiClient
{
    /// <summary>
    /// Gets account balances
    /// </summary>
    Task<ApiResponse<BalanceResponse>> GetBalancesAsync(string accessToken, string accountId);

    /// <summary>
    /// Gets account transactions
    /// </summary>
    Task<ApiResponse<TransactionResponse>> GetTransactionsAsync(string accessToken, string accountId, DateTime? fromDate = null, DateTime? toDate = null);
}