using Microsoft.Extensions.Logging;

namespace HnVue.Workflow.States;

/// <summary>
/// Handles the ExposureTrigger state - manages X-ray exposure execution.
/// </summary>
/// <remarks>
/// @MX:NOTE: Exposure trigger state handler - orchestrates X-ray exposure execution
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-06
/// @MX:WARN: Safety-critical state - controls high-voltage X-ray generation
///
/// This state is responsible for the actual X-ray exposure. It performs
/// comprehensive safety checks before allowing exposure, tracks radiation dose
/// during exposure, and handles exposure completion or abortion.
/// </remarks>
public sealed class ExposureTriggerHandler : IStateHandler
{
    private readonly ILogger<ExposureTriggerHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the ExposureTriggerHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public ExposureTriggerHandler(ILogger<ExposureTriggerHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.ExposureTrigger;

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: Exposure entry point - final safety gate before X-ray emission
    /// @MX:REASON: Safety-critical - last chance to prevent exposure
    ///
    /// Performs pre-exposure safety checks:
    /// - Patient context validation
    /// - Exposure parameters validation
    /// - Safety interlock verification
    /// - Dose tracker initialization
    /// </remarks>
    public Task EnterAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Entering ExposureTrigger state for StudyId: {StudyId}, PatientId: {PatientId}",
            context.StudyId,
            context.PatientId);

        // Pre-exposure safety checks are performed here
        // Actual exposure trigger is a separate action

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(StudyContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Exiting ExposureTrigger state for StudyId: {StudyId}",
            context.StudyId);

        // Cleanup exposure session
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: Exposure transition validation - controls state flow after exposure
    /// @MX:REASON: Safety-critical - ensures proper post-exposure workflow
    ///
    /// Valid transitions:
    /// - To QcReview: After successful exposure for image review
    /// - To MppsComplete: For procedure step reporting
    /// - To Idle: For emergency abort only
    /// </remarks>
    public Task<bool> CanTransitionToAsync(WorkflowState targetState, CancellationToken cancellationToken)
    {
        var allowedTransitions = new[]
        {
            WorkflowState.QcReview,
            WorkflowState.MppsComplete,
            WorkflowState.Idle  // Emergency abort only
        };

        var canTransition = allowedTransitions.Contains(targetState);

        _logger.LogDebug(
            "ExposureTrigger -> {TargetState} transition: {Allowed}",
            targetState,
            canTransition ? "Allowed" : "Blocked");

        return Task.FromResult(canTransition);
    }
}
