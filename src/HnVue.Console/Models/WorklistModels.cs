namespace HnVue.Console.Models;

/// <summary>
/// Worklist item (MWL procedure).
/// SPEC-UI-001: FR-UI-02 Worklist Display.
/// </summary>
public record WorklistItem
{
    public required string ProcedureId { get; init; }
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public required string AccessionNumber { get; init; }
    public required string ScheduledProcedureStepDescription { get; init; }
    public required DateTimeOffset ScheduledDateTime { get; init; }
    public required string BodyPart { get; init; }
    public required string Projection { get; init; }
    public WorklistStatus Status { get; init; } = WorklistStatus.Scheduled;
}

/// <summary>
/// Worklist status enumeration.
/// </summary>
public enum WorklistStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>
/// Worklist refresh request.
/// </summary>
public record WorklistRefreshRequest
{
    public DateTimeOffset? Since { get; init; }
}

/// <summary>
/// Worklist refresh result.
/// </summary>
public record WorklistRefreshResult
{
    public required IReadOnlyList<WorklistItem> Items { get; init; }
    public required DateTimeOffset RefreshedAt { get; init; }
}
