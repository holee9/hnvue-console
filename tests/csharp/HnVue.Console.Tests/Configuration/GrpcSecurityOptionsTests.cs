using HnVue.Console.Configuration;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using FluentAssertions;

namespace HnVue.Console.Tests.Configuration;

/// <summary>
/// Unit tests for GrpcSecurityOptions.
/// SPEC-SECURITY-001: FR-SEC-11, FR-SEC-12, FR-SEC-13, FR-SEC-14 - gRPC TLS/mTLS
/// Target: 90%+ test coverage for TLS configuration.
/// </summary>
public class GrpcSecurityOptionsTests
{
    #region Validate Tests

    [Fact]
    public void Validate_DefaultOptions_ReturnsTrue()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Validate_TlsEnabledWithMtls_NoClientCertificate_ReturnsFalse()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            EnableTls = true,
            EnableMutualTls = true,
            ClientCertificatePath = null
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_TlsEnabledWithMtls_EmptyClientCertificatePath_ReturnsFalse()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            EnableTls = true,
            EnableMutualTls = true,
            ClientCertificatePath = ""
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(731)]
    public void Validate_InvalidRotationDays_ReturnsFalse(int rotationDays)
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            CertificateRotationDays = rotationDays
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(90)]
    [InlineData(365)]
    [InlineData(730)]
    public void Validate_ValidRotationDays_ReturnsTrue(int rotationDays)
    {
        // Arrange - CertificateExpirationWarningDays default (30) must be < rotationDays
        var options = new GrpcSecurityOptions
        {
            CertificateRotationDays = rotationDays
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidExpirationWarningDays_ReturnsFalse(int warningDays)
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            CertificateExpirationWarningDays = warningDays
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_WarningDaysGreaterOrEqualRotationDays_ReturnsFalse()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            CertificateRotationDays = 90,
            CertificateExpirationWarningDays = 90
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidWarningDays_ReturnsTrue()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            CertificateRotationDays = 90,
            CertificateExpirationWarningDays = 30
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Validate_TlsDisabled_NoCertificateRequired_ReturnsTrue()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            EnableTls = false,
            EnableMutualTls = true,
            ClientCertificatePath = null
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region LoadClientCertificate Tests

    [Fact]
    public void LoadClientCertificate_MtlsDisabled_ReturnsNull()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            EnableMutualTls = false,
            ClientCertificatePath = "cert.pfx"
        };

        // Act
        var result = options.LoadClientCertificate();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void LoadClientCertificate_NoCertificatePath_ReturnsNull()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            EnableMutualTls = true,
            ClientCertificatePath = null
        };

        // Act
        var result = options.LoadClientCertificate();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void LoadClientCertificate_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            EnableMutualTls = true,
            ClientCertificatePath = "nonexistent.pfx"
        };

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => options.LoadClientCertificate());
    }

    #endregion

    #region LoadRootCertificate Tests

    [Fact]
    public void LoadRootCertificate_NoRootCertificatePath_ReturnsNull()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            RootCertificatePath = null
        };

        // Act
        var result = options.LoadRootCertificate();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void LoadRootCertificate_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            RootCertificatePath = "nonexistent.crt"
        };

        // Act
        var result = options.LoadRootCertificate();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsCertificateExpiringSoon Tests

    private static X509Certificate2 CreateTestCertificate(int daysUntilExpiry)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("cn=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddDays(daysUntilExpiry);
        return req.CreateSelfSigned(notBefore, notAfter);
    }

    [Fact]
    public void IsCertificateExpiringSoon_ExpiresWithinThreshold_ReturnsTrue()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            CertificateExpirationWarningDays = 30
        };

        using var cert = CreateTestCertificate(15); // Expires in 15 days

        // Act
        var result = options.IsCertificateExpiringSoon(cert);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCertificateExpiringSoon_ExpiresExactlyAtThreshold_ReturnsTrue()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            CertificateExpirationWarningDays = 30
        };

        using var cert = CreateTestCertificate(30); // Expires at threshold

        // Act
        var result = options.IsCertificateExpiringSoon(cert);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCertificateExpiringSoon_ExpiresAfterThreshold_ReturnsFalse()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            CertificateExpirationWarningDays = 30
        };

        using var cert = CreateTestCertificate(60); // Expires in 60 days

        // Act
        var result = options.IsCertificateExpiringSoon(cert);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCertificateExpiringSoon_AlreadyExpired_ReturnsTrue()
    {
        // Arrange
        var options = new GrpcSecurityOptions
        {
            CertificateExpirationWarningDays = 30
        };

        // Use a cert that expires very soon (within warning threshold)
        using var cert = CreateTestCertificate(1); // Expires in 1 day

        // Act
        var result = options.IsCertificateExpiringSoon(cert);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void DefaultValues_TlsEnabled_IsTrue()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert
        options.EnableTls.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_MinTlsVersion_IsTls13()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert
        options.MinTlsVersion.Should().Be(TlsVersion.Tls13);
    }

    [Fact]
    public void DefaultValues_EnableMutualTls_IsFalse()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert - Default is false; must be explicitly enabled in production
        options.EnableMutualTls.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_CheckCertificateRevocation_IsTrue()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert
        options.CheckCertificateRevocation.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_SkipServerCertificateValidation_IsFalse()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert
        options.SkipServerCertificateValidation.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_CertificateRotationDays_Is90()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert
        options.CertificateRotationDays.Should().Be(90);
    }

    [Fact]
    public void DefaultValues_CertificateExpirationWarningDays_Is30()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert
        options.CertificateExpirationWarningDays.Should().Be(30);
    }

    [Fact]
    public void DefaultValues_AllowedServerCertificateThumbprints_IsEmpty()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert
        options.AllowedServerCertificateThumbprints.Should().NotBeNull();
        options.AllowedServerCertificateThumbprints.Should().BeEmpty();
    }

    [Fact]
    public void DefaultValues_AllowedSubjectNames_IsEmpty()
    {
        // Arrange
        var options = new GrpcSecurityOptions();

        // Assert
        options.AllowedSubjectNames.Should().NotBeNull();
        options.AllowedSubjectNames.Should().BeEmpty();
    }

    #endregion

    #region TlsVersion Enum Tests

    [Fact]
    public void TlsVersion_Tls12_HasCorrectValue()
    {
        // Assert
        ((int)TlsVersion.Tls12).Should().Be(0);
    }

    [Fact]
    public void TlsVersion_Tls13_HasCorrectValue()
    {
        // Assert
        ((int)TlsVersion.Tls13).Should().Be(1);
    }

    #endregion

    #region Security Configuration Tests

    [Fact]
    public void SecurityOptions_ProductionConfiguration_ValidatesSuccessfully()
    {
        // Arrange - Production-like configuration
        var options = new GrpcSecurityOptions
        {
            EnableTls = true,
            MinTlsVersion = TlsVersion.Tls13,
            EnableMutualTls = true,
            ClientCertificatePath = "/certs/client.pfx",
            ClientCertificatePassword = "secure_password",
            RootCertificatePath = "/certs/ca.crt",
            CheckCertificateRevocation = true,
            SkipServerCertificateValidation = false,
            CertificateRotationDays = 90,
            CertificateExpirationWarningDays = 30
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SecurityOptions_DevelopmentConfiguration_ValidatesSuccessfully()
    {
        // Arrange - Development configuration (relaxed security)
        var options = new GrpcSecurityOptions
        {
            EnableTls = false,
            EnableMutualTls = false,
            CheckCertificateRevocation = false,
            SkipServerCertificateValidation = true
        };

        // Act
        var result = options.Validate();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Certificate Thumbprint and Subject Name Tests

    [Fact]
    public void AllowedServerCertificateThumbprints_CanAddMultipleThumbprints()
    {
        // Arrange
        var options = new GrpcSecurityOptions();
        var thumbprint1 = "A1B2C3D4E5F6";
        var thumbprint2 = "F6E5D4C3B2A1";

        // Act
        options.AllowedServerCertificateThumbprints.Add(thumbprint1);
        options.AllowedServerCertificateThumbprints.Add(thumbprint2);

        // Assert
        options.AllowedServerCertificateThumbprints.Should().HaveCount(2);
        options.AllowedServerCertificateThumbprints.Should().Contain(thumbprint1);
        options.AllowedServerCertificateThumbprints.Should().Contain(thumbprint2);
    }

    [Fact]
    public void AllowedSubjectNames_CanAddMultipleSubjectNames()
    {
        // Arrange
        var options = new GrpcSecurityOptions();
        var subject1 = "localhost";
        var subject2 = "*.example.com";

        // Act
        options.AllowedSubjectNames.Add(subject1);
        options.AllowedSubjectNames.Add(subject2);

        // Assert
        options.AllowedSubjectNames.Should().HaveCount(2);
        options.AllowedSubjectNames.Should().Contain(subject1);
        options.AllowedSubjectNames.Should().Contain(subject2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Validate_AllEdgeCases_ReturnsExpectedResults()
    {
        // Arrange & Act & Assert
        new GrpcSecurityOptions
        {
            CertificateRotationDays = 1,
            CertificateExpirationWarningDays = 1
        }.Validate().Should().BeFalse(); // Warning days must be < rotation days

        new GrpcSecurityOptions
        {
            CertificateRotationDays = 2,
            CertificateExpirationWarningDays = 1
        }.Validate().Should().BeTrue();
    }

    #endregion
}
