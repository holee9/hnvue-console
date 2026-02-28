namespace HnVue.Workflow.Journal;

using System;
using System.Collections.Generic;
using HnVue.Workflow.StateMachine;

/// <summary>
/// Journal entry for workflow state transitions.
///
/// SPEC-WORKFLOW-001 NFR-WF-01-b: Journal record format
/// </summary>
public class WorkflowJournalEntry
{
    /// <summary>
    /// Unique ID for this journal entry.
    /// </summary>
    public Guid TransitionId { get; init; }

    /// <summary>
    /// Millisecond-precision UTC timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Source state.
    /// </summary>
    public WorkflowState FromState { get; init; }

    /// <summary>
    /// Target state.
    /// </summary>
    public WorkflowState ToState { get; init; }

    /// <summary>
    /// Event/trigger name.
    /// </summary>
    public string Trigger { get; init; } = string.Empty;

    /// <summary>
    /// Array of guard evaluation results.
    /// </summary>
    public GuardResult[] GuardResults { get; init; } = Array.Empty<GuardResult>();

    /// <summary>
    /// Authenticated operator identifier.
    /// </summary>
    public string OperatorId { get; init; } = string.Empty;

    /// <summary>
    /// Active study UID at time of transition (if any).
    /// </summary>
    public string? StudyInstanceUID { get; init; }

    /// <summary>
    /// Extensible key-value metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Log category (WORKFLOW / SAFETY / HARDWARE / SYSTEM).
    /// </summary>
    public LogCategory Category { get; init; } = LogCategory.WORKFLOW;
}

/// <summary>
/// Result of a single guard evaluation.
/// </summary>
public record GuardResult
{
    public string GuardName { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Log category for journal entries.
/// SPEC-WORKFLOW-001 Safety-04: SAFETY category for regulatory traceability
/// </summary>
public enum LogCategory
{
    /// <summary>
    /// Normal workflow state transitions.
    /// </summary>
    WORKFLOW,

    /// <summary>
    /// Safety-critical events (interlock checks, parameter validations).
    /// </summary>
    SAFETY,

    /// <summary>
    /// Hardware-related events.
    /// </summary>
    HARDWARE,

    /// <summary>
    /// System-level events (startup, shutdown, errors).
    /// </summary>
    SYSTEM
}
