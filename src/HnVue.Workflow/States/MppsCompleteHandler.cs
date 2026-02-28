using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the MPPS (Modality Performed Procedure Step) Complete state.
/// </summary>
/// <remarks>
/// @MX:NOTE: MPPS completion state handler - finalize procedure step reporting
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-06
///
/// This state handles completion of MPPS reporting to the DICOM MPPS SCP.
/// After successful MPPS completion, the workflow transitions to QcReview for quality control.
/// </remarks>
public sealed class MppsCompleteHandler : IStateHandler
{
    private readonly ILogger<MppsCompleteHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the MppsCompleteHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MppsCompleteHandler(ILogger<MppsCompleteHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.MppsComplete;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering MppsComplete state for StudyId: {StudyId}",
            context.StudyId);

        // MPPS completion event would be published here
        // Actual MPPS SCP communication is handled by IDicomServiceFacade

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting MppsComplete state for StudyId: {StudyId}",
            context.StudyId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        // After MPPS completion, proceed to QC review
        var canTransition = targetState == WorkflowState.QcReview;

        _logger.LogDebug(
            "MppsComplete -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
