namespace HnVue.Console.Models;

/// <summary>
/// Audit log entry.
/// SPEC-UI-001: FR-UI-13 Audit Log Viewer.
/// </summary>
public record AuditLogEntry
{
    public required string EntryId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required AuditEventType EventType { get; init; }
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string EventDescription { get; init; }
    public required string? PatientId { get; init; }
    public required string? StudyId { get; init; }
    public required AuditOutcome Outcome { get; init; }
}

/// <summary>
/// Audit event type enumeration.
/// </summary>
public enum AuditEventType
{
    PatientRegistration,
    PatientEdit,
    StudyStart,
    ExposureInitiated,
    ImageAccepted,
    ImageRejected,
    ImageReprocessed,
    ConfigChange,
    UserLogin,
    UserLogout,
    SystemError,
    DoseAlertExceeded
}

/// <summary>
/// Audit outcome enumeration.
/// </summary>
public enum AuditOutcome
{
    Success,
    Failure,
    Warning
}

/// <summary>
/// Audit log filter.
/// </summary>
public record AuditLogFilter
{
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public AuditEventType? EventType { get; init; }
    public string? UserId { get; init; }
    public string? PatientId { get; init; }
    public AuditOutcome? Outcome { get; init; }
}

/// <summary>
/// Paged audit log result.
/// </summary>
public record PagedAuditLogResult
{
    public required IReadOnlyList<AuditLogEntry> Entries { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required bool HasMorePages { get; init; }
}
