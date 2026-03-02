using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for INetworkService.
/// GetNetworkConfigAsync and UpdateNetworkConfigAsync use ConfigService (partial).
/// Connection testing returns graceful defaults.
/// </summary>
public sealed class NetworkServiceAdapter : GrpcAdapterBase, INetworkService
{
    private readonly ILogger<NetworkServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="NetworkServiceAdapter"/>.
    /// </summary>
    public NetworkServiceAdapter(IConfiguration configuration, ILogger<NetworkServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NetworkConfig> GetNetworkConfigAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ConfigService.ConfigServiceClient>();
            var request = new HnVue.Ipc.GetConfigRequest();
            request.ParameterKeys.Add("network");
            await client.GetConfigurationAsync(request, cancellationToken: ct);
            // Return default - proto-to-model key mapping not yet established
            return new NetworkConfig
            {
                DicomAeTitle = string.Empty,
                DicomPort = string.Empty,
                PacsHostName = string.Empty,
                PacsPort = 0,
                MwlEnabled = false
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(INetworkService), nameof(GetNetworkConfigAsync));
            return new NetworkConfig
            {
                DicomAeTitle = string.Empty,
                DicomPort = string.Empty,
                PacsHostName = string.Empty,
                PacsPort = 0,
                MwlEnabled = false
            };
        }
    }

    /// <inheritdoc />
    public async Task UpdateNetworkConfigAsync(NetworkConfig config, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ConfigService.ConfigServiceClient>();
            var request = new HnVue.Ipc.SetConfigRequest();
            await client.SetConfigurationAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(INetworkService), nameof(UpdateNetworkConfigAsync));
        }
    }

    /// <inheritdoc />
    public Task<bool> TestPacsConnectionAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(INetworkService), nameof(TestPacsConnectionAsync));
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> TestMwlConnectionAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(INetworkService), nameof(TestMwlConnectionAsync));
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<NetworkConnectionStatus> GetConnectionStatusAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(INetworkService), nameof(GetConnectionStatusAsync));
        return Task.FromResult(new NetworkConnectionStatus
        {
            PacsConnected = false,
            MwlConnected = false,
            DicomNodeReachable = false,
            CheckedAt = DateTimeOffset.UtcNow
        });
    }
}
