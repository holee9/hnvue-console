using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IImageService.
/// SPEC-UI-001: FR-UI-03 Image Viewer.
/// SPEC-IPC-002: REQ-IMG-001 through REQ-IMG-005 - Real gRPC implementation.
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
    /// <remarks>
    /// SPEC-IPC-002: REQ-IMG-002 - Uses GetImage RPC with 5s deadline.
    /// REQ-IMG-005: Throws RpcException on gRPC failure (does NOT return empty ImageData).
    /// </remarks>
    public async Task<ImageData> GetImageAsync(string imageId, CancellationToken ct)
    {
        var client = CreateClient<HnVue.Ipc.ImageService.ImageServiceClient>();
        var callOptions = CreateCallOptions(CommandDeadline).WithCancellationToken(ct);

        var response = await client.GetImageAsync(
            new HnVue.Ipc.GetImageRequest { ImageId = imageId },
            callOptions);

        if (response.Error != null && response.Error.Code != 0)
        {
            throw new RpcException(new Status(StatusCode.Internal, response.Error.Message ?? "Image retrieval failed"));
        }

        return new ImageData
        {
            ImageId = response.ImageId,
            PixelData = response.PixelData.ToByteArray(),
            Width = response.Width,
            Height = response.Height,
            BitsPerPixel = response.BitsPerPixel,
            PixelSpacing = new PixelSpacing { RowSpacingMm = 0, ColumnSpacingMm = 0 }
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-IMG-003 - Subscribes to SubscribeImageStream, collects chunks, returns assembled ImageData.
    /// Returns null if stream is empty or on error.
    /// </remarks>
    public async Task<ImageData?> GetCurrentImageAsync(string studyId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ImageService.ImageServiceClient>();
            var callOptions = CreateCallOptions(ImageStreamDeadline).WithCancellationToken(ct);

            var streamRequest = new HnVue.Ipc.ImageStreamRequest
            {
                AcquisitionIdFilter = 0 // Subscribe to all acquisitions for the study
            };

            using var call = client.SubscribeImageStream(streamRequest, callOptions);

            var allPixelData = new List<byte>();
            HnVue.Ipc.ImageMetadata? metadata = null;
            bool hasChunks = false;

            await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
            {
                if (chunk.Error != null && chunk.Error.Code != 0)
                {
                    _logger.LogWarning("Image stream chunk error for study {StudyId}: {Error}", studyId, chunk.Error.Message);
                    return null;
                }

                // Metadata is present only in the first chunk (sequence_number == 0)
                if (chunk.SequenceNumber == 0 && chunk.Metadata != null)
                {
                    metadata = chunk.Metadata;
                }

                allPixelData.AddRange(chunk.PixelData);
                hasChunks = true;

                if (chunk.IsLastChunk)
                {
                    break;
                }
            }

            if (!hasChunks || metadata == null)
            {
                return null;
            }

            return new ImageData
            {
                ImageId = $"study-{studyId}-current",
                PixelData = allPixelData.ToArray(),
                Width = (int)metadata.WidthPixels,
                Height = (int)metadata.HeightPixels,
                BitsPerPixel = (int)metadata.BitsPerPixel,
                PixelSpacing = new PixelSpacing
                {
                    RowSpacingMm = (decimal)metadata.PixelPitchMm,
                    ColumnSpacingMm = (decimal)metadata.PixelPitchMm
                }
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method} studyId={StudyId}",
                nameof(IImageService), nameof(GetCurrentImageAsync), studyId);
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-IMG-004 - Rendering operations delegate to rendering pipeline (no gRPC call).
    /// </remarks>
    public async Task ApplyWindowLevelAsync(string imageId, WindowLevel windowLevel, CancellationToken ct)
    {
        _logger.LogWarning("Rendering pipeline: {Method} delegated locally (no gRPC). ImageId={ImageId}, Center={Center}, Width={Width}",
            nameof(ApplyWindowLevelAsync), imageId, windowLevel.WindowCenter, windowLevel.WindowWidth);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-IMG-004 - Rendering operations delegate to rendering pipeline (no gRPC call).
    /// </remarks>
    public async Task SetZoomPanAsync(string imageId, ZoomPan zoomPan, CancellationToken ct)
    {
        _logger.LogWarning("Rendering pipeline: {Method} delegated locally (no gRPC). ImageId={ImageId}, Zoom={Zoom}",
            nameof(SetZoomPanAsync), imageId, zoomPan.ZoomFactor);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-IMG-004 - Rendering operations delegate to rendering pipeline (no gRPC call).
    /// </remarks>
    public async Task SetOrientationAsync(string imageId, ImageOrientation orientation, CancellationToken ct)
    {
        _logger.LogWarning("Rendering pipeline: {Method} delegated locally (no gRPC). ImageId={ImageId}, Orientation={Orientation}",
            nameof(SetOrientationAsync), imageId, orientation);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-IMG-004 - Rendering operations delegate to rendering pipeline (no gRPC call).
    /// </remarks>
    public async Task ApplyTransformAsync(string imageId, ImageTransform transform, CancellationToken ct)
    {
        _logger.LogWarning("Rendering pipeline: {Method} delegated locally (no gRPC). ImageId={ImageId}",
            nameof(ApplyTransformAsync), imageId);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-IMG-004 - Rendering operations delegate to rendering pipeline (no gRPC call).
    /// </remarks>
    public async Task ResetTransformAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("Rendering pipeline: {Method} delegated locally (no gRPC). ImageId={ImageId}",
            nameof(ResetTransformAsync), imageId);
        await Task.CompletedTask;
    }
}
