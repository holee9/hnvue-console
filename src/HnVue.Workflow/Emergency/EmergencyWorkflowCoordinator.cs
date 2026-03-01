using Microsoft.Extensions.Logging;
using HnVue.Workflow.StateMachine;
using HnVue.Workflow.States;
using WorkflowState = HnVue.Workflow.StateMachine.WorkflowState;
using StudyContext = HnVue.Workflow.States.StudyContext;
using GuardEvaluationContext = HnVue.Workflow.StateMachine.GuardEvaluationContext;
using TransitionResult = HnVue.Workflow.StateMachine.TransitionResult;

namespace HnVue.Workflow.Emergency;

/// <summary>
/// Coordinator for emergency workflow bypass operations.
/// SPEC-WORKFLOW-001: FR-WF-07 Emergency workflow bypass.
///
/// Provides a direct path from IDLE to PATIENT_SELECT state for emergency cases
/// where worklist sync is skipped and patient data is entered manually.
/// </summary>
public class EmergencyWorkflowCoordinator
{
    private readonly ILogger<EmergencyWorkflowCoordinator> _logger;
    private readonly IWorkflowStateMachineFactory _stateMachineFactory;

    /// <summary>
    /// Trigger name for emergency workflow requests.
    /// </summary>
    public const string EmergencyTrigger = "EmergencyWorkflowRequested";

    /// <summary>
    /// Metadata key for emergency workflow flag.
    /// </summary>
    public const string EmergencyMetadataKey = "IsEmergency";

    /// <summary>
    /// Metadata key for minimal patient data flag.
    /// </summary>
    public const string MinimalPatientDataKey = "HasMinimalData";

    /// <summary>
    /// Initializes a new instance of the EmergencyWorkflowCoordinator class.
    /// </summary>
    public EmergencyWorkflowCoordinator(
        ILogger<EmergencyWorkflowCoordinator> logger,
        IWorkflowStateMachineFactory stateMachineFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateMachineFactory = stateMachineFactory ?? throw new ArgumentNullException(nameof(stateMachineFactory));
    }

    /// <summary>
    /// Initiates an emergency workflow bypass from IDLE to PATIENT_SELECT.
    /// </summary>
    /// <param name="patientId">The patient identifier (may be generated or manually entered).</param>
    /// <param name="patientName">The patient name (may be minimal for emergency).</param>
    /// <param name="operatorId">The operator initiating the emergency workflow.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the emergency workflow initiation.</returns>
    public async Task<EmergencyWorkflowResult> InitiateEmergencyWorkflowAsync(
        string patientId,
        string patientName,
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Emergency workflow initiated by {OperatorId} for patient: {PatientId} ({PatientName})",
            operatorId,
            patientId,
            patientName);

        // Generate emergency study ID
        var studyId = GenerateEmergencyStudyId();

        // Create guard context with emergency flag
        var guardContext = new GuardEvaluationContext
        {
            IsEmergencyWorkflow = true,
            HardwareInterlockOk = true // Will be evaluated by guards
        };
        guardContext.Metadata[EmergencyMetadataKey] = true;
        guardContext.Metadata[MinimalPatientDataKey] = true;

        // Get state machine
        var stateMachine = _stateMachineFactory.GetStateMachine();

        // Verify current state is IDLE
        var currentState = stateMachine.CurrentState;
        if (currentState != WorkflowState.Idle)
        {
            _logger.LogWarning(
                "Cannot initiate emergency workflow from {CurrentState} state (must be IDLE)",
                currentState);

            return EmergencyWorkflowResult.Failed(
                EmergencyWorkflowFailureReason.NotInIdleState,
                $"Current state is {currentState}, must be IDLE");
        }

        // Attempt transition to PATIENT_SELECT
        var transitionResult = await stateMachine.TryTransitionAsync(
            WorkflowState.PatientSelect,
            EmergencyTrigger,
            operatorId,
            guardContext,
            cancellationToken);

        if (!transitionResult.IsSuccess)
        {
            var failureReason = transitionResult.ErrorType switch
            {
                TransitionErrorType.GuardFailed => EmergencyWorkflowFailureReason.GuardNotSatisfied,
                TransitionErrorType.Exception => EmergencyWorkflowFailureReason.TransitionException,
                _ => EmergencyWorkflowFailureReason.InvalidTransition
            };

            _logger.LogError(
                "Emergency workflow transition failed: {FailureReason} - {ErrorDetails}",
                failureReason,
                transitionResult.ErrorDetails?.Message ?? "Unknown error");

            return EmergencyWorkflowResult.Failed(failureReason, transitionResult.ErrorDetails?.Message ?? "Transition failed");
        }

        // Create emergency study context
        var studyContext = StudyContext.CreateEmergency(
            studyId,
            patientId,
            patientName,
            new Dictionary<string, object?>
            {
                { EmergencyMetadataKey, true },
                { MinimalPatientDataKey, true },
                { "EmergencyStartTime", DateTime.UtcNow }
            });

        _logger.LogInformation(
            "Emergency workflow successfully initiated: StudyId={StudyId}, PatientId={PatientId}",
            studyId,
            patientId);

        return EmergencyWorkflowResult.Success(studyId, patientId, studyContext);
    }

    /// <summary>
    /// Validates if emergency data is sufficient for workflow continuation.
    /// </summary>
    /// <param name="patientId">The patient identifier.</param>
    /// <param name="patientName">The patient name.</param>
    /// <returns>Validation result with any missing required fields.</returns>
    public EmergencyDataValidationResult ValidateEmergencyData(
        string patientId,
        string patientName)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(patientId))
        {
            missingFields.Add("PatientId");
        }

        // Patient name is optional for emergency, but recommended
        // We'll allow empty names but warn about it
        var hasName = !string.IsNullOrWhiteSpace(patientName);

        return new EmergencyDataValidationResult
        {
            IsValid = missingFields.Count == 0,
            MissingRequiredFields = missingFields.ToArray(),
            HasMinimalData = true, // Emergency workflow always has minimal data
            HasPatientName = hasName
        };
    }

    /// <summary>
    /// Generates a unique study ID for emergency workflows.
    /// Emergency studies use a different prefix to distinguish from scheduled studies.
    /// </summary>
    private string GenerateEmergencyStudyId()
    {
        // Format: EMER-YYYYMMDD-HHMMSS-RRRR (random 4 digits)
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var random = new Random().Next(1000, 10000).ToString("D4");
        return $"EMER-{timestamp}-{random}";
    }
}

/// <summary>
/// Factory interface for obtaining the workflow state machine.
/// </summary>
public interface IWorkflowStateMachineFactory
{
    /// <summary>
    /// Gets the current workflow state machine instance.
    /// </summary>
    IWorkflowStateMachine GetStateMachine();
}

/// <summary>
/// Result of an emergency workflow initiation attempt.
/// </summary>
public record EmergencyWorkflowResult
{
    public bool IsSuccess { get; init; }
    public string? StudyId { get; init; }
    public string? PatientId { get; init; }
    public StudyContext? StudyContext { get; init; }
    public EmergencyWorkflowFailureReason? FailureReason { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful emergency workflow result.
    /// </summary>
    public static EmergencyWorkflowResult Success(
        string studyId,
        string patientId,
        StudyContext studyContext)
    {
        return new EmergencyWorkflowResult
        {
            IsSuccess = true,
            StudyId = studyId,
            PatientId = patientId,
            StudyContext = studyContext
        };
    }

    /// <summary>
    /// Creates a failed emergency workflow result.
    /// </summary>
    public static EmergencyWorkflowResult Failed(
        EmergencyWorkflowFailureReason reason,
        string errorMessage)
    {
        return new EmergencyWorkflowResult
        {
            IsSuccess = false,
            FailureReason = reason,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Reasons for emergency workflow failure.
/// </summary>
public enum EmergencyWorkflowFailureReason
{
    /// <summary>
    /// System is not in IDLE state when emergency is requested.
    /// </summary>
    NotInIdleState,

    /// <summary>
    /// Hardware interlocks are not satisfied.
    /// </summary>
    GuardNotSatisfied,

    /// <summary>
    /// Transition to PATIENT_SELECT failed due to an exception.
    /// </summary>
    TransitionException,

    /// <summary>
    /// The transition is not valid from the current state.
    /// </summary>
    InvalidTransition
}

/// <summary>
/// Validation result for emergency patient data.
/// </summary>
public record EmergencyDataValidationResult
{
    public bool IsValid { get; init; }
    public string[] MissingRequiredFields { get; init; } = Array.Empty<string>();
    public bool HasMinimalData { get; init; }
    public bool HasPatientName { get; init; }
}
