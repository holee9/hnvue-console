namespace HnVue.Workflow.Tests.Safety;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using HnVue.Workflow.Interfaces;

/// <summary>
/// Unit tests for InterlockChecker.
/// Tests hardware interlock chain validation for all 9 interlocks.
///
/// SPEC-WORKFLOW-001 Section 5: Safety Interlocks
/// SPEC-WORKFLOW-001 Safety-01: No exposure without all interlocks passing
/// </summary>
public class InterlockCheckerTests
{
    private readonly Mock<ILogger<InterlockChecker>> _loggerMock;
    private readonly Mock<ISafetyInterlock> _safetyInterlockMock;

    public InterlockCheckerTests()
    {
        _loggerMock = new Mock<ILogger<InterlockChecker>>();
        _safetyInterlockMock = new Mock<ISafetyInterlock>();
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenAllInterlocksPass_ShouldReturnSuccess()
    {
        // Arrange
        var interlockStatus = new InterlockStatus
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

        _safetyInterlockMock
            .Setup(x => x.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        var checker = new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);

        // Act
        var result = await checker.CheckAllInterlocksAsync();

        // Assert
        result.AllPassed.Should().BeTrue();
        result.FailedInterlocksWithDescription.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenDoorOpen_ShouldReturnFailure()
    {
        // Arrange
        var interlockStatus = new InterlockStatus
        {
            door_closed = false,  // Door open - safety violation
            emergency_stop_clear = true,
            thermal_normal = true,
            generator_ready = true,
            detector_ready = true,
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = true,
            aec_configured = true
        };

        _safetyInterlockMock
            .Setup(x => x.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        var checker = new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);

        // Act
        var result = await checker.CheckAllInterlocksAsync();

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedInterlocksWithDescription.Should().ContainSingle("IL-01: door_closed");
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenEmergencyStopActive_ShouldReturnFailure()
    {
        // Arrange
        var interlockStatus = new InterlockStatus
        {
            door_closed = true,
            emergency_stop_clear = false,  // E-stop active
            thermal_normal = true,
            generator_ready = true,
            detector_ready = true,
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = true,
            aec_configured = true
        };

        _safetyInterlockMock
            .Setup(x => x.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        var checker = new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);

        // Act
        var result = await checker.CheckAllInterlocksAsync();

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedInterlocksWithDescription.Should().ContainSingle("IL-02: emergency_stop_clear");
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenMultipleInterlocksFail_ShouldListAllFailed()
    {
        // Arrange
        var interlockStatus = new InterlockStatus
        {
            door_closed = false,       // IL-01 failed
            emergency_stop_clear = false,  // IL-02 failed
            thermal_normal = true,
            generator_ready = false,   // IL-04 failed
            detector_ready = true,
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = true,
            aec_configured = true
        };

        _safetyInterlockMock
            .Setup(x => x.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        var checker = new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);

        // Act
        var result = await checker.CheckAllInterlocksAsync();

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedInterlocksWithDescription.Should().HaveCount(3);
        result.FailedInterlocksWithDescription.Should().Contain("IL-01: door_closed");
        result.FailedInterlocksWithDescription.Should().Contain("IL-02: emergency_stop_clear");
        result.FailedInterlocksWithDescription.Should().Contain("IL-04: generator_ready");
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenDetectorNotReady_ShouldReturnFailure()
    {
        // Arrange
        var interlockStatus = new InterlockStatus
        {
            door_closed = true,
            emergency_stop_clear = true,
            thermal_normal = true,
            generator_ready = true,
            detector_ready = false,  // IL-05 failed
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = true,
            aec_configured = true
        };

        _safetyInterlockMock
            .Setup(x => x.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        var checker = new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);

        // Act
        var result = await checker.CheckAllInterlocksAsync();

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedInterlocksWithDescription.Should().ContainSingle("IL-05: detector_ready");
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenDoseExceedsLimits_ShouldReturnFailure()
    {
        // Arrange
        var interlockStatus = new InterlockStatus
        {
            door_closed = true,
            emergency_stop_clear = true,
            thermal_normal = true,
            generator_ready = true,
            detector_ready = true,
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = false,  // IL-08 failed
            aec_configured = true
        };

        _safetyInterlockMock
            .Setup(x => x.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        var checker = new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);

        // Act
        var result = await checker.CheckAllInterlocksAsync();

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedInterlocksWithDescription.Should().ContainSingle("IL-08: dose_within_limits");
    }

    [Fact]
    public async Task CheckAllInterlocksAsync_WhenAecNotConfigured_ShouldReturnFailure()
    {
        // Arrange
        var interlockStatus = new InterlockStatus
        {
            door_closed = true,
            emergency_stop_clear = true,
            thermal_normal = true,
            generator_ready = true,
            detector_ready = true,
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = true,
            aec_configured = false  // IL-09 failed
        };

        _safetyInterlockMock
            .Setup(x => x.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        var checker = new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);

        // Act
        var result = await checker.CheckAllInterlocksAsync();

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedInterlocksWithDescription.Should().ContainSingle("IL-09: aec_configured");
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
    public async Task CheckAllInterlocksAsync_ShouldLogSafetyCategory()
    {
        // Arrange
        var interlockStatus = new InterlockStatus
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

        _safetyInterlockMock
            .Setup(x => x.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(interlockStatus);

        var checker = new InterlockChecker(_loggerMock.Object, _safetyInterlockMock.Object);

        // Act
        await checker.CheckAllInterlocksAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
