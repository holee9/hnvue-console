using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HnVue.Console.Converters;

/// <summary>
/// Converts SystemStatus enum to Brush for traffic light badge.
/// SPEC-UI-001: Value converter infrastructure.
/// </summary>
public class StatusToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ViewModels.SystemStatus status)
        {
            var app = System.Windows.Application.Current;
            var resources = app?.Resources;

            return status switch
            {
                ViewModels.SystemStatus.Healthy => resources?["SuccessBrush"] ?? new SolidColorBrush(Colors.Green),
                ViewModels.SystemStatus.Warning => resources?["WarningBrush"] ?? new SolidColorBrush(Colors.Yellow),
                ViewModels.SystemStatus.Error => resources?["ErrorBrush"] ?? new SolidColorBrush(Colors.Red),
                _ => resources?["SecondaryTextBrush"] ?? new SolidColorBrush(Colors.Gray),
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
