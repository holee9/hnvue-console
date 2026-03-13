using HnVue.Console.Security.Models;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.Security;

/// <summary>
/// WORM (Write Once, Read Many) storage provider for audit logs.
/// SPEC-SECURITY-001: FR-SEC-06 - Audit Log Integrity with tamper-proof storage.
/// </summary>
public interface IWormStorageProvider
{
    /// <summary>
    /// Writes an audit log entry to WORM storage.
    /// Once written, the entry cannot be modified or deleted (WORM semantics).
    /// </summary>
    /// <param name="entry">The audit entry to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the entry already exists (overwrite protection).
    /// </exception>
    Task WriteEntryAsync(WormEntry entry, CancellationToken ct);

    /// <summary>
    /// Reads an audit log entry from WORM storage by entry ID.
    /// </summary>
    /// <param name="entryId">The unique entry identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The audit entry if found; otherwise, null.</returns>
    Task<WormEntry?> ReadEntryAsync(string entryId, CancellationToken ct);

    /// <summary>
    /// Queries audit log entries with optional filtering.
    /// </summary>
    /// <param name="filter">Filter criteria for the query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of matching audit entries.</returns>
    Task<IReadOnlyList<WormEntry>> QueryEntriesAsync(
        AuditLogFilter filter,
        CancellationToken ct);

    /// <summary>
    /// Verifies the integrity of the audit log hash chain.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Verification result indicating whether the hash chain is intact.
    /// </returns>
    Task<AuditVerificationResult> VerifyIntegrityAsync(CancellationToken ct);

    /// <summary>
    /// Enforces retention policy by removing entries older than the retention period.
    /// This is the ONLY allowed deletion operation in WORM storage.
    /// SPEC-SECURITY-001: 6-year retention policy for medical device compliance.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of entries removed.</returns>
    Task<int> EnforceRetentionPolicyAsync(CancellationToken ct);
}
