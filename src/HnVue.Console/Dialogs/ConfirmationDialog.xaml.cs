using System.Windows;

namespace HnVue.Console.Dialogs;

/// <summary>
/// Confirmation dialog for yes/no prompts.
/// SPEC-UI-001: FR-UI-08 System Configuration (confirmation dialogs).
/// </summary>
public partial class ConfirmationDialog : Window
{
    /// <summary>
    /// Initializes a new instance of <see cref="ConfirmationDialog"/>.
    /// </summary>
    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance with title and message.
    /// </summary>
    public ConfirmationDialog(string title, string message) : this()
    {
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
    }

    /// <summary>
    /// Shows a confirmation dialog.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message.</param>
    /// <returns>True if user confirmed, false otherwise.</returns>
    public static bool Show(Window owner, string title, string message)
    {
        var dialog = new ConfirmationDialog(title, message)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true;
    }

    private void OnYesClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnNoClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
