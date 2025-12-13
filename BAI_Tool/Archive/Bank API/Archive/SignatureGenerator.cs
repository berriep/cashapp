using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace RabobankZero
{
    public class SignatureGenerator : IDisposable
    {
        private readonly Config _config;
        private readonly X509Certificate2 _certificate;
        private readonly RSA _privateKey;

        public SignatureGenerator(Config config)
        {
            _config = config;
            _certificate = LoadCertificateFromPem(config.CertificatePath, config.PrivateKeyPath);
            
            // Extract private key for signing - try more robust loading
            try
            {
                string keyPem = File.ReadAllText(config.PrivateKeyPath);
                _privateKey = RSA.Create();
                _privateKey.ImportFromPem(keyPem.ToCharArray());
                
                Console.WriteLine($"[DEBUG] Private key loaded successfully");
                Console.WriteLine($"[DEBUG] Key size: {_privateKey.KeySize} bits");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load private key: {ex.Message}");
                throw;
            }
        }

        public string GenerateDigest(string body = "")
        {
            // For GET requests, body is empty
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            
            using var sha512 = SHA512.Create();
            byte[] hash = sha512.ComputeHash(bodyBytes);
            string digestValue = Convert.ToBase64String(hash);
            
            return $"sha-512={digestValue}";
        }

        public string GetCertificateFingerprint()
        {
            // Calculate SHA-256 fingerprint of certificate (for keyId)
            byte[] certBytes = _certificate.RawData;
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(certBytes);
            
            // Format as hex with colons (like your Python code)
            return string.Join(":", Array.ConvertAll(hash, b => b.ToString("X2")));
        }

        public string GetCertificateBase64()
        {
            // Get certificate as base64 for Signature-Certificate header
            return Convert.ToBase64String(_certificate.RawData);
        }

        public string GenerateSignature(string date, string digest, string xRequestId)
        {
            // This method is deprecated - we need a new method that follows working examples
            throw new NotImplementedException("Use GenerateSignature with full headers instead");
        }

        public string GenerateSignature(string method, string urlPath, Dictionary<string, string> headers)
        {
            try
            {
                // Build signing string according to OFFICIAL API spec: "date digest x-request-id"
                var signingStringBuilder = new StringBuilder();
                
                // Add headers in API spec order: date, digest, x-request-id
                if (headers.ContainsKey("Date"))
                    signingStringBuilder.AppendLine($"date: {headers["Date"]}");
                
                if (headers.ContainsKey("Digest"))
                    signingStringBuilder.AppendLine($"digest: {headers["Digest"]}");
                
                if (headers.ContainsKey("X-Request-ID"))
                    signingStringBuilder.AppendLine($"x-request-id: {headers["X-Request-ID"]}");

                // Remove the last newline
                string signingString = signingStringBuilder.ToString().TrimEnd('\n', '\r');
                
                Console.WriteLine("[DEBUG] Signing string:");
                Console.WriteLine(signingString);
                Console.WriteLine();

                // Sign with RSA-SHA512 according to API spec
                byte[] data = Encoding.UTF8.GetBytes(signingString);
                byte[] signature = _privateKey.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
                string signatureBase64 = Convert.ToBase64String(signature);

                // Use certificate serial number as keyId (converted from hex to integer)
                string keyId = "41703392498275823274478450484290741484992002829";  // Our certificate serial in integer format
                string signatureHeader = $"keyId=\"{keyId}\",algorithm=\"rsa-sha512\",headers=\"date digest x-request-id\",signature=\"{signatureBase64}\"";

                Console.WriteLine("[DEBUG] Generated signature header:");
                Console.WriteLine(signatureHeader);
                Console.WriteLine();

                return signatureHeader;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error generating signature: {ex.Message}");
                throw;
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
                Console.WriteLine($"[ERROR] Failed to load certificate for signing: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _privateKey?.Dispose();
            _certificate?.Dispose();
        }
    }
}