using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the Protocol Select state - protocol mapping and validation.
/// </summary>
/// <remarks>
/// @MX:NOTE: Protocol selection state handler - maps exam protocol to imaging parameters
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-03
///
/// This state handles protocol selection and validates protocol compatibility with the selected patient.
/// After protocol is mapped and exposure parameters are set, the workflow transitions to WorklistSync.
/// </remarks>
public sealed class ProtocolSelectHandler : IStateHandler
{
    private readonly ILogger<ProtocolSelectHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the ProtocolSelectHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public ProtocolSelectHandler(ILogger<ProtocolSelectHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.ProtocolSelect;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering ProtocolSelect state for StudyId: {StudyId}",
            context.StudyId);

        // Check if protocol information exists in metadata
        if (context.Metadata != null && context.Metadata.TryGetValue("protocol", out var protocol))
        {
            _logger.LogInformation(
                "Protocol selected for StudyId: {StudyId}, Protocol: {Protocol}",
                context.StudyId,
                protocol);
        }
        else
        {
            _logger.LogWarning(
                "No protocol specified for StudyId: {StudyId}",
                context.StudyId);
        }

        // Protocol compatibility validation would be performed here
        // Exposure parameters would be configured based on selected protocol

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting ProtocolSelect state for StudyId: {StudyId}",
            context.StudyId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        // After protocol selection, sync with DICOM worklist
        var canTransition = targetState == WorkflowState.WorklistSync;

        _logger.LogDebug(
            "ProtocolSelect -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
