using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RabobankZero
{
    public class RabobankApiClient : IDisposable
    {
        private readonly Config _config;
        private readonly HttpClient _httpClient;
        private readonly SignatureGenerator _signatureGenerator;

        public RabobankApiClient(Config config)
        {
            _config = config;
            _signatureGenerator = new SignatureGenerator(config);
            
            // Setup HttpClient with certificate for mTLS
            var handler = new HttpClientHandler();
            var cert = LoadCertificateFromPem(config.CertificatePath, config.PrivateKeyPath);
            handler.ClientCertificates.Add(cert);
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
            _httpClient = new HttpClient(handler);
        }

        public async Task<string?> GetTransactions(TokenResponse tokens, string? accountId = null, 
            DateTime? dateFrom = null, DateTime? dateTo = null, int size = 10, string? iban = null)
        {
            try
            {
                // Use default account if not specified
                accountId ??= _config.DefaultAccountId;
                
                // Set date range (default: last 30 days to 5 minutes ago)
                dateFrom ??= DateTime.UtcNow.AddDays(-30);
                dateTo ??= DateTime.UtcNow.AddMinutes(-5); // API recommendation: 5 minutes before current time
                
                // Build URL with required date parameters 
                string dateFromStr = dateFrom?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string dateToStr = dateTo?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                
                string url = $"{_config.ApiBaseUrl}/accounts/{accountId}/transactions" +
                           $"?bookingStatus=booked" +
                           $"&dateFrom={dateFromStr}" +
                           $"&dateTo={dateToStr}" +
                           $"&size={size}";
                
                Console.WriteLine($"[DEBUG] API URL: {url}");

                // Generate headers
                string date = DateTime.UtcNow.ToString("R"); // RFC 1123 format
                string xRequestId = Guid.NewGuid().ToString();
                string digest = _signatureGenerator.GenerateDigest(); // Empty body for GET
                string? consentId = ExtractConsentId(tokens);
                
                // Parse URL to get path for signature
                var uri = new Uri(url);
                string urlPath = uri.PathAndQuery;
                
                // Prepare headers for signature (according to API spec: only date, digest, x-request-id)
                var headersForSignature = new Dictionary<string, string>
                {
                    ["Date"] = date,
                    ["Digest"] = digest,
                    ["X-Request-ID"] = xRequestId  // Use official API spec name: X-Request-ID
                };
                
                // Generate signature using working pattern
                string signature = _signatureGenerator.GenerateSignature("GET", urlPath, headersForSignature);
                string certificateBase64 = _signatureGenerator.GetCertificateBase64();

                // Create request
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                
                // Add required headers (including mandatory digest)
                request.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");
                request.Headers.Add("X-IBM-Client-Id", _config.ClientId);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Date", date);
                request.Headers.Add("Digest", digest);
                request.Headers.Add("X-Request-ID", xRequestId);  // Use official API spec name: X-Request-ID
                request.Headers.Add("Signature", signature);
                request.Headers.Add("Signature-Certificate", certificateBase64);
                
                if (!string.IsNullOrEmpty(consentId))
                {
                    request.Headers.Add("consentId", consentId);  // lowercase 'c'
                }

                Console.WriteLine("[DEBUG] Request headers:");
                Console.WriteLine($"Authorization: Bearer {tokens.AccessToken?.Substring(0, 20)}...");
                Console.WriteLine($"X-IBM-Client-Id: {_config.ClientId}");
                Console.WriteLine($"Date: {date}");
                Console.WriteLine($"Digest: {digest}");
                Console.WriteLine($"X-Request-ID: {xRequestId}");  // Update debug output to match
                Console.WriteLine($"ConsentId: {consentId}");
                Console.WriteLine($"Signature-Certificate: {certificateBase64.Substring(0, 50)}...");
                Console.WriteLine();

                // Send request
                Console.WriteLine("[DEBUG] Sending API request...");
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"[DEBUG] Response status: {response.StatusCode}");
                Console.WriteLine($"[DEBUG] Response headers:");
                foreach (var header in response.Headers)
                {
                    Console.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                Console.WriteLine();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[SUCCESS] Transactions retrieved successfully!");
                    
                    // Save response to file (with improved error handling)
                    try
                    {
                        // Generate filename with IBAN if provided
                        string fileName;
                        if (!string.IsNullOrEmpty(iban))
                        {
                            string ibanShort = iban.Replace("NL", "").Substring(0, 8);
                            fileName = $"transactions_{ibanShort}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                        }
                        else
                        {
                            fileName = $"transactions_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                        }
                        
                        // Create Output directory if it doesn't exist
                        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Output");
                        Directory.CreateDirectory(outputDir);
                        
                        // Try writing to Output directory first, then fallback locations
                        string[] possiblePaths = {
                            Path.Combine(outputDir, fileName),
                            Path.Combine(Directory.GetCurrentDirectory(), fileName),
                            Path.Combine(Path.GetTempPath(), fileName),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName)
                        };
                        
                        bool fileWritten = false;
                        foreach (string fullPath in possiblePaths)
                        {
                            try
                            {
                                Console.WriteLine($"[DEBUG] Attempting to write to: {fullPath}");
                                Console.WriteLine($"[DEBUG] Response size: {responseBody.Length} characters");
                                
                                // Try synchronous write - only create file if successful
                                using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
                                using (var writer = new StreamWriter(fileStream))
                                {
                                    writer.Write(responseBody);
                                }
                                
                                // Verify the write was successful
                                if (File.Exists(fullPath))
                                {
                                    var fileInfo = new FileInfo(fullPath);
                                    if (fileInfo.Length > 0)
                                    {
                                        Console.WriteLine($"[SUCCESS] File written successfully: {fullPath} ({fileInfo.Length} bytes)");
                                        fileWritten = true;
                                        break;
                                    }
                                    else
                                    {
                                        File.Delete(fullPath); // Delete empty file
                                        Console.WriteLine($"[DEBUG] Deleted empty file: {fullPath}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] Failed to write to {fullPath}: {ex.Message}");
                                // Clean up any partially created file
                                try
                                {
                                    if (File.Exists(fullPath))
                                    {
                                        File.Delete(fullPath);
                                    }
                                }
                                catch { }
                                continue;
                            }
                        }
                        
                        if (!fileWritten)
                        {
                            Console.WriteLine("[WARNING] Could not write file to any location. Data shown in console only.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] All file write attempts failed: {ex.GetType().Name}: {ex.Message}");
                    }
                    
                    // Pretty print ALL transaction data in console
                    try
                    {
                        var jsonObject = JsonConvert.DeserializeObject(responseBody);
                        string prettyJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                        Console.WriteLine("\n" + "=".PadRight(80, '='));
                        Console.WriteLine("COMPLETE TRANSACTION DATA:");
                        Console.WriteLine("=".PadRight(80, '='));
                        Console.WriteLine(prettyJson);
                        Console.WriteLine("=".PadRight(80, '='));
                    }
                    catch
                    {
                        Console.WriteLine("\n" + "=".PadRight(80, '='));
                        Console.WriteLine("RAW TRANSACTION DATA:");
                        Console.WriteLine("=".PadRight(80, '='));
                        Console.WriteLine(responseBody);
                        Console.WriteLine("=".PadRight(80, '='));
                    }
                    
                    return responseBody;
                }
                else
                {
                    Console.WriteLine($"[ERROR] API call failed: {response.StatusCode}");
                    Console.WriteLine($"[ERROR] Response: {responseBody}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in GetTransactions: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<CamtDataSet?> GetCamtDataSet(TokenResponse tokens, DateTime dateFrom, DateTime dateTo, string? accountId = null, string? iban = null)
        {
            try
            {
                Console.WriteLine("[INFO] Preparing CAMT dataset...");
                var camtDataSet = new CamtDataSet();

                // Step 1: Get opening balance (from day before period start)
                DateTime openingBalanceDate = dateFrom.AddDays(-1);
                Console.WriteLine($"[INFO] Getting opening balance for: {openingBalanceDate:yyyy-MM-dd}");
                
                var openingBalance = await GetHistoricalBalance(tokens, openingBalanceDate, accountId);
                if (openingBalance?.Balances != null && openingBalance.Balances.Count > 0)
                {
                    camtDataSet.OpeningBalance = openingBalance.Balances[0];
                    Console.WriteLine($"[SUCCESS] Opening balance: {camtDataSet.OpeningBalance.BalanceAmount.Value} {camtDataSet.OpeningBalance.BalanceAmount.Currency}");
                }
                else
                {
                    Console.WriteLine("[WARNING] Could not retrieve opening balance");
                }

                // Step 2: Get transactions for the period
                Console.WriteLine($"[INFO] Getting transactions from {dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}");
                var transactionsJson = await GetTransactions(tokens, accountId, dateFrom, dateTo, 500, iban);
                
                if (!string.IsNullOrEmpty(transactionsJson))
                {
                    var transactionResponse = JsonConvert.DeserializeObject<TransactionResponse>(transactionsJson);
                    if (transactionResponse != null)
                    {
                        camtDataSet.Transactions = transactionResponse;
                        Console.WriteLine($"[SUCCESS] Retrieved {transactionResponse.Transactions.Booked.Count} transactions");
                    }
                }

                // Step 3: Get closing balance (from end of period)
                Console.WriteLine($"[INFO] Getting closing balance for: {dateTo:yyyy-MM-dd}");
                var closingBalance = await GetHistoricalBalance(tokens, dateTo, accountId);
                if (closingBalance?.Balances != null && closingBalance.Balances.Count > 0)
                {
                    camtDataSet.ClosingBalance = closingBalance.Balances[0];
                    Console.WriteLine($"[SUCCESS] Closing balance: {camtDataSet.ClosingBalance.BalanceAmount.Value} {camtDataSet.ClosingBalance.BalanceAmount.Currency}");
                }
                else
                {
                    Console.WriteLine("[WARNING] Could not retrieve closing balance");
                }

                camtDataSet.Account = openingBalance?.Account ?? new AccountReference();
                camtDataSet.DateFrom = dateFrom;
                camtDataSet.DateTo = dateTo;

                return camtDataSet;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to build CAMT dataset: {ex.Message}");
                return null;
            }
        }

        public async Task<BalanceResponse?> GetCurrentBalances(TokenResponse tokens, string? accountId = null)
        {
            try
            {
                accountId ??= _config.DefaultAccountId;
                string url = $"{_config.ApiBaseUrl}/accounts/{accountId}/balances";

                Console.WriteLine($"[DEBUG] Balance API URL: {url}");

                return await ExecuteBalanceRequest(url, tokens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in GetCurrentBalances: {ex.Message}");
                return null;
            }
        }

        public async Task<BalanceResponse?> GetHistoricalBalance(TokenResponse tokens, DateTime closingBookedReferenceDate, string? accountId = null)
        {
            try
            {
                accountId ??= _config.DefaultAccountId;
                string referenceDateStr = closingBookedReferenceDate.ToString("yyyy-MM-dd");
                string url = $"{_config.ApiBaseUrl}/accounts/{accountId}/balances?closingBookedReferenceDate={referenceDateStr}";

                Console.WriteLine($"[DEBUG] Historical Balance API URL: {url}");

                return await ExecuteBalanceRequest(url, tokens);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in GetHistoricalBalance: {ex.Message}");
                return null;
            }
        }

        private async Task<BalanceResponse?> ExecuteBalanceRequest(string url, TokenResponse tokens)
        {
            try
            {
                // Generate headers
                string date = DateTime.UtcNow.ToString("R");
                string xRequestId = Guid.NewGuid().ToString();
                string digest = _signatureGenerator.GenerateDigest();
                string? consentId = ExtractConsentId(tokens);

                var uri = new Uri(url);
                string urlPath = uri.PathAndQuery;

                var headersForSignature = new Dictionary<string, string>
                {
                    ["Date"] = date,
                    ["Digest"] = digest,
                    ["X-Request-ID"] = xRequestId
                };

                string signature = _signatureGenerator.GenerateSignature("GET", urlPath, headersForSignature);
                string certificateBase64 = _signatureGenerator.GetCertificateBase64();

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");
                request.Headers.Add("X-IBM-Client-Id", _config.ClientId);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Date", date);
                request.Headers.Add("Digest", digest);
                request.Headers.Add("X-Request-ID", xRequestId);
                request.Headers.Add("Signature", signature);
                request.Headers.Add("Signature-Certificate", certificateBase64);

                if (!string.IsNullOrEmpty(consentId))
                {
                    request.Headers.Add("consentId", consentId);
                }

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[DEBUG] Balance API Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[SUCCESS] Balance data retrieved successfully!");
                    
                    try
                    {
                        var balanceResponse = JsonConvert.DeserializeObject<BalanceResponse>(responseBody);
                        
                        // Log balance information
                        if (balanceResponse?.Balances != null)
                        {
                            Console.WriteLine($"[INFO] Retrieved {balanceResponse.Balances.Count} balance(s):");
                            foreach (var balance in balanceResponse.Balances)
                            {
                                Console.WriteLine($"[INFO] - {balance.BalanceType}: {balance.BalanceAmount.Value} {balance.BalanceAmount.Currency}");
                            }
                        }
                        
                        return balanceResponse;
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to parse balance response: {ex.Message}");
                        Console.WriteLine($"[DEBUG] Raw response: {responseBody}");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine($"[ERROR] Balance API call failed: {response.StatusCode}");
                    Console.WriteLine($"[ERROR] Response: {responseBody}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in ExecuteBalanceRequest: {ex.Message}");
                return null;
            }
        }

        private string? ExtractConsentId(TokenResponse tokens)
        {
            try
            {
                // First try the ConsentId field
                if (!string.IsNullOrEmpty(tokens.ConsentId))
                {
                    return tokens.ConsentId;
                }
                
                // Fall back to extracting from Metadata field
                if (!string.IsNullOrEmpty(tokens.Metadata))
                {
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
                string certPem = File.ReadAllText(certPath);
                string keyPem = File.ReadAllText(keyPath);
                
                return X509Certificate2.CreateFromPem(certPem, keyPem);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load certificate for API client: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _signatureGenerator?.Dispose();
        }
    }
}