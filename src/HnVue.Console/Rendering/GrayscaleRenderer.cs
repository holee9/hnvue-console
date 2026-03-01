using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HnVue.Console.Models;

namespace HnVue.Console.Rendering;

/// <summary>
/// Renders 16-bit grayscale DICOM images to WPF WriteableBitmap.
/// SPEC-UI-001: FR-UI-03 Image Viewer with high-performance rendering.
/// </summary>
public class GrayscaleRenderer
{
    private readonly WindowLevelTransform _windowLevelTransform;
    private WriteableBitmap? _bitmap;
    private int _width;
    private int _height;
    private const int BitsPerPixel = 16;

    /// <summary>
    /// Initializes a new instance of <see cref="GrayscaleRenderer"/>.
    /// </summary>
    public GrayscaleRenderer()
    {
        _windowLevelTransform = new WindowLevelTransform();
    }

    /// <summary>
    /// Gets the current WriteableBitmap.
    /// </summary>
    public WriteableBitmap? Bitmap => _bitmap;

    /// <summary>
    /// Gets the current window/level settings.
    /// </summary>
    public WindowLevel CurrentWindowLevel => _windowLevelTransform.CurrentWindowLevel;

    /// <summary>
    /// Initializes the renderer for a specific image size.
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    public void Initialize(int width, int height)
    {
        if (_bitmap == null || _width != width || _height != height)
        {
            _width = width;
            _height = height;

            // Create Gray16 WriteableBitmap for 16-bit grayscale
            _bitmap = new WriteableBitmap(
                width, height,
                96, 96,
                PixelFormats.Gray16,
                null);

            Debug.WriteLine($"[GrayscaleRenderer] Initialized {width}x{height} Gray16 bitmap");
        }
    }

    /// <summary>
    /// Renders 16-bit pixel data with current window/level applied.
    /// </summary>
    /// <param name="pixelData">16-bit grayscale pixel data (little-endian).</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="windowCenter">Window center for W/L transform.</param>
    /// <param name="windowWidth">Window width for W/L transform.</param>
    public unsafe void Render(ushort[] pixelData, int width, int height, int windowCenter, int windowWidth)
    {
        if (pixelData.Length != width * height)
        {
            throw new ArgumentException($"Pixel data length {pixelData.Length} does not match image size {width}x{height}");
        }

        Initialize(width, height);

        // Update window/level transform
        _windowLevelTransform.SetWindowLevel(windowCenter, windowWidth);
        var lut = _windowLevelTransform.GetLookupTable();

        // Get bitmap back buffer
        var bitmap = _bitmap!;
        bitmap.Lock();

        try
        {
            var stride = width * 2; // 2 bytes per pixel for Gray16
            var bufferPtr = (ushort*)bitmap.BackBuffer.ToPointer();

            // Apply window/level and write to bitmap
            fixed (ushort* sourcePtr = pixelData)
            {
                for (int i = 0; i < pixelData.Length; i++)
                {
                    // Apply window/level LUT transformation
                    bufferPtr[i] = lut[sourcePtr[i]];
                }
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
        }

        Debug.WriteLine($"[GrayscaleRenderer] Rendered {width}x{height} with W/L ({windowCenter}/{windowWidth})");
    }

    /// <summary>
    /// Renders byte pixel data (8 or 16-bit) with current window/level applied.
    /// </summary>
    /// <param name="pixelData">Pixel data as byte array.</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="bitsPerPixel">Bits per pixel (8 or 16).</param>
    /// <param name="windowCenter">Window center for W/L transform.</param>
    /// <param name="windowWidth">Window width for W/L transform.</param>
    public unsafe void Render(byte[] pixelData, int width, int height, int bitsPerPixel, int windowCenter, int windowWidth)
    {
        Initialize(width, height);

        // Update window/level transform
        _windowLevelTransform.SetWindowLevel(windowCenter, windowWidth);
        var lut = _windowLevelTransform.GetLookupTable();

        var bitmap = _bitmap!;
        bitmap.Lock();

        try
        {
            var stride = width * 2; // 2 bytes per pixel for Gray16
            var bufferPtr = (ushort*)bitmap.BackBuffer.ToPointer();

            fixed (byte* sourcePtr = pixelData)
            {
                if (bitsPerPixel == 16)
                {
                    // 16-bit data - treat as ushort array
                    var ushortPtr = (ushort*)sourcePtr;

                    for (int i = 0; i < width * height; i++)
                    {
                        // Apply window/level LUT transformation
                        bufferPtr[i] = lut[ushortPtr[i]];
                    }
                }
                else if (bitsPerPixel == 8)
                {
                    // 8-bit data - scale to 16-bit
                    for (int i = 0; i < width * height; i++)
                    {
                        // Scale 8-bit (0-255) to 16-bit range (0-65535)
                        bufferPtr[i] = lut[sourcePtr[i] * 256];
                    }
                }
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
        }

        Debug.WriteLine($"[GrayscaleRenderer] Rendered {width}x{height} @ {bitsPerPixel}bpp with W/L ({windowCenter}/{windowWidth})");
    }

    /// <summary>
    /// Updates the window/level setting.
    /// </summary>
    /// <param name="windowCenter">New window center.</param>
    /// <param name="windowWidth">New window width.</param>
    public void SetWindowLevel(int windowCenter, int windowWidth)
    {
        _windowLevelTransform.SetWindowLevel(windowCenter, windowWidth);
        Debug.WriteLine($"[GrayscaleRenderer] W/L updated: {windowCenter}/{windowWidth}");
    }

    /// <summary>
    /// Gets the pixel value at a specific location.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>The pixel value at the specified location.</returns>
    public unsafe ushort GetPixelValue(int x, int y)
    {
        if (_bitmap == null || x < 0 || x >= _width || y < 0 || y >= _height)
            return 0;

        _bitmap.Lock();

        try
        {
            var ptr = (ushort*)_bitmap.BackBuffer.ToPointer();
            var stride = _width * 2;
            return ptr[y * stride / 2 + x];
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    /// <summary>
    /// Computes optimal window/level for the given pixel data.
    /// </summary>
    /// <param name="pixelData">16-bit pixel data.</param>
    /// <returns>Optimal window/level values.</returns>
    public (int center, int width) ComputeOptimalWindowLevel(ushort[] pixelData)
    {
        if (pixelData.Length == 0)
            return (32768, 65536); // Default for empty data

        // Compute histogram for efficient min/max with clipping
        const int histogramBins = 256;
        var histogram = new int[histogramBins];

        // Build histogram
        foreach (var pixel in pixelData)
        {
            int bin = pixel >> 8; // Use upper 8 bits for histogram
            histogram[bin]++;
        }

        // Clip 1% from each end
        int clipCount = pixelData.Length / 100;
        int cumulative = 0;
        int minBin = 0;
        int maxBin = histogramBins - 1;

        // Find minimum
        for (int i = 0; i < histogramBins; i++)
        {
            cumulative += histogram[i];
            if (cumulative > clipCount)
            {
                minBin = i;
                break;
            }
        }

        // Find maximum
        cumulative = 0;
        for (int i = histogramBins - 1; i >= 0; i--)
        {
            cumulative += histogram[i];
            if (cumulative > clipCount)
            {
                maxBin = i;
                break;
            }
        }

        // Convert to pixel values
        int minValue = minBin << 8;
        int maxValue = (maxBin << 8) | 0xFF;

        // Calculate window/level
        int width = maxValue - minValue;
        int center = minValue + width / 2;

        if (width < 1)
            width = 1;

        Debug.WriteLine($"[GrayscaleRenderer] Optimal W/L: {center}/{width} (range: {minValue}-{maxValue})");

        return (center, width);
    }
}
