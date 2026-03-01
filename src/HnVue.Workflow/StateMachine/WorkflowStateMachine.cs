namespace HnVue.Workflow.StateMachine;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HnVue.Workflow.Journal;

/// <summary>
/// Interface for the workflow state machine.
/// Used by emergency workflow coordinator and other components.
/// SPEC-WORKFLOW-001: FR-WF-07 Emergency workflow bypass
/// </summary>
public interface IWorkflowStateMachine
{
    /// <summary>
    /// Gets the current workflow state.
    /// </summary>
    WorkflowState CurrentState { get; }

    /// <summary>
    /// Attempts to transition to a new state.
    /// </summary>
    Task<TransitionResult> TryTransitionAsync(
        WorkflowState targetState,
        string trigger,
        string operatorId,
        GuardEvaluationContext? context = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Core workflow state machine orchestrator.
///
/// SPEC-WORKFLOW-001 FR-WF-01: Full Workflow State Machine
/// SPEC-WORKFLOW-001 NFR-WF-01: Atomic, Logged State Transitions
/// SPEC-WORKFLOW-001 NFR-WF-03: Invalid Transition Prevention
/// SPEC-WORKFLOW-001 IEC 62304 Class C: X-ray exposure control
///
/// Key Design Principles:
/// - Single Active State: Exclusive lock on state transitions
/// - Guard-Before-Act: Guards evaluated before any side-effecting action
/// - Journal-Before-Notify: Journal write prerequisite for external events
/// - Fail-Safe Default: Exceptions result in safe state
/// </summary>
// @MX:ANCHOR: Primary state machine orchestrator
// @MX:REASON: High fan_in - central component for all workflow state management. Critical for patient safety.
public class WorkflowStateMachine : IWorkflowStateMachine
{
    private readonly ILogger<WorkflowStateMachine> _logger;
    private readonly IWorkflowJournal _journal;
    private readonly ITransitionGuardMatrix _guardMatrix;
    private readonly SemaphoreSlim _stateLock;
    private WorkflowState _currentState;

    /// <summary>
    /// Event raised when state changes successfully.
    /// SPEC-WORKFLOW-001 FR-WF-01-e: Publish event within 50ms
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets the current workflow state.
    /// Thread-safe access to the state.
    /// </summary>
    public WorkflowState CurrentState
    {
        get
        {
            _stateLock.Wait();
            try
            {
                return _currentState;
            }
            finally
            {
                _stateLock.Release();
            }
        }
    }

    /// <summary>
    /// Initializes a new instance with IDLE as the initial state.
    /// </summary>
    public WorkflowStateMachine(
        ILogger<WorkflowStateMachine> logger,
        IWorkflowJournal journal,
        ITransitionGuardMatrix guardMatrix)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _guardMatrix = guardMatrix ?? throw new ArgumentNullException(nameof(guardMatrix));
        _stateLock = new SemaphoreSlim(1, 1);
        _currentState = WorkflowState.Idle;

        _logger.LogInformation("WorkflowStateMachine initialized in {State} state", WorkflowState.Idle);
    }

    /// <summary>
    /// Attempts to transition to a new state.
    ///
    /// SPEC-WORKFLOW-001 FR-WF-01-b: Evaluate guards before transition
    /// SPEC-WORKFLOW-001 NFR-WF-01-a: Atomic journal write before event publish
    /// </summary>
    /// <param name="targetState">The state to transition to.</param>
    /// <param name="trigger">The trigger causing the transition.</param>
    /// <param name="operatorId">The operator performing the action.</param>
    /// <param name="context">Optional guard evaluation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transition result indicating success or failure.</returns>
    public async Task<TransitionResult> TryTransitionAsync(
        WorkflowState targetState,
        string trigger,
        string operatorId,
        GuardEvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteTransitionAsync(targetState, trigger, operatorId, context, cancellationToken);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task<TransitionResult> ExecuteTransitionAsync(
        WorkflowState targetState,
        string trigger,
        string operatorId,
        GuardEvaluationContext? context,
        CancellationToken cancellationToken)
    {
        var fromState = _currentState;
        var transitionId = Guid.NewGuid();

        _logger.LogDebug(
            "Attempting transition: {FromState} -> {ToState} (Trigger: {Trigger}, Operator: {OperatorId})",
            fromState, targetState, trigger, operatorId);

        // Step 1: Validate transition is defined
        if (!_guardMatrix.IsTransitionDefined(fromState, targetState, trigger))
        {
            _logger.LogWarning(
                "Invalid transition attempted: {FromState} -> {ToState} (Trigger: {Trigger})",
                fromState, targetState, trigger);

            return TransitionResult.Errored(
                fromState,
                new InvalidStateTransitionException(fromState, targetState, trigger));
        }

        // Step 2: Evaluate guards
        GuardEvaluationResult guardResult;
        try
        {
            guardResult = await _guardMatrix.EvaluateGuardsAsync(
                fromState, targetState, trigger, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Guard evaluation failed for transition {FromState} -> {ToState}",
                fromState, targetState);
            return TransitionResult.Errored(fromState, ex);
        }

        if (!guardResult.AllPassed)
        {
            _logger.LogWarning(
                "Transition {FromState} -> {ToState} blocked by guards: {FailedGuards}",
                fromState, targetState, string.Join(", ", guardResult.FailedGuards));

            return TransitionResult.GuardFailed(fromState, guardResult.FailedGuards);
        }

        // Step 3: Write to journal (write-ahead log pattern)
        var journalEntry = CreateJournalEntry(
            transitionId, fromState, targetState, trigger, operatorId, context);

        try
        {
            await _journal.WriteEntryAsync(journalEntry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Journal write failed for transition {FromState} -> {ToState}",
                fromState, targetState);
            return TransitionResult.Errored(fromState, ex);
        }

        // Step 4: Apply state change
        _currentState = targetState;

        // Step 5: Publish event
        try
        {
            StateChanged?.Invoke(this, new StateChangedEventArgs
            {
                PreviousState = fromState,
                NewState = targetState,
                Trigger = trigger,
                TransitionId = transitionId,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation(
                "Transition completed: {FromState} -> {ToState} (Trigger: {Trigger}, TransitionId: {TransitionId})",
                fromState, targetState, trigger, transitionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Event publish failed for transition {FromState} -> {ToState}",
                fromState, targetState);
            // State change is already applied; event failure is logged but doesn't roll back
        }

        return TransitionResult.Success(fromState, targetState, trigger);
    }

    private WorkflowJournalEntry CreateJournalEntry(
        Guid transitionId,
        WorkflowState fromState,
        WorkflowState toState,
        string trigger,
        string operatorId,
        GuardEvaluationContext? context)
    {
        return new WorkflowJournalEntry
        {
            TransitionId = transitionId,
            Timestamp = DateTime.UtcNow,
            FromState = fromState,
            ToState = toState,
            Trigger = trigger,
            GuardResults = Array.Empty<GuardResult>(),
            OperatorId = operatorId,
            StudyInstanceUID = context?.Metadata.GetValueOrDefault("StudyInstanceUID") as string,
            Category = IsSafetyCriticalTransition(fromState, toState) ? LogCategory.SAFETY : LogCategory.WORKFLOW
        };
    }

    private bool IsSafetyCriticalTransition(WorkflowState from, WorkflowState to)
    {
        // Transitions involving exposure trigger or critical safety states
        return to == WorkflowState.ExposureTrigger ||
               from == WorkflowState.ExposureTrigger ||
               to == WorkflowState.PositionAndPreview;
    }
}

/// <summary>
/// Interface for transition guard matrix (for testability).
/// </summary>
public interface ITransitionGuardMatrix
{
    bool IsTransitionDefined(WorkflowState from, WorkflowState to, string trigger);
    Task<GuardEvaluationResult> EvaluateGuardsAsync(
        WorkflowState from,
        WorkflowState to,
        string trigger,
        GuardEvaluationContext? context = null);
}

/// <summary>
/// Adapter to make TransitionGuardMatrix implement ITransitionGuardMatrix.
/// </summary>
public class TransitionGuardMatrixAdapter : ITransitionGuardMatrix
{
    private readonly TransitionGuardMatrix _matrix;

    public TransitionGuardMatrixAdapter(TransitionGuardMatrix matrix)
    {
        _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
    }

    public bool IsTransitionDefined(WorkflowState from, WorkflowState to, string trigger)
        => _matrix.IsTransitionDefined(from, to, trigger);

    public Task<GuardEvaluationResult> EvaluateGuardsAsync(
        WorkflowState from,
        WorkflowState to,
        string trigger,
        GuardEvaluationContext? context = null)
        => _matrix.EvaluateGuardsAsync(from, to, trigger, context);
}

/// <summary>
/// Event arguments for state changed event.
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public WorkflowState PreviousState { get; init; }
    public WorkflowState NewState { get; init; }
    public string Trigger { get; init; } = string.Empty;
    public Guid TransitionId { get; init; }
    public DateTime Timestamp { get; init; }
}
