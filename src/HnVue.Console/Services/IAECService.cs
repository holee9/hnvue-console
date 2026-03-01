namespace HnVue.Console.Services;

/// <summary>
/// AEC service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-11 AEC Mode Toggle.
/// </summary>
public interface IAECService
{
    /// <summary>
    /// Enables AEC mode.
    /// </summary>
    Task EnableAECAsync(CancellationToken ct);

    /// <summary>
    /// Disables AEC mode.
    /// </summary>
    Task DisableAECAsync(CancellationToken ct);

    /// <summary>
    /// Gets current AEC state.
    /// </summary>
    Task<bool> GetAECStateAsync(CancellationToken ct);

    /// <summary>
    /// Subscribes to AEC state changes.
    /// </summary>
    IAsyncEnumerable<bool> SubscribeAECStateChangesAsync(CancellationToken ct);
}
