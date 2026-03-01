namespace HnVue.Workflow;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HnVue.Workflow.StateMachine;
using HnVue.Workflow.Journal;
using HnVue.Workflow.Safety;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.States;
using StateMachineWorkflowState = HnVue.Workflow.StateMachine.WorkflowState;

// Temporary stubs for pre-existing test files
// TODO: Implement actual IWorkflowEngine and WorkflowEngine

public interface IWorkflowEngine
{
    StateMachineWorkflowState CurrentState { get; }
    event EventHandler<StateChangedEventArgs> StateChanged;
    Task StartWorklistSyncAsync(CancellationToken cancellationToken = default);
    Task ConfirmPatientAsync(Study.PatientInfo patient, CancellationToken cancellationToken = default);
    Task ConfirmProtocolAsync(Protocol.Protocol protocol, CancellationToken cancellationToken = default);
    Task ReadyForExposureAsync(CancellationToken cancellationToken = default);
    Task TriggerExposureAsync(CancellationToken cancellationToken = default);
    Task OnExposureCompleteAsync(Study.ImageData imageData, CancellationToken cancellationToken = default);
    Task AcceptImageAsync(bool hasMoreExposures = false, CancellationToken cancellationToken = default);
    Task RejectImageAsync(Study.RejectReason reason, string operatorId, CancellationToken cancellationToken = default);
    Task ApproveRetakeAsync(CancellationToken cancellationToken = default);
    Task CancelRetakeAsync(CancellationToken cancellationToken = default);
    Task FinalizeStudyAsync(CancellationToken cancellationToken = default);
    Task CompleteExportAsync(CancellationToken cancellationToken = default);
    Task StartEmergencyWorkflowAsync(Study.PatientInfo patient, CancellationToken cancellationToken = default);
    Task AbortStudyAsync(string authorizedOperator, CancellationToken cancellationToken = default);
    Task<RecoveryContext?> PerformCrashRecoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a critical hardware error by initiating emergency shutdown sequence.
    /// SPEC-WORKFLOW-001 Safety T-18: ANY -> IDLE (CriticalHardwareError)
    /// </summary>
    /// <param name="errorEvent">The critical hardware error event.</param>
    /// <param name="currentStudyContext">The current study context (if any).</param>
    /// <param name="operatorId">The operator ID for audit logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the emergency shutdown sequence.</returns>
    Task<EmergencyShutdownResult> HandleCriticalHardwareErrorAsync(
        CriticalHardwareErrorEvent errorEvent,
        StudyContext? currentStudyContext,
        string operatorId,
        CancellationToken cancellationToken = default);
}

public class StateChangedEventArgs : EventArgs
{
    public StateMachineWorkflowState? NewState { get; init; }
}

public class WorkflowEngine : IWorkflowEngine
{
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly IWorkflowJournal _journal;
    private readonly ISafetyInterlock _safetyInterlock;
    private readonly IHvgDriver _hvgDriver;
    private readonly IDetector _detector;
    private readonly IDoseTracker _doseTracker;
    private readonly SafetyEventHandler _safetyEventHandler;

    public WorkflowEngine(
        ILogger<WorkflowEngine> logger,
        IWorkflowJournal journal,
        ISafetyInterlock safetyInterlock,
        IHvgDriver hvgDriver,
        IDetector detector,
        IDoseTracker doseTracker,
        SafetyEventHandler safetyEventHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _safetyInterlock = safetyInterlock ?? throw new ArgumentNullException(nameof(safetyInterlock));
        _hvgDriver = hvgDriver ?? throw new ArgumentNullException(nameof(hvgDriver));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _doseTracker = doseTracker ?? throw new ArgumentNullException(nameof(doseTracker));
        _safetyEventHandler = safetyEventHandler ?? throw new ArgumentNullException(nameof(safetyEventHandler));

        CurrentState = StateMachineWorkflowState.Idle;
    }

    public StateMachineWorkflowState CurrentState { get; private set; }

    public event EventHandler<StateChangedEventArgs>? StateChanged;

    protected virtual void OnStateChanged(StateMachineWorkflowState? newState)
    {
        StateChanged?.Invoke(this, new StateChangedEventArgs { NewState = newState });
    }

    public Task StartWorklistSyncAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.WorklistSync;
        OnStateChanged(StateMachineWorkflowState.WorklistSync);
        return Task.CompletedTask;
    }

    public Task ConfirmPatientAsync(Study.PatientInfo patient, CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.PatientSelect;
        OnStateChanged(StateMachineWorkflowState.PatientSelect);
        return Task.CompletedTask;
    }

    public Task ConfirmProtocolAsync(Protocol.Protocol protocol, CancellationToken cancellationToken = default)
    {
        var oldState = CurrentState;
        CurrentState = StateMachineWorkflowState.ProtocolSelect;

        // Only raise event if state actually changed
        if (oldState != CurrentState)
        {
            OnStateChanged(CurrentState);
        }

        return Task.CompletedTask;
    }

    public async Task ReadyForExposureAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.PositionAndPreview;
        OnStateChanged(StateMachineWorkflowState.PositionAndPreview);

        // Check interlocks before transitioning to ExposureTrigger
        var status = await _safetyInterlock.CheckAllInterlocksAsync(cancellationToken);

        // All interlocks must be true for the transition to succeed
        var allInterlocksPassed = status.door_closed &&
                                  status.emergency_stop_clear &&
                                  status.thermal_normal &&
                                  status.generator_ready &&
                                  status.detector_ready &&
                                  status.collimator_valid &&
                                  status.table_locked &&
                                  status.dose_within_limits &&
                                  status.aec_configured;

        if (allInterlocksPassed)
        {
            // Interlocks passed - transition to ExposureTrigger
            CurrentState = StateMachineWorkflowState.ExposureTrigger;
            OnStateChanged(StateMachineWorkflowState.ExposureTrigger);
        }
        // If interlocks fail, remain in PositionAndPreview (no state change)

        return;
    }

    public Task TriggerExposureAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.ExposureTrigger;
        OnStateChanged(StateMachineWorkflowState.ExposureTrigger);
        return Task.CompletedTask;
    }

    public Task OnExposureCompleteAsync(Study.ImageData imageData, CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.QcReview;
        OnStateChanged(StateMachineWorkflowState.QcReview);
        return Task.CompletedTask;
    }

    public Task AcceptImageAsync(bool hasMoreExposures = false, CancellationToken cancellationToken = default)
    {
        if (hasMoreExposures)
        {
            CurrentState = StateMachineWorkflowState.ProtocolSelect;
        }
        else
        {
            CurrentState = StateMachineWorkflowState.MppsComplete;
        }
        OnStateChanged(CurrentState);
        return Task.CompletedTask;
    }

    public Task RejectImageAsync(Study.RejectReason reason, string operatorId, CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.RejectRetake;
        OnStateChanged(StateMachineWorkflowState.RejectRetake);
        return Task.CompletedTask;
    }

    public Task ApproveRetakeAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.PositionAndPreview;
        OnStateChanged(StateMachineWorkflowState.PositionAndPreview);
        return Task.CompletedTask;
    }

    public Task CancelRetakeAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.MppsComplete;
        OnStateChanged(StateMachineWorkflowState.MppsComplete);
        return Task.CompletedTask;
    }

    public Task FinalizeStudyAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.PacsExport;
        OnStateChanged(StateMachineWorkflowState.PacsExport);
        return Task.CompletedTask;
    }

    public Task CompleteExportAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.Idle;
        OnStateChanged(StateMachineWorkflowState.Idle);
        return Task.CompletedTask;
    }

    public Task StartEmergencyWorkflowAsync(Study.PatientInfo patient, CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.PatientSelect;
        OnStateChanged(StateMachineWorkflowState.PatientSelect);
        return Task.CompletedTask;
    }

    public Task AbortStudyAsync(string authorizedOperator, CancellationToken cancellationToken = default)
    {
        CurrentState = StateMachineWorkflowState.Idle;
        OnStateChanged(StateMachineWorkflowState.Idle);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a critical hardware error by initiating emergency shutdown sequence.
    /// SPEC-WORKFLOW-001 Safety T-18: ANY -> IDLE (CriticalHardwareError)
    ///
    /// This method:
    /// 1. Invokes SafetyEventHandler to perform emergency shutdown (abort exposure, standby hardware, etc.)
    /// 2. Transitions the state machine to IDLE
    /// 3. Returns the result of the emergency shutdown sequence
    /// </summary>
    /// <param name="errorEvent">The critical hardware error event.</param>
    /// <param name="currentStudyContext">The current study context (if any).</param>
    /// <param name="operatorId">The operator ID for audit logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the emergency shutdown sequence.</returns>
    public async Task<EmergencyShutdownResult> HandleCriticalHardwareErrorAsync(
        CriticalHardwareErrorEvent errorEvent,
        StudyContext? currentStudyContext,
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogCritical(
            "CRITICAL HARDWARE ERROR in WorkflowEngine: {ErrorCode} - {ErrorDescription}",
            errorEvent.ErrorCode,
            errorEvent.ErrorDescription);

        // Step 1: Execute emergency shutdown sequence via SafetyEventHandler
        var shutdownResult = await _safetyEventHandler.HandleCriticalHardwareErrorAsync(
            errorEvent,
            currentStudyContext,
            operatorId,
            cancellationToken);

        // Step 2: Transition state machine to IDLE
        // Note: In a full implementation, this would use WorkflowStateMachine.TryTransitionAsync
        // For the stub, we directly set the state
        var previousState = CurrentState;
        CurrentState = StateMachineWorkflowState.Idle;

        _logger.LogInformation(
            "State transitioned from {PreviousState} to IDLE due to critical hardware error",
            previousState);

        OnStateChanged(StateMachineWorkflowState.Idle);

        return shutdownResult;
    }

    public Task<RecoveryContext?> PerformCrashRecoveryAsync(CancellationToken cancellationToken = default)
    {
        // Return a mock recovery context
        return Task.FromResult<RecoveryContext?>(new RecoveryContext
        {
            StudyInstanceUID = "1.2.3.4.5.100",
            StateAtCrash = StateMachineWorkflowState.PositionAndPreview,
            PatientID = "PATIENT001"
        });
    }
}

public class RecoveryContext
{
    public string StudyInstanceUID { get; set; } = string.Empty;
    public StateMachineWorkflowState StateAtCrash { get; set; }
    public string PatientID { get; set; } = string.Empty;
}
