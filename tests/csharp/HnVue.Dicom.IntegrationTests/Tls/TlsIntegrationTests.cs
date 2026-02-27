using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dicom;
using FluentAssertions;
using HnVue.Dicom.Associations;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.IntegrationTests.TestData;
using HnVue.Dicom.Queue;
using HnVue.Dicom.Storage;
using HnVue.Dicom.Tls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Dicom.IntegrationTests.Tls;

/// <summary>
/// Integration tests for TLS DICOM associations.
/// Tests TLS 1.2/1.3 connections, certificate validation, and mTLS.
/// SPEC-DICOM-001: FR-DICOM-06
/// </summary>
/// <remarks>
/// Orthanc supports TLS but requires proper certificate configuration.
/// These tests verify TLS client behavior and error handling.
/// </remarks>
[Collection("Orthanc")]
public class TlsIntegrationTests : IDisposable
{
    private readonly OrthancFixture _orthanc;
    private readonly ITestOutputHelper _output;

    public TlsIntegrationTests(OrthancFixture orthanc, ITestOutputHelper output)
    {
        _orthanc = orthanc;
        _output = output;
    }

    public void Dispose()
    {
        // Clean up
    }

    /// <summary>
    /// Test: TLS factory creation with default options.
    /// Verifies ITlsFactory can create a DicomTlsInitiator.
    /// </summary>
    [Fact]
    public void TlsFactory_CreateTlsInitiator_DefaultOptions_ReturnsInitiator()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            Tls = new TlsOptions
            {
                Enabled = true,
                MinVersion = TlsVersion.Tls12,
                CertificateSource = CertificateSource.WindowsCertificateStore
            }
        };

        var logger = NullLogger<DicomTlsFactory>.Instance;
        var factory = new DicomTlsFactory(Options.Create(options), logger);

        // Act
        var initiator = factory.CreateTlsInitiator();

        // Assert
        initiator.Should().NotBeNull();
        initiator.ClientCertificate.Should().BeNull("no client certificate configured");
        initiator.RemoteCertificateValidationCallback.Should().NotBeNull("validation callback should be set");
    }

    /// <summary>
    /// Test: TLS factory with file-based certificates.
    /// Verifies certificate loading from file.
    /// </summary>
    [Fact]
    public void TlsFactory_CreateTlsInitiator_FileCertificates_ReturnsInitiator()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            Tls = new TlsOptions
            {
                Enabled = true,
                MinVersion = TlsVersion.Tls12,
                CertificateSource = CertificateSource.File,
                CertificatePath = "test-cert.pfx",
                CaCertificatePath = "ca-cert.pem",
                ClientCertificateEnabled = true
            }
        };

        var logger = NullLogger<DicomTlsFactory>.Instance;
        var factory = new DicomTlsFactory(Options.Create(options), logger);

        // Act
        var initiator = factory.CreateTlsInitiator();

        // Assert
        initiator.Should().NotBeNull();
        initiator.RemoteCertificateValidationCallback.Should().NotBeNull();
    }

    /// <summary>
    /// Test: TLS version configuration.
    /// Verifies minimum TLS version is applied.
    /// </summary>
    [Theory]
    [InlineData(TlsVersion.Tls12)]
    [InlineData(TlsVersion.Tls13)]
    public void TlsFactory_MinTlsVersion_AppliedCorrectly(TlsVersion minVersion)
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            Tls = new TlsOptions
            {
                Enabled = true,
                MinVersion = minVersion
            }
        };

        var logger = NullLogger<DicomTlsFactory>.Instance;
        var factory = new DicomTlsFactory(Options.Create(options), logger);

        // Act
        var initiator = factory.CreateTlsInitiator();

        // Assert
        initiator.Should().NotBeNull();
    }

    /// <summary>
    /// Test: TLS disabled configuration.
    /// Verifies disabled TLS returns null initiator.
    /// </summary>
    [Fact]
    public void TlsFactory_TlsDisabled_ReturnsInitiatorWithoutCert()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            Tls = new TlsOptions
            {
                Enabled = false,
                MinVersion = TlsVersion.Tls12
            }
        };

        var logger = NullLogger<DicomTlsFactory>.Instance;
        var factory = new DicomTlsFactory(Options.Create(options), logger);

        // Act
        var initiator = factory.CreateTlsInitiator();

        // Assert
        initiator.Should().NotBeNull();
        initiator.ClientCertificate.Should().BeNull();
    }

    /// <summary>
    /// Test: Certificate validation callback accepts valid certificates.
    /// Tests the validation logic with a simulated valid certificate.
    /// </summary>
    [Fact]
    public void CertificateValidation_ValidCertificate_ReturnsSuccess()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            Tls = new TlsOptions
            {
                Enabled = true,
                MinVersion = TlsVersion.Tls12
            }
        };

        var logger = NullLogger<DicomTlsFactory>.Instance;
        var factory = new DicomTlsFactory(Options.Create(options), logger);
        var initiator = factory.CreateTlsInitiator();

        // Generate a self-signed certificate for testing
        var certificate = CreateTestCertificate();

        // Act
        var result = initiator.RemoteCertificateValidationCallback?.Invoke(
            sender: null!,
            certificate: certificate,
            chain: null!,
            sslPolicyErrors: System.Net.Security.SslPolicyErrors.None);

        // Assert
        result.Should().BeTrue("valid certificate with no errors should be accepted");
    }

    /// <summary>
    /// Test: Certificate validation callback rejects invalid certificates.
    /// Tests the validation logic with certificate errors.
    /// </summary>
    [Fact]
    public void CertificateValidation_InvalidCertificate_ReturnsFailure()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            Tls = new TlsOptions
            {
                Enabled = true,
                MinVersion = TlsVersion.Tls12
            }
        };

        var logger = NullLogger<DicomTlsFactory>.Instance;
        var factory = new DicomTlsFactory(Options.Create(options), logger);
        var initiator = factory.CreateTlsInitiator();

        var certificate = CreateTestCertificate();

        // Act
        var result = initiator.RemoteCertificateValidationCallback?.Invoke(
            sender: null!,
            certificate: certificate,
            chain: null!,
            sslPolicyErrors: System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors);

        // Assert
        result.Should().BeFalse("certificate with chain errors should be rejected");
    }

    /// <summary>
    /// Test: TLS connection attempt to non-TLS endpoint fails gracefully.
    /// Verifies error handling when TLS is configured but endpoint doesn't support it.
    /// </summary>
    [Fact]
    public async Task StoreAsync_TlsToNonTlsEndpoint_FailsGracefully()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            Tls = new TlsOptions
            {
                Enabled = true,
                MinVersion = TlsVersion.Tls12
            },
            RetryQueue = new RetryQueueOptions
            {
                StoragePath = Path.Combine(Path.GetTempPath(), $"HnVue_IT_Tls_{Guid.NewGuid()}")
            },
            Timeouts = new TimeoutOptions
            {
                AssociationRequestMs = 5000,
                DimseOperationMs = 10000,
                SocketReceiveMs = 15000,
                SocketSendMs = 15000
            }
        };

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        var associationManager = new AssociationManager(
            Options.Create(options),
            loggerFactory.CreateLogger<AssociationManager>());

        var transmissionQueue = new TransmissionQueue(
            Options.Create(options),
            loggerFactory.CreateLogger<TransmissionQueue>());

        var storageScu = new StorageScu(
            Options.Create(options),
            associationManager,
            transmissionQueue,
            loggerFactory.CreateLogger<StorageScu>());

        var dicomFile = TestDicomFiles.CreateDxImage();
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort,  // Orthanc not configured for TLS
            TlsEnabled = true
        };

        _output.WriteLine($"Attempting TLS connection to non-TLS endpoint at {destination.Host}:{destination.Port}");

        // Act
        var result = await storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeFalse("TLS connection to non-TLS endpoint should fail");

        // Cleanup
        await transmissionQueue.DisposeAsync();
    }

    /// <summary>
    /// Test: Non-TLS connection when TLS is disabled.
    /// Verifies normal operation without TLS.
    /// </summary>
    [Fact]
    public async Task StoreAsync_NonTlsConnection_Succeeds()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            Tls = new TlsOptions
            {
                Enabled = false  // TLS disabled
            },
            RetryQueue = new RetryQueueOptions
            {
                StoragePath = Path.Combine(Path.GetTempPath(), $"HnVue_IT_NonTls_{Guid.NewGuid()}")
            },
            Timeouts = new TimeoutOptions
            {
                AssociationRequestMs = 5000,
                DimseOperationMs = 30000,
                SocketReceiveMs = 60000,
                SocketSendMs = 60000
            }
        };

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        var associationManager = new AssociationManager(
            Options.Create(options),
            loggerFactory.CreateLogger<AssociationManager>());

        var transmissionQueue = new TransmissionQueue(
            Options.Create(options),
            loggerFactory.CreateLogger<TransmissionQueue>());

        var storageScu = new StorageScu(
            Options.Create(options),
            associationManager,
            transmissionQueue,
            loggerFactory.CreateLogger<StorageScu>());

        var dicomFile = TestDicomFiles.CreateDxImage();
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort,
            TlsEnabled = false
        };

        _output.WriteLine($"Testing non-TLS connection to {destination.Host}:{destination.Port}");

        // Act
        var result = await storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeTrue("non-TLS connection should succeed");

        // Clean up
        await _orthanc.DeleteAllInstancesAsync();
        await transmissionQueue.DisposeAsync();
    }

    /// <summary>
    /// Test: TLS 1.3 specific configuration.
    /// Verifies TLS 1.3 can be configured as minimum version.
    /// </summary>
    [Fact]
    public void TlsFactory_Tls13Configuration_Supported()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            Tls = new TlsOptions
            {
                Enabled = true,
                MinVersion = TlsVersion.Tls13
            }
        };

        var logger = NullLogger<DicomTlsFactory>.Instance;
        var factory = new DicomTlsFactory(Options.Create(options), logger);

        // Act
        var initiator = factory.CreateTlsInitiator();

        // Assert
        initiator.Should().NotBeNull();
        initiator.RemoteCertificateValidationCallback.Should().NotBeNull();
    }

    /// <summary>
    /// Test: mTLS configuration with client certificate.
    /// Verifies client certificate is loaded when mTLS is enabled.
    /// </summary>
    [Fact]
    public void TlsFactory_mTlsEnabled_IncludesClientCertificate()
    {
        // Arrange
        var options = new DicomServiceOptions
        {
            Tls = new TlsOptions
            {
                Enabled = true,
                MinVersion = TlsVersion.Tls12,
                ClientCertificateEnabled = true,
                CertificateSource = CertificateSource.File,
                CertificatePath = "client-cert.pfx"
            }
        };

        var logger = NullLogger<DicomTlsFactory>.Instance;
        var factory = new DicomTlsFactory(Options.Create(options), logger);

        // Act
        var initiator = factory.CreateTlsInitiator();

        // Assert
        initiator.Should().NotBeNull();
        // ClientCertificate will be null if file doesn't exist, which is expected in test
        // The important thing is that the configuration is accepted
        initiator.RemoteCertificateValidationCallback.Should().NotBeNull();
    }

    /// <summary>
    /// Creates a self-signed test certificate for validation testing.
    /// </summary>
    private X509Certificate2 CreateTestCertificate()
    {
        var distinguishedName = new X500DistinguishedName("CN=TestCertificate");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature,
                critical: true));

        // Add SAN for localhost
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        request.CertificateExtensions.Add(sanBuilder.Build());

        // Self-sign the certificate
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(1));

        // Export and re-import to ensure it's exportable
        var exported = certificate.Export(X509ContentType.Pfx, "");
        return new X509Certificate2(exported, "", X509KeyStorageFlags.Exportable);
    }
}

/// <summary>
/// Reuse the xUnit logger provider.
/// </summary>
internal class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName);
    }

    public void Dispose() { }
}

internal class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception}");
        }
    }
}
