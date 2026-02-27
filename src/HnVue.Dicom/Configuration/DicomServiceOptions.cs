using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Configuration;

/// <summary>
/// Configuration options for HnVue DICOM services.
/// Bound via IOptions pattern from app configuration.
/// </summary>
public class DicomServiceOptions
{
    /// <summary>
    /// Gets or sets the calling AE Title (device identifier).
    /// Maximum 16 characters, uppercase letters and numbers only.
    /// </summary>
    public string CallingAeTitle { get; set; } = "HNVUE_CONSOLE";

    /// <summary>
    /// Gets or sets the organization's registered DICOM UID root.
    /// Used for generating Study, Series, SOP Instance, and MPPS UIDs.
    /// </summary>
    public string UidRoot { get; set; } = "2.25";

    /// <summary>
    /// Gets or sets the device serial number for UID generation.
    /// </summary>
    public string DeviceSerial { get; set; } = "HNVUE001";

    /// <summary>
    /// Gets or sets the association pool configuration.
    /// </summary>
    public AssociationPoolOptions AssociationPool { get; set; } = new();

    /// <summary>
    /// Gets or sets the retry queue configuration.
    /// </summary>
    public RetryQueueOptions RetryQueue { get; set; } = new();

    /// <summary>
    /// Gets or sets the TLS configuration.
    /// </summary>
    public TlsOptions Tls { get; set; } = new();

    /// <summary>
    /// Gets or sets the timeout configuration for DICOM operations.
    /// </summary>
    public TimeoutOptions Timeouts { get; set; } = new();

    /// <summary>
    /// Gets or sets the configured PACS destinations for C-STORE operations.
    /// </summary>
    public List<DicomDestination> StorageDestinations { get; set; } = new();

    /// <summary>
    /// Gets or sets the Modality Worklist SCP configuration.
    /// </summary>
    public DicomDestination? WorklistScp { get; set; }

    /// <summary>
    /// Gets or sets the MPPS SCP configuration.
    /// </summary>
    public DicomDestination? MppsScp { get; set; }

    /// <summary>
    /// Gets or sets the minimum log level for DICOM operations.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
}

/// <summary>
/// Configuration for DICOM association pooling.
/// </summary>
public class AssociationPoolOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent associations per destination.
    /// Default: 4
    /// </summary>
    public int MaxSize { get; set; } = 4;

    /// <summary>
    /// Gets or sets the connection acquisition timeout in milliseconds.
    /// Default: 30000 (30 seconds)
    /// </summary>
    public int AcquisitionTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the idle timeout in milliseconds before an association is released.
    /// Default: 30000 (30 seconds)
    /// </summary>
    public int IdleTimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Configuration for the persistent retry queue.
/// </summary>
public class RetryQueueOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// Default: 5
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the initial retry interval in seconds.
    /// Default: 30 seconds
    /// </summary>
    public int InitialIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the exponential back-off multiplier.
    /// Default: 2.0
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum retry interval in seconds.
    /// Default: 3600 seconds (1 hour)
    /// </summary>
    public int MaxIntervalSeconds { get; set; } = 3600;

    /// <summary>
    /// Gets or sets the path for persistent queue storage.
    /// Default: "./data/dicom-queue"
    /// </summary>
    public string StoragePath { get; set; } = "./data/dicom-queue";
}

/// <summary>
/// Configuration for TLS transport security.
/// </summary>
public class TlsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether TLS is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum TLS version.
    /// Default: Tls12
    /// </summary>
    public TlsVersion MinVersion { get; set; } = TlsVersion.Tls12;

    /// <summary>
    /// Gets or sets the certificate source.
    /// </summary>
    public CertificateSource CertificateSource { get; set; } = CertificateSource.WindowsCertificateStore;

    /// <summary>
    /// Gets or sets the certificate thumbprint for Windows Certificate Store lookup.
    /// Used when CertificateSource is WindowsCertificateStore.
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Gets or sets the path to the certificate file (PEM or PFX).
    /// Used when CertificateSource is File.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the path to the CA certificate bundle for server verification.
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether client certificate (mTLS) is used.
    /// </summary>
    public bool ClientCertificateEnabled { get; set; } = false;
}

/// <summary>
/// TLS versions supported by the DICOM service.
/// </summary>
public enum TlsVersion
{
    /// <summary>TLS 1.0 (not recommended for production)</summary>
    Tls10,

    /// <summary>TLS 1.1 (not recommended for production)</summary>
    Tls11,

    /// <summary>TLS 1.2 (minimum recommended)</summary>
    Tls12,

    /// <summary>TLS 1.3 (preferred)</summary>
    Tls13
}

/// <summary>
/// Certificate source options.
/// </summary>
public enum CertificateSource
{
    /// <summary>Windows Certificate Store (recommended for production)</summary>
    WindowsCertificateStore,

    /// <summary>File-based certificate (PEM or PFX)</summary>
    File
}

/// <summary>
/// Configuration for DICOM operation timeouts.
/// </summary>
public class TimeoutOptions
{
    /// <summary>
    /// Gets or sets the association request timeout in milliseconds.
    /// Default: 5000 (5 seconds)
    /// </summary>
    public int AssociationRequestMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the DIMSE operation timeout in milliseconds.
    /// Default: 30000 (30 seconds)
    /// </summary>
    public int DimseOperationMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the socket receive timeout in milliseconds.
    /// Default: 60000 (60 seconds)
    /// </summary>
    public int SocketReceiveMs { get; set; } = 60000;

    /// <summary>
    /// Gets or sets the socket send timeout in milliseconds.
    /// Default: 60000 (60 seconds)
    /// </summary>
    public int SocketSendMs { get; set; } = 60000;

    /// <summary>
    /// Gets or sets the Storage Commitment N-EVENT-REPORT timeout in milliseconds.
    /// Default: 300000 (300 seconds / 5 minutes)
    /// </summary>
    public int StorageCommitmentWaitMs { get; set; } = 300000;
}

/// <summary>
/// Configuration for a DICOM destination (SCP).
/// </summary>
public class DicomDestination
{
    /// <summary>
    /// Gets or sets the called AE Title (remote SCP identifier).
    /// </summary>
    public string AeTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the host address (IP or hostname).
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port number.
    /// </summary>
    public int Port { get; set; } = 104;

    /// <summary>
    /// Gets or sets a value indicating whether TLS is enabled for this destination.
    /// Overrides global TLS setting if set.
    /// </summary>
    public bool? TlsEnabled { get; set; }
}
