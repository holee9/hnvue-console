using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HnVue.Console.Models;

namespace HnVue.Console.Converters;

/// <summary>
/// Converts FocalSpotSize enum to display string.
/// SPEC-UI-001: Value converter for exposure parameters.
/// </summary>
public class FocalSpotSizeToStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FocalSpotSize spotSize)
        {
            return spotSize switch
            {
                FocalSpotSize.Small => "Small (0.6 mm)",
                FocalSpotSize.Large => "Large (1.2 mm)",
                _ => "Unknown"
            };
        }

        return "Unknown";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts AEC enabled state to brush (green for active, gray for inactive).
/// SPEC-UI-001: Value converter for AEC status.
/// </summary>
public class AecModeToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isAecEnabled)
        {
            return isAecEnabled
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))  // Green for active
                : new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Gray for inactive
        }

        return new SolidColorBrush(Color.FromRgb(107, 114, 128));
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts AEC enabled state to status text.
/// SPEC-UI-001: Value converter for AEC status.
/// </summary>
public class AecStatusToStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isAecEnabled)
        {
            return isAecEnabled ? "ACTIVE" : "INACTIVE";
        }

        return "INACTIVE";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts AEC enabled state to description.
/// SPEC-UI-001: Value converter for AEC status.
/// </summary>
public class AecModeToDescriptionConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isAecEnabled)
        {
            return isAecEnabled
                ? "AEC is controlling exposure parameters automatically"
                : "Exposure parameters are set manually";
        }

        return "Exposure parameters are set manually";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts dose alert level to brush.
/// SPEC-UI-001: Value converter for dose alerts.
/// </summary>
public class DoseAlertLevelToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string alertLevel)
        {
            return alertLevel.ToUpperInvariant() switch
            {
                "ERROR" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),    // Red
                "WARNING" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // Amber
                _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))         // Gray
            };
        }

        return new SolidColorBrush(Color.FromRgb(107, 114, 128));
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts boolean value (true -> false, false -> true).
/// SPEC-UI-001: Value converter for enabling/disabling controls.
/// </summary>
public class BoolToInverseConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        return true;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        return false;
    }
}
