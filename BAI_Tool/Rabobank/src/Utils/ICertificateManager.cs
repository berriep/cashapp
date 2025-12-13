using System.Security.Cryptography.X509Certificates;

namespace RabobankBAI.Utils;

/// <summary>
/// Interface for certificate management
/// </summary>
public interface ICertificateManager
{
    /// <summary>
    /// Loads an X509 certificate from PEM files
    /// </summary>
    Task<X509Certificate2> LoadCertificateAsync(string certificatePath, string privateKeyPath);

    /// <summary>
    /// Validates a certificate
    /// </summary>
    bool ValidateCertificate(X509Certificate2 certificate);

    /// <summary>
    /// Gets certificate expiry information
    /// </summary>
    CertificateInfo GetCertificateInfo(X509Certificate2 certificate);
}

/// <summary>
/// Certificate information
/// </summary>
public class CertificateInfo
{
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public string Subject { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Thumbprint { get; set; } = "";
    public bool IsExpired => DateTime.Now > NotAfter;
    public bool ExpiresWithin(TimeSpan timeSpan) => DateTime.Now.Add(timeSpan) > NotAfter;
    public TimeSpan TimeUntilExpiry => NotAfter - DateTime.Now;
}