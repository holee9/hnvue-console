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
                Category = e.EventCategory,
                Description = e.EventDescription,
                UserId = e.UserId,
                WorkstationId = e.WorkstationId,
                PatientId = e.PatientId,
                StudyId = e.StudyId
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
                Category = e.EventCategory,
                Description = e.EventDescription,
                UserId = e.UserId,
                WorkstationId = e.WorkstationId,
                PatientId = e.PatientId,
                StudyId = e.StudyId
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
                    EntryId = entryId
                },
                cancellationToken: ct);

            return new AuditLogEntry
            {
                EntryId = response.Entry.AuditEntryId,
                Timestamp = DateTimeOffset.UtcNow,
                EventType = MapAuditEventType(response.Entry.EventType),
                Category = response.Entry.EventCategory,
                Description = response.Entry.EventDescription,
                UserId = response.Entry.UserId,
                WorkstationId = response.Entry.WorkstationId,
                PatientId = response.Entry.PatientId,
                StudyId = response.Entry.StudyId
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

            return Convert.FromBase64String(response.ExportData);
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
            HnVue.Ipc.AuditEventType.AuditEventTypeUserLogin => AuditEventType.UserLogin,
            HnVue.Ipc.AuditEventType.AuditEventTypeUserLogout => AuditEventType.UserLogout,
            HnVue.Ipc.AuditEventType.AuditEventTypePatientViewed => AuditEventType.PatientViewed,
            HnVue.Ipc.AuditEventType.AuditEventTypeExposureStarted => AuditEventType.ExposureStarted,
            HnVue.Ipc.AuditEventType.AuditEventTypeExposureCompleted => AuditEventType.ExposureCompleted,
            HnVue.Ipc.AuditEventType.AuditEventTypeExposureAborted => AuditEventType.ExposureAborted,
            HnVue.Ipc.AuditEventType.AuditEventTypeSystemStartup => AuditEventType.SystemStartup,
            HnVue.Ipc.AuditEventType.AuditEventTypeSystemShutdown => AuditEventType.SystemShutdown,
            _ => AuditEventType.Other
        };
    }
}
