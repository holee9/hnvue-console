using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the Worklist Sync state - DICOM worklist query and synchronization.
/// </summary>
/// <remarks>
/// @MX:NOTE: Worklist synchronization state handler - queries DICOM modality worklist
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-04
///
/// This state handles DICOM worklist synchronization to retrieve scheduled procedure information.
/// After worklist entry is matched and synced, the workflow transitions to PositionAndPreview.
/// </remarks>
public sealed class WorklistSyncHandler : IStateHandler
{
    private readonly ILogger<WorklistSyncHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the WorklistSyncHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public WorklistSyncHandler(ILogger<WorklistSyncHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.WorklistSync;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering WorklistSync state for StudyId: {StudyId}, PatientId: {PatientId}",
            context.StudyId,
            context.PatientId);

        // DICOM worklist query would be performed here (stub for now)
        // Actual worklist SCP communication is handled by IDicomServiceFacade

        _logger.LogInformation(
            "Querying DICOM worklist for PatientId: {PatientId}",
            context.PatientId);

        // Worklist entry matching would be performed here

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting WorklistSync state for StudyId: {StudyId}",
            context.StudyId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        // After worklist sync, proceed to patient positioning and preview
        var canTransition = targetState == WorkflowState.PositionAndPreview;

        _logger.LogDebug(
            "WorklistSync -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
