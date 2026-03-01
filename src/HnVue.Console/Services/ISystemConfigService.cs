using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// System config service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-08 System Configuration.
/// </summary>
public interface ISystemConfigService
{
    /// <summary>
    /// Gets system configuration.
    /// </summary>
    Task<SystemConfig> GetConfigAsync(CancellationToken ct);

    /// <summary>
    /// Gets a specific configuration section.
    /// </summary>
    Task<object> GetConfigSectionAsync(ConfigSection section, CancellationToken ct);

    /// <summary>
    /// Updates configuration.
    /// </summary>
    Task UpdateConfigAsync(ConfigUpdate update, CancellationToken ct);

    /// <summary>
    /// Initiates a calibration procedure.
    /// </summary>
    Task StartCalibrationAsync(CancellationToken ct);

    /// <summary>
    /// Gets calibration status.
    /// </summary>
    Task<CalibrationConfig> GetCalibrationStatusAsync(CancellationToken ct);

    /// <summary>
    /// Validates network configuration.
    /// </summary>
    Task<bool> ValidateNetworkConfigAsync(NetworkConfig config, CancellationToken ct);
}
