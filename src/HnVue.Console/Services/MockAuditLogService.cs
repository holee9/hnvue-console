using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock audit log service for development.
/// SPEC-UI-001: FR-UI-13 Audit Log Viewer.
/// </summary>
public class MockAuditLogService : IAuditLogService
{
    private readonly List<AuditLogEntry> _mockEntries;

    public MockAuditLogService()
    {
        var now = DateTimeOffset.Now;
        _mockEntries = new List<AuditLogEntry>();

        // Generate sample entries
        for (int i = 0; i < 150; i++)
        {
            var eventType = GetRandomEventType(i);
            var outcome = GetRandomOutcome(eventType);

            _mockEntries.Add(new AuditLogEntry
            {
                EntryId = $"LOG-{i + 1:D6}",
                Timestamp = now.AddHours(-i * 0.5),
                EventType = eventType,
                UserId = i % 5 == 0 ? "admin" : "operator1",
                UserName = i % 5 == 0 ? "System Administrator" : "Technician Johnson",
                EventDescription = GetEventDescription(eventType, i),
                PatientId = i % 3 == 0 ? $"PT{(i / 3) + 1:D6}" : null,
                StudyId = i % 3 == 0 ? $"ST{(i / 3) + 1:D6}" : null,
                Outcome = outcome
            });
        }
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        var filtered = ApplyFilter(_mockEntries, filter);
        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(filtered.ToList());
    }

    public Task<PagedAuditLogResult> GetLogsPagedAsync(int pageNumber, int pageSize, AuditLogFilter? filter, CancellationToken ct)
    {
        var filtered = ApplyFilter(_mockEntries, filter ?? new AuditLogFilter());
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
        var entry = _mockEntries.FirstOrDefault(e => e.EntryId == entryId);
        return Task.FromResult(entry);
    }

    public Task<int> GetLogCountAsync(AuditLogFilter filter, CancellationToken ct)
    {
        var filtered = ApplyFilter(_mockEntries, filter);
        return Task.FromResult(filtered.Count());
    }

    public Task<byte[]> ExportLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        // Mock CSV export
        var filtered = ApplyFilter(_mockEntries, filter);
        var csv = "Timestamp,EventType,User,Description,PatientId,StudyId,Outcome\n" +
                  string.Join("\n", filtered.Select(e =>
                      $"{e.Timestamp:yyyy-MM-dd HH:mm:ss},{e.EventType},{e.UserName},{e.EventDescription},{e.PatientId ?? ""},{e.StudyId ?? ""},{e.Outcome}"));

        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(csv));
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
            var endDate = filter.EndDate.Value.AddDays(1); // Include end date
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

    private static AuditEventType GetRandomEventType(int index)
    {
        var types = Enum.GetValues<AuditEventType>();
        return types[index % types.Length];
    }

    private static AuditOutcome GetRandomOutcome(AuditEventType eventType)
    {
        // Most operations succeed, some have warnings
        return Random.Shared.NextDouble() < 0.85 ? AuditOutcome.Success :
               Random.Shared.NextDouble() < 0.5 ? AuditOutcome.Warning :
               AuditOutcome.Failure;
    }

    private static string GetEventDescription(AuditEventType eventType, int index)
    {
        return eventType switch
        {
            AuditEventType.PatientRegistration => $"Registered new patient record PT{(index / 3) + 1:D6}",
            AuditEventType.PatientEdit => $"Updated patient demographic information",
            AuditEventType.StudyStart => $"Started new examination study ST{(index / 3) + 1:D6}",
            AuditEventType.ExposureInitiated => $"X-Ray exposure initiated - 80kV, 100mA",
            AuditEventType.ImageAccepted => $"Image accepted and archived to PACS",
            AuditEventType.ImageRejected => $"Image rejected - Motion blur detected",
            AuditEventType.ImageReprocessed => $"Image reprocessed with window level adjustment",
            AuditEventType.ConfigChange => $"Configuration updated - Network settings",
            AuditEventType.UserLogin => $"User login successful",
            AuditEventType.UserLogout => $"User logout",
            AuditEventType.SystemError => $"System error detected - Recovered automatically",
            AuditEventType.DoseAlertExceeded => $"Dose alert - Daily limit approaching",
            _ => "Unknown event"
        };
    }
}
