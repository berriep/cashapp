using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RabobankZero
{
    public class TokenManager : IDisposable
    {
        private readonly Config _config;
        private readonly HttpClient _httpClient;

        public TokenManager(Config config)
        {
            _config = config;
            
            // Setup HttpClient with certificate
            var handler = new HttpClientHandler();
            var cert = LoadCertificateFromPem(_config.CertificatePath, _config.PrivateKeyPath);
            handler.ClientCertificates.Add(cert);
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
            _httpClient = new HttpClient(handler);
        }

        public async Task<TokenResponse?> ExchangeAuthCodeForTokens()
        {
            try
            {
                string authCode = await File.ReadAllTextAsync(_config.AuthCodeFile);
                authCode = authCode.Trim();
                
                Console.WriteLine($"[DEBUG] Using auth code: {authCode.Substring(0, 20)}...");

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "authorization_code"),
                    new("code", authCode),
                    new("client_id", _config.ClientId),
                    new("client_secret", _config.ClientSecret),
                    new("redirect_uri", "http://localhost:8080/callback")
                };

                var content = new FormUrlEncodedContent(formData);
                
                Console.WriteLine($"[DEBUG] Posting to: {_config.TokenUrl}");
                var response = await _httpClient.PostAsync(_config.TokenUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"[DEBUG] Token response status: {response.StatusCode}");
                Console.WriteLine($"[DEBUG] Token response: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseBody);
                    
                    // Save tokens to file with robust error handling
                    try
                    {
                        // Ensure the file is not read-only and we have write permissions
                        if (File.Exists(_config.TokenFile))
                        {
                            File.SetAttributes(_config.TokenFile, FileAttributes.Normal);
                        }
                        
                        await File.WriteAllTextAsync(_config.TokenFile, responseBody);
                        Console.WriteLine($"[SUCCESS] Tokens saved to {_config.TokenFile}");
                    }
                    catch (Exception fileEx)
                    {
                        Console.WriteLine($"[WARNING] Could not save to {_config.TokenFile}: {fileEx.Message}");
                        Console.WriteLine("[INFO] Token exchange was successful, but file saving failed.");
                        Console.WriteLine("[DEBUG] Token response for manual save:");
                        Console.WriteLine(responseBody);
                    }
                    
                    return tokenResponse;
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to exchange auth code: {response.StatusCode}");
                    Console.WriteLine($"[ERROR] Response: {responseBody}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in ExchangeAuthCodeForTokens: {ex.Message}");
                return null;
            }
        }

        public async Task<TokenResponse?> RefreshTokens(string refreshToken)
        {
            try
            {
                Console.WriteLine("[DEBUG] Refreshing access token...");

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "refresh_token"),
                    new("refresh_token", refreshToken),
                    new("client_id", _config.ClientId),
                    new("client_secret", _config.ClientSecret)
                };

                var content = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(_config.TokenUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"[DEBUG] Refresh response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseBody);
                    
                    // Save refreshed tokens with robust error handling
                    try
                    {
                        // Ensure the file is not read-only and we have write permissions
                        if (File.Exists(_config.TokenFile))
                        {
                            File.SetAttributes(_config.TokenFile, FileAttributes.Normal);
                        }
                        
                        await File.WriteAllTextAsync(_config.TokenFile, responseBody);
                        Console.WriteLine("[SUCCESS] Tokens refreshed and saved");
                    }
                    catch (Exception fileEx)
                    {
                        Console.WriteLine($"[WARNING] Could not save refreshed tokens to {_config.TokenFile}: {fileEx.Message}");
                        Console.WriteLine("[INFO] Token refresh was successful, but file saving failed.");
                        Console.WriteLine("[DEBUG] Refreshed token response for manual save:");
                        Console.WriteLine(responseBody);
                    }
                    
                    return tokenResponse;
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to refresh token: {response.StatusCode}");
                    Console.WriteLine($"[ERROR] Response: {responseBody}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in RefreshTokens: {ex.Message}");
                return null;
            }
        }

        public async Task<TokenResponse?> LoadTokens()
        {
            try
            {
                if (!File.Exists(_config.TokenFile))
                {
                    Console.WriteLine($"[INFO] Token file {_config.TokenFile} not found");
                    return null;
                }

                string tokenJson = await File.ReadAllTextAsync(_config.TokenFile);
                var tokens = JsonConvert.DeserializeObject<TokenResponse>(tokenJson);
                
                Console.WriteLine("[DEBUG] Tokens loaded from file");
                return tokens;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load tokens: {ex.Message}");
                return null;
            }
        }

        public bool IsTokenValid(TokenResponse tokens)
        {
            if (tokens?.ExpiresIn == null) return false;
            
            // Simple check - in production you'd track the exact expiry time
            // For now, assume tokens are valid if they exist and were recently loaded
            return !string.IsNullOrEmpty(tokens.AccessToken);
        }

        public string? ExtractConsentId(TokenResponse tokens)
        {
            try
            {
                // From your Python code, consent_id comes from metadata
                if (!string.IsNullOrEmpty(tokens.Metadata))
                {
                    // Look for consent ID in metadata
                    var parts = tokens.Metadata.Split(' ');
                    return parts.Length > 0 ? parts[^1] : null;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private X509Certificate2 LoadCertificateFromPem(string certPath, string keyPath)
        {
            try
            {
                // Read certificate and key files
                string certPem = File.ReadAllText(certPath);
                string keyPem = File.ReadAllText(keyPath);
                
                // Create certificate from PEM data
                var cert = X509Certificate2.CreateFromPem(certPem, keyPem);
                
                Console.WriteLine($"[DEBUG] Certificate loaded successfully");
                Console.WriteLine($"[DEBUG] Certificate subject: {cert.Subject}");
                
                return cert;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load certificate: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

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

        [JsonProperty("scope")]
        public string? Scope { get; set; }

        [JsonProperty("metadata")]
        public string? Metadata { get; set; }

        [JsonProperty("consent_id")]
        public string? ConsentId { get; set; }
    }
}