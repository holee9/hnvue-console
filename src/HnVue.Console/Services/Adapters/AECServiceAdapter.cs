using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IAECService.
/// SPEC-UI-001: FR-UI-11 AEC Mode Toggle.
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
    public async Task EnableAECAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            var grpcRequest = new HnVue.Ipc.SetAecEnabledRequest
            {
                Enabled = true
            };

            await client.SetAecEnabledAsync(grpcRequest, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(EnableAECAsync));
        }
    }

    /// <inheritdoc />
    public async Task DisableAECAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            var grpcRequest = new HnVue.Ipc.SetAecEnabledRequest
            {
                Enabled = false
            };

            await client.SetAecEnabledAsync(grpcRequest, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(DisableAECAsync));
        }
    }

    /// <inheritdoc />
    public async Task<bool> GetAECStateAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            var grpcRequest = new HnVue.Ipc.GetAecStatusRequest();

            var response = await client.GetAecStatusAsync(grpcRequest, cancellationToken: ct);
            return response.IsEnabled;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(GetAECStateAsync));
            return false;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<bool> SubscribeAECStateChangesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        HnVue.Ipc.AECService.AECServiceClient client;
        Grpc.Core.AsyncServerStreamingCall<HnVue.Ipc.AecChangeEvent> call;

        try
        {
            client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            call = client.SubscribeAecChanges(new HnVue.Ipc.AecChangeSubscribeRequest(), cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(SubscribeAECStateChangesAsync));
            yield break;
        }

        await foreach (var changeEvent in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return changeEvent.IsEnabled;
        }
    }
}
