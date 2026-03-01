namespace HnVue.Workflow.Tests.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Workflow.Hal.Simulators;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.Safety;
using Xunit;

/// <summary>
/// Unit tests for HalSimulatorOrchestrator.
/// SPEC-WORKFLOW-001 TASK-405: HAL Simulator Orchestration implementation
/// </summary>
public class HalSimulatorOrchestratorTests
{
    /// <summary>
    /// Test that orchestrator initializes all simulators.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_InitializesAllSimulators()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();

        // Act
        await orchestrator.InitializeAsync(CancellationToken.None);

        // Assert
        var hvgStatus = await orchestrator.HvgDriver.GetStatusAsync(CancellationToken.None);
        hvgStatus.State.Should().Be(HvgState.Idle);

        var detectorStatus = await orchestrator.Detector.GetStatusAsync(CancellationToken.None);
        detectorStatus.State.Should().Be(DetectorState.Ready);

        var interlockStatus = await orchestrator.SafetyInterlock.CheckAllInterlocksAsync(CancellationToken.None);
        interlockStatus.door_closed.Should().BeTrue();

        var aecStatus = await orchestrator.AecController.GetAecReadinessAsync(CancellationToken.None);
        aecStatus.State.Should().Be(AecState.NotConfigured);
    }

    /// <summary>
    /// Test that orchestrator provides access to all simulators.
    /// </summary>
    [Fact]
    public void Orchestrator_ProvidesAccessToAllSimulators()
    {
        // Arrange & Act
        var orchestrator = new HalSimulatorOrchestrator();

        // Assert
        orchestrator.HvgDriver.Should().NotBeNull();
        orchestrator.Detector.Should().NotBeNull();
        orchestrator.SafetyInterlock.Should().NotBeNull();
        orchestrator.AecController.Should().NotBeNull();
        orchestrator.DoseTracker.Should().NotBeNull();
    }

    /// <summary>
    /// Test that ResetAsync resets all simulators.
    /// </summary>
    [Fact]
    public async Task ResetAsync_ResetsAllSimulators()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        // Change some states
        await orchestrator.SafetyInterlock.SetInterlockStateAsync("door_closed", false, CancellationToken.None);
        var parameters = new AecParameters
        {
            AecEnabled = true,
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };
        await orchestrator.AecController.SetAecParametersAsync(parameters, CancellationToken.None);

        // Act
        await orchestrator.ResetAsync(CancellationToken.None);

        // Assert
        var interlockStatus = await orchestrator.SafetyInterlock.CheckAllInterlocksAsync(CancellationToken.None);
        interlockStatus.door_closed.Should().BeTrue();

        var aecStatus = await orchestrator.AecController.GetAecReadinessAsync(CancellationToken.None);
        aecStatus.State.Should().Be(AecState.NotConfigured);
    }

    /// <summary>
    /// Test that scenario playback works for normal workflow.
    /// </summary>
    [Fact]
    public async Task PlayScenarioAsync_NormalWorkflowScenario_Succeeds()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var scenario = SimulatorScenario.CreateNormalWorkflow();

        // Act
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Assert - All simulators should be in appropriate states
        var hvgStatus = await orchestrator.HvgDriver.GetStatusAsync(CancellationToken.None);
        hvgStatus.State.Should().Be(HvgState.Idle);

        var isBlocked = await orchestrator.SafetyInterlock.IsExposureBlockedAsync(CancellationToken.None);
        isBlocked.Should().BeFalse();
    }

    /// <summary>
    /// Test that scenario playback works for door opens during exposure.
    /// </summary>
    [Fact]
    public async Task PlayScenarioAsync_DoorOpensDuringExposure_BlocksExposure()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var scenario = SimulatorScenario.CreateDoorOpensDuringExposure();

        // Act
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Assert - Door should be open, exposure blocked
        var interlockStatus = await orchestrator.SafetyInterlock.CheckAllInterlocksAsync(CancellationToken.None);
        interlockStatus.door_closed.Should().BeFalse();

        var isBlocked = await orchestrator.SafetyInterlock.IsExposureBlockedAsync(CancellationToken.None);
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that scenario playback works for emergency stop activation.
    /// </summary>
    [Fact]
    public async Task PlayScenarioAsync_EmergencyStopActivation_ActivatesEmergencyStandby()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var scenario = SimulatorScenario.CreateEmergencyStopActivation();

        // Act
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Assert - Emergency stop should be activated
        var interlockStatus = await orchestrator.SafetyInterlock.CheckAllInterlocksAsync(CancellationToken.None);
        interlockStatus.emergency_stop_clear.Should().BeFalse();

        var isBlocked = await orchestrator.SafetyInterlock.IsExposureBlockedAsync(CancellationToken.None);
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that scenario playback works for temperature overheat.
    /// </summary>
    [Fact]
    public async Task PlayScenarioAsync_TemperatureOverheat_BlocksExposure()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var scenario = SimulatorScenario.CreateTemperatureOverheat();

        // Act
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Assert - Temperature should be abnormal, exposure blocked
        var interlockStatus = await orchestrator.SafetyInterlock.CheckAllInterlocksAsync(CancellationToken.None);
        interlockStatus.thermal_normal.Should().BeFalse();

        var isBlocked = await orchestrator.SafetyInterlock.IsExposureBlockedAsync(CancellationToken.None);
        isBlocked.Should().BeTrue();
    }

    /// <summary>
    /// Test that scenario playback can be cancelled.
    /// </summary>
    [Fact]
    public async Task PlayScenarioAsync_CancellationTokenCancelsPlayback()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var scenario = SimulatorScenario.CreateNormalWorkflow();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => orchestrator.PlayScenarioAsync(scenario, cts.Token));
    }

    /// <summary>
    /// Test that custom scenarios can be created and played.
    /// </summary>
    [Fact]
    public async Task PlayScenarioAsync_CustomScenario_Succeeds()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        // Create a custom scenario
        var scenario = new SimulatorScenario("Custom Test Scenario");
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.SafetyInterlock.SetInterlockStateAsync("detector_ready", false, ct);
        });
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.SafetyInterlock.SetInterlockStateAsync("detector_ready", true, ct);
        });

        // Act
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Assert - Detector should be ready again
        var interlockStatus = await orchestrator.SafetyInterlock.CheckAllInterlocksAsync(CancellationToken.None);
        interlockStatus.detector_ready.Should().BeTrue();
    }

    /// <summary>
    /// Test that scenario steps execute in order.
    /// </summary>
    [Fact]
    public async Task PlayScenarioAsync_StepsExecuteInOrder()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var executionOrder = new System.Collections.Generic.List<string>();
        var scenario = new SimulatorScenario("Ordered Steps");
        scenario.AddStep(async (orch, ct) =>
        {
            executionOrder.Add("Step1");
            await Task.CompletedTask;
        });
        scenario.AddStep(async (orch, ct) =>
        {
            executionOrder.Add("Step2");
            await Task.CompletedTask;
        });
        scenario.AddStep(async (orch, ct) =>
        {
            executionOrder.Add("Step3");
            await Task.CompletedTask;
        });

        // Act
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Assert
        executionOrder.Should().HaveCount(3);
        executionOrder[0].Should().Be("Step1");
        executionOrder[1].Should().Be("Step2");
        executionOrder[2].Should().Be("Step3");
    }

    /// <summary>
    /// Test that scenario reports progress.
    /// </summary>
    [Fact]
    public async Task PlayScenarioAsync_ReportsProgressCorrectly()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var progressUpdates = new System.Collections.Generic.List<int>();
        var scenario = SimulatorScenario.CreateNormalWorkflow();
        scenario.ProgressChanged += (sender, progress) => progressUpdates.Add(progress);

        // Act
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Assert - Should have received progress updates
        progressUpdates.Should().NotBeEmpty();
        progressUpdates[^1].Should().Be(100); // Last update should be 100%
    }

    /// <summary>
    /// Test that orchestrator can reset after scenario playback.
    /// </summary>
    [Fact]
    public async Task ResetAfterScenario_ResetsAllSimulatorsToInitialState()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var scenario = SimulatorScenario.CreateEmergencyStopActivation();
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Act
        await orchestrator.ResetAsync(CancellationToken.None);

        // Assert - All simulators should be back to initial state
        var interlockStatus = await orchestrator.SafetyInterlock.CheckAllInterlocksAsync(CancellationToken.None);
        interlockStatus.emergency_stop_clear.Should().BeTrue();
        interlockStatus.door_closed.Should().BeTrue();
        interlockStatus.thermal_normal.Should().BeTrue();
    }

    /// <summary>
    /// Test that scenario can be created with multiple interlock changes.
    /// </summary>
    [Fact]
    public async Task ScenarioWithMultipleInterlockChanges_ExecutesAllChanges()
    {
        // Arrange
        var orchestrator = new HalSimulatorOrchestrator();
        await orchestrator.InitializeAsync(CancellationToken.None);

        var scenario = new SimulatorScenario("Multiple Interlocks");
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.SafetyInterlock.SetInterlockStateAsync("door_closed", false, ct);
        });
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.SafetyInterlock.SetInterlockStateAsync("thermal_normal", false, ct);
        });
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.SafetyInterlock.SetInterlockStateAsync("detector_ready", false, ct);
        });

        // Act
        await orchestrator.PlayScenarioAsync(scenario, CancellationToken.None);

        // Assert
        var status = await orchestrator.SafetyInterlock.CheckAllInterlocksAsync(CancellationToken.None);
        status.door_closed.Should().BeFalse();
        status.thermal_normal.Should().BeFalse();
        status.detector_ready.Should().BeFalse();
    }
}
