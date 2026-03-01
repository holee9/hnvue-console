using System.Globalization;
using System.Windows.Data;

namespace HnVue.Console.Converters;

/// <summary>
/// Converts SystemStatus enum to display string.
/// SPEC-UI-001: Value converter infrastructure.
/// </summary>
public class StatusToStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ViewModels.SystemStatus status)
        {
            return status switch
            {
                ViewModels.SystemStatus.Healthy => "OK",
                ViewModels.SystemStatus.Warning => "WARN",
                ViewModels.SystemStatus.Error => "ERR",
                _ => "UNKNOWN"
            };
        }

        return "UNKNOWN";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
