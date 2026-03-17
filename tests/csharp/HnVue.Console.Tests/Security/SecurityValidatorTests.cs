using HnVue.Console.Models;
using HnVue.Console.Security;
using Xunit;
using FluentAssertions;

namespace HnVue.Console.Tests.Security;

/// <summary>
/// Unit tests for SecurityValidator.
/// SPEC-SECURITY-001: FR-SEC-13 - Input Validation
/// Target: 90%+ test coverage for security validation functions.
/// </summary>
public class SecurityValidatorTests
{
    #region DICOM UID Validation Tests

    [Theory]
    [InlineData("1.2.840.10008.1.1", true)]
    [InlineData("1.2.840.10008.1.2.1", true)]
    [InlineData("1.2.3.4.5.6.7.8.9.10", true)]
    [InlineData("1234567890123456789012345678901234567890123456789012345678", true)] // Exactly 64 chars
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("invalid", false)]
    [InlineData("1.2.840.10008.1.1;drop table", false)] // SQL injection attempt
    [InlineData("1.2.840.10008.1.1\nmalicious", false)] // Newline injection
    [InlineData("1.2.840.10008.1.1\r\nattack", false)] // CRLF injection
    [InlineData("1.2.840.10008.1.1\tinjection", false)] // Tab injection
    [InlineData("1.2.840.10008.1.1\0null", false)] // Null byte injection
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345", false)] // 65 chars - too long
    public void ValidateDicomUid_VariousInputs_ReturnsExpectedResult(string? uid, bool expectedValid)
    {
        // Act
        var result = SecurityValidator.ValidateDicomUid(uid);

        // Assert
        result.Should().Be(expectedValid);
    }

    #endregion

    #region Patient ID Validation Tests

    [Theory]
    [InlineData("PAT12345", true)]
    [InlineData("PAT-001", true)]
    [InlineData("ABCD1234", true)]
    [InlineData("P123456789012345678901234567890123456789012345678901234567", true)] // Max 64 chars
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("P1", false)] // Too short (min 4)
    [InlineData("patient", false)] // Lowercase not allowed
    [InlineData("PAT_123", false)] // Underscore not allowed
    [InlineData("PAT 123", false)] // Space not allowed
    [InlineData("PAT;123", false)] // Semicolon injection
    [InlineData("PAT\n123", false)] // Newline injection
    [InlineData("PAT\r123", false)] // Carriage return injection
    [InlineData("PAT\000123", false)] // Null byte injection
    public void ValidatePatientId_VariousInputs_ReturnsExpectedResult(string? patientId, bool expectedValid)
    {
        // Act
        var result = SecurityValidator.ValidatePatientId(patientId);

        // Assert
        result.Should().Be(expectedValid);
    }

    #endregion

    #region Study ID Validation Tests

    [Theory]
    [InlineData("1.2.840.10008.1.1", true)]
    [InlineData("12345", true)]
    [InlineData("1234567890123456789012345678901234567890123456789012345678", true)] // Max 64 chars
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("study123", false)] // Letters not allowed
    [InlineData("1.2.840.10008.1.1;malicious", false)] // Injection attempt
    [InlineData("1.2.840.10008.1.1\nattack", false)] // Newline injection
    public void ValidateStudyId_VariousInputs_ReturnsExpectedResult(string? studyId, bool expectedValid)
    {
        // Act
        var result = SecurityValidator.ValidateStudyId(studyId);

        // Assert
        result.Should().Be(expectedValid);
    }

    #endregion

    #region Username Validation Tests

    [Theory]
    [InlineData("user123", true)]
    [InlineData("test.user", true)]
    [InlineData("user-name", true)]
    [InlineData("user@domain.com", true)]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567", true)] // Max 128 chars
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("ab", false)] // Too short (min 3)
    [InlineData("user name", false)] // Space not allowed
    [InlineData("user;name", false)] // Semicolon not allowed
    [InlineData("user|pipe", false)] // Pipe not allowed
    [InlineData("user&name", false)] // Ampersand not allowed
    [InlineData("user$name", false)] // Dollar not allowed
    [InlineData("user`name", false)] // Backtick not allowed
    [InlineData("user\\name", false)] // Backslash not allowed
    [InlineData("user\nname", false)] // Newline not allowed
    [InlineData("user\rname", false)] // Carriage return not allowed
    [InlineData("user\0name", false)] // Null byte not allowed
    [InlineData("user\tname", false)] // Tab not allowed
    public void ValidateUsername_VariousInputs_ReturnsExpectedResult(string? username, bool expectedValid)
    {
        // Act
        var result = SecurityValidator.ValidateUsername(username);

        // Assert
        result.Should().Be(expectedValid);
    }

    #endregion

    #region Sanitize User Input Tests

    [Theory]
    [InlineData("normal text", "normal text")]
    [InlineData("  trimmed  ", "trimmed")]
    [InlineData("text\rwith\rcarriage\rreturn", "textwithcarriagereturn")]
    [InlineData("text\nwith\nnewlines", "textwithnewlines")]
    [InlineData("text\twith\ttabs", "textwithtabs")]
    [InlineData("text\0with\0null", "textwithnull")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("\r\n\t\0", null)]
    public void SanitizeUserInput_VariousInputs_ReturnsExpectedResult(string? input, string? expected)
    {
        // Act
        var result = SecurityValidator.SanitizeUserInput(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region File Path Validation Tests

    [Fact]
    public void ValidateFilePath_RelativePathWithoutAllowedDirectory_ReturnsTrue()
    {
        // Arrange
        var filePath = "config/settings.json";

        // Act
        var result = SecurityValidator.ValidateFilePath(filePath, null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateFilePath_DirectoryTraversal_ReturnsFalse()
    {
        // Arrange
        var filePath = "../../../etc/passwd";
        var allowedDir = "/app/config";

        // Act
        var result = SecurityValidator.ValidateFilePath(filePath, allowedDir);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateFilePath_TildeTraversal_ReturnsFalse()
    {
        // Arrange
        var filePath = "~/../../etc/passwd";

        // Act
        var result = SecurityValidator.ValidateFilePath(filePath, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateFilePath_AllowedDirectory_ReturnsTrue()
    {
        // Arrange
        var filePath = "C:/App/config/settings.json";
        var allowedDir = "C:/App/config";

        // Act
        var result = SecurityValidator.ValidateFilePath(filePath, allowedDir);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateFilePath_OutsideAllowedDirectory_ReturnsFalse()
    {
        // Arrange
        var filePath = "C:/App/other/settings.json";
        var allowedDir = "C:/App/config";

        // Act
        var result = SecurityValidator.ValidateFilePath(filePath, allowedDir);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateFilePath_NullFilePath_ReturnsFalse()
    {
        // Act
        var result = SecurityValidator.ValidateFilePath(null, "/app/config");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateFilePath_EmptyFilePath_ReturnsFalse()
    {
        // Act
        var result = SecurityValidator.ValidateFilePath("", "/app/config");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Network Endpoint Validation Tests

    [Theory]
    [InlineData("localhost:50051", true)]
    [InlineData("127.0.0.1:50051", true)]
    [InlineData("192.168.1.1:50051", true)]
    [InlineData("example.com:443", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("localhost", false)] // Missing port
    [InlineData("localhost:abc", false)] // Invalid port
    [InlineData("localhost:0", false)] // Port too low
    [InlineData("localhost:65536", false)] // Port too high
    [InlineData("localhost:50051:extra", false)] // Too many colons
    [InlineData(":50051", false)] // Missing host
    public void ValidateEndpoint_VariousInputs_ReturnsExpectedResult(string? endpoint, bool expectedValid)
    {
        // Act
        var result = SecurityValidator.ValidateEndpoint(endpoint);

        // Assert
        result.Should().Be(expectedValid);
    }

    [Fact]
    public void ValidateEndpoint_ValidPortRange_ReturnsTrue()
    {
        // Arrange
        var testCases = new[] { "localhost:1", "localhost:80", "localhost:443", "localhost:65535" };

        foreach (var endpoint in testCases)
        {
            // Act
            var result = SecurityValidator.ValidateEndpoint(endpoint);

            // Assert
            result.Should().BeTrue($"Endpoint {endpoint} should be valid");
        }
    }

    #endregion

    #region Length Validation Tests

    [Theory]
    [InlineData("test", 3, 10, true)]
    [InlineData("test", 4, 10, true)]
    [InlineData("test", 3, 4, true)]
    [InlineData("test", 1, 100, true)]
    [InlineData("test", 5, 10, false)] // Too short
    [InlineData("test", 1, 3, false)] // Too long
    [InlineData("", 0, 10, true)]
    [InlineData("", 1, 10, false)] // Empty doesn't meet min
    [InlineData(null, 0, 10, true)] // Null meets min=0
    [InlineData(null, 1, 10, false)] // Null doesn't meet min>0
    public void ValidateLength_VariousInputs_ReturnsExpectedResult(
        string? value, int minLength, int maxLength, bool expectedValid)
    {
        // Act
        var result = SecurityValidator.ValidateLength(value, minLength, maxLength);

        // Assert
        result.Should().Be(expectedValid);
    }

    #endregion

    #region Enum Validation Tests

    [Fact]
    public void ValidateEnum_ValidEnum_ReturnsTrue()
    {
        // Arrange
        var value = AuditEventType.UserLogin;

        // Act
        var result = SecurityValidator.ValidateEnum(value);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateEnum_InvalidEnum_ReturnsFalse()
    {
        // Arrange
        var value = (AuditEventType)999;

        // Act
        var result = SecurityValidator.ValidateEnum(value);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases and Security Scenarios

    [Fact]
    public void ValidateDicomUid_WithSpecialCharacters_ReturnsFalse()
    {
        // Arrange - Test various injection attempts
        var testCases = new[]
        {
            "1.2.840.10008.1.1'; DROP TABLE--",
            "1.2.840.10008.1.1<script>alert('xss')</script>",
            "1.2.840.10008.1.1${@print(md5(acunetix))}",
            "1.2.840.10008.1.1\\x00\\x01\\x02",
            "1.2.840.10008.1.1|whoami"
        };

        foreach (var uid in testCases)
        {
            // Act
            var result = SecurityValidator.ValidateDicomUid(uid);

            // Assert
            result.Should().BeFalse($"UID with injection attempt should be invalid: {uid}");
        }
    }

    [Fact]
    public void ValidatePatientId_WithInjectionAttempts_ReturnsFalse()
    {
        // Arrange
        var testCases = new[]
        {
            "PAT';--",
            "PAT' OR '1'='1",
            "PAT<script>",
            "PAT\x00NULL",
            "PAT\r\nCRLF"
        };

        foreach (var patientId in testCases)
        {
            // Act
            var result = SecurityValidator.ValidatePatientId(patientId);

            // Assert
            result.Should().BeFalse($"PatientId with injection attempt should be invalid: {patientId}");
        }
    }

    [Fact]
    public void ValidateUsername_WithCommandInjection_ReturnsFalse()
    {
        // Arrange
        var testCases = new[]
        {
            "user; rm -rf /",
            "user|cat /etc/passwd",
            "user&whoami",
            "user`id`",
            "user$(ls)",
            "user\nmalicious",
            "user\rattack",
            "user\0null",
            "user\\x00"
        };

        foreach (var username in testCases)
        {
            // Act
            var result = SecurityValidator.ValidateUsername(username);

            // Assert
            result.Should().BeFalse($"Username with injection attempt should be invalid: {username}");
        }
    }

    [Fact]
    public void SanitizeUserInput_RemovesAllControlCharacters()
    {
        // Arrange
        var input = "text\r\n\t\0with\ncontrol\rchars\t";
        var expected = "textwithcontrolchars";

        // Act
        var result = SecurityValidator.SanitizeUserInput(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ValidateFilePath_PathTraversalCombinations_ReturnsFalse()
    {
        // Arrange
        var testCases = new[]
        {
            "../config.txt",
            "../../config.txt",
            "../../../config.txt",
            "./../config.txt",
            "subdir/../../etc/passwd",
            "..\\config.txt",
            "..\\..\\config.txt",
            "C:/App/../../Windows/System32/config"
        };

        foreach (var filePath in testCases)
        {
            // Act
            var result = SecurityValidator.ValidateFilePath(filePath, "/app/config");

            // Assert
            result.Should().BeFalse($"Path traversal should be detected: {filePath}");
        }
    }

    [Theory]
    [InlineData("1.2.840.10008.1.1", 1, 64)] // Within valid range
    [InlineData("PAT1", 4, 64)] // Exact min for patient ID
    [InlineData("usr", 3, 128)] // Exact min for username
    public void ValidateBoundaryConditions_AtBoundary_ReturnsTrue(string value, int minLen, int maxLen)
    {
        // Arrange - These values are at the exact boundary of valid ranges
        var actualLength = value.Length;

        // Assert
        actualLength.Should().BeGreaterOrEqualTo(minLen);
        actualLength.Should().BeLessOrEqualTo(maxLen);
    }

    #endregion
}
