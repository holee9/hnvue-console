namespace HnVue.Workflow;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HnVue.Workflow.StateMachine;
using HnVue.Workflow.Journal;
using HnVue.Workflow.Safety;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.States;
using StudyPatientInfo = HnVue.Workflow.Study.PatientInfo;
using StudyImageData = HnVue.Workflow.Study.ImageData;
using StudyRejectReason = HnVue.Workflow.Study.RejectReason;
using StudyStudyContext = HnVue.Workflow.Study.StudyContext;
using ProtocolProtocol = HnVue.Workflow.Protocol.Protocol;
using StateMachineWorkflowState = HnVue.Workflow.StateMachine.WorkflowState;

/// <summary>
/// Interface for the workflow engine that orchestrates the imaging workflow.
/// SPEC-WORKFLOW-001 FR-WF-01: Full Workflow State Machine
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Gets the current workflow state.
    /// </summary>
    StateMachineWorkflowState CurrentState { get; }

    /// <summary>
    /// Event raised when the workflow state changes.
    /// </summary>
    event EventHandler<StateChangedEventArgs> StateChanged;

    /// <summary>
    /// Starts the worklist synchronization process.
    /// </summary>
    Task StartWorklistSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms patient selection and transitions to protocol selection.
    /// </summary>
    Task ConfirmPatientAsync(StudyPatientInfo patient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms protocol selection and transitions to positioning.
    /// </summary>
    Task ConfirmProtocolAsync(ProtocolProtocol protocol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals operator ready for exposure.
    /// </summary>
    Task ReadyForExposureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers the X-ray exposure.
    /// </summary>
    Task TriggerExposureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when exposure acquisition completes.
    /// </summary>
    Task OnExposureCompleteAsync(StudyImageData imageData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts the current image.
    /// </summary>
    Task AcceptImageAsync(bool hasMoreExposures = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects the current image.
    /// </summary>
    Task RejectImageAsync(StudyRejectReason reason, string operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the retake and returns to positioning.
    /// </summary>
    Task ApproveRetakeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the retake and proceeds to study completion.
    /// </summary>
    Task CancelRetakeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes the study and initiates PACS export.
    /// </summary>
    Task FinalizeStudyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the PACS export and returns to idle.
    /// </summary>
    Task CompleteExportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an emergency workflow.
    /// </summary>
    Task StartEmergencyWorkflowAsync(StudyPatientInfo patient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts the current study and returns to idle.
    /// </summary>
    Task AbortStudyAsync(string authorizedOperator, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs crash recovery by reading the journal.
    /// </summary>
    Task<RecoveryContext?> PerformCrashRecoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a critical hardware error by initiating emergency shutdown.
    /// SPEC-WORKFLOW-001 Safety T-18: ANY -> IDLE (CriticalHardwareError)
    /// </summary>
    Task<EmergencyShutdownResult> HandleCriticalHardwareErrorAsync(
        CriticalHardwareErrorEvent errorEvent,
        StudyStudyContext? currentStudyContext,
        string operatorId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Main workflow engine that orchestrates the imaging workflow.
///
/// SPEC-WORKFLOW-001 FR-WF-01: Full Workflow State Machine
/// SPEC-WORKFLOW-001 NFR-WF-01: Atomic, Logged State Transitions
///
/// This engine:
/// - Uses WorkflowStateMachine for all state transitions
/// - Delegates state-specific behavior to IStateHandler implementations
/// - Integrates with safety interlocks, HVG driver, detector, and dose tracker
/// - Publishes state change events for UI synchronization
/// - Handles emergency workflows and critical hardware errors
///
/// <para>@MX:ANCHOR: Primary workflow orchestration engine</para>
/// <para>@MX:REASON: High fan_in - central component for all workflow operations. Critical for patient safety.</para>
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly IWorkflowStateMachine _stateMachine;
    private readonly IWorkflowJournal _journal;
    private readonly ISafetyInterlock _safetyInterlock;
    private readonly IHvgDriver _hvgDriver;
    private readonly IDetector _detector;
    private readonly IDoseTracker _doseTracker;
    private readonly SafetyEventHandler _safetyEventHandler;
    private readonly Dictionary<States.WorkflowState, IStateHandler> _stateHandlers;
    private readonly SemaphoreSlim _contextLock;
    private StudyStudyContext? _currentStudyContext;

    /// <summary>
    /// Initializes a new instance of the WorkflowEngine class.
    /// </summary>
    public WorkflowEngine(
        ILogger<WorkflowEngine> logger,
        IWorkflowStateMachine stateMachine,
        IWorkflowJournal journal,
        ISafetyInterlock safetyInterlock,
        IHvgDriver hvgDriver,
        IDetector detector,
        IDoseTracker doseTracker,
        SafetyEventHandler safetyEventHandler,
        IEnumerable<IStateHandler> stateHandlers)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _safetyInterlock = safetyInterlock ?? throw new ArgumentNullException(nameof(safetyInterlock));
        _hvgDriver = hvgDriver ?? throw new ArgumentNullException(nameof(hvgDriver));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _doseTracker = doseTracker ?? throw new ArgumentNullException(nameof(doseTracker));
        _safetyEventHandler = safetyEventHandler ?? throw new ArgumentNullException(nameof(safetyEventHandler));

        if (stateHandlers == null)
        {
            throw new ArgumentNullException(nameof(stateHandlers));
        }

        _contextLock = new SemaphoreSlim(1, 1);
        _stateHandlers = stateHandlers.ToDictionary(h => h.State);

        // Subscribe to state machine events if it's the concrete implementation
        if (_stateMachine is WorkflowStateMachine concreteStateMachine)
        {
            concreteStateMachine.StateChanged += OnStateMachineStateChanged;
        }

        _logger.LogInformation("WorkflowEngine initialized with {HandlerCount} state handlers", _stateHandlers.Count);
    }

    /// <summary>
    /// Gets the current workflow state.
    /// </summary>
    public StateMachineWorkflowState CurrentState => _stateMachine.CurrentState;

    /// <summary>
    /// Event raised when the workflow state changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Handles state change events from the state machine.
    /// </summary>
    private void OnStateMachineStateChanged(object? sender, StateMachine.StateChangedEventArgs e)
    {
        _logger.LogInformation(
            "Workflow state changed: {PreviousState} -> {NewState} (Trigger: {Trigger})",
            e.PreviousState, e.NewState, e.Trigger);

        // Notify subscribers
        StateChanged?.Invoke(this, new StateChangedEventArgs { NewState = e.NewState });

        // Enter the new state handler - map state machine state to states namespace
        var stateHandlerKey = MapToStatesWorkflowState(e.NewState);
        if (_stateHandlers.TryGetValue(stateHandlerKey, out var handler))
        {
            Task.Run(async () =>
            {
                try
                {
                    var context = await GetCurrentStudyContextAsync();
                    var mappedContext = MapToStatesStudyContext(context);
                    await handler.EnterAsync(mappedContext, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error entering state {State}", e.NewState);
                }
            });
        }
    }

    /// <summary>
    /// Maps StateMachine.WorkflowState to States.WorkflowState.
    /// </summary>
    private static States.WorkflowState MapToStatesWorkflowState(StateMachineWorkflowState state) =>
        state.ToString() switch
        {
            "Idle" => States.WorkflowState.Idle,
            "WorklistSync" => States.WorkflowState.WorklistSync,
            "PatientSelect" => States.WorkflowState.PatientSelect,
            "ProtocolSelect" => States.WorkflowState.ProtocolSelect,
            "PositionAndPreview" => States.WorkflowState.PositionAndPreview,
            "ExposureTrigger" => States.WorkflowState.ExposureTrigger,
            "QcReview" => States.WorkflowState.QcReview,
            "RejectRetake" => States.WorkflowState.RejectRetake,
            "MppsComplete" => States.WorkflowState.MppsComplete,
            "PacsExport" => States.WorkflowState.PacsExport,
            _ => throw new ArgumentException($"Unknown state: {state}")
        };

    /// <summary>
    /// Maps StateMachine StudyContext to States StudyContext.
    /// </summary>
    private static States.StudyContext MapToStatesStudyContext(StudyStudyContext context) =>
        new States.StudyContext
        {
            StudyId = context.StudyInstanceUID,
            PatientId = context.PatientID,
            CurrentState = States.WorkflowState.Idle, // Default to Idle since Study.StudyContext doesn't have CurrentState
            IsEmergency = context.IsEmergency,
            PatientName = context.PatientName
        };

    /// <summary>
    /// Gets the current study context in a thread-safe manner.
    /// </summary>
    private async Task<StudyStudyContext> GetCurrentStudyContextAsync()
    {
        await _contextLock.WaitAsync();
        try
        {
            return _currentStudyContext ?? CreateDefaultContext();
        }
        finally
        {
            _contextLock.Release();
        }
    }

    /// <summary>
    /// Creates a default study context for when none exists.
    /// </summary>
    private StudyStudyContext CreateDefaultContext()
    {
        return new StudyStudyContext(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            string.Empty,
            string.Empty,
            false);
    }

    /// <summary>
    /// Starts the worklist synchronization process.
    /// Trigger: WorklistSyncRequested
    /// Target State: WorklistSync
    /// SPEC-WORKFLOW-001 T-01: IDLE -> WORKLIST_SYNC
    /// </summary>
    public async Task StartWorklistSyncAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting worklist sync");

        var context = new GuardEvaluationContext
        {
            NetworkReachable = true,
            AutoSyncIntervalElapsed = true
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.WorklistSync,
            "WorklistSyncRequested",
            GetCurrentOperatorId(),
            context,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "WorklistSync");
        }
    }

    /// <summary>
    /// Confirms patient selection and transitions to protocol selection.
    /// Trigger: PatientConfirmed
    /// Target State: ProtocolSelect
    /// SPEC-WORKFLOW-001 T-05: PATIENT_SELECT -> PROTOCOL_SELECT
    /// </summary>
    public async Task ConfirmPatientAsync(StudyPatientInfo patient, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Confirming patient: {PatientId}", patient.PatientID);

        await UpdateStudyContextAsync(context => new StudyStudyContext(
            context.StudyInstanceUID,
            context.AccessionNumber,
            patient.PatientID,
            patient.PatientName,
            patient.IsEmergency,
            patient.WorklistItemUID,
            patient.PatientBirthDate,
            patient.PatientSex));

        var guardContext = new GuardEvaluationContext
        {
            PatientIdNotEmpty = !string.IsNullOrEmpty(patient.PatientID)
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.ProtocolSelect,
            "PatientConfirmed",
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "ProtocolSelect");
        }
    }

    /// <summary>
    /// Confirms protocol selection and transitions to positioning.
    /// Trigger: ProtocolConfirmed
    /// Target State: PositionAndPreview
    /// SPEC-WORKFLOW-001 T-06: PROTOCOL_SELECT -> POSITION_AND_PREVIEW
    /// </summary>
    public async Task ConfirmProtocolAsync(ProtocolProtocol protocol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Confirming protocol: {ProtocolId}", protocol.ProtocolId);

        var guardContext = new GuardEvaluationContext
        {
            ProtocolValid = IsProtocolValid(protocol),
            ExposureParamsInSafeRange = AreExposureParamsSafe(protocol)
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.PositionAndPreview,
            "ProtocolConfirmed",
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "PositionAndPreview");
        }
    }

    /// <summary>
    /// Signals operator ready for exposure.
    /// Checks safety interlocks before allowing exposure trigger.
    /// Trigger: OperatorReady
    /// Target State: ExposureTrigger
    /// SPEC-WORKFLOW-001 T-07: POSITION_AND_PREVIEW -> EXPOSURE_TRIGGER
    /// </summary>
    public async Task ReadyForExposureAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Operator ready for exposure");

        // Check all safety interlocks
        var status = await _safetyInterlock.CheckAllInterlocksAsync(cancellationToken);
        var allInterlocksPassed = AreAllInterlocksSatisfied(status);

        var guardContext = new GuardEvaluationContext
        {
            HardwareInterlockOk = allInterlocksPassed,
            DetectorReady = status.detector_ready
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.ExposureTrigger,
            "OperatorReady",
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Exposure trigger blocked: {Reason}", result.ErrorMessage);
            HandleTransitionFailure(result, "ExposureTrigger");
        }
    }

    /// <summary>
    /// Triggers the X-ray exposure.
    /// This should be called after ReadyForExposureAsync succeeds.
    /// </summary>
    public Task TriggerExposureAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Triggering exposure");
        // The actual exposure trigger is handled by the ExposureTrigger state handler
        // This method is a no-op in the orchestration layer
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when exposure acquisition completes (success or failure).
    /// Trigger: AcquisitionComplete or AcquisitionFailed
    /// Target State: QcReview
    /// SPEC-WORKFLOW-001 T-08/T-09: EXPOSURE_TRIGGER -> QC_REVIEW
    /// </summary>
    public async Task OnExposureCompleteAsync(StudyImageData imageData, CancellationToken cancellationToken = default)
    {
        var isValid = IsImageDataValid(imageData);
        _logger.LogInformation("Exposure complete. Valid: {IsValid}", isValid);

        var trigger = isValid ? "AcquisitionComplete" : "AcquisitionFailed";
        var guardContext = new GuardEvaluationContext
        {
            ImageDataValid = isValid
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.QcReview,
            trigger,
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "QcReview");
        }
    }

    /// <summary>
    /// Accepts the current image.
    /// Trigger: ImageAccepted
    /// Target State: ProtocolSelect (if more exposures) or MppsComplete (if final)
    /// SPEC-WORKFLOW-001 T-10/T-11: QC_REVIEW -> MPPS_COMPLETE or PROTOCOL_SELECT
    /// </summary>
    public async Task AcceptImageAsync(bool hasMoreExposures = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Image accepted. Has more exposures: {HasMoreExposures}", hasMoreExposures);

        var targetState = hasMoreExposures
            ? StateMachineWorkflowState.ProtocolSelect
            : StateMachineWorkflowState.MppsComplete;

        var guardContext = new GuardEvaluationContext
        {
            StudyHasMoreExposures = hasMoreExposures
        };

        var result = await _stateMachine.TryTransitionAsync(
            targetState,
            "ImageAccepted",
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, targetState.ToString());
        }
    }

    /// <summary>
    /// Rejects the current image and enters reject/retake state.
    /// Trigger: ImageRejected
    /// Target State: RejectRetake
    /// SPEC-WORKFLOW-001 T-12: QC_REVIEW -> REJECT_RETAKE
    /// </summary>
    public async Task RejectImageAsync(StudyRejectReason reason, string operatorId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Image rejected. Reason: {Reason}", reason);

        var guardContext = new GuardEvaluationContext
        {
            RejectReasonProvided = true
        };
        guardContext.Metadata["RejectReason"] = reason;

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.RejectRetake,
            "ImageRejected",
            operatorId,
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "RejectRetake");
        }
    }

    /// <summary>
    /// Approves the retake and returns to positioning.
    /// Trigger: RetakeApproved
    /// Target State: PositionAndPreview
    /// SPEC-WORKFLOW-001 T-13: REJECT_RETAKE -> POSITION_AND_PREVIEW
    /// </summary>
    public async Task ApproveRetakeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retake approved");

        var status = await _safetyInterlock.CheckAllInterlocksAsync(cancellationToken);
        var allInterlocksPassed = AreAllInterlocksSatisfied(status);

        var guardContext = new GuardEvaluationContext
        {
            HardwareInterlockOk = allInterlocksPassed
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.PositionAndPreview,
            "RetakeApproved",
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "PositionAndPreview");
        }
    }

    /// <summary>
    /// Cancels the retake and proceeds to study completion.
    /// Trigger: RetakeCancelled
    /// Target State: MppsComplete
    /// SPEC-WORKFLOW-001 T-14: REJECT_RETAKE -> MPPS_COMPLETE
    /// </summary>
    public async Task CancelRetakeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retake cancelled");

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.MppsComplete,
            "RetakeCancelled",
            GetCurrentOperatorId(),
            null,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "MppsComplete");
        }
    }

    /// <summary>
    /// Finalizes the study and initiates PACS export.
    /// Trigger: ExportInitiated
    /// Target State: PacsExport
    /// SPEC-WORKFLOW-001 T-15: MPPS_COMPLETE -> PACS_EXPORT
    /// </summary>
    public async Task FinalizeStudyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Finalizing study");

        var guardContext = new GuardEvaluationContext
        {
            StudyHasImages = true
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.PacsExport,
            "ExportInitiated",
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "PacsExport");
        }
    }

    /// <summary>
    /// Completes the PACS export and returns to idle.
    /// Trigger: ExportComplete or ExportFailed
    /// Target State: Idle
    /// SPEC-WORKFLOW-001 T-16/T-17: PACS_EXPORT -> IDLE
    /// </summary>
    public async Task CompleteExportAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Export complete");

        var guardContext = new GuardEvaluationContext
        {
            AllImagesTransferred = true
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.Idle,
            "ExportComplete",
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "Idle");
        }
    }

    /// <summary>
    /// Starts an emergency workflow, bypassing normal patient selection.
    /// Trigger: EmergencyWorkflowRequested
    /// Target State: PatientSelect
    /// SPEC-WORKFLOW-001 T-02: IDLE -> PATIENT_SELECT (Emergency)
    /// </summary>
    public async Task StartEmergencyWorkflowAsync(StudyPatientInfo patient, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting emergency workflow for patient: {PatientId}", patient.PatientID);

        // Create emergency study context
        await UpdateStudyContextAsync(_ => new StudyStudyContext(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            patient.PatientID,
            patient.PatientName,
            true));

        var status = await _safetyInterlock.CheckAllInterlocksAsync(cancellationToken);
        var allInterlocksPassed = AreAllInterlocksSatisfied(status);

        var guardContext = new GuardEvaluationContext
        {
            HardwareInterlockOk = allInterlocksPassed
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.PatientSelect,
            "EmergencyWorkflowRequested",
            GetCurrentOperatorId(),
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "PatientSelect");
        }
    }

    /// <summary>
    /// Aborts the current study and returns to idle.
    /// Trigger: StudyAbortRequested
    /// Target State: Idle
    /// SPEC-WORKFLOW-001 T-19: ANY (except IDLE) -> IDLE (StudyAbort)
    /// </summary>
    public async Task AbortStudyAsync(string authorizedOperator, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Study abort requested by operator: {OperatorId}", authorizedOperator);

        var guardContext = new GuardEvaluationContext
        {
            OperatorAuthorized = true
        };

        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.Idle,
            "StudyAbortRequested",
            authorizedOperator,
            guardContext,
            cancellationToken);

        if (!result.IsSuccess)
        {
            HandleTransitionFailure(result, "Idle");
        }
    }

    /// <summary>
    /// Handles a critical hardware error by initiating emergency shutdown sequence.
    /// SPEC-WORKFLOW-001 Safety T-18: ANY -> IDLE (CriticalHardwareError)
    /// </summary>
    public async Task<EmergencyShutdownResult> HandleCriticalHardwareErrorAsync(
        CriticalHardwareErrorEvent errorEvent,
        StudyStudyContext? currentStudyContext,
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogCritical(
            "CRITICAL HARDWARE ERROR: {ErrorCode} - {ErrorDescription}",
            errorEvent.ErrorCode,
            errorEvent.ErrorDescription);

        // Convert Study.StudyContext to States.StudyContext for SafetyEventHandler
        States.StudyContext? statesContext = null;
        if (currentStudyContext != null)
        {
            statesContext = new States.StudyContext
            {
                StudyId = currentStudyContext.StudyInstanceUID,
                PatientId = currentStudyContext.PatientID,
                CurrentState = States.WorkflowState.Idle,
                IsEmergency = currentStudyContext.IsEmergency,
                PatientName = currentStudyContext.PatientName
            };
        }

        // Step 1: Execute emergency shutdown sequence
        var shutdownResult = await _safetyEventHandler.HandleCriticalHardwareErrorAsync(
            errorEvent,
            statesContext,
            operatorId,
            cancellationToken);

        // Step 2: Transition to IDLE state
        var result = await _stateMachine.TryTransitionAsync(
            StateMachineWorkflowState.Idle,
            "CriticalHardwareError",
            operatorId,
            null,
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to transition to IDLE after critical hardware error");
        }

        return shutdownResult ?? new EmergencyShutdownResult();
    }

    /// <summary>
    /// Performs crash recovery by reading the journal and restoring state.
    /// </summary>
    public async Task<RecoveryContext?> PerformCrashRecoveryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing crash recovery");

        try
        {
            // Read journal to find last state
            var lastEntry = await _journal.GetLastEntryAsync(cancellationToken);

            if (lastEntry == null)
            {
                _logger.LogWarning("No journal entries found for recovery");
                return null;
            }

            var recoveryContext = new RecoveryContext
            {
                StudyInstanceUID = lastEntry.StudyInstanceUID ?? string.Empty,
                StateAtCrash = lastEntry.ToState,
                PatientID = lastEntry.OperatorId
            };

            _logger.LogInformation(
                "Recovery context found: StudyId={StudyId}, State={State}",
                recoveryContext.StudyInstanceUID,
                recoveryContext.StateAtCrash);

            return recoveryContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing crash recovery");
            return null;
        }
    }

    /// <summary>
    /// Updates the study context in a thread-safe manner.
    /// </summary>
    private async Task UpdateStudyContextAsync(Func<StudyStudyContext, StudyStudyContext> updateFunc)
    {
        await _contextLock.WaitAsync();
        try
        {
            var current = _currentStudyContext ?? CreateDefaultContext();
            _currentStudyContext = updateFunc(current);
        }
        finally
        {
            _contextLock.Release();
        }
    }

    /// <summary>
    /// Gets the current operator ID from context.
    /// </summary>
    private string GetCurrentOperatorId()
    {
        return _currentStudyContext?.ExposureSeries.FirstOrDefault()?.OperatorId ?? "system";
    }

    /// <summary>
    /// Checks if all interlocks are satisfied.
    /// </summary>
    private static bool AreAllInterlocksSatisfied(InterlockStatus status) =>
        status.door_closed &&
        status.emergency_stop_clear &&
        status.thermal_normal &&
        status.generator_ready &&
        status.detector_ready &&
        status.collimator_valid &&
        status.table_locked &&
        status.dose_within_limits &&
        status.aec_configured;

    /// <summary>
    /// Checks if protocol is valid.
    /// </summary>
    private static bool IsProtocolValid(ProtocolProtocol protocol) =>
        protocol != null &&
        !string.IsNullOrEmpty(protocol.BodyPart) &&
        !string.IsNullOrEmpty(protocol.Projection);

    /// <summary>
    /// Checks if exposure parameters are within safe range.
    /// </summary>
    private static bool AreExposureParamsSafe(ProtocolProtocol protocol) =>
        protocol != null &&
        protocol.Kv > 0 &&
        protocol.Kv <= 150 &&
        protocol.Ma > 0 &&
        protocol.Ma <= 500 &&
        protocol.ExposureTimeMs > 0 &&
        protocol.ExposureTimeMs <= 5000;

    /// <summary>
    /// Checks if image data is valid.
    /// </summary>
    private static bool IsImageDataValid(StudyImageData imageData) =>
        imageData != null &&
        !string.IsNullOrEmpty(imageData.ImageInstanceUID) &&
        imageData.Width > 0 &&
        imageData.Height > 0 &&
        imageData.PixelData?.Length > 0;

    /// <summary>
    /// Handles transition failures by logging and potentially raising events.
    /// </summary>
    private void HandleTransitionFailure(TransitionResult result, string targetState)
    {
        _logger.LogError(
            "Transition to {TargetState} failed: {ErrorMessage}",
            targetState,
            result.ErrorMessage ?? "Unknown error");

        if (result.FailedGuards.Length > 0)
        {
            _logger.LogWarning("Failed guards: {Guards}", string.Join(", ", result.FailedGuards));
        }
    }
}

/// <summary>
/// Event arguments for state changed events.
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public StateMachineWorkflowState? NewState { get; init; }
}

/// <summary>
/// Recovery context for crash recovery.
/// </summary>
public class RecoveryContext
{
    public string StudyInstanceUID { get; set; } = string.Empty;
    public StateMachineWorkflowState StateAtCrash { get; set; }
    public string PatientID { get; set; } = string.Empty;
}
