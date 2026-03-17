using HnVue.Console.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace HnVue.Console.Services;

/// <summary>
/// Mock audit log service for development.
/// SPEC-SECURITY-001: R2 AuditLogService - SHA-256 integrity, 6-year retention, PHI masking.
/// Implements:
/// - FR-SEC-06: SHA-256 hash chain for integrity
/// - FR-SEC-07: 6-year retention policy
/// - FR-SEC-08: NTP-synchronized timestamps (UTC)
/// - FR-SEC-09: Required audit event types
/// - FR-SEC-10: PHI masking for Patient ID and Name
/// </summary>
public class MockAuditLogService : IAuditLogService
{
    private readonly ConcurrentDictionary<string, AuditLogEntry> _entries = new();
    private readonly ConcurrentQueue<string> _entryOrder = new();
    private string? _lastEntryHash;

    // SPEC-SECURITY-001: FR-SEC-07 - 6-year retention policy
    private const int RetentionYears = 6;

    public MockAuditLogService()
    {
        // Initialize with some sample entries for testing
        _ = InitializeSampleEntriesAsync();
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        var filtered = ApplyFilter(_entries.Values.OrderBy(e => GetEntryOrder(e.EntryId)), filter);
        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(filtered.ToList());
    }

    public Task<PagedAuditLogResult> GetLogsPagedAsync(int pageNumber, int pageSize, AuditLogFilter? filter, CancellationToken ct)
    {
        var filtered = ApplyFilter(_entries.Values.OrderBy(e => GetEntryOrder(e.EntryId)), filter ?? new AuditLogFilter());
        var totalCount = filtered.Count();
        var skip = (pageNumber - 1) * pageSize;
        var entries = filtered.Skip(skip).Take(pageSize).ToList();

        var result = new PagedAuditLogResult
        {
            Entries = entries,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            HasMorePages = skip + entries.Count < totalCount
        };

        return Task.FromResult(result);
    }

    public Task<AuditLogEntry?> GetLogEntryAsync(string entryId, CancellationToken ct)
    {
        _entries.TryGetValue(entryId, out var entry);
        return Task.FromResult(entry);
    }

    public Task<int> GetLogCountAsync(AuditLogFilter filter, CancellationToken ct)
    {
        var filtered = ApplyFilter(_entries.Values, filter);
        return Task.FromResult(filtered.Count());
    }

    public Task<byte[]> ExportLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        var filtered = ApplyFilter(_entries.Values, filter);
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,EventType,UserId,UserName,Description,PatientId,StudyId,Outcome,EntryHash,PreviousHash");

        foreach (var entry in filtered)
        {
            csv.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}Z," +
                          $"{entry.EventType}," +
                          $"{entry.UserId}," +
                          $"{MaskUserName(entry.UserName)}," +
                          $"{entry.EventDescription}," +
                          $"{MaskPatientId(entry.PatientId)}," +
                          $"{entry.StudyId ?? ""}," +
                          $"{entry.Outcome}," +
                          $"{entry.EntryHash}," +
                          $"{entry.PreviousEntryHash ?? ""}");
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    /// <summary>
    /// SPEC-SECURITY-001: FR-SEC-06 - Creates audit log entry with SHA-256 hash chain.
    /// Each entry contains hash of itself and reference to previous entry's hash.
    /// </summary>
    public Task<string> LogAsync(
        AuditEventType eventType,
        string userId,
        string userName,
        string eventDescription,
        AuditOutcome outcome,
        string? patientId = null,
        string? studyId = null,
        CancellationToken ct = default)
    {
        var entryId = $"LOG-{Guid.NewGuid():N}".Substring(0, 12);
        var timestamp = DateTimeOffset.UtcNow;

        // Create entry content for hashing (spec-compliant order)
        var entryContent = $"{entryId}|{timestamp:O}|{eventType}|{userId}|{userName}|{eventDescription}|{outcome}|{patientId}|{studyId}";
        var entryHash = ComputeSha256Hash(entryContent);

        var entry = new AuditLogEntry
        {
            EntryId = entryId,
            Timestamp = timestamp,
            EventType = eventType,
            UserId = userId,
            UserName = userName,
            EventDescription = eventDescription,
            PatientId = patientId,
            StudyId = studyId,
            Outcome = outcome,
            EntryHash = entryHash,
            PreviousEntryHash = _lastEntryHash,
            WorkstationId = Environment.MachineName
        };

        _entries[entryId] = entry;
        _entryOrder.Enqueue(entryId);
        _lastEntryHash = entryHash;

        return Task.FromResult(entryId);
    }

    /// <summary>
    /// SPEC-SECURITY-001: FR-SEC-06 - Verifies integrity of audit log hash chain.
    /// Validates that each entry's hash matches its content and chain is unbroken.
    /// </summary>
    public Task<AuditVerificationResult> VerifyIntegrityAsync(CancellationToken ct = default)
    {
        var entries = _entries.Values.OrderBy(e => GetEntryOrder(e.EntryId)).ToList();
        var previousHash = (string?)null;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            // Verify entry hash matches content
            var entryContent = $"{entry.EntryId}|{entry.Timestamp:O}|{entry.EventType}|{entry.UserId}|{entry.UserName}|{entry.EventDescription}|{entry.Outcome}|{entry.PatientId}|{entry.StudyId}";
            var computedHash = ComputeSha256Hash(entryContent);

            if (computedHash != entry.EntryHash)
            {
                return Task.FromResult(new AuditVerificationResult
                {
                    IsValid = false,
                    BrokenAtEntryId = entry.EntryId,
                    Message = $"Hash mismatch at entry {entry.EntryId}: expected {computedHash}, got {entry.EntryHash}",
                    EntriesVerified = i
                });
            }

            // Verify chain integrity
            if (entry.PreviousEntryHash != previousHash)
            {
                return Task.FromResult(new AuditVerificationResult
                {
                    IsValid = false,
                    BrokenAtEntryId = entry.EntryId,
                    Message = $"Chain broken at entry {entry.EntryId}: expected previous hash {previousHash}, got {entry.PreviousEntryHash}",
                    EntriesVerified = i
                });
            }

            previousHash = entry.EntryHash;
        }

        return Task.FromResult(new AuditVerificationResult
        {
            IsValid = true,
            Message = $"Audit trail integrity verified: {entries.Count} entries",
            EntriesVerified = entries.Count
        });
    }

    /// <summary>
    /// SPEC-SECURITY-001: FR-SEC-07 - Enforces 6-year retention policy.
    /// Removes audit log entries older than 6 years.
    /// </summary>
    public Task<int> EnforceRetentionPolicyAsync(CancellationToken ct = default)
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddYears(-RetentionYears);
        var entriesToRemove = _entries.Values.Where(e => e.Timestamp < cutoffDate).ToList();

        foreach (var entry in entriesToRemove)
        {
            _entries.TryRemove(entry.EntryId, out _);
        }

        var deletedCount = entriesToRemove.Count;
        return Task.FromResult(deletedCount);
    }

    /// <summary>
    /// SPEC-SECURITY-001: FR-SEC-10 - Masks Patient ID for privacy.
    /// Shows format and last 2 characters for correlation.
    /// </summary>
    private static string MaskPatientId(string? patientId)
    {
        if (string.IsNullOrEmpty(patientId) || patientId.Length <= 4)
            return "****";

        return $"{patientId.Substring(0, 2)}{new string('*', patientId.Length - 4)}{patientId.Substring(patientId.Length - 2)}";
    }

    /// <summary>
    /// SPEC-SECURITY-001: FR-SEC-10 - Masks User Name for privacy in exports.
    /// Shows first initial and last name.
    /// </summary>
    private static string MaskUserName(string userName)
    {
        if (string.IsNullOrEmpty(userName))
            return "****";

        var parts = userName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0][0]}****** {parts[^1]}"; // First initial + masked + last name
        }
        return $"{userName[0]}******";
    }

    /// <summary>
    /// Computes SHA-256 hash of input string.
    /// SPEC-SECURITY-001: FR-SEC-06 - Cryptographic hash for integrity.
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IEnumerable<AuditLogEntry> ApplyFilter(IEnumerable<AuditLogEntry> entries, AuditLogFilter filter)
    {
        var query = entries.AsEnumerable();

        if (filter.StartDate.HasValue)
        {
            query = query.Where(e => e.Timestamp >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            var endDate = filter.EndDate.Value.AddDays(1);
            query = query.Where(e => e.Timestamp < endDate);
        }

        if (filter.EventType.HasValue)
        {
            query = query.Where(e => e.EventType == filter.EventType.Value);
        }

        if (!string.IsNullOrEmpty(filter.UserId))
        {
            query = query.Where(e => e.UserId.Contains(filter.UserId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(filter.PatientId))
        {
            query = query.Where(e => e.PatientId != null && e.PatientId.Contains(filter.PatientId, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Outcome.HasValue)
        {
            query = query.Where(e => e.Outcome == filter.Outcome.Value);
        }

        return query.OrderByDescending(e => e.Timestamp);
    }

    private int GetEntryOrder(string entryId)
    {
        var index = 0;
        foreach (var id in _entryOrder)
        {
            if (id == entryId)
                return index;
            index++;
        }
        return int.MaxValue;
    }

    private async Task InitializeSampleEntriesAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // Generate entries with proper hash chain
        for (int i = 0; i < 50; i++)
        {
            var eventType = GetEventTypeForIndex(i);
            var timestamp = now.AddHours(-i * 0.5);

            await LogAsync(
                eventType,
                i % 5 == 0 ? "admin" : "operator1",
                i % 5 == 0 ? "System Administrator" : "Technician Johnson",
                GetEventDescription(eventType, i),
                i % 10 == 0 ? AuditOutcome.Warning : AuditOutcome.Success,
                i % 3 == 0 ? $"PT{(i / 3) + 1:D6}" : null,
                i % 3 == 0 ? $"ST{(i / 3) + 1:D6}" : null,
                CancellationToken.None);
        }
    }

    private static AuditEventType GetEventTypeForIndex(int index)
    {
        var types = new[]
        {
            AuditEventType.UserLogin,
            AuditEventType.UserLogout,
            AuditEventType.PatientRegistration,
            AuditEventType.ExposureInitiated,
            AuditEventType.ExposureCompleted,
            AuditEventType.ConfigChange,
            AuditEventType.DataExport,
            AuditEventType.AccessDenied,
            AuditEventType.PasswordChange
        };
        return types[index % types.Length];
    }

    private static string GetEventDescription(AuditEventType eventType, int index)
    {
        return eventType switch
        {
            AuditEventType.UserLogin => "User login successful",
            AuditEventType.UserLogout => "User logout",
            AuditEventType.PatientRegistration => $"Registered new patient PT{(index / 3) + 1:D6}",
            AuditEventType.ExposureInitiated => "X-Ray exposure initiated - 80kV, 100mA",
            AuditEventType.ExposureCompleted => "X-Ray exposure completed successfully",
            AuditEventType.ConfigChange => "Configuration updated - Network settings",
            AuditEventType.DataExport => "Study data exported to PACS",
            AuditEventType.AccessDenied => "Access denied to restricted configuration",
            AuditEventType.PasswordChange => "User password changed",
            _ => "System event logged"
        };
    }
}
