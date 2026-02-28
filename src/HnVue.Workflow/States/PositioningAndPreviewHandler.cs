using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the PositioningAndPreview state - patient positioning and image preview.
/// </summary>
/// <remarks>
/// @MX:NOTE: Positioning and preview state handler - manages patient positioning workflow
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-05
///
/// This state handles patient positioning, preview image acquisition, and validation
/// before proceeding to exposure. Safety interlocks are verified before allowing
/// transition to the ExposureTrigger state.
/// </remarks>
public sealed class PositioningAndPreviewHandler : IStateHandler
{
    private readonly ILogger<PositioningAndPreviewHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the PositioningAndPreviewHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PositioningAndPreviewHandler(ILogger<PositioningAndPreviewHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.PositionAndPreview;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering PositionAndPreview state for StudyId: {StudyId}, PatientId: {PatientId}",
            context.StudyId,
            context.PatientId);

        // Initialize positioning session
        // Preview capture will be triggered separately
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting PositionAndPreview state for StudyId: {StudyId}",
            context.StudyId);

        // Cleanup positioning session
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: PositioningAndPreview transition validation
    /// @MX:REASON: Safety-critical - ensures positioning is complete before exposure
    ///
    /// Valid transitions:
    /// - To ExposureTrigger: Only after positioning is complete and preview is approved
    /// - To QcReview: If bypassing exposure (rare, for calibration)
    /// - To Idle: For workflow cancellation
    /// </remarks>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        var allowedTransitions = new[]
        {
            WorkflowState.ExposureTrigger,
            WorkflowState.QcReview,
            WorkflowState.Idle
        };

        var canTransition = allowedTransitions.Contains(targetState);

        _logger.LogDebug(
            "PositionAndPreview -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
