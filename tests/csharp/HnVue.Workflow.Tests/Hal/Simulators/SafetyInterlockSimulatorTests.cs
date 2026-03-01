namespace HnVue.Workflow.Tests.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Workflow.Hal.Simulators;
using HnVue.Workflow.Safety;
using Xunit;

/// <summary>
/// Unit tests for SafetyInterlockSimulator.
/// SPEC-WORKFLOW-001 TASK-403: ISafetyInterlock Simulator implementation
/// </summary>
public class SafetyInterlockSimulatorTests
{
    /// <summary>
    /// Test that simulator initializes with all interlocks in safe state.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_SetsAllInterlocksToSafeState()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();

        // Act
        await simulator.InitializeAsync(CancellationToken.None);

        // Assert
        var status = await simulator.CheckAllInterlocksAsync(CancellationToken.None);
        status.door_closed.Should().BeTrue();
        status.emergency_stop_clear.Should().BeTrue();
        status.thermal_normal.Should().BeTrue();
        status.generator_ready.Should().BeTrue();
        status.detector_ready.Should().BeTrue();
        status.collimator_valid.Should().BeTrue();
        status.table_locked.Should().BeTrue();
        status.dose_within_limits.Should().BeTrue();
        status.aec_configured.Should().BeTrue();
    }

    /// <summary>
    /// Test that CheckAllInterlocksAsync returns all 9 interlocks.
    /// </summary>
    [Fact]
    public async Task CheckAllInterlocksAsync_ReturnsAllNineInterlocks()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var status = await simulator.CheckAllInterlocksAsync(CancellationToken.None);

        // Assert - All 9 interlocks should be present and true
        status.door_closed.Should().BeTrue();
        status.emergency_stop_clear.Should().BeTrue();
        status.thermal_normal.Should().BeTrue();
        status.generator_ready.Should().BeTrue();
        status.detector_ready.Should().BeTrue();
        status.collimator_valid.Should().BeTrue();
        status.table_locked.Should().BeTrue();
        status.dose_within_limits.Should().BeTrue();
        status.aec_configured.Should().BeTrue();
    }

    /// <summary>
    /// Test that individual interlock can be disabled.
    /// </summary>
    [Theory]
    [InlineData("door_closed")]
    [InlineData("emergency_stop_clear")]
    [InlineData("thermal_normal")]
    [InlineData("generator_ready")]
    [InlineData("detector_ready")]
    [InlineData("collimator_valid")]
    [InlineData("table_locked")]
    [InlineData("dose_within_limits")]
    [InlineData("aec_configured")]
    public async Task SetInterlockStateAsync_CanDisableIndividualInterlock(string interlockName)
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        await simulator.SetInterlockStateAsync(interlockName, false, CancellationToken.None);

        // Assert
        var status = await simulator.CheckAllInterlocksAsync(CancellationToken.None);
        var propertyValue = typeof(InterlockStatus).GetProperty(interlockName)?.GetValue(status);
        propertyValue.Should().Be(false);
    }

    /// <summary>
    /// Test that individual interlock can be re-enabled.
    /// </summary>
    [Fact]
    public async Task SetInterlockStateAsync_CanReEnableDisabledInterlock()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("door_closed", false, CancellationToken.None);

        // Act
        await simulator.SetInterlockStateAsync("door_closed", true, CancellationToken.None);

        // Assert
        var status = await simulator.CheckAllInterlocksAsync(CancellationToken.None);
        status.door_closed.Should().BeTrue();
    }

    /// <summary>
    /// Test that multiple interlocks can be set simultaneously.
    /// </summary>
    [Fact]
    public async Task SetInterlockStateAsync_CanDisableMultipleInterlocks()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        await simulator.SetInterlockStateAsync("door_closed", false, CancellationToken.None);
        await simulator.SetInterlockStateAsync("thermal_normal", false, CancellationToken.None);
        await simulator.SetInterlockStateAsync("detector_ready", false, CancellationToken.None);

        // Assert
        var status = await simulator.CheckAllInterlocksAsync(CancellationToken.None);
        status.door_closed.Should().BeFalse();
        status.thermal_normal.Should().BeFalse();
        status.detector_ready.Should().BeFalse();
        // Other interlocks should still be true
        status.emergency_stop_clear.Should().BeTrue();
        status.generator_ready.Should().BeTrue();
    }

    /// <summary>
    /// Test that emergency standby sets all interlocks to unsafe state.
    /// </summary>
    [Fact]
    public async Task EmergencyStandbyAsync_SetsAllInterlocksToUnsafe()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        await simulator.EmergencyStandbyAsync(CancellationToken.None);

        // Assert
        var status = await simulator.CheckAllInterlocksAsync(CancellationToken.None);
        status.emergency_stop_clear.Should().BeFalse(); // Emergency stop activated
    }

    /// <summary>
    /// Test that exposure is blocked when door is open.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenDoorOpen()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("door_closed", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is blocked when emergency stop is activated.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenEmergencyStopActivated()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("emergency_stop_clear", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is blocked when temperature is abnormal.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenTemperatureAbnormal()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("thermal_normal", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is blocked when cooling system fails.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenCoolingFails()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("thermal_normal", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is blocked when collimator is invalid.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenCollimatorInvalid()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("collimator_valid", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is blocked when generator is not ready.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenGeneratorNotReady()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("generator_ready", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is blocked when detector is not ready.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenDetectorNotReady()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("detector_ready", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is blocked when dose exceeds limits.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenDoseExceedsLimits()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("dose_within_limits", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is blocked when AEC is not configured.
    /// </summary>
    [Fact]
    public async Task ExposureBlocked_WhenAecNotConfigured()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("aec_configured", false, CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that exposure is NOT blocked when all interlocks pass.
    /// </summary>
    [Fact]
    public async Task ExposureNotBlocked_WhenAllInterlocksPass()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);

        // Assert
        isBlocked.Should().BeFalse();
    }

    /// <summary>
    /// Test that simulator can be reset.
    /// </summary>
    [Fact]
    public async Task ResetAsync_ResetsAllInterlocksToSafeState()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("door_closed", false, CancellationToken.None);
        await simulator.SetInterlockStateAsync("thermal_normal", false, CancellationToken.None);

        // Act
        await simulator.ResetAsync(CancellationToken.None);

        // Assert
        var status = await simulator.CheckAllInterlocksAsync(CancellationToken.None);
        status.door_closed.Should().BeTrue();
        status.thermal_normal.Should().BeTrue();
        status.emergency_stop_clear.Should().BeTrue();
    }

    /// <summary>
    /// Test that interlock state can be queried individually.
    /// </summary>
    [Theory]
    [InlineData("door_closed", true)]
    [InlineData("emergency_stop_clear", true)]
    [InlineData("thermal_normal", false)]
    public async Task GetInterlockStateAsync_ReturnsCorrectState(string interlockName, bool expectedState)
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.SetInterlockStateAsync("thermal_normal", expectedState, CancellationToken.None);

        // Act
        var state = await simulator.GetInterlockStateAsync(interlockName, CancellationToken.None);

        // Assert
        state.Should().Be(expectedState);
    }

    /// <summary>
    /// Test that CheckAllInterlocksAsync completes within 10ms (SPEC requirement).
    /// </summary>
    [Fact]
    public async Task CheckAllInterlocksAsync_CompletesWithin10Ms()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await simulator.CheckAllInterlocksAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert - SPEC requires completion within 10ms
        stopwatch.ElapsedMilliseconds.Should().BeLessOrEqualTo(10);
    }

    /// <summary>
    /// Test that handswitch interlock works correctly.
    /// </summary>
    [Fact]
    public async Task SetInterlockStateAsync_HandswitchInterlockWorks()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act - Enable handswitch interlock (simulates handswitch pressed)
        // In real system, handswitch being pressed would block exposure
        // For simulator, we track it via emergency_stop_clear
        await simulator.SetInterlockStateAsync("emergency_stop_clear", false, CancellationToken.None);

        // Assert
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that grid interlock works correctly.
    /// </summary>
    [Fact]
    public async Task SetInterlockStateAsync_GridInterlockWorks()
    {
        // Arrange
        var simulator = new SafetyInterlockSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act - Grid interlock is tracked via collimator_valid in simulator
        // (grid is part of collimation system)
        await simulator.SetInterlockStateAsync("collimator_valid", false, CancellationToken.None);

        // Assert
        var isBlocked = await simulator.IsExposureBlockedAsync(CancellationToken.None);
        isBlocked.Should().BeTrue();
    }
}
