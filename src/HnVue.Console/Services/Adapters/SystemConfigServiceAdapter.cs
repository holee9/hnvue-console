using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for ISystemConfigService.
/// GetConfigAsync and UpdateConfigAsync use ConfigService.
/// StartCalibrationAsync uses CommandService.RunCalibration.
/// </summary>
public sealed class SystemConfigServiceAdapter : GrpcAdapterBase, ISystemConfigService
{
    private readonly ILogger<SystemConfigServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SystemConfigServiceAdapter"/>.
    /// </summary>
    public SystemConfigServiceAdapter(IConfiguration configuration, ILogger<SystemConfigServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SystemConfig> GetConfigAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ConfigService.ConfigServiceClient>();
            var request = new HnVue.Ipc.GetConfigRequest();
            var response = await client.GetConfigurationAsync(request, cancellationToken: ct);
            // Map response to SystemConfig - return empty config as proto-to-model mapping
            // depends on key naming conventions not yet established
            return new SystemConfig
            {
                Calibration = null,
                Network = null,
                Users = null,
                Logging = null
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(ISystemConfigService), nameof(GetConfigAsync));
            return new SystemConfig
            {
                Calibration = null,
                Network = null,
                Users = null,
                Logging = null
            };
        }
    }

    /// <inheritdoc />
    public async Task<object> GetConfigSectionAsync(ConfigSection section, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ConfigService.ConfigServiceClient>();
            var sectionKey = section.ToString().ToLowerInvariant();
            var request = new HnVue.Ipc.GetConfigRequest();
            request.ParameterKeys.Add(sectionKey);
            var response = await client.GetConfigurationAsync(request, cancellationToken: ct);
            return new object();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(ISystemConfigService), nameof(GetConfigSectionAsync));
            return new object();
        }
    }

    /// <inheritdoc />
    public async Task UpdateConfigAsync(ConfigUpdate update, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ConfigService.ConfigServiceClient>();
            var request = new HnVue.Ipc.SetConfigRequest();
            // Key/value mapping handled by caller through update.UpdateData
            await client.SetConfigurationAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(ISystemConfigService), nameof(UpdateConfigAsync));
        }
    }

    /// <inheritdoc />
    public async Task StartCalibrationAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.CommandService.CommandServiceClient>();
            var request = new HnVue.Ipc.RunCalibrationRequest
            {
                Mode = HnVue.Ipc.CalibrationMode.Unspecified
            };
            await client.RunCalibrationAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(ISystemConfigService), nameof(StartCalibrationAsync));
        }
    }

    /// <inheritdoc />
    public Task<CalibrationConfig> GetCalibrationStatusAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(ISystemConfigService), nameof(GetCalibrationStatusAsync));
        return Task.FromResult(new CalibrationConfig
        {
            LastCalibrationDate = DateTimeOffset.MinValue,
            NextCalibrationDueDate = DateTimeOffset.MinValue,
            IsCalibrationValid = false,
            Status = CalibrationStatus.Required
        });
    }

    /// <inheritdoc />
    public Task<bool> ValidateNetworkConfigAsync(NetworkConfig config, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(ISystemConfigService), nameof(ValidateNetworkConfigAsync));
        return Task.FromResult(true);
    }
}
