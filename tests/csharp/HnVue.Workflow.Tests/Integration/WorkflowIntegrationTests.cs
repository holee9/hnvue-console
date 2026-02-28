using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HnVue.Workflow.Tests.Integration;

/// <summary>
/// Integration tests for complete workflow scenarios.
/// SPEC-WORKFLOW-001 Section 2: State Machine Architecture
///
/// These tests exercise the full workflow engine through multiple state transitions.
/// </summary>
public class WorkflowIntegrationTests
{
    private readonly Mock<ILogger<WorkflowEngine>> _loggerMock;
    private readonly Mock<IWorkflowJournal> _journalMock;
    private readonly Mock<ISafetyInterlock> _safetyInterlockMock;
    private readonly Mock<IHvgDriver> _hvgDriverMock;
    private readonly Mock<IDetector> _detectorMock;
    private readonly Mock<IDoseTracker> _doseTrackerMock;

    public WorkflowIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<WorkflowEngine>>();
        _journalMock = new Mock<IWorkflowJournal>();
        _safetyInterlockMock = new Mock<ISafetyInterlock>();
        _hvgDriverMock = new Mock<IHvgDriver>();
        _detectorMock = new Mock<IDetector>();
        _doseTrackerMock = new Mock<IDoseTracker>();
    }

    [Fact]
    public async Task CompleteWorkflow_StandardPath_ShouldTransitionThroughAllStates()
    {
        // Arrange
        var engine = CreateWorkflowEngine();
        WorkflowState?[] expectedStates =
        {
            WorkflowState.WorklistSync,
            WorkflowState.PatientSelect,
            WorkflowState.ProtocolSelect,
            WorkflowState.PositionAndPreview,
            WorkflowState.ExposureTrigger,
            WorkflowState.QcReview,
            WorkflowState.MppsComplete,
            WorkflowState.PacsExport,
            WorkflowState.Idle
        };

        SetupAllInterlocksPass();
        SetupJournalSuccess();
        SetupDetectorReady();
        SetupExposureSuccess();
        SetupMppsSuccess();
        SetupExportSuccess();

        var actualStates = new List<WorkflowState?>();
        engine.StateChanged += (s, e) => actualStates.Add(e.NewState);

        // Act
        await engine.StartWorklistSyncAsync();
        await engine.ConfirmPatientAsync(CreateValidPatientInfo());
        await engine.ConfirmProtocolAsync(CreateValidProtocol());
        await engine.ReadyForExposureAsync();
        await engine.OnExposureCompleteAsync(CreateValidImageData());
        await engine.AcceptImageAsync();
        await engine.FinalizeStudyAsync();
        await engine.CompleteExportAsync();

        // Assert
        actualStates.Should().Equal(expectedStates);
    }

    [Fact]
    public async Task EmergencyWorkflow_ShouldBypassWorklistSync()
    {
        // Arrange
        var engine = CreateWorkflowEngine();
        var actualStates = new List<WorkflowState?>();
        engine.StateChanged += (s, e) => actualStates.Add(e.NewState);

        SetupAllInterlocksPass();
        SetupJournalSuccess();
        SetupDetectorReady();
        SetupExposureSuccess();
        SetupMppsSuccess();
        SetupExportSuccess();

        // Act
        await engine.StartEmergencyWorkflowAsync(CreateEmergencyPatientInfo());
        await engine.ConfirmProtocolAsync(CreateValidProtocol());
        await engine.ReadyForExposureAsync();
        await engine.OnExposureCompleteAsync(CreateValidImageData());
        await engine.AcceptImageAsync();
        await engine.FinalizeStudyAsync();
        await engine.CompleteExportAsync();

        // Assert
        actualStates.Should().NotContain(WorkflowState.WorklistSync,
            "emergency workflow should bypass worklist sync");
        actualStates.Should().Contain(WorkflowState.PatientSelect);
    }

    [Fact]
    public async Task MultiExposureStudy_ShouldReturnToProtocolSelectAfterEachExposure()
    {
        // Arrange
        var engine = CreateWorkflowEngine();
        var actualStates = new List<WorkflowState?>();
        engine.StateChanged += (s, e) => actualStates.Add(e.NewState);

        SetupAllInterlocksPass();
        SetupJournalSuccess();
        SetupDetectorReady();
        SetupExposureSuccess();
        SetupMppsSuccess();
        SetupExportSuccess();

        // Act - First exposure
        await engine.StartEmergencyWorkflowAsync(CreateEmergencyPatientInfo());
        await engine.ConfirmProtocolAsync(CreateValidProtocol());
        await engine.ReadyForExposureAsync();
        await engine.OnExposureCompleteAsync(CreateValidImageData());
        await engine.AcceptImageAsync(hasMoreExposures: true);

        // Act - Second exposure
        await engine.ConfirmProtocolAsync(CreateValidProtocol());
        await engine.ReadyForExposureAsync();
        await engine.OnExposureCompleteAsync(CreateValidImageData());
        await engine.AcceptImageAsync(hasMoreExposures: false);

        // Act - Finalize
        await engine.FinalizeStudyAsync();
        await engine.CompleteExportAsync();

        // Assert
        var protocolSelectCount = actualStates.Count(s => s == WorkflowState.ProtocolSelect);
        protocolSelectCount.Should().Be(2, "should return to PROTOCOL_SELECT for each exposure");
    }

    [Fact]
    public async Task RejectAndRetakeWorkflow_ShouldAllowRetake()
    {
        // Arrange
        var engine = CreateWorkflowEngine();
        var actualStates = new List<WorkflowState?>();
        engine.StateChanged += (s, e) => actualStates.Add(e.NewState);

        SetupAllInterlocksPass();
        SetupJournalSuccess();
        SetupDetectorReady();
        SetupExposureSuccess();
        SetupMppsSuccess();

        // Act - First exposure (rejected)
        await engine.StartEmergencyWorkflowAsync(CreateEmergencyPatientInfo());
        await engine.ConfirmProtocolAsync(CreateValidProtocol());
        await engine.ReadyForExposureAsync();
        await engine.OnExposureCompleteAsync(CreateValidImageData());
        await engine.RejectImageAsync(RejectReason.Positioning, "operator1");

        // Act - Approve retake
        await engine.ApproveRetakeAsync();

        // Act - Retake exposure (accepted)
        await engine.ReadyForExposureAsync();
        await engine.OnExposureCompleteAsync(CreateValidImageData());
        await engine.AcceptImageAsync();
        await engine.FinalizeStudyAsync();

        // Assert
        actualStates.Should().Contain(WorkflowState.RejectRetake);
        actualStates.Should().ContainInOrder(
            WorkflowState.QcReview,
            WorkflowState.RejectRetake,
            WorkflowState.PositionAndPreview);
    }

    [Fact]
    public async Task RejectAndCancelWorkflow_ShouldMarkExposureIncomplete()
    {
        // Arrange
        var engine = CreateWorkflowEngine();
        var actualStates = new List<WorkflowState?>();
        engine.StateChanged += (s, e) => actualStates.Add(e.NewState);

        SetupAllInterlocksPass();
        SetupJournalSuccess();
        SetupDetectorReady();
        SetupExposureSuccess();
        SetupMppsSuccess();

        // Act
        await engine.StartEmergencyWorkflowAsync(CreateEmergencyPatientInfo());
        await engine.ConfirmProtocolAsync(CreateValidProtocol());
        await engine.ReadyForExposureAsync();
        await engine.OnExposureCompleteAsync(CreateValidImageData());
        await engine.RejectImageAsync(RejectReason.Positioning, "operator1");
        await engine.CancelRetakeAsync();
        await engine.FinalizeStudyAsync();

        // Assert
        actualStates.Should().ContainInOrder(
            WorkflowState.QcReview,
            WorkflowState.RejectRetake,
            WorkflowState.MppsComplete);
    }

    [Fact]
    public async Task ExposureTrigger_WhenInterlockFails_ShouldRemainInPositionAndPreview()
    {
        // Arrange
        var engine = CreateWorkflowEngine();
        var actualStates = new List<WorkflowState?>();
        engine.StateChanged += (s, e) => actualStates.Add(e.NewState);

        SetupInterlockFailure("IL-01", "door_closed");
        SetupJournalSuccess();
        SetupDetectorReady();

        // Act
        await engine.StartEmergencyWorkflowAsync(CreateEmergencyPatientInfo());
        await engine.ConfirmProtocolAsync(CreateValidProtocol());
        await engine.ReadyForExposureAsync(); // Should fail due to interlock

        // Assert
        engine.CurrentState.Should().Be(WorkflowState.PositionAndPreview,
            "should remain in POSITION_AND_PREVIEW when interlock fails");
    }

    [Fact]
    public async Task ExposureTriggerLatency_ShouldNotExceed200ms()
    {
        // Arrange
        var engine = CreateWorkflowEngine();
        var latencyMs = new List<long>();

        _hvgDriverMock
            .Setup(h => h.TriggerExposureAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var sw = Stopwatch.StartNew();
                // Simulate realistic delay
                Thread.Sleep(50);
                sw.Stop();
                latencyMs.Add(sw.ElapsedMilliseconds);
            })
            .ReturnsAsync(true);

        SetupAllInterlocksPass();
        SetupJournalSuccess();
        SetupDetectorReady();
        SetupExposureSuccess();

        // Act
        await engine.StartEmergencyWorkflowAsync(CreateEmergencyPatientInfo());
        await engine.ConfirmProtocolAsync(CreateValidProtocol());

        var triggerSw = Stopwatch.StartNew();
        await engine.ReadyForExposureAsync();
        triggerSw.Stop();

        // Assert - NFR-WF-05-a: Exposure trigger latency <= 200ms
        triggerSw.ElapsedMilliseconds.Should().BeLessOrEqualTo(200,
            "exposure trigger latency must not exceed 200ms");
    }

    [Fact]
    public async Task StudyAbort_ShouldTransitionToIdleFromAnyState()
    {
        // Arrange - Test abort from each state
        var statesToTest = new[]
        {
            WorkflowState.WorklistSync,
            WorkflowState.PatientSelect,
            WorkflowState.ProtocolSelect,
            WorkflowState.PositionAndPreview
        };

        foreach (var state in statesToTest)
        {
            // Arrange
            var engine = CreateWorkflowEngine();
            SetupAllInterlocksPass();
            SetupJournalSuccess();

            // Navigate to the test state
            await NavigateToStateAsync(engine, state);

            // Act
            await engine.AbortStudyAsync(authorizedOperator: "admin");

            // Assert
            engine.CurrentState.Should().Be(WorkflowState.Idle,
                $"abort from {state} should transition to IDLE");
        }
    }

    [Fact]
    public async Task CrashRecovery_ShouldRestoreStudyContext()
    {
        // Arrange
        var engine = CreateWorkflowEngine();
        var recoveryEntry = new WorkflowJournalEntry
        {
            TransitionId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            FromState = WorkflowState.ProtocolSelect,
            ToState = WorkflowState.PositionAndPreview,
            Trigger = "OperatorReady",
            StudyInstanceUID = "1.2.3.4.5.100",
            Metadata = new Dictionary<string, object>
            {
                ["PatientID"] = "PATIENT001",
                ["PatientName"] = "Test^Patient",
                ["IsEmergency"] = false
            }
        };

        _journalMock
            .Setup(j => j.GetLastEntryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(recoveryEntry);

        SetupAllInterlocksPass();

        // Act
        var recoveredContext = await engine.PerformCrashRecoveryAsync();

        // Assert
        recoveredContext.Should().NotBeNull();
        recoveredContext!.StudyInstanceUID.Should().Be("1.2.3.4.5.100");
        recoveredContext.PatientID.Should().Be("PATIENT001");
        recoveredContext.StateAtCrash.Should().Be(WorkflowState.PositionAndPreview);
    }

    private WorkflowEngine CreateWorkflowEngine()
    {
        return new WorkflowEngine(
            _loggerMock.Object,
            _journalMock.Object,
            _safetyInterlockMock.Object,
            _hvgDriverMock.Object,
            _detectorMock.Object,
            _doseTrackerMock.Object);
    }

    private void SetupAllInterlocksPass()
    {
        _safetyInterlockMock
            .Setup(s => s.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InterlockStatus
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
            });
    }

    private void SetupInterlockFailure(string interlockId, string fieldName)
    {
        _safetyInterlockMock
            .Setup(s => s.CheckAllInterlocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InterlockStatus
            {
                door_closed = fieldName == "door_closed" ? false : true,
                emergency_stop_clear = fieldName == "emergency_stop_clear" ? false : true,
                thermal_normal = true,
                generator_ready = true,
                detector_ready = true,
                collimator_valid = true,
                table_locked = true,
                dose_within_limits = true,
                aec_configured = true
            });
    }

    private void SetupJournalSuccess()
    {
        _journalMock
            .Setup(j => j.WriteEntryAsync(It.IsAny<WorkflowJournalEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupDetectorReady()
    {
        _detectorMock
            .Setup(d => d.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DetectorStatus.Ready);
    }

    private void SetupExposureSuccess()
    {
        _hvgDriverMock
            .Setup(h => h.TriggerExposureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupMppsSuccess()
    {
        // MPPS setup would go here
    }

    private void SetupExportSuccess()
    {
        // Export setup would go here
    }

    private async Task NavigateToStateAsync(WorkflowEngine engine, WorkflowState targetState)
    {
        switch (targetState)
        {
            case WorkflowState.WorklistSync:
                await engine.StartWorklistSyncAsync();
                break;
            case WorkflowState.PatientSelect:
                await engine.StartWorklistSyncAsync();
                await engine.ConfirmPatientAsync(CreateValidPatientInfo());
                break;
            case WorkflowState.ProtocolSelect:
                await engine.StartEmergencyWorkflowAsync(CreateEmergencyPatientInfo());
                break;
            case WorkflowState.PositionAndPreview:
                await engine.StartEmergencyWorkflowAsync(CreateEmergencyPatientInfo());
                await engine.ConfirmProtocolAsync(CreateValidProtocol());
                break;
        }
    }

    private PatientInfo CreateValidPatientInfo()
    {
        return new PatientInfo
        {
            PatientID = "PATIENT001",
            PatientName = "Test^Patient",
            PatientBirthDate = new DateOnly(1980, 1, 1),
            PatientSex = 'M',
            AccessionNumber = "ACC001",
            WorklistItemUID = "1.2.3.4.5.200"
        };
    }

    private PatientInfo CreateEmergencyPatientInfo()
    {
        return new PatientInfo
        {
            PatientID = "EMERGENCY001",
            PatientName = "Emergency^Patient",
            IsEmergency = true
        };
    }

    private HnVue.Workflow.Protocol.Protocol CreateValidProtocol()
    {
        return new HnVue.Workflow.Protocol.Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            AecMode = AecMode.Disabled,
            FocusSize = FocusSize.Large,
            GridUsed = true,
            DeviceModel = "HVG-3000"
        };
    }

    private ImageData CreateValidImageData()
    {
        return new ImageData
        {
            ImageInstanceUID = "1.2.3.4.5.300",
            Width = 2048,
            Height = 2048,
            BitsPerPixel = 16,
            PixelData = new byte[2048 * 2048 * 2]
        };
    }
}
