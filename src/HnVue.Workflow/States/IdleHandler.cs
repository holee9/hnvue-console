using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the Idle state - initial state ready to start a new study.
/// </summary>
/// <remarks>
/// @MX:NOTE: Idle state handler - entry point for workflow initialization
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-01
///
/// The Idle state is the starting point for all studies. From here, the workflow
/// can transition to PatientSelect to begin patient registration and validation.
/// </remarks>
public sealed class IdleHandler : IStateHandler
{
    private readonly ILogger<IdleHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the IdleHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public IdleHandler(ILogger<IdleHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.Idle;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering Idle state for StudyId: {StudyId}, PatientId: {PatientId}",
            context.StudyId,
            context.PatientId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting Idle state for StudyId: {StudyId}",
            context.StudyId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        var canTransition = targetState == WorkflowState.PatientSelect;

        _logger.LogDebug(
            "Idle -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
