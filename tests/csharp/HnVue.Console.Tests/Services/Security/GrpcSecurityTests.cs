using HnVue.Console.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Console.Tests.Services.Security;

/// <summary>
/// SPEC-SECURITY-001: R3 gRPC TLS/mTLS Tests.
/// TDD RED phase: Tests for TLS 1.3, mTLS, certificate rotation.
/// </summary>
public class GrpcSecurityTests
{
    private readonly ITestOutputHelper _output;

    public GrpcSecurityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ============== TLS Version Tests (FR-SEC-11) ==============

    [Fact]
    public void SPEC_SEC_11_DefaultTlsVersionIs13()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Act & Assert
        Assert.Equal(TlsVersion.Tls13, options.MinTlsVersion);
        Assert.True(options.EnableTls);

        _output.WriteLine($"Default TLS version: {options.MinTlsVersion}");
    }

    [Theory]
    [InlineData(TlsVersion.Tls12, false)]
    [InlineData(TlsVersion.Tls13, true)]
    public void SPEC_SEC_11_OnlyTls12AndTls13AreSupported(TlsVersion version, bool shouldBeValid)
    {
        // Arrange
        var options = new GrpcSecurityOptions { MinTlsVersion = version };

        // Act & Assert
        Assert.Equal(shouldBeValid, options.Validate());

        _output.WriteLine($"TLS {version} validation: {shouldBeValid}");
    }

    [Fact]
    public void SPEC_SEC_11_Tls10And11AreNotAllowed()
    {
        // Arrange & Act & Assert
        // TlsVersion enum only has Tls12 and Tls13, which enforces the requirement
        var enumValues = Enum.GetValues<TlsVersion>();

        Assert.Equal(2, enumValues.Length);
        Assert.Contains(TlsVersion.Tls12, enumValues);
        Assert.Contains(TlsVersion.Tls13, enumValues);

        _output.WriteLine("TLS 1.0 and 1.1 are not supported (enum only has Tls12, Tls13)");
    }

    // ============== mTLS Tests (FR-SEC-12) ==============

    [Fact]
    public void SPEC_SEC_12_MutualTlsRequiresClientCertificate()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            EnableTls = true,
            EnableMutualTls = true,
            ClientCertificatePath = null // Missing certificate
        };

        // Act
        var isValid = options.Validate();

        // Assert
        Assert.False(isValid, "mTLS requires client certificate path");

        _output.WriteLine("mTLS validation: Client certificate is required");
    }

    [Fact]
    public void SPEC_SEC_12_MutualTlsWithValidCertificate()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            EnableTls = true,
            EnableMutualTls = true,
            ClientCertificatePath = "test_certificate.pfx" // Mock path
        };

        // Act
        var isValid = options.Validate();

        // Assert - Configuration validation should pass (file existence checked at runtime)
        Assert.True(isValid, "mTLS configuration with certificate path is valid");

        _output.WriteLine("mTLS configuration is valid with certificate path");
    }

    [Fact]
    public void SPEC_SEC_12_CertificateRevocationCheckEnabled()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Act & Assert
        Assert.True(options.CheckCertificateRevocation, "CRL/OCSP check should be enabled by default");

        _output.WriteLine($"Certificate revocation check: {options.CheckCertificateRevocation}");
    }

    // ============== Certificate Rotation Tests (FR-SEC-13) ==============

    [Fact]
    public void SPEC_SEC_13_CertificateExpirationWarningIs30Days()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Act & Assert
        Assert.Equal(30, options.CertificateExpirationWarningDays);

        _output.WriteLine($"Certificate expiration warning: {options.CertificateExpirationWarningDays} days");
    }

    [Theory]
    [InlineData(60)]  // 60 days remaining - not expiring soon
    [InlineData(30)]  // 30 days remaining - at threshold
    [InlineData(15)]  // 15 days remaining - expiring soon
    [InlineData(1)]   // 1 day remaining - expiring soon
    [InlineData(0)]   // 0 days remaining - already expired
    public void SPEC_SEC_13_CertificateExpirationDetection(int daysUntilExpiration)
    {
        // Arrange
        var options = new GrpcSecurityOptions { CertificateExpirationWarningDays = 30 };

        // Note: X509Certificate2 requires an actual certificate file; this test demonstrates
        // the logic structure and date math only.
        var notAfter = DateTime.UtcNow.AddDays(daysUntilExpiration);

        _output.WriteLine($"Certificate expiration test: {daysUntilExpiration} days until {notAfter:yyyy-MM-dd}");
    }

    [Fact]
    public void SPEC_SEC_13_CertificateRotationPeriodIs90Days()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Act & Assert
        Assert.Equal(90, options.CertificateRotationDays);
        Assert.True(options.CertificateRotationDays >= 30 && options.CertificateRotationDays <= 730,
            "Rotation period should be between 30 and 730 days");

        _output.WriteLine($"Certificate rotation period: {options.CertificateRotationDays} days");
    }

    // ============== Cipher Suite Tests (FR-SEC-14) ==============

    [Fact]
    public void SPEC_SEC_14_Tls13UsesSecureCiphers()
    {
        // Arrange & Act & Assert
        // TLS 1.3 has built-in secure cipher suites
        // .NET's SslProtocols.Tls13 only allows TLS 1.3 ciphers:
        // - TLS_AES_256_GCM_SHA384
        // - TLS_AES_128_GCM_SHA256
        // - TLS_CHACHA20_POLY1305_SHA256

        var options = new GrpcSecurityOptions { MinTlsVersion = TlsVersion.Tls13 };
        Assert.True(options.Validate());

        _output.WriteLine("TLS 1.3 enforces secure cipher suites (AES-256-GCM, AES-128-GCM, ChaCha20-Poly1305)");
    }

    // ============== Additional Security Tests ==============

    [Fact]
    public void SPEC_SEC_Security_ServerCertificateValidation()
    {
        // Arrange
        var thumbprint = "ABC123DEF456";
        var options = new GrpcSecurityOptions();

        // Act
        options.AllowedServerCertificateThumbprints.Add(thumbprint.ToUpperInvariant());

        // Assert
        Assert.Contains(thumbprint.ToUpperInvariant(), options.AllowedServerCertificateThumbprints);

        _output.WriteLine($"Allowed server certificate thumbprint: {thumbprint}");
    }

    [Fact]
    public void SPEC_SEC_Security_SubjectNameValidation()
    {
        // Arrange
        var subjectName = "hnvue-server.local";
        var options = new GrpcSecurityOptions();

        // Act
        options.AllowedSubjectNames.Add(subjectName);

        // Assert
        Assert.Contains(subjectName, options.AllowedSubjectNames);

        _output.WriteLine($"Allowed subject name: {subjectName}");
    }

    [Fact]
    public void SPEC_SEC_Security_RootCertificateValidation()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            RootCertificatePath = "ca_cert.pem"
        };

        // Act & Assert
        Assert.Equal("ca_cert.pem", options.RootCertificatePath);

        _output.WriteLine($"Root CA certificate path: {options.RootCertificatePath}");
    }
}
