namespace HnVue.Workflow.Tests.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Workflow.Hal.Simulators;
using HnVue.Workflow.Interfaces;
using Xunit;

/// <summary>
/// Unit tests for HvgDriverSimulator.
/// SPEC-WORKFLOW-001 TASK-401: IHvgDriver Simulator implementation
/// </summary>
public class HvgDriverSimulatorTests
{
    /// <summary>
    /// Test that simulator initializes in Idle state.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_SetsStateToIdle()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();

        // Act
        await simulator.InitializeAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Idle);
        status.IsReady.Should().BeFalse();
        status.FaultCode.Should().BeNull();
    }

    /// <summary>
    /// Test that PrepareAsync transitions from Idle to Preparing state.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_FromIdleTransitionsToPreparing()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var prepareTask = simulator.PrepareAsync(CancellationToken.None);

        // Assert - Should be in Preparing state immediately
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Preparing);
        status.IsReady.Should().BeFalse();

        await prepareTask;
    }

    /// <summary>
    /// Test that PrepareAsync completes and transitions to Ready state.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_CompletesAndTransitionsToReady()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        await simulator.PrepareAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Ready);
        status.IsReady.Should().BeTrue();
        status.FaultCode.Should().BeNull();
    }

    /// <summary>
    /// Test that TriggerExposureAsync fails when not in Ready state.
    /// </summary>
    [Fact]
    public async Task TriggerExposureAsync_WhenNotReadyReturnsFalse()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var result = await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 100 },
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Test that TriggerExposureAsync succeeds when in Ready state.
    /// </summary>
    [Fact]
    public async Task TriggerExposureAsync_WhenReadyReturnsTrue()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        // Act
        var result = await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 100 },
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Test that TriggerExposureAsync transitions to Exposing state.
    /// </summary>
    [Fact]
    public async Task TriggerExposureAsync_TransitionsToExposingState()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        // Act
        _ = simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 100 },
            CancellationToken.None);

        // Assert - Check status immediately after trigger
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Exposing);
    }

    /// <summary>
    /// Test that exposure completes and returns to Idle state.
    /// </summary>
    [Fact]
    public async Task TriggerExposureAsync_ExposureCompletesReturnsToIdle()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        // Act
        await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 10 }, // Short exposure
            CancellationToken.None);

        // Wait for exposure to complete
        await Task.Delay(100);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Idle);
    }

    /// <summary>
    /// Test that AbortExposureAsync stops ongoing exposure.
    /// </summary>
    [Fact]
    public async Task AbortExposureAsync_StopsOngoingExposure()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        // Start exposure (long enough to abort)
        var exposureTask = simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 1000 },
            CancellationToken.None);

        // Wait a bit then abort
        await Task.Delay(50);
        await simulator.AbortExposureAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Idle);
    }

    /// <summary>
    /// Test that fault injection works correctly.
    /// </summary>
    [Fact]
    public async Task SetFaultMode_EnablesFaultInjection()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        // Act
        simulator.SetFaultMode(true);

        // Try to trigger exposure - should fail due to fault
        var result = await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 100 },
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();

        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Fault);
        status.FaultCode.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Test that fault can be cleared.
    /// </summary>
    [Fact]
    public async Task ClearFaultAsync_ResetsToIdleState()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        simulator.SetFaultMode(true);
        await simulator.PrepareAsync(CancellationToken.None);
        _ = await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 100 },
            CancellationToken.None);

        // Act
        await simulator.ClearFaultAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Idle);
        status.FaultCode.Should().BeNull();
    }

    /// <summary>
    /// Test that GetStatusAsync returns current status.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ReturnsCurrentStatus()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var status = await simulator.GetStatusAsync(CancellationToken.None);

        // Assert
        status.State.Should().Be(HvgState.Idle);
        status.IsReady.Should().BeFalse();
        status.FaultCode.Should().BeNull();
    }

    /// <summary>
    /// Test that exposure timing is simulated correctly.
    /// </summary>
    [Fact]
    public async Task TriggerExposureAsync_SimulatesExposureTiming()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        var exposureTimeMs = 100;
        var startTime = DateTime.UtcNow;

        // Act
        var exposureTask = simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = exposureTimeMs },
            CancellationToken.None);

        // Check that we're in Exposing state during exposure
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Exposing);

        await exposureTask;

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert - Exposure should take approximately the specified time
        // Allow some tolerance for test execution
        elapsed.Should().BeGreaterOrEqualTo(exposureTimeMs - 10);
        elapsed.Should().BeLessThan(exposureTimeMs + 200); // Allow some overhead
    }

    /// <summary>
    /// Test that multiple exposures can be performed sequentially.
    /// </summary>
    [Fact]
    public async Task TriggerExposureAsync_MultipleExposuresSucceed()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // First exposure
        await simulator.PrepareAsync(CancellationToken.None);
        var result1 = await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 50 },
            CancellationToken.None);
        result1.Should().BeTrue();

        // Second exposure - needs to prepare again
        await simulator.PrepareAsync(CancellationToken.None);
        var result2 = await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 120, Ma = 150, Ms = 50 },
            CancellationToken.None);
        result2.Should().BeTrue();

        // Final state should be Idle
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Idle);
    }

    /// <summary>
    /// Test that cancellation token works for PrepareAsync.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_CancellationTokenCancelsOperation()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => simulator.PrepareAsync(cts.Token));
    }

    /// <summary>
    /// Test that cancellation token works for TriggerExposureAsync.
    /// </summary>
    [Fact]
    public async Task TriggerExposureAsync_CancellationTokenCancelsOperation()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => simulator.TriggerExposureAsync(
                new ExposureParameters { Kv = 100, Ma = 200, Ms = 100 },
                cts.Token));
    }

    /// <summary>
    /// Test that simulator handles state transitions correctly.
    /// </summary>
    [Fact]
    public async Task StateTransitions_FollowExpectedSequence()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();

        // Act & Assert - Initial state (before init)
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Initializing);

        // Initialize
        await simulator.InitializeAsync(CancellationToken.None);
        status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Idle);

        // Prepare
        await simulator.PrepareAsync(CancellationToken.None);
        status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Ready);

        // Start exposure
        _ = simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 10 },
            CancellationToken.None);
        status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Exposing);

        // Wait for completion
        await Task.Delay(50);
        status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Idle);
    }

    /// <summary>
    /// Test that exposure parameters are validated.
    /// </summary>
    [Theory]
    [InlineData(0, 200, 100)]    // Invalid kV
    [InlineData(100, 0, 100)]    // Invalid mA
    [InlineData(100, 200, 0)]    // Invalid ms
    [InlineData(-1, 200, 100)]   // Negative kV
    [InlineData(100, -1, 100)]   // Negative mA
    public async Task TriggerExposureAsync_InvalidParametersReturnFalse(
        int kv, int ma, int ms)
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        // Act
        var result = await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = kv, Ma = ma, Ms = ms },
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Test that last exposure parameters are tracked.
    /// </summary>
    [Fact]
    public async Task GetLastExposureParameters_ReturnsLastExposure()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        var parameters = new ExposureParameters { Kv = 120, Ma = 300, Ms = 200 };

        // Act
        await simulator.TriggerExposureAsync(parameters, CancellationToken.None);
        await Task.Delay(300); // Wait for exposure to complete

        // Assert
        var lastExposure = simulator.GetLastExposureParameters();
        lastExposure.Should().BeEquivalentTo(parameters);
    }

    /// <summary>
    /// Test that exposure count is tracked.
    /// </summary>
    [Fact]
    public async Task GetExposureCount_ReturnsAccurateCount()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act - Perform 3 exposures
        for (int i = 0; i < 3; i++)
        {
            await simulator.PrepareAsync(CancellationToken.None);
            await simulator.TriggerExposureAsync(
                new ExposureParameters { Kv = 100, Ma = 200, Ms = 10 },
                CancellationToken.None);
        }

        // Assert
        simulator.GetExposureCount().Should().Be(3);
    }

    /// <summary>
    /// Test that simulator can be reset.
    /// </summary>
    [Fact]
    public async Task ResetAsync_ResetsSimulatorToInitialState()
    {
        // Arrange
        var simulator = new HvgDriverSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.PrepareAsync(CancellationToken.None);

        // Perform an exposure
        await simulator.TriggerExposureAsync(
            new ExposureParameters { Kv = 100, Ma = 200, Ms = 10 },
            CancellationToken.None);
        await Task.Delay(50);

        // Act
        await simulator.ResetAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(HvgState.Initializing);
        simulator.GetExposureCount().Should().Be(0);
    }
}
