using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IAuditLogService.
/// No gRPC proto defined yet; returns graceful defaults.
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
    public Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAuditLogService), nameof(GetLogsAsync));
        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(Array.Empty<AuditLogEntry>());
    }

    /// <inheritdoc />
    public Task<PagedAuditLogResult> GetLogsPagedAsync(int pageNumber, int pageSize, AuditLogFilter? filter, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAuditLogService), nameof(GetLogsPagedAsync));
        return Task.FromResult(new PagedAuditLogResult
        {
            Entries = Array.Empty<AuditLogEntry>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize,
            HasMorePages = false
        });
    }

    /// <inheritdoc />
    public Task<AuditLogEntry?> GetLogEntryAsync(string entryId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAuditLogService), nameof(GetLogEntryAsync));
        return Task.FromResult<AuditLogEntry?>(null);
    }

    /// <inheritdoc />
    public Task<int> GetLogCountAsync(AuditLogFilter filter, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAuditLogService), nameof(GetLogCountAsync));
        return Task.FromResult(0);
    }

    /// <inheritdoc />
    public Task<byte[]> ExportLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAuditLogService), nameof(ExportLogsAsync));
        return Task.FromResult(Array.Empty<byte>());
    }
}
