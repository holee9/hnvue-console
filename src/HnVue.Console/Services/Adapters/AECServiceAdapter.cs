using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IAECService.
/// SPEC-ADAPTER-001: Automatic Exposure Control monitoring and configuration.
/// @MX:NOTE Uses AECService gRPC for AEC enable/disable and state streaming.
/// </summary>
public sealed class AECServiceAdapter : GrpcAdapterBase, IAECService
{
    private readonly ILogger<AECServiceAdapter> _logger;

    public AECServiceAdapter(IConfiguration configuration, ILogger<AECServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    public async Task EnableAECAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            await client.SetAecEnabledAsync(
                new HnVue.Ipc.SetAecEnabledRequest { Enabled = true },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(EnableAECAsync));
        }
    }

    public async Task DisableAECAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            await client.SetAecEnabledAsync(
                new HnVue.Ipc.SetAecEnabledRequest { Enabled = false },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(DisableAECAsync));
        }
    }

    public async Task<bool> GetAECStateAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            var response = await client.GetAecStatusAsync(
                new HnVue.Ipc.GetAecStatusRequest(),
                cancellationToken: ct);
            return response.IsEnabled;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(GetAECStateAsync));
            return false;
        }
    }

    public async IAsyncEnumerable<bool> SubscribeAECStateChangesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        AsyncServerStreamingCall<HnVue.Ipc.AecChangeEvent> call;

        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
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
