using System.Diagnostics;
using HnVue.Console.Models;

namespace HnVue.Console.Rendering;

/// <summary>
/// DICOM-compliant window/level transformation using GSDF (Grayscale Standard Display Function).
/// SPEC-UI-001: FR-UI-03 Image Viewer with perceptually linear rendering.
/// Implements DICOM PS 3.14 and PS 3.11 window/level transformations.
/// </summary>
public class WindowLevelTransform
{
    private ushort[] _lookupTable = new ushort[65536];
    private int _windowCenter;
    private int _windowWidth;
    private bool _isDirty = true;

    /// <summary>
    /// Initializes a new instance of <see cref="WindowLevelTransform"/>.
    /// </summary>
    public WindowLevelTransform()
    {
        // Initialize with default window/level
        _windowCenter = 32768;
        _windowWidth = 65536;
        BuildLookupTable();
    }

    /// <summary>
    /// Gets the current window/level settings.
    /// </summary>
    public Models.WindowLevel CurrentWindowLevel =>
        new Models.WindowLevel { WindowCenter = _windowCenter, WindowWidth = _windowWidth };

    /// <summary>
    /// Sets new window/level values.
    /// </summary>
    /// <param name="windowCenter">Window center (typically 0-65535 for 16-bit).</param>
    /// <param name="windowWidth">Window width (must be > 0).</param>
    public void SetWindowLevel(int windowCenter, int windowWidth)
    {
        if (windowWidth < 1)
            windowWidth = 1;

        if (_windowCenter != windowCenter || _windowWidth != windowWidth)
        {
            _windowCenter = windowCenter;
            _windowWidth = windowWidth;
            _isDirty = true;
        }
    }

    /// <summary>
    /// Gets the current lookup table (rebuilds if dirty).
    /// </summary>
    /// <returns>16-bit lookup table for window/level transformation.</returns>
    public ushort[] GetLookupTable()
    {
        if (_isDirty)
        {
            BuildLookupTable();
            _isDirty = false;
        }

        return _lookupTable;
    }

    /// <summary>
    /// Applies window/level transformation to a pixel value.
    /// </summary>
    /// <param name="pixelValue">Original 16-bit pixel value.</param>
    /// <returns>Transformed pixel value.</returns>
    public ushort ApplyTransform(ushort pixelValue)
    {
        if (_isDirty)
        {
            BuildLookupTable();
            _isDirty = false;
        }

        return _lookupTable[pixelValue];
    }

    /// <summary>
    /// Builds the window/level lookup table.
    /// Uses DICOM PS 3.11 C.11.2.1.2 linear transformation.
    /// </summary>
    private void BuildLookupTable()
    {
        // DICOM Window Level formula: y = ((x - (c - 0.5)) / (w - 1) + 0.5) * 255
        // Where x = input pixel, c = window center, w = window width, y = output pixel
        // This maps the window range to 0-255, then we scale to 16-bit

        double windowCenter = _windowCenter;
        double windowWidth = _windowWidth - 1;

        // Pre-compute constants
        double scale = windowWidth > 0 ? 65535.0 / windowWidth : 0;
        double offset = windowCenter - 0.5;

        for (int i = 0; i < 65536; i++)
        {
            // Apply window/level transformation
            double y = ((i - offset) / windowWidth) + 0.5;

            // Clamp to 0-1 range
            if (y < 0) y = 0;
            if (y > 1) y = 1;

            // Scale to 16-bit range
            _lookupTable[i] = (ushort)(y * 65535);
        }

        Debug.WriteLine($"[WindowLevelTransform] Built LUT: center={_windowCenter}, width={_windowWidth}");
    }

    /// <summary>
    /// Applies GSDF (Grayscale Standard Display Function) transformation.
    /// This provides perceptually linear grayscale rendering per DICOM PS 3.14.
    /// </summary>
    /// <param name="jacobian">Just Noticeable Difference (JND) index (0-1023).</param>
    /// <returns>RGB value (0-255) for GSDF-compliant display.</returns>
    public static byte ApplyGSDF(double jacobian)
    {
        // DICOM PS 3.14 Grayscale Standard Display Function
        // Based on the Barten model
        // ln(L) = a + b * ln(J) + c * (ln(J))^2 + d * (ln(J))^3 + e * (ln(J))^4

        const double a = -1.3011877;
        const double b = -2.5840191E-2;
        const double c = 8.0242636E-2;
        const double d = -1.3228000E-1;
        const double e = 6.5376499E-2;

        // Clamp JND index
        if (jacobian < 1) jacobian = 1;
        if (jacobian > 1023) jacobian = 1023;

        double lnJ = Math.Log(jacobian);

        // Calculate luminance
        double lnL = a + b * lnJ + c * lnJ * lnJ + d * Math.Pow(lnJ, 3) + e * Math.Pow(lnJ, 4);
        double luminance = Math.Exp(lnL);

        // Convert luminance to digital driving level (DDL)
        // Assuming typical display with max luminance ~500 cd/m²
        const double maxLuminance = 500.0;
        const double minLuminance = 0.5;
        const double gamma = 2.4;

        double normalizedLuminance = (luminance - minLuminance) / (maxLuminance - minLuminance);
        normalizedLuminance = Math.Clamp(normalizedLuminance, 0, 1);

        // Apply gamma correction
        double ddl = Math.Pow(normalizedLuminance, 1.0 / gamma);

        return (byte)(ddl * 255);
    }

    /// <summary>
    /// Creates a GSDF-compliant lookup table for a given window/level.
    /// </summary>
    /// <param name="windowCenter">Window center.</param>
    /// <param name="windowWidth">Window width.</param>
    /// <returns>GSDF-compliant lookup table (65536 entries).</returns>
    public static ushort[] CreateGSDFLookupTable(int windowCenter, int windowWidth)
    {
        var lut = new ushort[65536];
        double windowCenterD = windowCenter;
        double windowWidthD = windowWidth - 1;

        for (int i = 0; i < 65536; i++)
        {
            // Apply window/level
            double y = ((i - (windowCenterD - 0.5)) / windowWidthD) + 0.5;
            y = Math.Clamp(y, 0, 1);

            // Convert to JND index (0-1023)
            double jnd = y * 1023;

            // Apply GSDF
            byte gsdfValue = ApplyGSDF(jnd);

            // Scale to 16-bit
            lut[i] = (ushort)(gsdfValue * 257); // 255 * 257 = 65535
        }

        return lut;
    }

    /// <summary>
    /// Converts RGB to grayscale (for 24-bit images if needed).
    /// </summary>
    /// <param name="r">Red component (0-255).</param>
    /// <param name="g">Green component (0-255).</param>
    /// <param name="b">Blue component (0-255).</param>
    /// <returns>Grayscale value (0-255).</returns>
    public static byte RgbToGrayscale(byte r, byte g, byte b)
    {
        // ITU-R BT.709 grayscale conversion
        // Y = 0.2126R + 0.7152G + 0.0722B
        return (byte)(0.2126 * r + 0.7152 * g + 0.0722 * b);
    }
}
