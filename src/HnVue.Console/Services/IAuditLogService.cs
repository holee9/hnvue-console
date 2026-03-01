using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Audit log service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-13 Audit Log Viewer.
/// </summary>
public interface IAuditLogService
{
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
}
