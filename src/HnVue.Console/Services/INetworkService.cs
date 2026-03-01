using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Network service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-08 System Configuration (Network section).
/// </summary>
public interface INetworkService
{
    /// <summary>
    /// Gets network configuration.
    /// </summary>
    Task<NetworkConfig> GetNetworkConfigAsync(CancellationToken ct);

    /// <summary>
    /// Updates network configuration.
    /// </summary>
    Task UpdateNetworkConfigAsync(NetworkConfig config, CancellationToken ct);

    /// <summary>
    /// Tests PACS connectivity.
    /// </summary>
    Task<bool> TestPacsConnectionAsync(CancellationToken ct);

    /// <summary>
    /// Tests MWL query connectivity.
    /// </summary>
    Task<bool> TestMwlConnectionAsync(CancellationToken ct);

    /// <summary>
    /// Gets network connection status.
    /// </summary>
    Task<NetworkConnectionStatus> GetConnectionStatusAsync(CancellationToken ct);
}

/// <summary>
/// Network connection status.
/// </summary>
public record NetworkConnectionStatus
{
    public required bool PacsConnected { get; init; }
    public required bool MwlConnected { get; init; }
    public required bool DicomNodeReachable { get; init; }
    public required DateTimeOffset CheckedAt { get; init; }
}
