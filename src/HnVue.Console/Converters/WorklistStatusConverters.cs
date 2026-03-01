using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HnVue.Console.Converters;

/// <summary>
/// Converts WorklistStatus enum to Brush for status display.
/// SPEC-UI-001: Value converter for WorklistStatus.
/// </summary>
public class WorklistStatusToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.WorklistStatus status)
        {
            var app = System.Windows.Application.Current;
            var resources = app?.Resources;

            return status switch
            {
                Models.WorklistStatus.Scheduled => resources?["InfoBrush"] ?? new SolidColorBrush(Colors.Blue),
                Models.WorklistStatus.InProgress => resources?["WarningBrush"] ?? new SolidColorBrush(Colors.Yellow),
                Models.WorklistStatus.Completed => resources?["SuccessBrush"] ?? new SolidColorBrush(Colors.Green),
                Models.WorklistStatus.Cancelled => resources?["ErrorBrush"] ?? new SolidColorBrush(Colors.Red),
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

/// <summary>
/// Converts WorklistStatus enum to display string.
/// SPEC-UI-001: Value converter for WorklistStatus.
/// </summary>
public class WorklistStatusToStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.WorklistStatus status)
        {
            return status switch
            {
                Models.WorklistStatus.Scheduled => "SCHD",
                Models.WorklistStatus.InProgress => "ACTV",
                Models.WorklistStatus.Completed => "DONE",
                Models.WorklistStatus.Cancelled => "CNCL",
                _ => "UNKN"
            };
        }

        return "UNKN";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
