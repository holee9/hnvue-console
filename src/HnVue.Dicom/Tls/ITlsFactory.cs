using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace HnVue.Dicom.Tls;

/// <summary>
/// Factory for creating TLS initiators used during DICOM association establishment.
/// Abstracts certificate loading strategy (Windows Cert Store vs file-based).
/// </summary>
public interface ITlsFactory
{
    /// <summary>
    /// Creates a TLS initiator configured from the current <see cref="HnVue.Dicom.Configuration.TlsOptions"/>.
    /// </summary>
    /// <returns>A configured <see cref="DicomTlsInitiator"/> ready for use in DicomClient.</returns>
    DicomTlsInitiator CreateTlsInitiator();
}

/// <summary>
/// Encapsulates the TLS client configuration for a DICOM association.
/// Holds the client certificate (optional for mTLS) and the remote certificate validation callback.
/// </summary>
public sealed class DicomTlsInitiator
{
    /// <summary>
    /// Gets the client certificate used for mutual TLS (mTLS).
    /// Null when mTLS is not configured.
    /// </summary>
    public X509Certificate2? ClientCertificate { get; init; }

    /// <summary>
    /// Gets the remote server certificate validation callback.
    /// When null, the default .NET chain validation is used.
    /// </summary>
    public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; init; }

    /// <summary>
    /// Gets the CA certificate collection used to validate the server certificate chain.
    /// </summary>
    public X509Certificate2Collection? CaCertificates { get; init; }
}
