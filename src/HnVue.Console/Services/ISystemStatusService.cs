using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// System status service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-12 System Status Dashboard.
/// </summary>
public interface ISystemStatusService
{
    /// <summary>
    /// Gets overall system status.
    /// </summary>
    Task<SystemOverallStatus> GetOverallStatusAsync(CancellationToken ct);

    /// <summary>
    /// Gets status for a specific component.
    /// </summary>
    Task<ComponentStatus?> GetComponentStatusAsync(string componentId, CancellationToken ct);

    /// <summary>
    /// Subscribes to status update notifications.
    /// </summary>
    IAsyncEnumerable<StatusUpdate> SubscribeStatusUpdatesAsync(CancellationToken ct);

    /// <summary>
    /// Checks if exposure can be initiated.
    /// </summary>
    Task<bool> CanInitiateExposureAsync(CancellationToken ct);
}
