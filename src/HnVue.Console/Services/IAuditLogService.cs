using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Audit log service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-13 Audit Log Viewer.
/// SPEC-SECURITY-001: FDA 21 CFR Part 11, IEC 62304 compliant audit logging.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Logs an audit event with SHA-256 signature for integrity.
    /// </summary>
    /// <param name="eventType">Type of audit event.</param>
    /// <param name="userId">User who performed the action.</param>
    /// <param name="userName">Display name of the user.</param>
    /// <param name="eventDescription">Human-readable description of the event.</param>
    /// <param name="outcome">Outcome of the event (success/failure/warning).</param>
    /// <param name="patientId">Optional patient ID if event involves patient data.</param>
    /// <param name="studyId">Optional study ID if event involves study data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created audit log entry ID.</returns>
    Task<string> LogAsync(
        AuditEventType eventType,
        string userId,
        string userName,
        string eventDescription,
        AuditOutcome outcome,
        string? patientId = null,
        string? studyId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets audit log entries with filtering.
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(AuditLogFilter filter, CancellationToken ct);

    /// <summary>
    /// Gets paged audit log entries.
    /// </summary>
    Task<PagedAuditLogResult> GetLogsPagedAsync(int pageNumber, int pageSize, AuditLogFilter? filter, CancellationToken ct);

    /// <summary>
    /// Gets a specific audit log entry.
    /// </summary>
    Task<AuditLogEntry?> GetLogEntryAsync(string entryId, CancellationToken ct);

    /// <summary>
    /// Gets audit log entry count for a filter.
    /// </summary>
    Task<int> GetLogCountAsync(AuditLogFilter filter, CancellationToken ct);

    /// <summary>
    /// Exports audit log entries to a file.
    /// </summary>
    Task<byte[]> ExportLogsAsync(AuditLogFilter filter, CancellationToken ct);

    /// <summary>
    /// Verifies the integrity of the audit log chain.
    /// </summary>
    /// <returns>True if all hashes are valid, false if tampering detected.</returns>
    Task<AuditVerificationResult> VerifyIntegrityAsync(CancellationToken ct = default);

    /// <summary>
    /// Enforces retention policy by deleting logs older than retention period.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of deleted entries.</returns>
    Task<int> EnforceRetentionPolicyAsync(CancellationToken ct = default);
}

/// <summary>
/// Result of audit trail integrity verification.
/// </summary>
public sealed record AuditVerificationResult
{
    /// <summary>
    /// Gets whether the audit trail integrity is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the first entry ID where integrity check failed, if any.
    /// </summary>
    public string? BrokenAtEntryId { get; init; }

    /// <summary>
    /// Gets the verification message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the total number of entries verified.
    /// </summary>
    public int EntriesVerified { get; init; }
}
