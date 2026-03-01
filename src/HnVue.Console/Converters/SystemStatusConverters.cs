using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace HnVue.Console.Converters;

/// <summary>
/// Converts ComponentHealth enum to Brush.
/// SPEC-UI-001: Value converter infrastructure.
/// </summary>
public class ComponentHealthToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.ComponentHealth health)
        {
            var app = System.Windows.Application.Current;
            var resources = app?.Resources;

            return health switch
            {
                Models.ComponentHealth.Healthy => resources?["SuccessBrush"] ?? new SolidColorBrush(Color.FromRgb(0x28, 0xA7, 0x45)),
                Models.ComponentHealth.Degraded => resources?["WarningBrush"] ?? new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
                Models.ComponentHealth.Error => resources?["ErrorBrush"] ?? new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45)),
                Models.ComponentHealth.Offline => resources?["SecondaryTextBrush"] ?? new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D)),
                _ => resources?["DisabledTextBrush"] ?? new SolidColorBrush(Color.FromRgb(0xAD, 0xB5, 0xBD)),
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ComponentHealth enum to display string.
/// SPEC-UI-001: Value converter infrastructure.
/// </summary>
public class ComponentHealthToStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.ComponentHealth health)
        {
            return health switch
            {
                Models.ComponentHealth.Healthy => "Healthy",
                Models.ComponentHealth.Degraded => "Degraded",
                Models.ComponentHealth.Error => "Error",
                Models.ComponentHealth.Offline => "Offline",
                Models.ComponentHealth.Unknown => "Unknown",
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
/// Converts ComponentId (ComponentType) to display name.
/// SPEC-UI-001: Value converter infrastructure.
/// </summary>
public class ComponentIdToNameConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string componentId)
        {
            return componentId switch
            {
                "XrayGenerator" => "X-Ray Generator",
                "Detector" => "Detector",
                "Collimator" => "Collimator",
                "Network" => "Network",
                "DicomService" => "DICOM Service",
                "CoreEngine" => "Core Engine",
                "DoseService" => "Dose Service",
                _ => componentId
            };
        }

        return value?.ToString() ?? string.Empty;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts alerts array to display string.
/// SPEC-UI-001: Value converter infrastructure.
/// </summary>
public class AlertsToStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string[] alerts && alerts.Length > 0)
        {
            return alerts.Length == 1
                ? $"Alert: {alerts[0]}"
                : $"Alerts ({alerts.Length}): {string.Join("; ", alerts.Take(2))}...";
        }

        return "No active alerts";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to success/error brush.
/// SPEC-UI-001: Value converter infrastructure.
/// </summary>
public class BoolToSuccessBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue)
        {
            var app = System.Windows.Application.Current;
            return app?.Resources["SuccessBrush"] ?? new SolidColorBrush(Colors.Green);
        }

        var resources = System.Windows.Application.Current?.Resources;
        return resources?["ErrorBrush"] ?? new SolidColorBrush(Colors.Red);
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts AuditOutcome to brush.
/// SPEC-UI-001: Value converter infrastructure.
/// </summary>
public class OutcomeToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.AuditOutcome outcome)
        {
            var app = System.Windows.Application.Current;
            var resources = app?.Resources;

            return outcome switch
            {
                Models.AuditOutcome.Success => resources?["SuccessBrush"] ?? new SolidColorBrush(Colors.Green),
                Models.AuditOutcome.Warning => resources?["WarningBrush"] ?? new SolidColorBrush(Colors.Yellow),
                Models.AuditOutcome.Failure => resources?["ErrorBrush"] ?? new SolidColorBrush(Colors.Red),
                _ => resources?["DisabledTextBrush"] ?? new SolidColorBrush(Colors.Gray),
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
