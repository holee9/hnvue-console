using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IWorklistService.
/// No gRPC proto defined yet; returns graceful defaults.
/// </summary>
public sealed class WorklistServiceAdapter : GrpcAdapterBase, IWorklistService
{
    private readonly ILogger<WorklistServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="WorklistServiceAdapter"/>.
    /// </summary>
    public WorklistServiceAdapter(IConfiguration configuration, ILogger<WorklistServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorklistItem>> GetWorklistAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IWorklistService), nameof(GetWorklistAsync));
        return Task.FromResult<IReadOnlyList<WorklistItem>>(Array.Empty<WorklistItem>());
    }

    /// <inheritdoc />
    public Task<WorklistRefreshResult> RefreshWorklistAsync(WorklistRefreshRequest request, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IWorklistService), nameof(RefreshWorklistAsync));
        return Task.FromResult(new WorklistRefreshResult
        {
            Items = Array.Empty<WorklistItem>(),
            RefreshedAt = DateTimeOffset.UtcNow
        });
    }

    /// <inheritdoc />
    public Task SelectWorklistItemAsync(string procedureId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IWorklistService), nameof(SelectWorklistItemAsync));
        return Task.CompletedTask;
    }
}
