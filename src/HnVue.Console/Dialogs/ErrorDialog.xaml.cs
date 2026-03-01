using System.Windows;

namespace HnVue.Console.Dialogs;

/// <summary>
/// Error dialog for displaying error messages.
/// SPEC-UI-001: FR-UI-08 System Configuration (error dialogs).
/// </summary>
public partial class ErrorDialog : Window
{
    /// <summary>
    /// Initializes a new instance of <see cref="ErrorDialog"/>.
    /// </summary>
    public ErrorDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance with title and message.
    /// </summary>
    public ErrorDialog(string title, string message, string? details = null) : this()
    {
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        if (!string.IsNullOrEmpty(details))
        {
            DetailsTextBlock.Text = details;
            DetailsBorder.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Shows an error dialog.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The error message.</param>
    /// <param name="details">Optional error details.</param>
    public static void Show(Window owner, string title, string message, string? details = null)
    {
        var dialog = new ErrorDialog(title, message, details)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
