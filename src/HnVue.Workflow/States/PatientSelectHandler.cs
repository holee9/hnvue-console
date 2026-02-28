using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the Patient Select state - patient information validation.
/// </summary>
/// <remarks>
/// @MX:NOTE: Patient selection state handler - validates patient data before proceeding
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-02
///
/// This state handles patient selection and validation. After patient information is
/// validated and confirmed, the workflow transitions to ProtocolSelect for protocol mapping.
/// </remarks>
public sealed class PatientSelectHandler : IStateHandler
{
    private readonly ILogger<PatientSelectHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the PatientSelectHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PatientSelectHandler(ILogger<PatientSelectHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.PatientSelect;

    /// <inheritdoc/>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering PatientSelect state for StudyId: {StudyId}, PatientId: {PatientId}",
            context.StudyId,
            context.PatientId);

        // Validate patient ID format
        if (string.IsNullOrWhiteSpace(context.PatientId) || context.PatientId.Length < 3)
        {
            _logger.LogWarning(
                "Invalid patient ID format for StudyId: {StudyId}, PatientId: {PatientId}",
                context.StudyId,
                context.PatientId);
        }
        else
        {
            _logger.LogInformation(
                "Patient ID validated successfully: {PatientId}",
                context.PatientId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting PatientSelect state for StudyId: {StudyId}",
            context.StudyId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        // After patient selection, proceed to protocol selection
        var canTransition = targetState == WorkflowState.ProtocolSelect;

        _logger.LogDebug(
            "PatientSelect -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
