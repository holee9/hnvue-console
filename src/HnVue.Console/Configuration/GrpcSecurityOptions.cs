using System.IO;
using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace HnVue.Console.Configuration;

/// <summary>
/// gRPC TLS/mTLS security configuration options.
/// SPEC-SECURITY-001: Secure gRPC communication between Console and Core Engine.
/// </summary>
public sealed class GrpcSecurityOptions
{
    /// <summary>
    /// Gets or sets whether TLS is enabled.
    /// Default: true (Production must use TLS).
    /// </summary>
    public bool EnableTls { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum TLS protocol version.
    /// Default: Tls13 for production security.
    /// </summary>
    public TlsVersion MinTlsVersion { get; set; } = TlsVersion.Tls13;

    /// <summary>
    /// Gets or sets whether mutual TLS (mTLS) is enabled.
    /// When enabled, client must present a valid certificate.
    /// Default: false (must be explicitly enabled in production).
    /// </summary>
    public bool EnableMutualTls { get; set; } = false;

    /// <summary>
    /// Gets or sets the path to the client certificate (PFX/P12 format).
    /// Required when EnableMutualTls is true.
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the client certificate password.
    /// </summary>
    /// <remarks>
    /// WARNING: Do not store passwords in appsettings.json.
    /// Use Azure Key Vault, user secrets, or environment variables.
    /// </remarks>
    public string? ClientCertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets the path to the CA certificate for server validation.
    /// Required for self-signed certificates in test environments.
    /// </summary>
    public string? RootCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets whether certificate revocation validation is performed.
    /// Default: true (enforce CRL/OCSP checks).
    /// </summary>
    public bool CheckCertificateRevocation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to skip server certificate validation.
    /// Default: false.
    /// WARNING: Only set to true for local development with self-signed certificates.
    /// </summary>
    public bool SkipServerCertificateValidation { get; set; }

    /// <summary>
    /// Gets or sets the certificate rotation period in days.
    /// Default: 90 days (security best practice).
    /// </summary>
    public int CertificateRotationDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets the certificate expiration warning threshold in days.
    /// Default: 30 days (alert before certificate expires).
    /// </summary>
    public int CertificateExpirationWarningDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the list of allowed certificate thumbprints for server validation.
    /// When populated, only certificates with matching thumbprints are accepted.
    /// </summary>
    public HashSet<string> AllowedServerCertificateThumbprints { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of allowed certificate subject names (SANs).
    /// Used for additional certificate validation beyond chain trust.
    /// </summary>
    public HashSet<string> AllowedSubjectNames { get; set; } = new();

    /// <summary>
    /// Validates the security options configuration.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise.</returns>
    public bool Validate()
    {
        if (EnableTls && EnableMutualTls)
        {
            if (string.IsNullOrEmpty(ClientCertificatePath))
            {
                return false;
            }
        }

        if (EnableTls && MinTlsVersion < TlsVersion.Tls13)
        {
            return false;
        }

        if (CertificateRotationDays < 1 || CertificateRotationDays > 730)
        {
            return false;
        }

        if (CertificateExpirationWarningDays < 1 || CertificateExpirationWarningDays >= CertificateRotationDays)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Loads the client certificate from the configured path.
    /// </summary>
    /// <returns>The client certificate, or null if mTLS is disabled.</returns>
    public X509Certificate2? LoadClientCertificate()
    {
        if (!EnableMutualTls || string.IsNullOrEmpty(ClientCertificatePath))
        {
            return null;
        }

        if (!File.Exists(ClientCertificatePath))
        {
            throw new FileNotFoundException($"Client certificate not found: {ClientCertificatePath}");
        }

        var password = ClientCertificatePassword ?? string.Empty;
        var certificate = new X509Certificate2(ClientCertificatePath, password);

        ValidateCertificateExpiration(certificate);

        return certificate;
    }

    /// <summary>
    /// Loads the root CA certificate for server validation.
    /// </summary>
    /// <returns>The root CA certificate, or null if not configured.</returns>
    public X509Certificate2? LoadRootCertificate()
    {
        if (string.IsNullOrEmpty(RootCertificatePath) || !File.Exists(RootCertificatePath))
        {
            return null;
        }

        return new X509Certificate2(RootCertificatePath);
    }

    /// <summary>
    /// Checks if a certificate is approaching expiration.
    /// </summary>
    /// <param name="certificate">The certificate to check.</param>
    /// <returns>True if certificate expires within warning threshold.</returns>
    public bool IsCertificateExpiringSoon(X509Certificate2 certificate)
    {
        var notAfterUtc = certificate.NotAfter.ToUniversalTime();
        var daysUntilExpiration = notAfterUtc - DateTime.UtcNow;
        return daysUntilExpiration.TotalDays <= CertificateExpirationWarningDays;
    }

    private void ValidateCertificateExpiration(X509Certificate2 certificate)
    {
        if (DateTime.UtcNow > certificate.NotAfter)
        {
            throw new SecurityException($"Client certificate has expired: {certificate.NotAfter}");
        }

        if (IsCertificateExpiringSoon(certificate))
        {
            // Log warning but allow connection
            var daysRemaining = (certificate.NotAfter - DateTime.UtcNow).Days;
            System.Console.WriteLine($"WARNING: Client certificate expires in {daysRemaining} days ({certificate.NotAfter:yyyy-MM-dd})");
        }
    }
}

/// <summary>
/// TLS protocol versions.
/// </summary>
public enum TlsVersion
{
    /// <summary>TLS 1.2 (Minimum for legacy compatibility).</summary>
    Tls12 = 0,

    /// <summary>TLS 1.3 (Recommended for production).</summary>
    Tls13 = 1
}
