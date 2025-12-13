using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabobankBAI.Configuration;
using RabobankBAI.Core;

namespace RabobankBAI.Services;

/// <summary>
/// Configuration manager for client-specific settings
/// </summary>
public class ConfigurationManager : IConfigurationManager
{
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly string _configBasePath;

    public ConfigurationManager(ILogger<ConfigurationManager> logger)
    {
        _logger = logger;
        _configBasePath = Path.Combine(Directory.GetCurrentDirectory(), "config", "clients");
    }

    /// <summary>
    /// Loads client configuration by name
    /// </summary>
    public async Task<ClientConfiguration?> LoadClientConfigurationAsync(string clientName)
    {
        try
        {
            var configPath = GetConfigFilePath(clientName);
            
            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Configuration file not found for client: {ClientName} at {Path}", 
                    clientName, configPath);
                return null;
            }

            var json = await File.ReadAllTextAsync(configPath);
            var configuration = JsonConvert.DeserializeObject<ClientConfiguration>(json);
            
            if (configuration == null)
            {
                _logger.LogError("Failed to deserialize configuration for client: {ClientName}", clientName);
                return null;
            }

            // Validate configuration
            var validation = ValidateConfiguration(configuration);
            if (!validation.IsValid)
            {
                _logger.LogError("Configuration validation failed for client: {ClientName}. Errors: {Errors}", 
                    clientName, string.Join(", ", validation.Errors));
                return null;
            }

            if (validation.Warnings.Any())
            {
                _logger.LogWarning("Configuration warnings for client: {ClientName}. Warnings: {Warnings}", 
                    clientName, string.Join(", ", validation.Warnings));
            }

            _logger.LogInformation("Configuration loaded successfully for client: {ClientName}", clientName);
            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration for client: {ClientName}", clientName);
            return null;
        }
    }

    /// <summary>
    /// Saves client configuration
    /// </summary>
    public async Task SaveClientConfigurationAsync(string clientName, ClientConfiguration configuration)
    {
        try
        {
            // Validate before saving
            var validation = ValidateConfiguration(configuration);
            if (!validation.IsValid)
            {
                throw new ArgumentException($"Configuration validation failed: {string.Join(", ", validation.Errors)}");
            }

            var configPath = GetConfigFilePath(clientName);
            var directory = Path.GetDirectoryName(configPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);
            await File.WriteAllTextAsync(configPath, json);
            
            _logger.LogInformation("Configuration saved successfully for client: {ClientName}", clientName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration for client: {ClientName}", clientName);
            throw;
        }
    }

    /// <summary>
    /// Lists all available client configurations
    /// </summary>
    public async Task<IEnumerable<string>> ListClientConfigurationsAsync()
    {
        try
        {
            if (!Directory.Exists(_configBasePath))
            {
                _logger.LogInformation("Configuration directory does not exist: {Path}", _configBasePath);
                return Enumerable.Empty<string>();
            }

            var configFiles = Directory.GetFiles(_configBasePath, "*.json");
            var clientNames = configFiles
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .ToList();

            _logger.LogInformation("Found {Count} client configurations", clientNames.Count);
            return clientNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing client configurations");
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Validates client configuration
    /// </summary>
    public ConfigurationValidationResult ValidateConfiguration(ClientConfiguration configuration)
    {
        var result = new ConfigurationValidationResult { IsValid = true };

        // Basic required fields
        if (string.IsNullOrWhiteSpace(configuration.ClientName))
        {
            result.AddError("ClientName is required");
        }

        if (string.IsNullOrWhiteSpace(configuration.Environment))
        {
            result.AddError("Environment is required");
        }
        else if (configuration.Environment != "sandbox" && configuration.Environment != "production")
        {
            result.AddWarning($"Environment '{configuration.Environment}' is not standard (expected: sandbox or production)");
        }

        // API Configuration validation
        ValidateApiConfiguration(configuration.ApiConfig, result);
        
        // Certificate Configuration validation
        ValidateCertificateConfiguration(configuration.Certificates, result);
        
        // Settings validation
        ValidateSettings(configuration.Settings, result);

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private void ValidateApiConfiguration(ApiConfiguration apiConfig, ConfigurationValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(apiConfig.ClientId))
        {
            result.AddError("ApiConfig.ClientId is required");
        }

        if (string.IsNullOrWhiteSpace(apiConfig.TokenUrl))
        {
            result.AddError("ApiConfig.TokenUrl is required");
        }
        else if (!Uri.TryCreate(apiConfig.TokenUrl, UriKind.Absolute, out _))
        {
            result.AddError("ApiConfig.TokenUrl must be a valid URL");
        }

        if (string.IsNullOrWhiteSpace(apiConfig.ApiBaseUrl))
        {
            result.AddError("ApiConfig.ApiBaseUrl is required");
        }
        else if (!Uri.TryCreate(apiConfig.ApiBaseUrl, UriKind.Absolute, out _))
        {
            result.AddError("ApiConfig.ApiBaseUrl must be a valid URL");
        }

        if (string.IsNullOrWhiteSpace(apiConfig.RedirectUri))
        {
            result.AddError("ApiConfig.RedirectUri is required");
        }
        else if (!Uri.TryCreate(apiConfig.RedirectUri, UriKind.Absolute, out _))
        {
            result.AddError("ApiConfig.RedirectUri must be a valid URL");
        }

        // Client secret is optional for some scenarios
        if (string.IsNullOrWhiteSpace(apiConfig.ClientSecret))
        {
            result.AddWarning("ApiConfig.ClientSecret is not provided (may be required for some endpoints)");
        }
    }

    private void ValidateCertificateConfiguration(CertificateConfiguration certConfig, ConfigurationValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(certConfig.CertificatePath))
        {
            result.AddError("Certificates.CertificatePath is required");
        }
        else if (!File.Exists(certConfig.CertificatePath))
        {
            // Check relative to current directory
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), certConfig.CertificatePath);
            if (!File.Exists(fullPath))
            {
                result.AddError($"Certificate file not found: {certConfig.CertificatePath}");
            }
        }

        if (string.IsNullOrWhiteSpace(certConfig.PrivateKeyPath))
        {
            result.AddError("Certificates.PrivateKeyPath is required");
        }
        else if (!File.Exists(certConfig.PrivateKeyPath))
        {
            // Check relative to current directory
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), certConfig.PrivateKeyPath);
            if (!File.Exists(fullPath))
            {
                result.AddError($"Private key file not found: {certConfig.PrivateKeyPath}");
            }
        }
    }

    private void ValidateSettings(ClientSettings settings, ConfigurationValidationResult result)
    {
        if (settings.TokenRefreshThresholdMinutes <= 0)
        {
            result.AddError("Settings.TokenRefreshThresholdMinutes must be greater than 0");
        }

        if (settings.MaxRetryAttempts < 0)
        {
            result.AddError("Settings.MaxRetryAttempts must be 0 or greater");
        }

        if (settings.TimeoutSeconds <= 0)
        {
            result.AddError("Settings.TimeoutSeconds must be greater than 0");
        }

        if (settings.TokenRefreshThresholdMinutes > 120)
        {
            result.AddWarning("Settings.TokenRefreshThresholdMinutes is quite high (>2 hours)");
        }

        if (settings.TimeoutSeconds > 300)
        {
            result.AddWarning("Settings.TimeoutSeconds is quite high (>5 minutes)");
        }
    }

    /// <summary>
    /// Gets the configuration file path for a client
    /// </summary>
    private string GetConfigFilePath(string clientName)
    {
        return Path.Combine(_configBasePath, $"{clientName}.json");
    }
}