namespace HnVue.Workflow.States;

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
    public IDictionary<string, object>? Metadata { get; init; }
}
