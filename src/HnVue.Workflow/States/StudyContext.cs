namespace HnVue.Workflow.States;

using HnVue.Workflow.Study;

/// <summary>
/// Context object containing data shared across workflow states.
/// </summary>
/// <remarks>
/// @MX:NOTE: Study context - immutable data carrier for state transitions
/// This record holds all workflow-relevant study information.
/// Use with-careful consideration of thread safety when accessing from multiple states.
/// </remarks>
public record StudyContext
{
    /// <summary>
    /// Gets the unique study identifier.
    /// </summary>
    public required string StudyId { get; init; }

    /// <summary>
    /// Gets the patient identifier.
    /// </summary>
    public required string PatientId { get; init; }

    /// <summary>
    /// Gets the current workflow state.
    /// </summary>
    public required WorkflowState CurrentState { get; init; }

    /// <summary>
    /// Gets optional additional context data.
    /// </summary>
    public IDictionary<string, object?>? Metadata { get; init; }

    /// <summary>
    /// Gets whether this is an emergency workflow (unscheduled).
    /// SPEC-WORKFLOW-001: FR-WF-07 Emergency workflow bypass
    /// </summary>
    public bool IsEmergency { get; init; }

    /// <summary>
    /// Gets the optional patient name for emergency workflows.
    /// May be minimal or incomplete for emergency cases.
    /// </summary>
    public string? PatientName { get; init; }

    /// <summary>
    /// Gets optional patient information for integration tests.
    /// This property is used by HAL simulators and integration tests.
    /// </summary>
    public Study.PatientInfo? PatientInfo { get; init; }

    /// <summary>
    /// Creates a new StudyContext with the specified values.
    /// </summary>
    public static StudyContext Create(
        string studyId,
        string patientId,
        WorkflowState currentState,
        bool isEmergency = false,
        string? patientName = null,
        IDictionary<string, object?>? metadata = null)
    {
        return new StudyContext
        {
            StudyId = studyId,
            PatientId = patientId,
            CurrentState = currentState,
            IsEmergency = isEmergency,
            PatientName = patientName,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Creates an emergency workflow context with minimal patient data.
    /// SPEC-WORKFLOW-001: FR-WF-07 Emergency workflow bypass
    /// </summary>
    public static StudyContext CreateEmergency(
        string studyId,
        string patientId,
        string patientName,
        IDictionary<string, object?>? metadata = null)
    {
        return new StudyContext
        {
            StudyId = studyId,
            PatientId = patientId,
            CurrentState = WorkflowState.PatientSelect,
            IsEmergency = true,
            PatientName = patientName,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Updates the context with a new state.
    /// </summary>
    public StudyContext WithState(WorkflowState newState)
    {
        return this with { CurrentState = newState };
    }

    /// <summary>
    /// Updates the context with additional metadata.
    /// </summary>
    public StudyContext WithMetadata(string key, object? value)
    {
        var updatedMetadata = new Dictionary<string, object?>(Metadata ?? new Dictionary<string, object?>());
        updatedMetadata[key] = value;
        return this with { Metadata = updatedMetadata };
    }
}
