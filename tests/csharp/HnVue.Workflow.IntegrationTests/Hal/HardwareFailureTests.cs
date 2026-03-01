using System;
using FluentAssertions;
using HnVue.Workflow.Events;
using HnVue.Workflow.Hal.Simulators;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.IntegrationTests.TestHelpers;
using HnVue.Workflow.Journal;
using HnVue.Workflow.Safety;
using HnVue.Workflow.StateMachine;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using StateMachineWorkflowState = HnVue.Workflow.StateMachine.WorkflowState;
using StatesWorkflowState = HnVue.Workflow.States.WorkflowState;
using InterfacesExposureParameters = HnVue.Workflow.Interfaces.ExposureParameters;

namespace HnVue.Workflow.IntegrationTests.Hal;

/// <summary>
/// Hardware failure scenario integration tests.
/// SPEC-WORKFLOW-001 Phase 4.4 TASK-417
///
/// Tests various hardware failure scenarios:
/// - HVG failure during exposure
/// - Detector readout failure
/// - Door opens during exposure
/// - Multiple interlocks active
/// - Interlock clears during exposure
/// - Recovery validation after failure
///
/// Safety verification: Exposure never completes with active interlock.
///
/// @MX:NOTE: Tests use HAL simulators to inject fault conditions
/// @MX:WARN: Safety-critical tests verify exposure blocking on faults
/// </summary>
public class HardwareFailureTests : IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly HvgDriverSimulator _hvgSimulator;
    private readonly DetectorSimulator _detectorSimulator;
    private readonly SafetyInterlockSimulator _safetySimulator;
    private readonly DoseTrackerSimulator _doseSimulator;
    private readonly AecControllerSimulator _aecSimulator;
    private readonly IWorkflowJournal _journal;
    private readonly InMemoryWorkflowEventPublisher _eventPublisher;


    public HardwareFailureTests()
    {
        _loggerFactory = NullLoggerFactory.Instance;
        _hvgSimulator = new HvgDriverSimulator();
        _detectorSimulator = new DetectorSimulator();
        _safetySimulator = new SafetyInterlockSimulator();
        _doseSimulator = new DoseTrackerSimulator();
        _aecSimulator = new AecControllerSimulator();
        _journal = new SqliteWorkflowJournal(":memory:");
        _eventPublisher = new InMemoryWorkflowEventPublisher();

        // Initialize all simulators
        Task.WhenAll(
            _hvgSimulator.InitializeAsync(),
            _detectorSimulator.InitializeAsync(),
            _safetySimulator.InitializeAsync(),
            _doseSimulator.InitializeAsync(),
            _aecSimulator.InitializeAsync()
        ).GetAwaiter().GetResult();

        // Initialize journal
        _journal.InitializeAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        // Clean up resources
        await _journal.DisposeAsync();
    }

    /// <summary>
    /// Test 1: HVG failure during exposure causes immediate abort
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test1_HvgFailureDuringExposure_AbortsTransition_SafetyCritical()
    {
        // Arrange
        var guardMatrix = new TransitionGuardMatrix(_journal, _safetySimulator, _doseSimulator);
        var stateMachine = new WorkflowStateMachine(
            _loggerFactory.CreateLogger<WorkflowStateMachine>(),
            _journal,
            guardMatrix);

        var patientInfo = WorkflowTestHelpers.CreateTestPatientInfo(
            patientId: "PATIENT-001",
            patientName: "HVG Failure Test",
            birthYear: 1980,
            sex: 'M',
            isEmergency: false);

        // Navigate to ExposureTrigger state
        await stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.ExposureTrigger,
            "NAVIGATE",
            "TEST_OPERATOR");

        // Enable HVG fault mode
        _hvgSimulator.SetFaultMode(true);

        // Prepare HVG for exposure (will fail due to fault mode)
        await _hvgSimulator.PrepareAsync();

        // Act - Try to trigger exposure with HVG in fault state
        var exposureParams = new InterfacesExposureParameters { Kv = 120, Ma = 100, Ms = 100 };
        var exposureResult = await _hvgSimulator.TriggerExposureAsync(exposureParams);

        // Assert - Exposure should fail
        exposureResult.Should().BeFalse(
            "HVG exposure should fail when HVG is in fault state");

        // Verify HVG is in fault state
        var hvgStatus = await _hvgSimulator.GetStatusAsync();
        hvgStatus.State.Should().Be(HvgState.Fault);
        hvgStatus.FaultCode.Should().NotBeNull(
            "HVG should have a fault code when in fault state");

        // Assert workflow state machine prevents transition to next state
        var transitionResult = await stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.QcReview,
            "COMPLETE_EXPOSURE",
            "TEST_OPERATOR");

        transitionResult.IsSuccess.Should().BeFalse(
            "Transition to QC Review should be blocked when exposure failed");

        // Safety-critical: Exposure must not complete
        exposureResult.Should().BeFalse("SAFETY: Exposure must not complete with HVG fault");
    }

    /// <summary>
    /// Test 2: Detector readout failure triggers error event with recovery path
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test2_DetectorReadoutFailure_ErrorEvent_RecoveryPath()
    {
        // Arrange
        var guardMatrix = new TransitionGuardMatrix(_journal, _safetySimulator, _doseSimulator);
        var stateMachine = new WorkflowStateMachine(
            _loggerFactory.CreateLogger<WorkflowStateMachine>(),
            _journal,
            guardMatrix);

        var patientInfo = WorkflowTestHelpers.CreateTestPatientInfo(
            patientId: "PATIENT-001",
            patientName: "Detector Failure Test",
            birthYear: 1985,
            sex: 'F',
            isEmergency: false);

        // Navigate to ExposureTrigger state
        await stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.ExposureTrigger,
            "NAVIGATE",
            "TEST_OPERATOR");

        // Enable detector fault mode
        _detectorSimulator.SetFaultMode(true);

        // Try to start acquisition
        await _detectorSimulator.StartAcquisitionAsync();

        // Act - Check detector status after fault
        var detectorStatus = await _detectorSimulator.GetStatusAsync();

        // Assert - Detector should be in error state
        detectorStatus.State.Should().Be(DetectorState.Error);
        detectorStatus.ErrorMessage.Should().NotBeNull(
            "Detector should have error message when in fault state");

        // Verify workflow detects detector error
        var interlockStatus = await _safetySimulator.CheckAllInterlocksAsync();
        var isExposureBlocked = await _safetySimulator.IsExposureBlockedAsync();

        // With detector error, exposure should be blocked
        isExposureBlocked.Should().BeTrue(
            "Exposure should be blocked when detector is in error state");

        // Act - Recovery: Clear fault and verify recovery path
        await _detectorSimulator.ClearFaultAsync();

        var recoveredStatus = await _detectorSimulator.GetStatusAsync();
        recoveredStatus.State.Should().Be(DetectorState.Ready,
            "Detector should return to Ready state after fault is cleared");

        // Verify exposure is no longer blocked after recovery
        await _safetySimulator.SetInterlockStateAsync("detector_ready", true);
        var isStillBlocked = await _safetySimulator.IsExposureBlockedAsync();

        isStillBlocked.Should().BeFalse(
            "Exposure should be allowed after detector fault is cleared");
    }

    /// <summary>
    /// Test 3: Door opens during exposure causes immediate abort
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// @MX:WARN: Safety-critical - door interlock must immediately halt exposure
    /// </summary>
    [Fact]
    public async Task Test3_DoorOpensDuringExposure_ImmediateAbort_SafetyCritical()
    {
        // Arrange
        var patientInfo = WorkflowTestHelpers.CreateTestPatientInfo(
            patientId: "PATIENT-001",
            patientName: "Door Interlock Test",
            birthYear: 1990,
            sex: 'M',
            isEmergency: false);

        // Set up simulators for exposure
        await _hvgSimulator.PrepareAsync();
        await _detectorSimulator.StartAcquisitionAsync();

        // Verify all interlocks are safe initially
        var initialStatus = await _safetySimulator.CheckAllInterlocksAsync();
        initialStatus.door_closed.Should().BeTrue("Door should be closed initially");
        var isInitiallyBlocked = await _safetySimulator.IsExposureBlockedAsync();
        isInitiallyBlocked.Should().BeFalse("Exposure should not be blocked initially");

        // Start exposure in background
        var exposureTask = Task.Run(async () =>
        {
            var exposureParams = new InterfacesExposureParameters { Kv = 120, Ma = 100, Ms = 200 }; // 200ms exposure
            return await _hvgSimulator.TriggerExposureAsync(exposureParams);
        });

        // Wait for exposure to start
        await Task.Delay(20);

        // Act - Simulate door opening during exposure
        await _safetySimulator.SetInterlockStateAsync("door_closed", false);

        // The exposure should be aborted
        var exposureResult = await exposureTask;

        // Assert - Exposure should fail or be aborted
        exposureResult.Should().BeFalse(
            "Exposure should be aborted when door opens during exposure");

        // Verify door interlock is active
        var afterStatus = await _safetySimulator.CheckAllInterlocksAsync();
        afterStatus.door_closed.Should().BeFalse("Door interlock should show door open");
        var isNowBlocked = await _safetySimulator.IsExposureBlockedAsync();
        isNowBlocked.Should().BeTrue("Exposure should be blocked with door open");

        // Safety-critical: Verify HVG is not exposing
        var hvgStatus = await _hvgSimulator.GetStatusAsync();
        hvgStatus.State.Should().NotBe(HvgState.Exposing,
            "SAFETY: HVG should not be in Exposing state when door is open");

        // Safety-critical assertion
        hvgStatus.State.Should().NotBe(HvgState.Exposing,
            "SAFETY-CRITICAL: Door interlock must prevent exposure");
    }

    /// <summary>
    /// Test 4: Multiple interlocks active blocks exposure
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test4_MultipleInterlocksActive_BlocksExposure_SafetyCritical()
    {
        // Arrange
        var patientInfo = WorkflowTestHelpers.CreateTestPatientInfo(
            patientId: "PATIENT-001",
            patientName: "Multiple Interlock Test",
            birthYear: 1988,
            sex: 'F',
            isEmergency: false);

        // Initially all interlocks should be safe
        var initialStatus = await _safetySimulator.CheckAllInterlocksAsync();
        var isInitiallyBlocked = await _safetySimulator.IsExposureBlockedAsync();
        isInitiallyBlocked.Should().BeFalse("Exposure should not be blocked initially");

        // Act - Activate multiple interlocks
        var failedInterlocks = new[]
        {
            "door_closed",
            "thermal_normal",
            "generator_ready"
        };

        foreach (var interlock in failedInterlocks)
        {
            await _safetySimulator.SetInterlockStateAsync(interlock, false);
        }

        // Check exposure block status
        var isBlocked = await _safetySimulator.IsExposureBlockedAsync();
        var interlockStatus = await _safetySimulator.CheckAllInterlocksAsync();

        // Assert - Exposure should be blocked
        isBlocked.Should().BeTrue(
            "Exposure should be blocked when multiple interlocks are active");

        // Verify specific interlocks failed
        interlockStatus.door_closed.Should().BeFalse("Door interlock should be active");
        interlockStatus.thermal_normal.Should().BeFalse("Thermal interlock should be active");
        interlockStatus.generator_ready.Should().BeFalse("Generator ready interlock should be active");

        // Try to trigger exposure (should fail)
        await _hvgSimulator.PrepareAsync();
        var exposureParams = new InterfacesExposureParameters { Kv = 120, Ma = 100, Ms = 100 };
        var exposureResult = await _hvgSimulator.TriggerExposureAsync(exposureParams);

        exposureResult.Should().BeFalse(
            "Exposure should fail when multiple interlocks are active");

        // Safety-critical: No exposure with active interlocks
        exposureResult.Should().BeFalse("SAFETY: Must not expose with any interlock active");
    }

    /// <summary>
    /// Test 5: Interlock clears during exposure allows completion
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test5_InterlockClearsDuringExposure_AllowsCompletion()
    {
        // Arrange
        var patientInfo = WorkflowTestHelpers.CreateTestPatientInfo(
            patientId: "PATIENT-001",
            patientName: "Interlock Clear Test",
            birthYear: 1992,
            sex: 'M',
            isEmergency: false);

        // Set one interlock to unsafe state
        await _safetySimulator.SetInterlockStateAsync("thermal_normal", false);

        // Verify exposure is blocked
        var isInitiallyBlocked = await _safetySimulator.IsExposureBlockedAsync();
        isInitiallyBlocked.Should().BeTrue("Exposure should be blocked initially");

        // Prepare HVG (should not start exposing)
        await _hvgSimulator.PrepareAsync();

        // Act - Clear the interlock
        await _safetySimulator.SetInterlockStateAsync("thermal_normal", true);

        // Verify exposure is now allowed
        var isNowBlocked = await _safetySimulator.IsExposureBlockedAsync();
        isNowBlocked.Should().BeFalse("Exposure should be allowed after interlock clears");

        // Try to trigger exposure (should succeed now)
        var exposureParams = new InterfacesExposureParameters { Kv = 120, Ma = 100, Ms = 50 };
        var exposureResult = await _hvgSimulator.TriggerExposureAsync(exposureParams);

        // Assert - Exposure should succeed after interlock clears
        exposureResult.Should().BeTrue(
            "Exposure should succeed after interlock is cleared");

        // Verify exposure completed
        var hvgStatus = await _hvgSimulator.GetStatusAsync();
        hvgStatus.State.Should().Be(HvgState.Idle, "HVG should return to Idle after exposure completes");
    }

    /// <summary>
    /// Test 6: Recovery validation after failure
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test6_RecoveryValidationAfterFailure_ResumesNormalOperation()
    {
        // Arrange
        var guardMatrix = new TransitionGuardMatrix(_journal, _safetySimulator, _doseSimulator);
        var stateMachine = new WorkflowStateMachine(
            _loggerFactory.CreateLogger<WorkflowStateMachine>(),
            _journal,
            guardMatrix);

        var patientInfo = WorkflowTestHelpers.CreateTestPatientInfo(
            patientId: "PATIENT-001",
            patientName: "Recovery Test",
            birthYear: 1986,
            sex: 'F',
            isEmergency: false);

        // Navigate to ExposureTrigger
        var pathToExposure = new[]
        {
            StateMachineWorkflowState.PatientSelect,
            StateMachineWorkflowState.ProtocolSelect,
            StateMachineWorkflowState.WorklistSync,
            StateMachineWorkflowState.PositionAndPreview,
            StateMachineWorkflowState.ExposureTrigger
        };

        foreach (var state in pathToExposure)
        {
            await stateMachine.TryTransitionAsync(state, "NAVIGATE", "TEST_OPERATOR");
        }

        // Inject fault - enable HVG fault mode
        _hvgSimulator.SetFaultMode(true);

        // Try to trigger exposure (should fail)
        await _hvgSimulator.PrepareAsync();
        var exposureParams = new InterfacesExposureParameters { Kv = 120, Ma = 100, Ms = 100 };
        var failedExposure = await _hvgSimulator.TriggerExposureAsync(exposureParams);

        failedExposure.Should().BeFalse("Exposure should fail with fault mode enabled");

        // Act - Recovery: Clear fault and reset
        await _hvgSimulator.ClearFaultAsync();
        await _hvgSimulator.ResetAsync();
        await _hvgSimulator.InitializeAsync();

        // Verify recovery
        var hvgStatus = await _hvgSimulator.GetStatusAsync();
        hvgStatus.State.Should().Be(HvgState.Idle, "HVG should be in Idle state after recovery");
        hvgStatus.FaultCode.Should().BeNull("HVG should have no fault code after recovery");

        // Try exposure again (should succeed)
        await _hvgSimulator.PrepareAsync();
        var recoveredExposure = await _hvgSimulator.TriggerExposureAsync(exposureParams);

        // Assert - Exposure should succeed after recovery
        recoveredExposure.Should().BeTrue(
            "Exposure should succeed after fault recovery");

        // Assert workflow can continue
        var transitionResult = await stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.QcReview,
            "COMPLETE_EXPOSURE",
            "TEST_OPERATOR");

        transitionResult.IsSuccess.Should().BeTrue(
            "Workflow should continue to QC Review after successful exposure");
    }

    /// <summary>
    /// Safety verification: Exposure never completes with active interlock
    /// @MX:ANCHOR: Safety-critical test - verifies fundamental safety invariant
    /// @MX:WARN: This test must never fail - patient safety depends on it
    /// </summary>
    [Fact]
    public async Task SafetyVerification_ExposureNeverCompletes_WithActiveInterlock()
    {
        // Arrange
        var patientInfo = WorkflowTestHelpers.CreateTestPatientInfo(
            patientId: "PATIENT-001",
            patientName: "Safety Verification",
            birthYear: 1984,
            sex: 'M',
            isEmergency: false);

        // Test all 9 interlocks
        var interlocks = new[]
        {
            "door_closed",
            "emergency_stop_clear",
            "thermal_normal",
            "generator_ready",
            "detector_ready",
            "collimator_valid",
            "table_locked",
            "dose_within_limits",
            "aec_configured"
        };

        foreach (var interlock in interlocks)
        {
            // Reset to safe state
            await _safetySimulator.ResetAsync();
            await _hvgSimulator.ResetAsync();
            await _hvgSimulator.InitializeAsync();

            // Verify exposure works with all interlocks safe
            await _hvgSimulator.PrepareAsync();
            var safeExposureParams = new InterfacesExposureParameters { Kv = 120, Ma = 100, Ms = 50 };
            var safeExposure = await _hvgSimulator.TriggerExposureAsync(safeExposureParams);
            safeExposure.Should().BeTrue($"Exposure should succeed with all interlocks safe (testing {interlock})");

            // Set one interlock to unsafe state
            await _safetySimulator.SetInterlockStateAsync(interlock, false);

            // Verify exposure is blocked
            var isBlocked = await _safetySimulator.IsExposureBlockedAsync();
            isBlocked.Should().BeTrue($"Exposure should be blocked when {interlock} is unsafe");

            // Try to trigger exposure (must fail)
            await _hvgSimulator.PrepareAsync();
            var unsafeExposure = await _hvgSimulator.TriggerExposureAsync(safeExposureParams);

            // SAFETY-CRITICAL ASSERTION - This must never pass if exposure occurs
            unsafeExposure.Should().BeFalse(
                $"SAFETY-CRITICAL: Exposure MUST NOT complete when {interlock} is unsafe");

            // Verify HVG is not exposing
            var hvgStatus = await _hvgSimulator.GetStatusAsync();
            hvgStatus.State.Should().NotBe(HvgState.Exposing,
                $"SAFETY-CRITICAL: HVG must not be in Exposing state when {interlock} is unsafe");
        }

        // Final safety assertion
        var finalStatus = await _safetySimulator.CheckAllInterlocksAsync();
        var finalBlocked = await _safetySimulator.IsExposureBlockedAsync();

        // If any interlock is unsafe, exposure must be blocked
        if (!finalStatus.door_closed ||
            !finalStatus.emergency_stop_clear ||
            !finalStatus.thermal_normal ||
            !finalStatus.generator_ready ||
            !finalStatus.detector_ready ||
            !finalStatus.collimator_valid ||
            !finalStatus.table_locked ||
            !finalStatus.dose_within_limits ||
            !finalStatus.aec_configured)
        {
            finalBlocked.Should().BeTrue(
                "SAFETY-CRITICAL: Exposure MUST be blocked when ANY interlock is unsafe");
        }
    }
}
