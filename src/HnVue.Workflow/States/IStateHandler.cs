namespace HnVue.Workflow.States;

/// <summary>
/// Defines the contract for handling workflow state transitions and lifecycle.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: State handler interface - core abstraction for workflow state management
/// @MX:REASON: All state handlers must implement this interface for consistent state transition behavior
/// @MX:SPEC: SPEC-WORKFLOW-001
///
/// This interface provides a uniform API for entering, exiting, and validating state transitions.
/// Each state handler encapsulates the behavior specific to its workflow state.
/// </remarks>
public interface IStateHandler
{
    /// <summary>
    /// Gets the workflow state this handler is responsible for.
    /// </summary>
    WorkflowState State { get; }

    /// <summary>
    /// Performs actions when entering the state.
    /// </summary>
    /// <param name="context">The study context containing workflow data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Entry point for state-specific initialization logic
    /// Implementations should log state entry and perform any required state setup.
    /// </remarks>
    Task EnterAsync(StudyContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Performs actions when exiting the state.
    /// </summary>
    /// <param name="context">The study context containing workflow data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Exit point for state-specific cleanup logic
    /// Implementations should log state exit and perform any required state cleanup.
    /// </remarks>
    Task ExitAsync(StudyContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether a transition to the target state is allowed.
    /// </summary>
    /// <param name="targetState">The destination state to transition to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the transition is allowed; otherwise, false.</returns>
    /// <remarks>
    /// @MX:NOTE: Guard clause for state transition validation
    /// Implementations should enforce state machine transition rules.
    /// </remarks>
    Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken);
}
