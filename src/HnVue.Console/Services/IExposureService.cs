using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Exposure service interface for gRPC communication.
/// SPEC-UI-001 FR-UI-07 and FR-UI-09 Exposure Parameters and Real-time Preview.
/// </summary>
public interface IExposureService
{
    /// <summary>
    /// Subscribes to real-time preview frames from the detector.
    /// </summary>
    IAsyncEnumerable<PreviewFrame> SubscribePreviewFramesAsync(CancellationToken ct);

    /// <summary>
    /// Gets current exposure parameter ranges.
    /// </summary>
    Task<ExposureParameterRange> GetExposureRangesAsync(CancellationToken ct);

    /// <summary>
    /// Gets current exposure parameters.
    /// </summary>
    Task<ExposureParameters> GetExposureParametersAsync(CancellationToken ct);

    /// <summary>
    /// Sets exposure parameters.
    /// </summary>
    Task SetExposureParametersAsync(ExposureParameters parameters, CancellationToken ct);

    /// <summary>
    /// Triggers an exposure.
    /// </summary>
    Task<ExposureTriggerResult> TriggerExposureAsync(ExposureTriggerRequest request, CancellationToken ct);

    /// <summary>
    /// Cancels an ongoing exposure.
    /// </summary>
    Task CancelExposureAsync(CancellationToken ct);
}
