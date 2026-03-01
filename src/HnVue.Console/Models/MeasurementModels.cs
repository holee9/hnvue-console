namespace HnVue.Console.Models;

/// <summary>
/// Measurement overlay data.
/// SPEC-UI-001: FR-UI-04 Measurement Tools.
/// </summary>
public record MeasurementOverlay
{
    public required string MeasurementId { get; init; }
    public required string ImageId { get; init; }
    public required MeasurementType Type { get; init; }
    public required IReadOnlyList<Point> Points { get; init; }
    public required string DisplayValue { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? Annotation { get; init; }
}

/// <summary>
/// Measurement type enumeration.
/// </summary>
public enum MeasurementType
{
    Distance,
    Angle,
    CobbAngle,
    Annotation
}

/// <summary>
/// 2D point for measurement.
/// </summary>
public record Point
{
    public required double X { get; init; }
    public required double Y { get; init; }
}

/// <summary>
/// Distance measurement result.
/// </summary>
public record DistanceMeasurement
{
    public required double DistanceMm { get; init; }
    public required Point StartPoint { get; init; }
    public required Point EndPoint { get; init; }
}

/// <summary>
/// Angle measurement result.
/// </summary>
public record AngleMeasurement
{
    public required double AngleDegrees { get; init; }
    public required Point VertexPoint { get; init; }
    public required Point Arm1EndPoint { get; init; }
    public required Point Arm2EndPoint { get; init; }
}

/// <summary>
/// Cobb angle measurement result.
/// </summary>
public record CobbAngleMeasurement
{
    public required double CobbAngleDegrees { get; init; }
    public required Line UpperEndplateLine { get; init; }
    public required Line LowerEndplateLine { get; init; }
}

/// <summary>
/// Line defined by two points.
/// </summary>
public record Line
{
    public required Point Start { get; init; }
    public required Point End { get; init; }
}
