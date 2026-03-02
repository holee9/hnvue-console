using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for ISystemStatusService.
/// GetOverallStatusAsync and CanInitiateExposureAsync use CommandService.GetSystemState.
/// SubscribeStatusUpdatesAsync uses HealthService.SubscribeHealth.
/// </summary>
public sealed class SystemStatusServiceAdapter : GrpcAdapterBase, ISystemStatusService
{
    private readonly ILogger<SystemStatusServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SystemStatusServiceAdapter"/>.
    /// </summary>
    public SystemStatusServiceAdapter(IConfiguration configuration, ILogger<SystemStatusServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SystemOverallStatus> GetOverallStatusAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.CommandService.CommandServiceClient>();
            var response = await client.GetSystemStateAsync(new HnVue.Ipc.GetSystemStateRequest(), cancellationToken: ct);

            var canInitiate = response.State == HnVue.Ipc.SystemState.Ready;
            var health = response.State switch
            {
                HnVue.Ipc.SystemState.Ready => ComponentHealth.Healthy,
                HnVue.Ipc.SystemState.Fault => ComponentHealth.Error,
                HnVue.Ipc.SystemState.Initializing => ComponentHealth.Unknown,
                HnVue.Ipc.SystemState.ShuttingDown => ComponentHealth.Offline,
                _ => ComponentHealth.Unknown
            };

            return new SystemOverallStatus
            {
                OverallHealth = health,
                ComponentStatuses = Array.Empty<ComponentStatus>(),
                CanInitiateExposure = canInitiate,
                ActiveAlerts = Array.Empty<string>(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(ISystemStatusService), nameof(GetOverallStatusAsync));
            return new SystemOverallStatus
            {
                OverallHealth = ComponentHealth.Unknown,
                ComponentStatuses = Array.Empty<ComponentStatus>(),
                CanInitiateExposure = false,
                ActiveAlerts = Array.Empty<string>(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    /// <inheritdoc />
    public Task<ComponentStatus?> GetComponentStatusAsync(string componentId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(ISystemStatusService), nameof(GetComponentStatusAsync));
        return Task.FromResult<ComponentStatus?>(null);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StatusUpdate> SubscribeStatusUpdatesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        HnVue.Ipc.HealthService.HealthServiceClient client;
        Grpc.Core.AsyncServerStreamingCall<HnVue.Ipc.HealthEvent> call;
        try
        {
            client = CreateClient<HnVue.Ipc.HealthService.HealthServiceClient>();
            call = client.SubscribeHealth(new HnVue.Ipc.HealthSubscribeRequest(), cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(ISystemStatusService), nameof(SubscribeStatusUpdatesAsync));
            yield break;
        }

        await foreach (var healthEvent in call.ResponseStream.ReadAllAsync(ct))
        {
            StatusUpdate? update = null;

            if (healthEvent.EventType == HnVue.Ipc.HealthEventType.HardwareStatus && healthEvent.HardwareStatus != null)
            {
                var hw = healthEvent.HardwareStatus;
                var componentHealth = hw.Status switch
                {
                    HnVue.Ipc.HardwareComponentStatus.HardwareStatusOnline => ComponentHealth.Healthy,
                    HnVue.Ipc.HardwareComponentStatus.HardwareStatusOffline => ComponentHealth.Offline,
                    HnVue.Ipc.HardwareComponentStatus.HardwareStatusDegraded => ComponentHealth.Degraded,
                    HnVue.Ipc.HardwareComponentStatus.HardwareStatusFault => ComponentHealth.Error,
                    _ => ComponentHealth.Unknown
                };

                update = new StatusUpdate
                {
                    Component = new ComponentStatus
                    {
                        ComponentId = hw.ComponentId.ToString(),
                        Type = ComponentType.CoreEngine,
                        Health = componentHealth,
                        StatusMessage = hw.Detail,
                        UpdatedAt = DateTimeOffset.UtcNow
                    },
                    RequiresExposureHalt = false
                };
            }
            else if (healthEvent.EventType == HnVue.Ipc.HealthEventType.Fault && healthEvent.Fault != null)
            {
                var fault = healthEvent.Fault;
                var requiresHalt = fault.Severity == HnVue.Ipc.FaultSeverity.Critical;
                update = new StatusUpdate
                {
                    Component = new ComponentStatus
                    {
                        ComponentId = "fault",
                        Type = ComponentType.CoreEngine,
                        Health = ComponentHealth.Error,
                        StatusMessage = fault.FaultDescription,
                        UpdatedAt = DateTimeOffset.UtcNow
                    },
                    RequiresExposureHalt = requiresHalt
                };
            }

            if (update != null)
            {
                yield return update;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanInitiateExposureAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.CommandService.CommandServiceClient>();
            var response = await client.GetSystemStateAsync(new HnVue.Ipc.GetSystemStateRequest(), cancellationToken: ct);
            return response.State == HnVue.Ipc.SystemState.Ready;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(ISystemStatusService), nameof(CanInitiateExposureAsync));
            return false;
        }
    }
}
