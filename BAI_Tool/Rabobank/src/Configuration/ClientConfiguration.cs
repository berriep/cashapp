using Newtonsoft.Json;

namespace RabobankBAI.Configuration;

/// <summary>
/// Client-specific configuration
/// </summary>
public class ClientConfiguration
{
    public string ClientName { get; set; } = "";
    public string Environment { get; set; } = "sandbox"; // sandbox, production
    public ApiConfiguration ApiConfig { get; set; } = new();
    public CertificateConfiguration Certificates { get; set; } = new();
    public AccountConfiguration Accounts { get; set; } = new();
    public ClientSettings Settings { get; set; } = new();
}

/// <summary>
/// API endpoint configuration
/// </summary>
public class ApiConfiguration
{
    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    public string TokenUrl { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "";
    public string RedirectUri { get; set; } = "http://localhost:8080/callback";
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

/// <summary>
/// Certificate configuration for mTLS
/// </summary>
public class CertificateConfiguration
{
    public string CertificatePath { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
    public bool ValidateServerCertificate { get; set; } = true;
}

/// <summary>
/// Account configuration
/// </summary>
public class AccountConfiguration
{
    public string? DefaultAccountId { get; set; }
    public Dictionary<string, string> AccountMappings { get; set; } = new();
}

/// <summary>
/// Client-specific settings
/// </summary>
public class ClientSettings
{
    public int TokenRefreshThresholdMinutes { get; set; } = 60;
    public int MaxRetryAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableDetailedLogging { get; set; } = true;
    public string TokenStoragePath { get; set; } = "tokens";
    public bool EnableAutomaticTokenRefresh { get; set; } = true;
}

/// <summary>
/// Configuration validation result
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public void AddError(string error) => Errors.Add(error);
    public void AddWarning(string warning) => Warnings.Add(warning);
}