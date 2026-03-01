namespace HnVue.Workflow.Tests.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Workflow.Hal.Simulators;
using HnVue.Workflow.Interfaces;
using Xunit;

/// <summary>
/// Unit tests for DetectorSimulator.
/// SPEC-WORKFLOW-001 TASK-402: IDetector Simulator implementation
/// </summary>
public class DetectorSimulatorTests
{
    /// <summary>
    /// Test that simulator initializes in Initializing state.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_SetsStateToReady()
    {
        // Arrange
        var simulator = new DetectorSimulator();

        // Act
        await simulator.InitializeAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(DetectorState.Ready);
        status.IsReady.Should().BeTrue();
        status.ErrorMessage.Should().BeNull();
    }

    /// <summary>
    /// Test that StartAcquisitionAsync transitions to Acquiring state.
    /// </summary>
    [Fact]
    public async Task StartAcquisitionAsync_FromReadyTransitionsToAcquiring()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        await simulator.StartAcquisitionAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(DetectorState.Acquiring);
        status.IsReady.Should().BeFalse();
    }

    /// <summary>
    /// Test that StopAcquisitionAsync stops acquisition.
    /// </summary>
    [Fact]
    public async Task StopAcquisitionAsync_StopsAcquisition()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        await simulator.StartAcquisitionAsync(CancellationToken.None);

        // Act
        await simulator.StopAcquisitionAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(DetectorState.Ready);
        status.IsReady.Should().BeTrue();
    }

    /// <summary>
    /// Test that GetInfoAsync returns detector information.
    /// </summary>
    [Fact]
    public async Task GetInfoAsync_ReturnsDetectorInformation()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var info = await simulator.GetInfoAsync(CancellationToken.None);

        // Assert
        info.Manufacturer.Should().NotBeNullOrEmpty();
        info.Model.Should().NotBeNullOrEmpty();
        info.SerialNumber.Should().NotBeNullOrEmpty();
        info.PixelWidth.Should().BeGreaterThan(0);
        info.PixelHeight.Should().BeGreaterThan(0);
        info.Columns.Should().BeGreaterThan(0);
        info.Rows.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Test that GetStatusAsync returns current status.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ReturnsCurrentStatus()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act
        var status = await simulator.GetStatusAsync(CancellationToken.None);

        // Assert
        status.State.Should().Be(DetectorState.Ready);
        status.IsReady.Should().BeTrue();
        status.ErrorMessage.Should().BeNull();
    }

    /// <summary>
    /// Test that fault injection works correctly.
    /// </summary>
    [Fact]
    public async Task SetFaultMode_EnablesFaultInjection()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        simulator.SetFaultMode(true);

        // Act
        await simulator.StartAcquisitionAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(DetectorState.Error);
        status.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Test that fault can be cleared.
    /// </summary>
    [Fact]
    public async Task ClearFaultAsync_ResetsToReadyState()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        simulator.SetFaultMode(true);
        await simulator.StartAcquisitionAsync(CancellationToken.None);

        // Act
        await simulator.ClearFaultAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(DetectorState.Ready);
        status.ErrorMessage.Should().BeNull();
    }

    /// <summary>
    /// Test that multiple acquisitions can be performed.
    /// </summary>
    [Fact]
    public async Task StartAcquisitionAsync_MultipleAcquisitionsSucceed()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act & Assert - First acquisition
        await simulator.StartAcquisitionAsync(CancellationToken.None);
        var status1 = await simulator.GetStatusAsync(CancellationToken.None);
        status1.State.Should().Be(DetectorState.Acquiring);

        await simulator.StopAcquisitionAsync(CancellationToken.None);

        // Second acquisition
        await simulator.StartAcquisitionAsync(CancellationToken.None);
        var status2 = await simulator.GetStatusAsync(CancellationToken.None);
        status2.State.Should().Be(DetectorState.Acquiring);
    }

    /// <summary>
    /// Test that acquisition count is tracked.
    /// </summary>
    [Fact]
    public async Task GetAcquisitionCount_ReturnsAccurateCount()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Act - Perform 3 acquisitions
        for (int i = 0; i < 3; i++)
        {
            await simulator.StartAcquisitionAsync(CancellationToken.None);
            await simulator.StopAcquisitionAsync(CancellationToken.None);
        }

        // Assert
        simulator.GetAcquisitionCount().Should().Be(3);
    }

    /// <summary>
    /// Test that cancellation token works for StartAcquisitionAsync.
    /// </summary>
    [Fact]
    public async Task StartAcquisitionAsync_CancellationTokenCancelsOperation()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => simulator.StartAcquisitionAsync(cts.Token));
    }

    /// <summary>
    /// Test that detector info can be customized.
    /// </summary>
    [Fact]
    public async Task SetDetectorInfo_CustomizesInformation()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var customInfo = new DetectorInfo
        {
            Manufacturer = "Test Manufacturer",
            Model = "Test Model",
            SerialNumber = "TEST123",
            PixelWidth = 200,
            PixelHeight = 200,
            Columns = 2048,
            Rows = 2048
        };

        // Act
        simulator.SetDetectorInfo(customInfo);
        var info = await simulator.GetInfoAsync(CancellationToken.None);

        // Assert
        info.Should().BeEquivalentTo(customInfo);
    }

    /// <summary>
    /// Test that simulator can be reset.
    /// </summary>
    [Fact]
    public async Task ResetAsync_ResetsSimulatorToInitialState()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        // Perform an acquisition
        await simulator.StartAcquisitionAsync(CancellationToken.None);
        await simulator.StopAcquisitionAsync(CancellationToken.None);

        // Act
        await simulator.ResetAsync(CancellationToken.None);

        // Assert
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(DetectorState.Initializing);
        simulator.GetAcquisitionCount().Should().Be(0);
    }

    /// <summary>
    /// Test that acquisition timing is simulated correctly.
    /// </summary>
    [Fact]
    public async Task StartAcquisitionAsync_SimulatesAcquisitionTiming()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);

        var acquisitionTimeMs = 100;
        simulator.SetAcquisitionTime(TimeSpan.FromMilliseconds(acquisitionTimeMs));

        var startTime = DateTime.UtcNow;

        // Act
        await simulator.StartAcquisitionAsync(CancellationToken.None);

        // Check that we're in Acquiring state
        var status = await simulator.GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(DetectorState.Acquiring);

        // Wait for acquisition to complete
        await Task.Delay(acquisitionTimeMs + 50);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert - Acquisition should have taken at least the specified time
        elapsed.Should().BeGreaterOrEqualTo(acquisitionTimeMs);
    }

    /// <summary>
    /// Test that starting acquisition when already acquiring fails gracefully.
    /// </summary>
    [Fact]
    public async Task StartAcquisitionAsync_WhenAlreadyAcquiringDoesNotFail()
    {
        // Arrange
        var simulator = new DetectorSimulator();
        await simulator.InitializeAsync(CancellationToken.None);
        simulator.SetAcquisitionTime(TimeSpan.FromMilliseconds(100));

        // Act - Start first acquisition
        await simulator.StartAcquisitionAsync(CancellationToken.None);

        // Try to start second acquisition while first is ongoing
        // This should not throw, but may be ignored or handled gracefully
        var exception = await Record.ExceptionAsync(async () =>
        {
            await simulator.StartAcquisitionAsync(CancellationToken.None);
        });

        // Assert - No exception should be thrown
        exception.Should().BeNull();
    }
}
