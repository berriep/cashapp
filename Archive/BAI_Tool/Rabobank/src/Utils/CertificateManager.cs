using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace RabobankBAI.Utils;

/// <summary>
/// Certificate manager for mTLS authentication
/// </summary>
public class CertificateManager : ICertificateManager
{
    private readonly ILogger<CertificateManager> _logger;

    public CertificateManager(ILogger<CertificateManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads an X509 certificate from PEM files
    /// </summary>
    public async Task<X509Certificate2> LoadCertificateAsync(string certificatePath, string privateKeyPath)
    {
        try
        {
            _logger.LogInformation("Loading certificate from: {CertPath}", certificatePath);
            
            if (!File.Exists(certificatePath))
            {
                throw new FileNotFoundException($"Certificate file not found: {certificatePath}");
            }
            
            if (!File.Exists(privateKeyPath))
            {
                throw new FileNotFoundException($"Private key file not found: {privateKeyPath}");
            }

            var certPem = await File.ReadAllTextAsync(certificatePath);
            var keyPem = await File.ReadAllTextAsync(privateKeyPath);

            var certificate = X509Certificate2.CreateFromPem(certPem, keyPem);
            
            _logger.LogInformation("Certificate loaded successfully. Subject: {Subject}, Expires: {Expiry}", 
                certificate.Subject, certificate.NotAfter);

            // Validate the certificate
            if (!ValidateCertificate(certificate))
            {
                _logger.LogWarning("Certificate validation failed for: {CertPath}", certificatePath);
            }

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading certificate from: {CertPath}", certificatePath);
            throw;
        }
    }

    /// <summary>
    /// Validates a certificate
    /// </summary>
    public bool ValidateCertificate(X509Certificate2 certificate)
    {
        try
        {
            var info = GetCertificateInfo(certificate);
            
            if (info.IsExpired)
            {
                _logger.LogWarning("Certificate has expired: {Subject}, Expired: {Expiry}", 
                    info.Subject, info.NotAfter);
                return false;
            }

            if (info.ExpiresWithin(TimeSpan.FromDays(30)))
            {
                _logger.LogWarning("Certificate expires within 30 days: {Subject}, Expires: {Expiry}", 
                    info.Subject, info.NotAfter);
            }

            // Check if certificate has a private key
            if (!certificate.HasPrivateKey)
            {
                _logger.LogWarning("Certificate does not have a private key: {Subject}", info.Subject);
                return false;
            }

            _logger.LogInformation("Certificate validation successful: {Subject}", info.Subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating certificate");
            return false;
        }
    }

    /// <summary>
    /// Gets certificate expiry information
    /// </summary>
    public CertificateInfo GetCertificateInfo(X509Certificate2 certificate)
    {
        return new CertificateInfo
        {
            NotBefore = certificate.NotBefore,
            NotAfter = certificate.NotAfter,
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            Thumbprint = certificate.Thumbprint
        };
    }
}