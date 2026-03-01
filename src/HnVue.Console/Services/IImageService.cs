using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Image service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-03 Image Viewer.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Gets image data by ID.
    /// </summary>
    Task<ImageData> GetImageAsync(string imageId, CancellationToken ct);

    /// <summary>
    /// Gets the current image for the study.
    /// </summary>
    Task<ImageData?> GetCurrentImageAsync(string studyId, CancellationToken ct);

    /// <summary>
    /// Applies window/level transformation.
    /// </summary>
    Task ApplyWindowLevelAsync(string imageId, WindowLevel windowLevel, CancellationToken ct);

    /// <summary>
    /// Sets zoom and pan parameters.
    /// </summary>
    Task SetZoomPanAsync(string imageId, ZoomPan zoomPan, CancellationToken ct);

    /// <summary>
    /// Sets image orientation.
    /// </summary>
    Task SetOrientationAsync(string imageId, ImageOrientation orientation, CancellationToken ct);

    /// <summary>
    /// Applies multiple transformations at once.
    /// </summary>
    Task ApplyTransformAsync(string imageId, ImageTransform transform, CancellationToken ct);

    /// <summary>
    /// Resets all transformations to default.
    /// </summary>
    Task ResetTransformAsync(string imageId, CancellationToken ct);
}
