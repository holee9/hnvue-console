using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the RejectRetake state - manages image rejection and retake coordination.
/// </summary>
/// <remarks>
/// @MX:NOTE: Reject and retake state handler - coordinates retake workflow
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-08
///
/// This state handles the workflow when an image is rejected and a retake is needed.
/// It preserves dose information from the rejected exposure and coordinates
/// return to an appropriate state for retake.
/// </remarks>
public sealed class RejectRetakeHandler : IStateHandler
{
    private readonly ILogger<RejectRetakeHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the RejectRetakeHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public RejectRetakeHandler(ILogger<RejectRetakeHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.RejectRetake;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering RejectRetake state for StudyId: {StudyId}, PatientId: {PatientId}",
            context.StudyId,
            context.PatientId);

        // Record rejection reason
        // Preserve dose information from rejected exposure
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting RejectRetake state for StudyId: {StudyId}",
            context.StudyId);

        // Update rejection statistics
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: Retake transition validation - controls retake workflow
    /// @MX:REASON: Workflow-critical - ensures proper retake state flow
    ///
    /// Valid transitions:
    /// - To PositionAndPreview: For repositioning and retake
    /// - To ExposureTrigger: If only parameters need adjustment (skip positioning)
    /// - To MppsComplete: If retake is cancelled and study is to be completed as-is
    /// </remarks>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        var allowedTransitions = new[]
        {
            WorkflowState.PositionAndPreview,
            WorkflowState.ExposureTrigger,
            WorkflowState.MppsComplete
        };

        var canTransition = allowedTransitions.Contains(targetState);

        _logger.LogDebug(
            "RejectRetake -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
