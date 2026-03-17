using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IImageService.
/// SPEC-UI-001: FR-UI-03 Image Viewer.
/// </summary>
public sealed class ImageServiceAdapter : GrpcAdapterBase, IImageService
{
    private readonly ILogger<ImageServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ImageServiceAdapter"/>.
    /// </summary>
    public ImageServiceAdapter(IConfiguration configuration, ILogger<ImageServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ImageData> GetImageAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(GetImageAsync));
        await Task.CompletedTask;
        return new ImageData
        {
            ImageId = imageId,
            PixelData = Array.Empty<byte>(),
            Width = 0,
            Height = 0,
            BitsPerPixel = 16,
            PixelSpacing = new PixelSpacing { RowSpacingMm = 0, ColumnSpacingMm = 0 }
        };
    }

    /// <inheritdoc />
    public async Task<ImageData?> GetCurrentImageAsync(string studyId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(GetCurrentImageAsync));
        await Task.CompletedTask;
        return null;
    }

    /// <inheritdoc />
    public async Task ApplyWindowLevelAsync(string imageId, WindowLevel windowLevel, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(ApplyWindowLevelAsync));
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SetZoomPanAsync(string imageId, ZoomPan zoomPan, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(SetZoomPanAsync));
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SetOrientationAsync(string imageId, ImageOrientation orientation, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(SetOrientationAsync));
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ApplyTransformAsync(string imageId, ImageTransform transform, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(ApplyTransformAsync));
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ResetTransformAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(ResetTransformAsync));
        await Task.CompletedTask;
    }
}
