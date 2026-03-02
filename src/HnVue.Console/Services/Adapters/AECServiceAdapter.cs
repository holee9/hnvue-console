using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IAECService.
/// No gRPC proto defined yet; returns graceful defaults.
/// </summary>
public sealed class AECServiceAdapter : GrpcAdapterBase, IAECService
{
    private readonly ILogger<AECServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AECServiceAdapter"/>.
    /// </summary>
    public AECServiceAdapter(IConfiguration configuration, ILogger<AECServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task EnableAECAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAECService), nameof(EnableAECAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisableAECAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAECService), nameof(DisableAECAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> GetAECStateAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAECService), nameof(GetAECStateAsync));
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<bool> SubscribeAECStateChangesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IAECService), nameof(SubscribeAECStateChangesAsync));
        yield break;
    }
}
