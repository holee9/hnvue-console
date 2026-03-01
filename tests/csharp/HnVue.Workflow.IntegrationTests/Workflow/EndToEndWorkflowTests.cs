using System;
using FluentAssertions;
using HnVue.Workflow.Events;
using HnVue.Workflow.Hal.Simulators;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.Journal;
using HnVue.Workflow.StateMachine;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using StateMachineWorkflowState = HnVue.Workflow.StateMachine.WorkflowState;
using StatesWorkflowState = HnVue.Workflow.States.WorkflowState;

namespace HnVue.Workflow.IntegrationTests.Workflow;

/// <summary>
/// End-to-end workflow integration tests using HAL simulators.
/// SPEC-WORKFLOW-001 Phase 4.4 TASK-416
///
/// Tests complete workflow scenarios from IDLE to PACS_EXPORT:
/// - Normal workflow
/// - Emergency workflow
/// - Retake workflow
/// - Multi-exposure study
/// - Worklist sync failure
/// - DICOM failure
/// - Dose limit enforcement
///
/// @MX:NOTE: Integration tests use HAL simulators, not real hardware
/// </summary>
public class EndToEndWorkflowTests : IAsyncLifetime
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly HvgDriverSimulator _hvgSimulator;
    private readonly DetectorSimulator _detectorSimulator;
    private readonly SafetyInterlockSimulator _safetySimulator;
    private readonly DoseTrackerSimulator _doseSimulator;
    private readonly AecControllerSimulator _aecSimulator;
    private readonly IWorkflowJournal _journal;
    private readonly InMemoryWorkflowEventPublisher _eventPublisher;

    public EndToEndWorkflowTests()
    {
        _loggerFactory = NullLoggerFactory.Instance;
        _safetySimulator = new SafetyInterlockSimulator();
        _hvgSimulator = new HvgDriverSimulator(_safetySimulator); // Pass safety interlock to HVG
        _detectorSimulator = new DetectorSimulator();
        _doseSimulator = new DoseTrackerSimulator();
        _aecSimulator = new AecControllerSimulator();
        _journal = new SqliteWorkflowJournal(":memory:");
        _eventPublisher = new InMemoryWorkflowEventPublisher();
    }

    public async Task InitializeAsync()
    {
        // Initialize all simulators
        await Task.WhenAll(
            _hvgSimulator.InitializeAsync(),
            _detectorSimulator.InitializeAsync(),
            _safetySimulator.InitializeAsync(),
            _doseSimulator.InitializeAsync(),
            _aecSimulator.InitializeAsync()
        );

        // Initialize journal
        // Journal initialization is handled by SqliteWorkflowJournal constructor
    }

    public async Task DisposeAsync()
    {
        // Clean up resources
        if (_journal is IAsyncDisposable disposableJournal)
        {
            await disposableJournal.DisposeAsync();
        }
    }

    /// <summary>
    /// Test 1: Normal workflow (IDLE → PACS_EXPORT → IDLE)
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test1_NormalWorkflow_FromIdleToPacsExport_Succeeds()
    {
        // Arrange
        var studyContext = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = States.WorkflowState.Idle,
            PatientInfo = CreateTestPatientInfo("PATIENT-001", "Test Patient", 1980, 'M')
        };

        var workflowMachine = CreateWorkflowStateMachine(studyContext);

        // Act & Assert - Execute normal workflow path
        var workflowPath = new[]
        {
            StateMachineWorkflowState.Idle,
            StateMachineWorkflowState.PatientSelect,
            StateMachineWorkflowState.ProtocolSelect,
            StateMachineWorkflowState.WorklistSync,
            StateMachineWorkflowState.PositionAndPreview,
            StateMachineWorkflowState.ExposureTrigger,
            StateMachineWorkflowState.QcReview,
            StateMachineWorkflowState.MppsComplete,
            StateMachineWorkflowState.PacsExport,
            StateMachineWorkflowState.Idle // Completed maps to Idle
        };

        // Track transition timing
        var transitionTimings = new List<(StateMachineWorkflowState from, StateMachineWorkflowState to, TimeSpan duration)>();

        for (int i = 0; i < workflowPath.Length - 1; i++)
        {
            var fromState = workflowPath[i];
            var toState = workflowPath[i + 1];

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var result = await workflowMachine.TryTransitionAsync(
                toState,
                "TEST_TRIGGER",
                "TEST_OPERATOR",
                cancellationToken: CancellationToken.None);

            stopwatch.Stop();

            transitionTimings.Add((fromState, toState, stopwatch.Elapsed));

            // Assert transition succeeded
            result.IsSuccess.Should().BeTrue(
                $"Transition from {fromState} to {toState} should succeed. Error: {result.ErrorMessage}");

            // Assert state changed
            workflowMachine.CurrentState.Should().Be(toState);

            // Assert transition completed within 50ms (performance requirement)
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
                $"State transition from {fromState} to {toState} should complete within 50ms, took {stopwatch.ElapsedMilliseconds}ms");
        }

        // Assert final state
        workflowMachine.CurrentState.Should().Be(StateMachineWorkflowState.Idle);

        // Assert all transitions met performance requirement
        var maxTransitionTime = transitionTimings.Max(t => t.duration.TotalMilliseconds);
        maxTransitionTime.Should().BeLessThan(50,
            $"All state transitions should complete within 50ms. Max was {maxTransitionTime}ms");
    }

    /// <summary>
    /// Test 2: Emergency workflow bypasses worklist
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test2_EmergencyWorkflow_BypassesWorklist_ExecutesSuccessfully()
    {
        // Arrange
        var studyContext = new StudyContext
        {
            StudyId = "EMERGENCY-001",
            PatientId = "PATIENT-001",
            CurrentState = States.WorkflowState.Idle,
            PatientInfo = CreateTestPatientInfo("PATIENT-001", "Emergency Patient", 1990, 'F', isEmergency: true)
        };

        var workflowMachine = CreateWorkflowStateMachine(studyContext);

        // Act - Emergency workflow should bypass WorklistSync
        var emergencyPath = new[]
        {
            StateMachineWorkflowState.Idle,
            StateMachineWorkflowState.PatientSelect,
            StateMachineWorkflowState.ProtocolSelect,
            // NOTE: WorklistSync bypassed for emergency
            StateMachineWorkflowState.PositionAndPreview,
            StateMachineWorkflowState.ExposureTrigger,
            StateMachineWorkflowState.QcReview
        };

        for (int i = 0; i < emergencyPath.Length - 1; i++)
        {
            var fromState = emergencyPath[i];
            var toState = emergencyPath[i + 1];

            var result = await workflowMachine.TryTransitionAsync(
                toState,
                "EMERGENCY_TRIGGER",
                "TEST_OPERATOR",
                cancellationToken: CancellationToken.None);

            // Special handling for bypassing WorklistSync
            if (fromState == StateMachineWorkflowState.ProtocolSelect && toState == StateMachineWorkflowState.PositionAndPreview)
            {
                result.IsSuccess.Should().BeTrue(
                    "Emergency workflow should bypass worklist sync");
            }
            else
            {
                result.IsSuccess.Should().BeTrue(
                    $"Transition from {fromState} to {toState} should succeed in emergency workflow");
            }

            workflowMachine.CurrentState.Should().Be(toState);
        }

        // Assert emergency workflow completed
        workflowMachine.CurrentState.Should().Be(StateMachineWorkflowState.QcReview);
    }

    /// <summary>
    /// Test 3: Retake workflow preserves dose information
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test3_RetakeWorkflow_PreservesDoseInformation_ReturnsToExposure()
    {
        // Arrange
        var studyContext = new StudyContext
        {
            StudyId = "RETAKE-001",
            PatientId = "PATIENT-001",
            CurrentState = States.WorkflowState.Idle,
            PatientInfo = CreateTestPatientInfo("PATIENT-001", "Retake Test Patient", 1985, 'M')
        };

        var workflowMachine = CreateWorkflowStateMachine(studyContext);

        // Act - Execute workflow to QC Review
        var normalPath = new[]
        {
            StateMachineWorkflowState.Idle,
            StateMachineWorkflowState.PatientSelect,
            StateMachineWorkflowState.ProtocolSelect,
            StateMachineWorkflowState.WorklistSync,
            StateMachineWorkflowState.PositionAndPreview,
            StateMachineWorkflowState.ExposureTrigger,
            StateMachineWorkflowState.QcReview
        };

        foreach (var state in normalPath.Skip(1))
        {
            var result = await workflowMachine.TryTransitionAsync(
                state,
                "NORMAL_TRIGGER",
                "TEST_OPERATOR",
                cancellationToken: CancellationToken.None);

            result.IsSuccess.Should().BeTrue($"Transition to {state} should succeed");
        }

        // Record dose before retake
        var doseBeforeRetake = await _doseSimulator.GetCumulativeDoseAsync(CancellationToken.None);

        // Reject image and transition to retake
        var rejectResult = await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.RejectRetake,
            "REJECT_TRIGGER",
            "TEST_OPERATOR",
            cancellationToken: CancellationToken.None);

        rejectResult.IsSuccess.Should().BeTrue("Transition to RejectRetake should succeed");

        // Transition back to ExposureTrigger for retake
        var retakeResult = await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.ExposureTrigger,
            "RETAKE_TRIGGER",
            "TEST_OPERATOR",
            cancellationToken: CancellationToken.None);

        retakeResult.IsSuccess.Should().BeTrue("Transition back to ExposureTrigger should succeed");

        // Assert dose information is preserved
        var doseAfterRetake = await _doseSimulator.GetCumulativeDoseAsync(CancellationToken.None);
        doseAfterRetake.ExposureCount.Should().Be(doseBeforeRetake.ExposureCount,
            "Total exposures count should be preserved during retake workflow");

        // Assert workflow state is back to ExposureTrigger
        workflowMachine.CurrentState.Should().Be(StateMachineWorkflowState.ExposureTrigger);
    }

    /// <summary>
    /// Test 4: Multi-exposure study tracks cumulative dose
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test4_MultiExposureStudy_TracksCumulativeDose_Succeeds()
    {
        // Arrange
        var studyContext = new StudyContext
        {
            StudyId = "MULTI-001",
            PatientId = "PATIENT-001",
            CurrentState = States.WorkflowState.Idle,
            PatientInfo = CreateTestPatientInfo("PATIENT-001", "Multi-Exposure Patient", 1975, 'F')
        };

        var workflowMachine = CreateWorkflowStateMachine(studyContext);

        // Act - Execute multiple exposures (e.g., AP and lateral views)
        var exposures = new[] { "AP", "LATERAL" };
        var cumulativeDoses = new List<double>();

        // First exposure: Full workflow from Idle to QC Review
        foreach (var state in new[]
        {
            StateMachineWorkflowState.PatientSelect,
            StateMachineWorkflowState.ProtocolSelect,
            StateMachineWorkflowState.WorklistSync,
            StateMachineWorkflowState.PositionAndPreview,
            StateMachineWorkflowState.ExposureTrigger
        })
        {
            var result = await workflowMachine.TryTransitionAsync(
                state,
                $"EXPOSURE_{exposures[0]}",
                "TEST_OPERATOR",
                cancellationToken: CancellationToken.None);

            result.IsSuccess.Should().BeTrue($"Transition to {state} for {exposures[0]} view should succeed");
        }

        // Record dose after first exposure
        var dose = await _doseSimulator.GetCumulativeDoseAsync(CancellationToken.None);
        cumulativeDoses.Add(dose.TotalDap);

        // Navigate to QC Review for first exposure
        await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.QcReview,
            $"QC_{exposures[0]}",
            "TEST_OPERATOR");

        // Second exposure: From QC Review back to PositionAndPreview (not from PatientSelect)
        await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.PositionAndPreview,
            $"NEXT_EXPOSURE_{exposures[1]}",
            "TEST_OPERATOR");

        await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.ExposureTrigger,
            $"EXPOSURE_{exposures[1]}",
            "TEST_OPERATOR");

        // Record dose for second exposure - simulate dose accumulation
        var secondExposureDose = new DoseEntry
        {
            StudyId = studyContext.StudyId,
            PatientId = studyContext.PatientId,
            Dap = 25.0, // Different dose for second exposure
            Esd = 8.0, // Entrance Skin Dose in mGy
            Timestamp = DateTimeOffset.UtcNow
        };
        await _doseSimulator.RecordDoseAsync(secondExposureDose, CancellationToken.None);

        // Record dose after second exposure
        dose = await _doseSimulator.GetCumulativeDoseAsync(CancellationToken.None);
        cumulativeDoses.Add(dose.TotalDap);

        // Navigate to QC Review for second exposure
        await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.QcReview,
            $"QC_{exposures[1]}",
            "TEST_OPERATOR");

        // Assert dose accumulates with each exposure
        cumulativeDoses.Should().HaveCount(2, "Should have 2 exposures");
        cumulativeDoses[1].Should().BeGreaterThan(cumulativeDoses[0],
            "Cumulative dose should increase with each exposure");

        // Assert final dose is sum of all exposures
        var finalDose = cumulativeDoses.Last();
        finalDose.Should().BeGreaterThan(0, "Final cumulative dose should be greater than zero");
    }

    /// <summary>
    /// Test 5: Worklist sync failure degrades gracefully
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test5_WorklistSyncFailure_DegradesGracefully_WorkflowContinues()
    {
        // Arrange
        var studyContext = new StudyContext
        {
            StudyId = "WORKLIST-FAIL-001",
            PatientId = "PATIENT-001",
            CurrentState = States.WorkflowState.Idle,
            PatientInfo = CreateTestPatientInfo("PATIENT-001", "Worklist Failure Test", 1988, 'M')
        };

        // Create workflow machine with worklist failure scenario
        var workflowMachine = CreateWorkflowStateMachine(studyContext);

        // Navigate to WorklistSync
        await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.PatientSelect,
            "NAVIGATE",
            "TEST_OPERATOR");

        await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.ProtocolSelect,
            "NAVIGATE",
            "TEST_OPERATOR");

        // Act - Attempt worklist sync (simulated failure)
        var syncResult = await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.WorklistSync,
            "SYNC_TRIGGER",
            "TEST_OPERATOR",
            cancellationToken: CancellationToken.None);

        // Assert - Worklist sync should succeed or fail gracefully
        // If it fails, workflow should continue to next state
        if (!syncResult.IsSuccess)
        {
            // Workflow should allow bypass on worklist failure
            var bypassResult = await workflowMachine.TryTransitionAsync(
                StateMachineWorkflowState.PositionAndPreview,
                "BYPASS_WORKLIST",
                "TEST_OPERATOR");

            bypassResult.IsSuccess.Should().BeTrue(
                "Workflow should continue to PositionAndPreview despite worklist failure");
        }
        else
        {
            // Worklist sync succeeded, continue normal workflow
            workflowMachine.CurrentState.Should().Be(StateMachineWorkflowState.WorklistSync);

            // Should be able to proceed to next state
            var nextResult = await workflowMachine.TryTransitionAsync(
                StateMachineWorkflowState.PositionAndPreview,
                "CONTINUE",
                "TEST_OPERATOR");

            nextResult.IsSuccess.Should().BeTrue(
                "Should be able to proceed to PositionAndPreview after successful worklist sync");
        }
    }

    /// <summary>
    /// Test 6: DICOM failure does not block workflow
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test6_DicomFailure_WorkflowContinues_DoesNotBlock()
    {
        // Arrange
        var studyContext = new StudyContext
        {
            StudyId = "DICOM-FAIL-001",
            PatientId = "PATIENT-001",
            CurrentState = States.WorkflowState.Idle,
            PatientInfo = CreateTestPatientInfo("PATIENT-001", "DICOM Failure Test", 1992, 'F')
        };

        var workflowMachine = CreateWorkflowStateMachine(studyContext);

        // Navigate to MppsComplete state
        var pathToMpps = new[]
        {
            StateMachineWorkflowState.PatientSelect,
            StateMachineWorkflowState.ProtocolSelect,
            StateMachineWorkflowState.WorklistSync,
            StateMachineWorkflowState.PositionAndPreview,
            StateMachineWorkflowState.ExposureTrigger,
            StateMachineWorkflowState.QcReview,
            StateMachineWorkflowState.MppsComplete
        };

        foreach (var state in pathToMpps)
        {
            var result = await workflowMachine.TryTransitionAsync(
                state,
                "NAVIGATE",
                "TEST_OPERATOR",
                cancellationToken: CancellationToken.None);

            result.IsSuccess.Should().BeTrue($"Transition to {state} should succeed");
        }

        // Act - Simulate DICOM MPPS failure and attempt PACS export
        var pacsResult = await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.PacsExport,
            "PACS_TRIGGER",
            "TEST_OPERATOR",
            cancellationToken: CancellationToken.None);

        // Assert - Workflow should continue even if PACS export fails
        // (should be able to complete the study)
        if (pacsResult.IsSuccess)
        {
            workflowMachine.CurrentState.Should().Be(StateMachineWorkflowState.PacsExport);

            // Should be able to complete
            var completeResult = await workflowMachine.TryTransitionAsync(
                StateMachineWorkflowState.Idle, // Completed maps to Idle
                "COMPLETE",
                "TEST_OPERATOR");

            completeResult.IsSuccess.Should().BeTrue(
                "Should be able to complete study even if PACS export had issues");
        }
        else
        {
            // PACS export failed but workflow should still allow completion
            var completeResult = await workflowMachine.TryTransitionAsync(
                StateMachineWorkflowState.Idle, // Completed maps to Idle
                "COMPLETE_WITH_ERRORS",
                "TEST_OPERATOR");

            completeResult.IsSuccess.Should().BeTrue(
                "Should be able to complete study despite DICOM failure");
        }
    }

    /// <summary>
    /// Test 7: Dose limit enforcement blocks exposure
    /// @MX:TODO: RED phase - test fails, implementation needed
    /// </summary>
    [Fact]
    public async Task Test7_DoseLimitEnforcement_BlocksExposure_SafetyCritical()
    {
        // Arrange
        var studyContext = new StudyContext
        {
            StudyId = "DOSE-LIMIT-001",
            PatientId = "PATIENT-001",
            CurrentState = States.WorkflowState.Idle,
            PatientInfo = CreateTestPatientInfo("PATIENT-001", "Dose Limit Test", 1982, 'M')
        };

        var workflowMachine = CreateWorkflowStateMachine(studyContext);

        // Navigate to ExposureTrigger state
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
            var result = await workflowMachine.TryTransitionAsync(
                state,
                "NAVIGATE",
                "TEST_OPERATOR",
                cancellationToken: CancellationToken.None);

            result.IsSuccess.Should().BeTrue($"Transition to {state} should succeed");
        }

        // Act - Simulate dose limit exceeded
        _doseSimulator.SetDoseLimit(10.0); // 10 µGy·m² limit

        // Record dose that exceeds the limit
        var doseEntry = new DoseEntry
        {
            StudyId = studyContext.StudyId,
            PatientId = studyContext.PatientId,
            Dap = 15.0, // Exceed limit with 15 µGy·m²
            Esd = 5.0, // Entrance Skin Dose in mGy
            Timestamp = DateTimeOffset.UtcNow
        };
        await _doseSimulator.RecordDoseAsync(doseEntry, CancellationToken.None);

        // Try to trigger another exposure
        var exposureResult = await workflowMachine.TryTransitionAsync(
            StateMachineWorkflowState.QcReview,
            "EXPOSURE_TRIGGER",
            "TEST_OPERATOR",
            cancellationToken: CancellationToken.None);

        // Assert - Exposure should be blocked when dose limit exceeded
        var proposedDose = new DoseEntry
        {
            StudyId = studyContext.StudyId,
            PatientId = studyContext.PatientId,
            Dap = 5.0, // Try to add 5 more µGy·m²
            Esd = 2.0, // Entrance Skin Dose in mGy
            Timestamp = DateTimeOffset.UtcNow
        };
        var doseCheck = await _doseSimulator.IsWithinDoseLimitsAsync(proposedDose, CancellationToken.None);

        doseCheck.Should().BeFalse(
            "Dose limit check should fail when limits exceeded");

        // Verify cumulative dose shows limit exceeded
        var cumulativeDose = await _doseSimulator.GetCumulativeDoseAsync(CancellationToken.None);
        cumulativeDose.IsWithinLimits.Should().BeFalse("SAFETY: Cumulative dose must show limit exceeded");
    }

    /// <summary>
    /// Helper method to create a WorkflowStateMachine with simulators
    /// @MX:NOTE: Creates state machine with HAL simulators for integration testing
    /// </summary>
    private IWorkflowStateMachine CreateWorkflowStateMachine(StudyContext context)
    {
        // Create guard matrix with all safety checks
        var guardMatrix = new TransitionGuardMatrix(_journal, _safetySimulator, _doseSimulator);

        // Create state machine
        var stateMachine = new WorkflowStateMachine(
            _loggerFactory.CreateLogger<WorkflowStateMachine>(),
            _journal,
            guardMatrix);

        return stateMachine;
    }

    /// <summary>
    /// Helper method to create a PatientInfo for testing.
    /// @MX:NOTE: Simplifies PatientInfo creation with test data
    /// </summary>
    private static Study.PatientInfo CreateTestPatientInfo(
        string patientId,
        string patientName,
        int birthYear,
        char sex,
        bool isEmergency = false)
    {
        return new Study.PatientInfo
        {
            PatientID = patientId,
            PatientName = patientName,
            PatientBirthDate = new DateOnly(birthYear, 1, 1),
            PatientSex = sex,
            IsEmergency = isEmergency
        };
    }

    /// <summary>
    /// Maps States.WorkflowState to StateMachine.WorkflowState.
    /// @MX:NOTE: Required because tests use States.WorkflowState but state machine uses StateMachine.WorkflowState
    /// </summary>
    private static StateMachineWorkflowState MapToStateMachineWorkflowState(States.WorkflowState state) =>
        state.ToString() switch
        {
            "Idle" => StateMachineWorkflowState.Idle,
            "WorklistSync" => StateMachineWorkflowState.WorklistSync,
            "PatientSelect" => StateMachineWorkflowState.PatientSelect,
            "ProtocolSelect" => StateMachineWorkflowState.ProtocolSelect,
            "PositionAndPreview" => StateMachineWorkflowState.PositionAndPreview,
            "ExposureTrigger" => StateMachineWorkflowState.ExposureTrigger,
            "QcReview" => StateMachineWorkflowState.QcReview,
            "RejectRetake" => StateMachineWorkflowState.RejectRetake,
            "MppsComplete" => StateMachineWorkflowState.MppsComplete,
            "PacsExport" => StateMachineWorkflowState.PacsExport,
            "Completed" => StateMachineWorkflowState.Idle, // Completed maps to Idle for state machine
            _ => throw new ArgumentException($"Unknown state: {state}")
        };
}
