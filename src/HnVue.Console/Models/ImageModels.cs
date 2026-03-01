namespace HnVue.Console.Models;

/// <summary>
/// Medical image data.
/// SPEC-UI-001: FR-UI-03 Image Viewer.
/// </summary>
public record ImageData
{
    public required string ImageId { get; init; }
    public required byte[] PixelData { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int BitsPerPixel { get; init; }
    public required PixelSpacing PixelSpacing { get; init; }
    public WindowLevel? CurrentWindowLevel { get; init; }
    public ImageOrientation Orientation { get; init; } = ImageOrientation.None;
}

/// <summary>
/// Pixel spacing for measurement conversion.
/// </summary>
public record PixelSpacing
{
    public required decimal RowSpacingMm { get; init; }
    public required decimal ColumnSpacingMm { get; init; }
}

/// <summary>
/// Window/Level transformation parameters.
/// </summary>
public record WindowLevel
{
    public required int WindowCenter { get; init; }
    public required int WindowWidth { get; init; }
}

/// <summary>
/// Zoom and pan parameters.
/// </summary>
public record ZoomPan
{
    public required double ZoomFactor { get; init; }
    public required double PanX { get; init; }
    public required double PanY { get; init; }
}

/// <summary>
/// Image orientation transformation.
/// </summary>
public enum ImageOrientation
{
    None,
    Rotate90,
    Rotate180,
    Rotate270,
    FlipHorizontal,
    FlipVertical
}

/// <summary>
/// Image transformation request.
/// </summary>
public record ImageTransform
{
    public WindowLevel? WindowLevel { get; init; }
    public ZoomPan? ZoomPan { get; init; }
    public ImageOrientation Orientation { get; init; }
}
