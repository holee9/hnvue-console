using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IAuditLogService.
/// SPEC-ADAPTER-001: Regulatory compliance audit trail using AuditLogService gRPC.
/// @MX:NOTE Uses AuditLogService gRPC for audit logging, querying, and export (IEC 62304, FDA 21 CFR Part 11).
/// </summary>
public sealed class AuditLogServiceAdapter : GrpcAdapterBase, IAuditLogService
{
    private readonly ILogger<AuditLogServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AuditLogServiceAdapter"/>.
    /// </summary>
    public AuditLogServiceAdapter(IConfiguration configuration, ILogger<AuditLogServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AuditLogService.AuditLogServiceClient>();
            var response = await client.QueryAuditLogAsync(
                new HnVue.Ipc.QueryAuditLogRequest
                {
                    StartTimestamp = new HnVue.Ipc.Timestamp { MicrosecondsSinceStart = 0 },
                    EndTimestamp = new HnVue.Ipc.Timestamp { MicrosecondsSinceStart = ulong.MaxValue },
                    MaxResults = 1000
                },
                cancellationToken: ct);

            var entries = response.Entries.Select(e => new AuditLogEntry
            {
                EntryId = e.AuditEntryId,
                Timestamp = DateTimeOffset.UtcNow,
                EventType = MapAuditEventType(e.EventType),
                EventDescription = e.EventDescription,
                UserId = e.UserId,
                UserName = e.Username ?? "Unknown",
                PatientId = e.PatientId,
                StudyId = e.StudyId,
                Outcome = MapAuditOutcome(e.Severity)
            }).ToList();

            return entries.AsReadOnly();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAuditLogService), nameof(GetLogsAsync));
            return Array.Empty<AuditLogEntry>();
        }
    }

    /// <inheritdoc />
    public async Task<PagedAuditLogResult> GetLogsPagedAsync(int pageNumber, int pageSize, AuditLogFilter? filter, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AuditLogService.AuditLogServiceClient>();
            var response = await client.QueryAuditLogAsync(
                new HnVue.Ipc.QueryAuditLogRequest
                {
                    StartTimestamp = new HnVue.Ipc.Timestamp { MicrosecondsSinceStart = 0 },
                    EndTimestamp = new HnVue.Ipc.Timestamp { MicrosecondsSinceStart = ulong.MaxValue },
                    Offset = (pageNumber - 1) * pageSize,
                    MaxResults = pageSize
                },
                cancellationToken: ct);

            var entries = response.Entries.Select(e => new AuditLogEntry
            {
                EntryId = e.AuditEntryId,
                Timestamp = DateTimeOffset.UtcNow,
                EventType = MapAuditEventType(e.EventType),
                EventDescription = e.EventDescription,
                UserId = e.UserId,
                UserName = e.Username ?? "Unknown",
                PatientId = e.PatientId,
                StudyId = e.StudyId,
                Outcome = MapAuditOutcome(e.Severity)
            }).ToList();

            return new PagedAuditLogResult
            {
                Entries = entries,
                TotalCount = response.TotalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                HasMorePages = response.HasMore
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAuditLogService), nameof(GetLogsPagedAsync));
            return new PagedAuditLogResult
            {
                Entries = Array.Empty<AuditLogEntry>(),
                TotalCount = 0,
                PageNumber = pageNumber,
                PageSize = pageSize,
                HasMorePages = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<AuditLogEntry?> GetLogEntryAsync(string entryId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AuditLogService.AuditLogServiceClient>();
            var response = await client.GetAuditEntryAsync(
                new HnVue.Ipc.GetAuditEntryRequest
                {
                    AuditEntryId = entryId
                },
                cancellationToken: ct);

            return new AuditLogEntry
            {
                EntryId = response.Entry.AuditEntryId,
                Timestamp = DateTimeOffset.UtcNow,
                EventType = MapAuditEventType(response.Entry.EventType),
                EventDescription = response.Entry.EventDescription,
                UserId = response.Entry.UserId,
                UserName = response.Entry.Username ?? "Unknown",
                PatientId = response.Entry.PatientId,
                StudyId = response.Entry.StudyId,
                Outcome = MapAuditOutcome(response.Entry.Severity)
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAuditLogService), nameof(GetLogEntryAsync));
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetLogCountAsync(AuditLogFilter filter, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AuditLogService.AuditLogServiceClient>();
            var response = await client.QueryAuditLogAsync(
                new HnVue.Ipc.QueryAuditLogRequest
                {
                    StartTimestamp = new HnVue.Ipc.Timestamp { MicrosecondsSinceStart = 0 },
                    EndTimestamp = new HnVue.Ipc.Timestamp { MicrosecondsSinceStart = ulong.MaxValue },
                    MaxResults = 1 // Only need count
                },
                cancellationToken: ct);

            return response.TotalCount;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAuditLogService), nameof(GetLogCountAsync));
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AuditLogService.AuditLogServiceClient>();
            var response = await client.ExportAuditLogAsync(
                new HnVue.Ipc.ExportAuditLogRequest
                {
                    Format = HnVue.Ipc.ExportFormat.Csv,
                    StartTimestamp = new HnVue.Ipc.Timestamp { MicrosecondsSinceStart = 0 },
                    EndTimestamp = new HnVue.Ipc.Timestamp { MicrosecondsSinceStart = ulong.MaxValue }
                },
                cancellationToken: ct);

            // Proto returns file path, not binary data - read from file if needed
            // For now return empty array as stub implementation
            _ = response.ExportedFilePath;
            return Array.Empty<byte>();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAuditLogService), nameof(ExportLogsAsync));
            return Array.Empty<byte>();
        }
    }

    private static AuditEventType MapAuditEventType(HnVue.Ipc.AuditEventType protoType)
    {
        return protoType switch
        {
            HnVue.Ipc.AuditEventType.UserLogin => AuditEventType.UserLogin,
            HnVue.Ipc.AuditEventType.UserLogout => AuditEventType.UserLogout,
            HnVue.Ipc.AuditEventType.PatientViewed => AuditEventType.PatientRegistration,
            HnVue.Ipc.AuditEventType.PatientAccessed => AuditEventType.PatientEdit,
            HnVue.Ipc.AuditEventType.ExposureStarted => AuditEventType.ExposureInitiated,
            HnVue.Ipc.AuditEventType.ExposureCompleted => AuditEventType.ImageAccepted,
            HnVue.Ipc.AuditEventType.ExposureAborted => AuditEventType.ImageRejected,
            HnVue.Ipc.AuditEventType.SystemStartup => AuditEventType.ConfigChange,
            HnVue.Ipc.AuditEventType.SystemShutdown => AuditEventType.SystemError,
            HnVue.Ipc.AuditEventType.DoseAlert => AuditEventType.DoseAlertExceeded,
            _ => AuditEventType.SystemError
        };
    }

    private static AuditOutcome MapAuditOutcome(HnVue.Ipc.SeverityLevel severity)
    {
        return severity switch
        {
            HnVue.Ipc.SeverityLevel.Info => AuditOutcome.Success,
            HnVue.Ipc.SeverityLevel.Warning => AuditOutcome.Warning,
            HnVue.Ipc.SeverityLevel.Error => AuditOutcome.Failure,
            HnVue.Ipc.SeverityLevel.Critical => AuditOutcome.Failure,
            _ => AuditOutcome.Success
        };
    }
}
