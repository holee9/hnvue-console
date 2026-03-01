using System.Collections.Concurrent;
using System.Diagnostics;
using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Service for managing measurement overlays on images.
/// SPEC-UI-001: FR-UI-04 Measurement Tools with session-scoped overlays.
/// </summary>
public class MeasurementOverlayService
{
    private readonly ConcurrentDictionary<string, List<MeasurementOverlay>> _imageOverlays = new();

    /// <summary>
    /// Gets all measurements for an image.
    /// </summary>
    public List<MeasurementOverlay> GetMeasurements(string imageId)
    {
        return _imageOverlays.GetOrAdd(imageId, _ => new List<MeasurementOverlay>()).ToList();
    }

    /// <summary>
    /// Adds a measurement to an image.
    /// </summary>
    public MeasurementOverlay AddMeasurement(string imageId, MeasurementType type, IReadOnlyList<Point> points, string displayValue, string? annotation = null)
    {
        var measurement = new MeasurementOverlay
        {
            MeasurementId = Guid.NewGuid().ToString(),
            ImageId = imageId,
            Type = type,
            Points = points,
            DisplayValue = displayValue,
            CreatedAt = DateTime.UtcNow,
            Annotation = annotation
        };

        _imageOverlays.AddOrUpdate(imageId,
            _ => new List<MeasurementOverlay> { measurement },
            (_, existing) =>
            {
                existing.Add(measurement);
                return existing;
            });

        Debug.WriteLine($"[MeasurementOverlayService] Added {type} to {imageId}: {displayValue}");
        return measurement;
    }

    /// <summary>
    /// Removes a measurement by ID.
    /// </summary>
    public bool RemoveMeasurement(string measurementId)
    {
        foreach (var kvp in _imageOverlays)
        {
            var removed = kvp.Value.RemoveAll(m => m.MeasurementId == measurementId);
            if (removed > 0)
            {
                Debug.WriteLine($"[MeasurementOverlayService] Removed measurement {measurementId}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears all measurements for an image.
    /// </summary>
    public void ClearMeasurements(string imageId)
    {
        if (_imageOverlays.TryRemove(imageId, out var measurements))
        {
            Debug.WriteLine($"[MeasurementOverlayService] Cleared {measurements.Count} measurements from {imageId}");
        }
    }

    /// <summary>
    /// Calculates distance between two points in mm.
    /// </summary>
    public double CalculateDistance(Point start, Point end, PixelSpacing spacing)
    {
        double dx = (end.X - start.X) * (double)spacing.ColumnSpacingMm;
        double dy = (end.Y - start.Y) * (double)spacing.RowSpacingMm;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Calculates angle between three points in degrees.
    /// </summary>
    public double CalculateAngle(Point vertex, Point arm1End, Point arm2End)
    {
        // Calculate vectors from vertex
        double v1x = arm1End.X - vertex.X;
        double v1y = arm1End.Y - vertex.Y;
        double v2x = arm2End.X - vertex.X;
        double v2y = arm2End.Y - vertex.Y;

        // Calculate dot product and magnitudes
        double dot = v1x * v2x + v1y * v2y;
        double mag1 = Math.Sqrt(v1x * v1x + v1y * v1y);
        double mag2 = Math.Sqrt(v2x * v2x + v2y * v2y);

        if (mag1 < 0.001 || mag2 < 0.001)
            return 0;

        // Calculate angle in radians
        double angleRad = Math.Acos(dot / (mag1 * mag2));

        // Convert to degrees
        return angleRad * 180.0 / Math.PI;
    }

    /// <summary>
    /// Calculates Cobb angle from four points.
    /// </summary>
    public double CalculateCobbAngle(Point upperStart, Point upperEnd, Point lowerStart, Point lowerEnd)
    {
        // Calculate angle of upper endplate line
        double upperAngle = Math.Atan2(upperEnd.Y - upperStart.Y, upperEnd.X - upperStart.X) * 180.0 / Math.PI;

        // Calculate angle of lower endplate line
        double lowerAngle = Math.Atan2(lowerEnd.Y - lowerStart.Y, lowerEnd.X - lowerStart.X) * 180.0 / Math.PI;

        // Cobb angle is the difference
        double cobbAngle = Math.Abs(upperAngle - lowerAngle);

        // Ensure angle is between 0 and 180
        if (cobbAngle > 180)
            cobbAngle = 360 - cobbAngle;

        return cobbAngle;
    }
}
