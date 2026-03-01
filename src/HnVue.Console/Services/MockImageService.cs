using System.Diagnostics;
using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock image service for development.
/// SPEC-UI-001: Mock service for image management.
/// </summary>
public class MockImageService : IImageService
{
    private readonly Dictionary<string, ImageData> _images = new();

    /// <inheritdoc/>
    public Task<ImageData> GetImageAsync(string imageId, CancellationToken ct)
    {
        if (_images.TryGetValue(imageId, out var image))
        {
            Debug.WriteLine($"[MockImageService] Retrieved image: {imageId}");
            return Task.FromResult(image);
        }

        // Generate a test image
        var testImage = GenerateTestImage(imageId);
        _images[imageId] = testImage;

        Debug.WriteLine($"[MockImageService] Generated test image: {imageId}");
        return Task.FromResult(testImage);
    }

    /// <inheritdoc/>
    public Task<ImageData?> GetCurrentImageAsync(string studyId, CancellationToken ct)
    {
        var imageId = $"{studyId}_latest";
        if (_images.TryGetValue(imageId, out var image))
        {
            return Task.FromResult<ImageData?>(image);
        }

        return Task.FromResult<ImageData?>(null);
    }

    /// <inheritdoc/>
    public Task ApplyWindowLevelAsync(string imageId, WindowLevel windowLevel, CancellationToken ct)
    {
        if (_images.TryGetValue(imageId, out var image))
        {
            _images[imageId] = image with { CurrentWindowLevel = windowLevel };
            Debug.WriteLine($"[MockImageService] Applied W/L to {imageId}: Center={windowLevel.WindowCenter}, Width={windowLevel.WindowWidth}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetZoomPanAsync(string imageId, ZoomPan zoomPan, CancellationToken ct)
    {
        Debug.WriteLine($"[MockImageService] Set zoom/pan for {imageId}: Zoom={zoomPan.ZoomFactor}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetOrientationAsync(string imageId, ImageOrientation orientation, CancellationToken ct)
    {
        if (_images.TryGetValue(imageId, out var image))
        {
            _images[imageId] = image with { Orientation = orientation };
            Debug.WriteLine($"[MockImageService] Set orientation for {imageId}: {orientation}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ApplyTransformAsync(string imageId, ImageTransform transform, CancellationToken ct)
    {
        Debug.WriteLine($"[MockImageService] Applied transform to {imageId}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResetTransformAsync(string imageId, CancellationToken ct)
    {
        Debug.WriteLine($"[MockImageService] Reset transform for {imageId}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a test 16-bit grayscale image.
    /// </summary>
    private ImageData GenerateTestImage(string imageId)
    {
        int width = 512;
        int height = 512;

        // Generate 16-bit grayscale test pattern
        var pixelData = new ushort[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Create a gradient pattern
                int gradient = (x * 65535) / width;
                int bar = (y / 64) * 4096; // Horizontal bars

                // Add some noise
                int noise = Random.Shared.Next(-100, 100);

                int value = gradient + bar + noise;
                value = Math.Clamp(value, 0, 65535);

                pixelData[y * width + x] = (ushort)value;
            }
        }

        // Convert to byte array (little-endian)
        var bytes = new byte[pixelData.Length * 2];
        Buffer.BlockCopy(pixelData, 0, bytes, 0, bytes.Length);

        return new ImageData
        {
            ImageId = imageId,
            PixelData = bytes,
            Width = width,
            Height = height,
            BitsPerPixel = 16,
            PixelSpacing = new PixelSpacing
            {
                RowSpacingMm = 0.5m,
                ColumnSpacingMm = 0.5m
            },
            CurrentWindowLevel = new WindowLevel
            {
                WindowCenter = 32768,
                WindowWidth = 65536
            }
        };
    }
}
