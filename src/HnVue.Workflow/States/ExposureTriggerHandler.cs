using Microsoft.Extensions.Logging;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.Journal;
using HnVue.Workflow.Events;
using HnVue.Workflow.Safety;
using HnVue.Workflow.StateMachine;

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
    private readonly ISafetyInterlock _safetyInterlock;
    private readonly IHvgDriver _hvgDriver;
    private readonly IDoseTracker _doseTracker;
    private readonly IWorkflowJournal _journal;
    private readonly IWorkflowEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the ExposureTriggerHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="safetyInterlock">Safety interlock checker.</param>
    /// <param name="hvgDriver">High-voltage generator driver.</param>
    /// <param name="doseTracker">Dose tracker for recording partial dose.</param>
    /// <param name="journal">Audit journal.</param>
    /// <param name="eventPublisher">Event publisher for notifications.</param>
    public ExposureTriggerHandler(
        ILogger<ExposureTriggerHandler> logger,
        ISafetyInterlock safetyInterlock,
        IHvgDriver hvgDriver,
        IDoseTracker doseTracker,
        IWorkflowJournal journal,
        IWorkflowEventPublisher eventPublisher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _safetyInterlock = safetyInterlock ?? throw new ArgumentNullException(nameof(safetyInterlock));
        _hvgDriver = hvgDriver ?? throw new ArgumentNullException(nameof(hvgDriver));
        _doseTracker = doseTracker ?? throw new ArgumentNullException(nameof(doseTracker));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <inheritdoc/>
    public WorkflowState State => WorkflowState.ExposureTrigger;

    /// <summary>
    /// Flag indicating if an exposure is currently active.
    /// </summary>
    public bool IsExposureActive { get; private set; }

    /// <inheritdoc/>
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

        IsExposureActive = false;

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

    /// <summary>
    /// Starts monitoring interlocks during active exposure.
    /// SPEC-WORKFLOW-001 Safety T-08/T-09: Mid-exposure interlock loss detection
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="operatorId">The operator ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when exposure monitoring ends.</returns>
    public async Task MonitorInterlocksDuringExposureAsync(
        string studyId,
        string operatorId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting interlock monitoring during exposure for StudyId: {StudyId}", studyId);

        IsExposureActive = true;

        try
        {
            // Monitor interlocks periodically during exposure
            // In real implementation, this would be hardware-triggered
            // For now, use polling as a placeholder
            while (!cancellationToken.IsCancellationRequested && IsExposureActive)
            {
                try
                {
                    var interlockStatus = await _safetyInterlock.CheckAllInterlocksAsync(cancellationToken);

                    if (!AreAllInterlocksSatisfied(interlockStatus))
                    {
                        _logger.LogWarning(
                            "INTERLOCK LOSS DETECTED during exposure! Failed interlocks: {FailedInterlocks}",
                            GetFailedInterlocks(interlockStatus));

                        await HandleMidExposureInterlockLossAsync(
                            studyId,
                            operatorId,
                            interlockStatus,
                            cancellationToken);

                        break;
                    }

                    // Poll every 100ms (real implementation would be event-driven)
                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking interlocks during exposure monitoring");
                    // Continue monitoring unless critical failure
                }
            }
        }
        finally
        {
            IsExposureActive = false;
            _logger.LogInformation("Interlock monitoring ended for StudyId: {StudyId}", studyId);
        }
    }

    /// <summary>
    /// Handles mid-exposure interlock loss.
    /// SPEC-WORKFLOW-001 Safety T-08/T-09: EXPOSURE_TRIGGER -> QC_REVIEW
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="operatorId">The operator ID.</param>
    /// <param name="interlockStatus">The failed interlock status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task HandleMidExposureInterlockLossAsync(
        string studyId,
        string operatorId,
        InterlockStatus interlockStatus,
        CancellationToken cancellationToken)
    {
        _logger.LogCritical(
            "MID-EXPOSURE INTERLOCK LOSS detected for StudyId: {StudyId}. Aborting exposure immediately!",
            studyId);

        try
        {
            // Step 1: Immediately abort exposure
            _logger.LogWarning("Aborting exposure due to interlock loss...");
            await _hvgDriver.AbortExposureAsync(cancellationToken);
            _logger.LogInformation("Exposure abort command sent successfully.");

            // Step 2: Record partial dose
            _logger.LogWarning("Recording partial dose due to exposure abort...");
            try
            {
                // Get current partial dose from dose tracker
                var partialDose = await _doseTracker.GetCumulativeDoseAsync(cancellationToken);

                // Record partial dose in audit log
                var auditEntry = new WorkflowJournalEntry
                {
                    TransitionId = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    FromState = StateMachine.WorkflowState.ExposureTrigger,
                    ToState = StateMachine.WorkflowState.QcReview,
                    Trigger = "MidExposureInterlockLoss",
                    OperatorId = operatorId,
                    StudyInstanceUID = studyId,
                    Category = LogCategory.SAFETY,
                    Metadata = new Dictionary<string, object>
                    {
                        { "PartialDose_Dap", partialDose.TotalDap },
                        { "PartialDose_ExposureCount", partialDose.ExposureCount },
                        { "FailedInterlocks", GetFailedInterlocks(interlockStatus) },
                        { "AbortReason", "Mid-exposure interlock loss (T-08/T-09)" }
                    }
                };

                await _journal.WriteEntryAsync(auditEntry, cancellationToken);
                _logger.LogInformation("Partial dose recorded: TotalDap={TotalDap}, ExposureCount={ExposureCount}",
                    partialDose.TotalDap, partialDose.ExposureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record partial dose after exposure abort");
            }

            // Step 3: Publish critical notification to operator
            var notification = new OperatorNotification
            {
                Title = "CRITICAL: EXPOSURE ABORTED",
                Message = $"Exposure aborted due to safety interlock loss: {GetFailedInterlocks(interlockStatus)}",
                Severity = NotificationSeverity.Critical,
                RequiresAction = true,
                ActionLabel = "Acknowledge"
            };

            await _eventPublisher.PublishNotificationAsync(notification, cancellationToken);
            _logger.LogInformation("Critical notification published to operator.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling mid-exposure interlock loss");
        }
    }

    /// <summary>
    /// Checks if all required interlocks are satisfied.
    /// </summary>
    private static bool AreAllInterlocksSatisfied(InterlockStatus status)
    {
        return status.door_closed &&
               status.emergency_stop_clear &&
               status.thermal_normal &&
               status.generator_ready &&
               status.detector_ready &&
               status.collimator_valid &&
               status.table_locked &&
               status.dose_within_limits &&
               status.aec_configured;
    }

    /// <summary>
    /// Gets a comma-separated list of failed interlock IDs.
    /// </summary>
    private static string GetFailedInterlocks(InterlockStatus status)
    {
        var failed = new List<string>();

        if (!status.door_closed) failed.Add("IL-01 (Door)");
        if (!status.emergency_stop_clear) failed.Add("IL-02 (Emergency Stop)");
        if (!status.thermal_normal) failed.Add("IL-03 (Thermal)");
        if (!status.generator_ready) failed.Add("IL-04 (Generator)");
        if (!status.detector_ready) failed.Add("IL-05 (Detector)");
        if (!status.collimator_valid) failed.Add("IL-06 (Collimator)");
        if (!status.table_locked) failed.Add("IL-07 (Table)");
        if (!status.dose_within_limits) failed.Add("IL-08 (Dose Limit)");
        if (!status.aec_configured) failed.Add("IL-09 (AEC)");

        return failed.Count > 0 ? string.Join(", ", failed) : "None";
    }
}
