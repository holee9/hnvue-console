namespace HnVue.Workflow.StateMachine;

using System;

/// <summary>
/// Result type for state transition attempts.
/// </summary>
public class TransitionResult
{
    /// <summary>
    /// Gets the new state after transition. For failed transitions, this is the original state.
    /// </summary>
    public WorkflowState NewState { get; init; }

    /// <summary>
    /// Gets the old state before transition (same as NewState for failed transitions).
    /// </summary>
    public WorkflowState OldState { get; init; }

    /// <summary>
    /// Gets whether the transition was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message that caused the transition to fail, if any.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the error that caused the transition to fail, if any.
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// Gets the list of guards that failed during evaluation, if any.
    /// </summary>
    public string[] FailedGuards { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful transition result.
    /// </summary>
    public static TransitionResult Success(WorkflowState oldState, WorkflowState newState, string trigger) =>
        new() { IsSuccess = true, NewState = newState, OldState = oldState };

    /// <summary>
    /// Creates a failed transition result with an error message and failed guards.
    /// </summary>
    public static TransitionResult Failure(WorkflowState originalState, string errorMessage, string[] failedGuards) =>
        new() { IsSuccess = false, NewState = originalState, OldState = originalState, ErrorMessage = errorMessage, FailedGuards = failedGuards };

    /// <summary>
    /// Creates a failed transition result due to guard failure.
    /// </summary>
    public static TransitionResult GuardFailed(WorkflowState originalState, string[] failedGuards) =>
        new() { IsSuccess = false, NewState = originalState, OldState = originalState, FailedGuards = failedGuards };

    /// <summary>
    /// Creates a failed transition result due to an error.
    /// </summary>
    public static TransitionResult Errored(WorkflowState originalState, Exception error) =>
        new() { IsSuccess = false, NewState = originalState, OldState = originalState, Error = error, ErrorMessage = error.Message };
}

/// <summary>
/// Exception thrown when an invalid state transition is attempted.
///
/// SPEC-WORKFLOW-001 NFR-WF-03: Invalid Transition Prevention
/// </summary>
public class InvalidStateTransitionException : Exception
{
    /// <summary>
    /// Gets the source state of the invalid transition.
    /// </summary>
    public WorkflowState FromState { get; }

    /// <summary>
    /// Gets the target state of the invalid transition.
    /// </summary>
    public WorkflowState ToState { get; }

    /// <summary>
    /// Gets the trigger that was attempted.
    /// </summary>
    public string Trigger { get; }

    public InvalidStateTransitionException(WorkflowState fromState, WorkflowState toState, string trigger)
        : base($"Invalid state transition from {fromState} to {toState} with trigger '{trigger}'. This transition is not defined in the Transition Guard Matrix.")
    {
        FromState = fromState;
        ToState = toState;
        Trigger = trigger;
    }
}
