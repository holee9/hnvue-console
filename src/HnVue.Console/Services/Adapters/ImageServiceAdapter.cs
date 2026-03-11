using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IImageService.
/// SPEC-ADAPTER-001: Image data operations using ImageService gRPC (streaming only).
/// @MX:NOTE ImageService proto only supports SubscribeImageStream (server-streaming).
/// @MX:TODO GetImage, ApplyWindowLevel, SetZoomPan require additional proto definitions.
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
    /// @MX:NOTE Proto does not define GetImage. Returns default implementation.
    public Task<ImageData> GetImageAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto does not define GetImage for {Service}.{Method}", nameof(IImageService), nameof(GetImageAsync));
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
    /// @MX:NOTE Proto does not define GetCurrentImage. Returns default implementation.
    public Task<ImageData?> GetCurrentImageAsync(string studyId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto does not define GetCurrentImage for {Service}.{Method}", nameof(IImageService), nameof(GetCurrentImageAsync));
        return Task.FromResult<ImageData?>(null);
    }

    /// <inheritdoc />
    /// @MX:NOTE Proto does not define ApplyWindowLevel. Returns completed task.
    public Task ApplyWindowLevelAsync(string imageId, WindowLevel windowLevel, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto does not define ApplyWindowLevel for {Service}.{Method}", nameof(IImageService), nameof(ApplyWindowLevelAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// @MX:NOTE Proto does not define SetZoomPan. Returns completed task.
    public Task SetZoomPanAsync(string imageId, ZoomPan zoomPan, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto does not define SetZoomPan for {Service}.{Method}", nameof(IImageService), nameof(SetZoomPanAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// @MX:NOTE Proto does not define SetOrientation. Returns completed task.
    public Task SetOrientationAsync(string imageId, ImageOrientation orientation, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto does not define SetOrientation for {Service}.{Method}", nameof(IImageService), nameof(SetOrientationAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// @MX:NOTE Proto does not define ApplyTransform. Returns completed task.
    public Task ApplyTransformAsync(string imageId, ImageTransform transform, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto does not define ApplyTransform for {Service}.{Method}", nameof(IImageService), nameof(ApplyTransformAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// @MX:NOTE Proto does not define ResetTransform. Returns completed task.
    public Task ResetTransformAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto does not define ResetTransform for {Service}.{Method}", nameof(IImageService), nameof(ResetTransformAsync));
        return Task.CompletedTask;
    }
}
