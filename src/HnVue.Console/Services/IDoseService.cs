using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Dose service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-10 Dose Display.
/// </summary>
public interface IDoseService
{
    /// <summary>
    /// Gets current dose display for the active study.
    /// </summary>
    Task<DoseDisplay> GetCurrentDoseDisplayAsync(CancellationToken ct);

    /// <summary>
    /// Gets dose alert threshold configuration.
    /// </summary>
    Task<DoseAlertThreshold> GetAlertThresholdAsync(CancellationToken ct);

    /// <summary>
    /// Sets dose alert threshold.
    /// </summary>
    Task SetAlertThresholdAsync(DoseAlertThreshold threshold, CancellationToken ct);

    /// <summary>
    /// Subscribes to dose update notifications.
    /// </summary>
    IAsyncEnumerable<DoseUpdate> SubscribeDoseUpdatesAsync(CancellationToken ct);

    /// <summary>
    /// Resets cumulative dose for a new study.
    /// </summary>
    Task ResetCumulativeDoseAsync(string studyId, CancellationToken ct);
}
