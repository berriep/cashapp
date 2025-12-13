using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabobankBAI.Configuration;
using RabobankBAI.Services;
using RabobankBAI.Core;
using RabobankBAI.Utils;

namespace RabobankBAI;

/// <summary>
/// Main entry point for the Rabobank BAI API Tool
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Configure services
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting Rabobank BAI API Tool...");

            // Example usage - this will be enhanced based on requirements
            var tokenManager = serviceProvider.GetRequiredService<ITokenManager>();
            var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();

            // Load client configuration (example)
            var clientConfig = await configManager.LoadClientConfigurationAsync("default");
            
            if (clientConfig != null)
            {
                logger.LogInformation("Loaded configuration for client: {ClientName}", clientConfig.ClientName);
                
                // Check for authorization code parameter
                string? authCode = null;
                if (args.Length > 0 && args[0].StartsWith("--auth-code="))
                {
                    authCode = args[0].Substring("--auth-code=".Length);
                    logger.LogInformation("Authorization code provided via command line");
                }
                
                TokenResult tokenResult;
                
                if (!string.IsNullOrEmpty(authCode))
                {
                    // Exchange authorization code for fresh tokens
                    logger.LogInformation("Exchanging authorization code for fresh tokens...");
                    tokenResult = await tokenManager.ExchangeAuthorizationCodeAsync(clientConfig, authCode);
                }
                else
                {
                    // Normal token management flow
                    logger.LogInformation("Using existing token management flow...");
                    tokenResult = await tokenManager.EnsureValidTokenAsync(clientConfig);
                }
                
                if (tokenResult.Success)
                {
                    logger.LogInformation("Token management successful");
                    logger.LogInformation("Access token length: {TokenLength}", tokenResult.AccessToken?.Length ?? 0);
                    logger.LogInformation("Operation type: {OperationType}", tokenResult.OperationType);
                    
                    if (tokenResult.OperationType == RabobankBAI.Models.TokenOperationType.AuthorizationCodeExchange)
                    {
                        logger.LogInformation("✅ Fresh tokens obtained via authorization code exchange!");
                        logger.LogInformation("💾 Tokens saved and ready for future use");
                    }
                }
                else
                {
                    logger.LogError("Token management failed: {ErrorMessage}", tokenResult.ErrorMessage);
                    
                    if (tokenResult.ErrorMessage?.Contains("authorization code") == true)
                    {
                        logger.LogWarning("💡 Tip: Use --auth-code=YOUR_CODE to exchange a fresh authorization code");
                        logger.LogWarning("💡 Example: dotnet run -- --auth-code=AAPdN1eL1JC5YEvoq8J2...");
                    }
                    
                    if (tokenResult.Exception != null)
                    {
                        logger.LogError(tokenResult.Exception, "Token management exception details");
                    }
                }
            }
            else
            {
                logger.LogError("No client configuration found for 'default'");
                
                // List available configurations
                var availableConfigs = await configManager.ListClientConfigurationsAsync();
                if (availableConfigs.Any())
                {
                    logger.LogInformation("Available configurations: {Configurations}", string.Join(", ", availableConfigs));
                }
                else
                {
                    logger.LogWarning("No client configurations found. Please create a configuration file.");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application error occurred");
        }
        
        logger.LogInformation("Application completed");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("config/appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        // HttpClient
        services.AddHttpClient();

        // Utility services
        services.AddSingleton<ICertificateManager, CertificateManager>();

        // Application services
        services.AddSingleton<IConfigurationManager, ConfigurationManager>();
        services.AddSingleton<ITokenManager, TokenManager>();
        services.AddSingleton<IRabobankApiClient, RabobankApiClient>();
    }
}