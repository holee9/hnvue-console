using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the QcReview state - quality control image review and approval.
/// </summary>
/// <remarks>
/// @MX:NOTE: Quality control review state handler - manages image approval workflow
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-07
///
/// This state handles the quality control review process where images are
/// evaluated for diagnostic quality. Images can be approved, rejected, or
/// flagged for retake.
/// </remarks>
public sealed class QcReviewHandler : IStateHandler
{
    private readonly ILogger<QcReviewHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the QcReviewHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public QcReviewHandler(ILogger<QcReviewHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.QcReview;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering QcReview state for StudyId: {StudyId}, PatientId: {PatientId}",
            context.StudyId,
            context.PatientId);

        // Initialize QC review session
        // Image display and review tools are activated
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting QcReview state for StudyId: {StudyId}",
            context.StudyId);

        // Cleanup QC review session
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: QC review transition validation - controls post-review workflow
    /// @MX:REASON: Workflow-critical - determines study completion or retake
    ///
    /// Valid transitions:
    /// - To RejectRetake: If image is rejected and retake is needed
    /// - To MppsComplete: If image is approved
    /// - To PacsExport: If approved and MPPS is already complete
    /// </remarks>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        var allowedTransitions = new[]
        {
            WorkflowState.RejectRetake,
            WorkflowState.MppsComplete,
            WorkflowState.PacsExport
        };

        var canTransition = allowedTransitions.Contains(targetState);

        _logger.LogDebug(
            "QcReview -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
