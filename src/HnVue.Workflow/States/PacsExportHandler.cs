using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the PACS (Picture Archiving and Communication System) Export state.
/// </summary>
/// <remarks>
/// @MX:NOTE: PACS export state handler - finalize image storage to PACS
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-09
///
/// This state handles the final export of images and metadata to the PACS system.
/// After successful PACS export, the workflow transitions to Completed state.
/// </remarks>
public sealed class PacsExportHandler : IStateHandler
{
    private readonly ILogger<PacsExportHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the PacsExportHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PacsExportHandler(ILogger<PacsExportHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.PacsExport;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering PacsExport state for StudyId: {StudyId}",
            context.StudyId);

        // PACS export orchestration would be handled here
        // Actual DICOM storage is handled by IDicomServiceFacade

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting PacsExport state for StudyId: {StudyId}",
            context.StudyId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        // After PACS export, workflow is complete
        var canTransition = targetState == WorkflowState.Completed;

        _logger.LogDebug(
            "PacsExport -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
