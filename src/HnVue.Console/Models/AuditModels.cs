namespace HnVue.Console.Models;

/// <summary>
/// Audit log entry.
/// SPEC-UI-001: FR-UI-13 Audit Log Viewer.
/// SPEC-SECURITY-001: R2 AuditLogService - SHA-256 integrity, 6-year retention, PHI masking.
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

    /// <summary>
    /// SHA-256 hash of this entry for integrity verification.
    /// SPEC-SECURITY-001: FR-SEC-06 - Each log entry has SHA-256 signature.
    /// </summary>
    public string? EntryHash { get; init; }

    /// <summary>
    /// SHA-256 hash of the previous entry for chain integrity.
    /// SPEC-SECURITY-001: FR-SEC-06 - Log entries form a hash chain.
    /// </summary>
    public string? PreviousEntryHash { get; init; }

    /// <summary>
    /// Workstation ID where the event originated.
    /// SPEC-SECURITY-001: FR-SEC-08 - Accurate timestamp with workstation context.
    /// </summary>
    public string? WorkstationId { get; init; }
}

/// <summary>
/// Audit event type enumeration.
/// SPEC-SECURITY-001: FR-SEC-09 - Minimum audit event types for medical device compliance.
/// </summary>
public enum AuditEventType
{
    // User authentication events (SPEC-SECURITY-001)
    UserLogin,
    UserLogout,

    // Access control events (SPEC-SECURITY-001)
    AccessDenied,

    // User management events (SPEC-SECURITY-001)
    PasswordChange,

    // Configuration events (SPEC-SECURITY-001)
    ConfigChange,

    // Data events (SPEC-SECURITY-001)
    DataExport,

    // Clinical events
    PatientRegistration,
    PatientEdit,
    StudyStart,
    ExposureInitiated,
    ExposureCompleted,

    // Image events
    ImageAccepted,
    ImageRejected,
    ImageReprocessed,

    // System events
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
