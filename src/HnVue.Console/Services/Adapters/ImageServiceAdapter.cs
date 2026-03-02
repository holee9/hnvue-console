using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IImageService.
/// No full gRPC proto support yet; returns graceful defaults.
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
    public Task<ImageData> GetImageAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(GetImageAsync));
        return Task.FromResult(new ImageData
        {
            ImageId = imageId,
            PixelData = Array.Empty<byte>(),
            Width = 0,
            Height = 0,
            BitsPerPixel = 0,
            PixelSpacing = new PixelSpacing { RowSpacingMm = 0m, ColumnSpacingMm = 0m }
        });
    }

    /// <inheritdoc />
    public Task<ImageData?> GetCurrentImageAsync(string studyId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(GetCurrentImageAsync));
        return Task.FromResult<ImageData?>(null);
    }

    /// <inheritdoc />
    public Task ApplyWindowLevelAsync(string imageId, WindowLevel windowLevel, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(ApplyWindowLevelAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetZoomPanAsync(string imageId, ZoomPan zoomPan, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(SetZoomPanAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetOrientationAsync(string imageId, ImageOrientation orientation, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(SetOrientationAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApplyTransformAsync(string imageId, ImageTransform transform, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(ApplyTransformAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetTransformAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IImageService), nameof(ResetTransformAsync));
        return Task.CompletedTask;
    }
}
