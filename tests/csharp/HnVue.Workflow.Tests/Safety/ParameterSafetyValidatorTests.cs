using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.Tests.Safety;

/// <summary>
/// Tests for parameter safety validation.
/// SPEC-WORKFLOW-001 Section 5.2: Parameter Safety Validation
///
/// Safety Requirements:
/// - Safety-02: No parameters outside DeviceSafetyLimits bounds
/// - FR-WF-02-d: Protocol not allowed if exposure parameter exceeds safety limits
/// </summary>
public class ParameterSafetyValidatorTests
{
    private readonly Mock<ILogger<ParameterSafetyValidator>> _loggerMock;
    private readonly Mock<IDeviceSafetyLimits> _safetyLimitsMock;

    public ParameterSafetyValidatorTests()
    {
        _loggerMock = new Mock<ILogger<ParameterSafetyValidator>>();
        _safetyLimitsMock = new Mock<IDeviceSafetyLimits>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new ParameterSafetyValidator(null!, _safetyLimitsMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSafetyLimits_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new ParameterSafetyValidator(_loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("safetyLimits");
    }

    [Theory]
    [InlineData(50, 60, 100, 50)]   // Below min kVp (50 < 60)
    [InlineData(150, 100, 120, 50)]  // Above max kVp (150 > 120)
    public void ValidateExposureParameters_WhenKvpOutsideLimits_ShouldReturnFailure(
        decimal kvp,
        decimal minKvp,
        decimal maxKvp,
        int exposureTimeMs)
    {
        // Arrange
        var validator = CreateValidator();
        var parameters = new ExposureParameters
        {
            Kv = kvp,
            Ma = 100,
            ExposureTimeMs = exposureTimeMs
        };

        SetupSafetyLimits(minKvp: minKvp, maxKvp: maxKvp);

        // Act
        var result = validator.ValidateExposureParameters(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Parameter == "kVp");
        result.Violations.Should().Contain(v => v.Reason.Contains("outside allowed range"));
    }

    [Theory]
    [InlineData(50, -1, 500)]  // Below min mA
    [InlineData(50, 1000, 500)]  // Above max mA
    public void ValidateExposureParameters_WhenMaOutsideLimits_ShouldReturnFailure(
        int kvp,
        int ma,
        int exposureTimeMs)
    {
        // Arrange
        var validator = CreateValidator();
        var parameters = new ExposureParameters
        {
            Kv = kvp,
            Ma = ma,
            ExposureTimeMs = exposureTimeMs
        };

        SetupSafetyLimits(minMa: 1, maxMa: 500);

        // Act
        var result = validator.ValidateExposureParameters(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Parameter == "mA");
    }

    [Fact]
    public void ValidateExposureParameters_WhenExposureTimeExceedsMaximum_ShouldReturnFailure()
    {
        // Arrange
        var validator = CreateValidator();
        var parameters = new ExposureParameters
        {
            Kv = 80,
            Ma = 100,
            ExposureTimeMs = 5000  // Exceeds typical max
        };

        SetupSafetyLimits(maxExposureTimeMs: 3000);

        // Act
        var result = validator.ValidateExposureParameters(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Parameter == "ExposureTime");
        result.Violations.Should().Contain(v => v.Reason.Contains("exceeds maximum"));
    }

    [Fact]
    public void ValidateExposureParameters_WhenMasExceedsMaximum_ShouldReturnFailure()
    {
        // Arrange
        var validator = CreateValidator();
        var parameters = new ExposureParameters
        {
            Kv = 120,
            Ma = 500,
            ExposureTimeMs = 2000  // 120 * 500 * 2000 / 1000 = 120,000 mAs
        };

        SetupSafetyLimits(maxMas: 500);

        // Act
        var result = validator.ValidateExposureParameters(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Parameter == "mAs");
        result.Violations.Should().Contain(v => v.Reason.Contains("exceeds maximum"));
    }

    [Fact]
    public void ValidateExposureParameters_WhenDapExceedsWarningLevel_ShouldReturnWarning()
    {
        // Arrange
        var validator = CreateValidator();
        var parameters = new ExposureParameters
        {
            Kv = 100,
            Ma = 20,  // Reduced to keep mAs within limits
            ExposureTimeMs = 100
        };

        SetupSafetyLimits(dapWarningLevel: 10000, maxMas: 500);

        // Act
        // mAs = 100 * 20 * 100 / 1000 = 200, which is within the 500 limit
        // DAP estimate = 200 * 0.1 = 20, accumulated = 9985 + 20 = 10005, exceeds 10000
        var result = validator.ValidateExposureParameters(parameters, accumulatedStudyDap: 9985);

        // Assert
        result.IsValid.Should().BeTrue("DAP warning is a soft limit, not a hard failure");
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Parameter == "DAP");
        result.Warnings.Should().Contain(w => w.Reason.Contains("exceeds warning level"));
    }

    [Fact]
    public void ValidateExposureParameters_WhenAllParametersInSafeRange_ShouldReturnSuccess()
    {
        // Arrange
        var validator = CreateValidator();
        var parameters = new ExposureParameters
        {
            Kv = 80,
            Ma = 10,  // Reduced to keep mAs within limits
            ExposureTimeMs = 50  // Reduced to keep mAs within limits
        };

        SetupSafetyLimits(
            minKvp: 40,
            maxKvp: 150,
            minMa: 1,
            maxMa: 500,
            maxExposureTimeMs: 3000,
            maxMas: 500);

        // Act
        var result = validator.ValidateExposureParameters(parameters);

        // Assert
        // mAs = 80 * 10 * 50 / 1000 = 40, which is within the 500 limit
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
        result.HasWarnings.Should().BeFalse();
    }

    [Fact]
    public void ValidateExposureParameters_ShouldLogSafetyCategoryEntry()
    {
        // Arrange
        var validator = CreateValidator();
        var parameters = new ExposureParameters { Kv = 80, Ma = 100, ExposureTimeMs = 100 };
        SetupSafetyLimits();

        // Act
        validator.ValidateExposureParameters(parameters);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SAFETY")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private ParameterSafetyValidator CreateValidator()
    {
        return new ParameterSafetyValidator(_loggerMock.Object, _safetyLimitsMock.Object);
    }

    private void SetupSafetyLimits(
        decimal minKvp = 40,
        decimal maxKvp = 150,
        decimal minMa = 1,
        decimal maxMa = 500,
        int maxExposureTimeMs = 3000,
        decimal maxMas = 500,
        decimal dapWarningLevel = 50000)
    {
        _safetyLimitsMock.SetupGet(x => x.MinKvp).Returns(minKvp);
        _safetyLimitsMock.SetupGet(x => x.MaxKvp).Returns(maxKvp);
        _safetyLimitsMock.SetupGet(x => x.MinMa).Returns(minMa);
        _safetyLimitsMock.SetupGet(x => x.MaxMa).Returns(maxMa);
        _safetyLimitsMock.SetupGet(x => x.MaxExposureTime).Returns(maxExposureTimeMs);
        _safetyLimitsMock.SetupGet(x => x.MaxMas).Returns(maxMas);
        _safetyLimitsMock.SetupGet(x => x.DapWarningLevel).Returns(dapWarningLevel);
    }
}
