using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.Tests.Safety;

/// <summary>
/// Tests for safety interlock validation.
/// SPEC-WORKFLOW-001 Section 5.1: Hardware Interlock Chain (9 interlocks)
///
/// Safety Requirements:
/// - Safety-01: No exposure command when any interlock is not in required state
/// - Safety-04: All interlock checks logged with SAFETY category
/// - Safety-07: Fresh interlock re-evaluation required after guard failure
/// </summary>
public class SafetyInterlockTests
{
    private readonly Mock<ILogger<InterlockChecker>> _loggerMock;
    private readonly Mock<ISafetyInterlock> _safetyInterlockMock;

    public SafetyInterlockTests()
    {
        _loggerMock = new Mock<ILogger<InterlockChecker>>();
        _safetyInterlockMock = new Mock<ISafetyInterlock>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new InterlockChecker(null!, _safetyInterlockMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSafetyInterlock_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new InterlockChecker(_loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("safetyInterlock");
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenAllInterlocksPass_ShouldReturnSuccess()
    {
        // Arrange
        var checker = CreateInterlockChecker();
        var interlockStatus = CreateAllPassInterlockStatus();

        _safetyInterlockMock
            .Setup(s => s.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        // Act
        var result = await checker.CheckAllInterlocksAsync(CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.FailedInterlocks.Should().BeEmpty();
        result.CheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(100));
    }

    [Theory]
    [InlineData("IL-01", "door_closed", false)]
    [InlineData("IL-02", "emergency_stop_clear", false)]
    [InlineData("IL-03", "thermal_normal", false)]
    [InlineData("IL-04", "generator_ready", false)]
    [InlineData("IL-05", "detector_ready", false)]
    [InlineData("IL-06", "collimator_valid", false)]
    [InlineData("IL-07", "table_locked", false)]
    [InlineData("IL-08", "dose_within_limits", false)]
    [InlineData("IL-09", "aec_configured", false)]
    public async Task CheckAllInterlocksAsync_WhenSpecificInterlockFails_ShouldReturnFailure(
        string interlockId,
        string fieldName,
        bool fieldValue)
    {
        // Arrange
        var checker = CreateInterlockChecker();
        var interlockStatus = CreateInterlockStatusWithFailedField(fieldName, fieldValue);

        _safetyInterlockMock
            .Setup(s => s.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        // Act
        var result = await checker.CheckAllInterlocksAsync(CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.FailedInterlocks.Should().Contain(interlockId);
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenMultipleInterlocksFail_ShouldReturnAllFailedIds()
    {
        // Arrange
        var checker = CreateInterlockChecker();
        var interlockStatus = new InterlockStatus
        {
            door_closed = false,           // IL-01
            emergency_stop_clear = false,  // IL-02
            thermal_normal = true,
            generator_ready = false,       // IL-04
            detector_ready = false,        // IL-05
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = true,
            aec_configured = true
        };

        _safetyInterlockMock
            .Setup(s => s.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        // Act
        var result = await checker.CheckAllInterlocksAsync(CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.FailedInterlocks.Should().HaveCount(4);
        result.FailedInterlocks.Should().ContainInOrder("IL-01", "IL-02", "IL-04", "IL-05");
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_ShouldCompleteWithin10Ms()
    {
        // Arrange
        var checker = CreateInterlockChecker();
        var interlockStatus = CreateAllPassInterlockStatus();

        _safetyInterlockMock
            .Setup(s => s.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await checker.CheckAllInterlocksAsync(CancellationToken.None);
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessOrEqualTo(10,
            "interlock check must complete within 10ms per SPEC Section 5.1");
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenTimeout_ShouldTreatAsInterlockFailed()
    {
        // Arrange
        var checker = CreateInterlockChecker();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));

        _safetyInterlockMock
            .Setup(s => s.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(100, ct); // Simulate timeout
                return CreateAllPassInterlockStatus();
            });

        // Act
        var result = await checker.CheckAllInterlocksAsync(cts.Token);

        // Assert
        result.IsSafe.Should().BeFalse("timeout should be treated as interlock failure");
    }

    [Fact]
    public void InterlockStatus_ShouldContainAll9Fields()
    {
        // Arrange & Act
        var status = new InterlockStatus();

        // Assert
        status.door_closed.Should().Be(false);
        status.emergency_stop_clear.Should().Be(false);
        status.thermal_normal.Should().Be(false);
        status.generator_ready.Should().Be(false);
        status.detector_ready.Should().Be(false);
        status.collimator_valid.Should().Be(false);
        status.table_locked.Should().Be(false);
        status.dose_within_limits.Should().Be(false);
        status.aec_configured.Should().Be(false);
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_ShouldLogSafetyCategoryEntry()
    {
        // Arrange
        var checker = CreateInterlockChecker();
        var interlockStatus = CreateAllPassInterlockStatus();

        _safetyInterlockMock
            .Setup(s => s.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        // Act
        await checker.CheckAllInterlocksAsync(CancellationToken.None);

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

    private InterlockChecker CreateInterlockChecker()
    {
        return new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);
    }

    private InterlockStatus CreateAllPassInterlockStatus()
    {
        return new InterlockStatus
        {
            door_closed = true,
            emergency_stop_clear = true,
            thermal_normal = true,
            generator_ready = true,
            detector_ready = true,
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = true,
            aec_configured = true
        };
    }

    private InterlockStatus CreateInterlockStatusWithFailedField(string fieldName, bool fieldValue)
    {
        return new InterlockStatus
        {
            door_closed = fieldName == "door_closed" ? fieldValue : true,
            emergency_stop_clear = fieldName == "emergency_stop_clear" ? fieldValue : true,
            thermal_normal = fieldName == "thermal_normal" ? fieldValue : true,
            generator_ready = fieldName == "generator_ready" ? fieldValue : true,
            detector_ready = fieldName == "detector_ready" ? fieldValue : true,
            collimator_valid = fieldName == "collimator_valid" ? fieldValue : true,
            table_locked = fieldName == "table_locked" ? fieldValue : true,
            dose_within_limits = fieldName == "dose_within_limits" ? fieldValue : true,
            aec_configured = fieldName == "aec_configured" ? fieldValue : true
        };
    }
}
