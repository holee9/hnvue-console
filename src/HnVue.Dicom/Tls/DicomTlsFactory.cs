using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using HnVue.Dicom.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HnVue.Dicom.Tls;

/// <summary>
/// Creates TLS initiators for DICOM associations based on the configured certificate source.
/// Supports Windows Certificate Store (by thumbprint) and file-based PEM/PFX certificates.
/// TLS 1.2 minimum; TLS 1.3 preferred when supported by the OS.
/// </summary>
// @MX:NOTE: [AUTO] DicomTlsFactory reads TlsOptions at call time so hot-reload of certs requires
//           application restart. OQ-05 tracks HSM/OS cert store strategy for production.
// @MX:ANCHOR: [AUTO] Public API boundary - ITlsFactory is injected into AssociationManager.
// @MX:REASON: fan_in >= 3 expected (AssociationManager, StorageScu, integration tests)
public sealed class DicomTlsFactory : ITlsFactory
{
    private readonly TlsOptions _tlsOptions;
    private readonly ILogger<DicomTlsFactory> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DicomTlsFactory"/>.
    /// </summary>
    /// <param name="options">The DICOM service options containing TLS configuration.</param>
    /// <param name="logger">Logger for TLS lifecycle events.</param>
    public DicomTlsFactory(
        IOptions<DicomServiceOptions> options,
        ILogger<DicomTlsFactory> logger)
    {
        _tlsOptions = options.Value.Tls;
        _logger = logger;
    }

    /// <inheritdoc/>
    public DicomTlsInitiator CreateTlsInitiator()
    {
        X509Certificate2? clientCert = null;

        if (_tlsOptions.ClientCertificateEnabled)
        {
            clientCert = LoadClientCertificate();
            _logger.LogInformation(
                "TLS client certificate loaded (Subject: {Subject})",
                clientCert.Subject);
        }

        X509Certificate2Collection? caCerts = LoadCaCertificates();

        RemoteCertificateValidationCallback? validationCallback = caCerts != null
            ? BuildCaValidationCallback(caCerts)
            : null;

        _logger.LogInformation(
            "TLS initiator created (MinVersion: {MinVersion}, ClientCert: {ClientCertEnabled}, CustomCA: {CustomCa})",
            _tlsOptions.MinVersion,
            _tlsOptions.ClientCertificateEnabled,
            caCerts != null);

        return new DicomTlsInitiator
        {
            ClientCertificate = clientCert,
            RemoteCertificateValidationCallback = validationCallback,
            CaCertificates = caCerts
        };
    }

    private X509Certificate2 LoadClientCertificate()
    {
        return _tlsOptions.CertificateSource switch
        {
            CertificateSource.WindowsCertificateStore => LoadFromWindowsStore(),
            CertificateSource.File => LoadFromFile(),
            _ => throw new InvalidOperationException(
                $"Unknown certificate source: {_tlsOptions.CertificateSource}")
        };
    }

    private X509Certificate2 LoadFromWindowsStore()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException(
                "Windows Certificate Store is only supported on Windows. Use CertificateSource.File on other platforms.");
        }

        if (string.IsNullOrWhiteSpace(_tlsOptions.CertificateThumbprint))
        {
            throw new InvalidOperationException(
                "CertificateThumbprint is required when CertificateSource is WindowsCertificateStore.");
        }

        // Normalize thumbprint: remove spaces and convert to uppercase
        var thumbprint = _tlsOptions.CertificateThumbprint
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        // Search in both CurrentUser and LocalMachine stores
        foreach (var storeLocation in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, storeLocation);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            var certs = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                validOnly: true);

            if (certs.Count > 0)
            {
                _logger.LogDebug(
                    "Certificate found in {StoreLocation} store",
                    storeLocation);
                return new X509Certificate2(certs[0]);
            }
        }

        throw new InvalidOperationException(
            $"No valid certificate with the specified thumbprint was found in the Windows Certificate Store.");
    }

    private X509Certificate2 LoadFromFile()
    {
        if (string.IsNullOrWhiteSpace(_tlsOptions.CertificatePath))
        {
            throw new InvalidOperationException(
                "CertificatePath is required when CertificateSource is File.");
        }

        if (!File.Exists(_tlsOptions.CertificatePath))
        {
            throw new FileNotFoundException(
                $"Certificate file not found.",
                _tlsOptions.CertificatePath);
        }

        _logger.LogDebug("Loading certificate from file path");

        return new X509Certificate2(
            _tlsOptions.CertificatePath,
            (string?)null,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
    }

    private X509Certificate2Collection? LoadCaCertificates()
    {
        if (string.IsNullOrWhiteSpace(_tlsOptions.CaCertificatePath))
        {
            return null;
        }

        if (!File.Exists(_tlsOptions.CaCertificatePath))
        {
            throw new FileNotFoundException(
                $"CA certificate bundle file not found.",
                _tlsOptions.CaCertificatePath);
        }

        var collection = new X509Certificate2Collection();
        collection.ImportFromPemFile(_tlsOptions.CaCertificatePath);

        _logger.LogDebug("Loaded {Count} CA certificate(s) from bundle", collection.Count);

        return collection;
    }

    private RemoteCertificateValidationCallback BuildCaValidationCallback(
        X509Certificate2Collection caCerts)
    {
        return (sender, certificate, chain, sslPolicyErrors) =>
        {
            if (certificate is null)
            {
                _logger.LogWarning("TLS: Server certificate is null - rejecting connection");
                return false;
            }

            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // Attempt validation against the configured CA bundle
            using var customChain = new X509Chain();
            customChain.ChainPolicy.ExtraStore.AddRange(caCerts);
            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            var serverCert = new X509Certificate2(certificate);
            var isValid = customChain.Build(serverCert);

            if (!isValid)
            {
                // Log security event without exposing PHI (cert subject may contain org details but not PHI)
                _logger.LogError(
                    "TLS: Server certificate chain validation failed. Policy errors: {PolicyErrors}",
                    sslPolicyErrors);
            }

            return isValid;
        };
    }
}
