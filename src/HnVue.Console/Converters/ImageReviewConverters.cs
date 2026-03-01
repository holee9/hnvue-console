using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HnVue.Console.Models;
using Point = HnVue.Console.Models.Point;

namespace HnVue.Console.Converters;

/// <summary>
/// Converts zoom factor to ScaleTransform.
/// SPEC-UI-001: Value converter for image zoom.
/// </summary>
public class ZoomTransformConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double zoom)
        {
            return new ScaleTransform(zoom, zoom);
        }

        return new ScaleTransform(1, 1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ImageOrientation to RotateTransform.
/// SPEC-UI-001: Value converter for image orientation.
/// </summary>
public class OrientationTransformConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is ImageOrientation orientation)
        {
            double angle = orientation switch
            {
                ImageOrientation.Rotate90 => 90,
                ImageOrientation.Rotate180 => 180,
                ImageOrientation.Rotate270 => 270,
                _ => 0
            };

            return new RotateTransform(angle);
        }

        return new RotateTransform(0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ImageOrientation to display string.
/// SPEC-UI-001: Value converter for orientation display.
/// </summary>
public class OrientationToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ImageOrientation orientation)
        {
            return orientation switch
            {
                ImageOrientation.None => "None",
                ImageOrientation.Rotate90 => "90° CW",
                ImageOrientation.Rotate180 => "180°",
                ImageOrientation.Rotate270 => "270° CW",
                ImageOrientation.FlipHorizontal => "Flip H",
                ImageOrientation.FlipVertical => "Flip V",
                _ => "Unknown"
            };
        }

        return "None";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts measurement points to polyline points string.
/// SPEC-UI-001: Value converter for measurement rendering.
/// </summary>
public class PointsToPolylineConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IReadOnlyList<Point> points)
        {
            return string.Join(" ", points.Select(p => $"{p.X},{p.Y}"));
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts point to canvas-centered position (for circle center).
/// SPEC-UI-001: Value converter for measurement markers.
/// </summary>
public class CenterCanvasConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return d - 4; // 4 is half of ellipse width/height
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts point with offset for canvas positioning.
/// SPEC-UI-001: Value converter for measurement labels.
/// </summary>
public class OffsetCanvasConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && parameter is string offsetStr && int.TryParse(offsetStr, out int offset))
        {
            return d + offset;
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts MeasurementType to user instructions.
/// SPEC-UI-001: Value converter for measurement tool instructions.
/// </summary>
public class MeasurementTypeToInstructionsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MeasurementType type)
        {
            return type switch
            {
                MeasurementType.Distance => "Click start point, then end point to measure distance.",
                MeasurementType.Angle => "Click vertex, then two end points to measure angle.",
                MeasurementType.CobbAngle => "Click upper endplate start/end, then lower endplate start/end.",
                MeasurementType.Annotation => "Click to add text annotation.",
                _ => "Select a measurement tool to begin."
            };
        }

        return "Select a measurement tool to begin.";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts RejectionReason to display string.
/// SPEC-UI-001: Value converter for rejection reasons.
/// </summary>
public class RejectionReasonToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RejectionReason reason)
        {
            return reason switch
            {
                RejectionReason.PatientMotion => "Patient Motion",
                RejectionReason.ExposureError => "Exposure Error",
                RejectionReason.PositioningError => "Positioning Error",
                RejectionReason.Artifact => "Artifact",
                RejectionReason.EquipmentMalfunction => "Equipment Malfunction",
                RejectionReason.WrongProtocol => "Wrong Protocol",
                RejectionReason.Duplicate => "Duplicate",
                RejectionReason.Other => "Other",
                _ => "Unknown"
            };
        }

        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
