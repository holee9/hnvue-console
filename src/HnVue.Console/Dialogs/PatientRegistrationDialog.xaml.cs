using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace HnVue.Console.Dialogs;

/// <summary>
/// Patient registration dialog.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public partial class PatientRegistrationDialog : Window
{
    public PatientRegistrationDialog()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.PatientRegistrationViewModel>();
        DataContext = viewModel;

        // Close dialog when registration succeeds
        if (viewModel != null)
        {
            viewModel.RegistrationCompleted += (s, e) => Close();
        }
    }

    /// <summary>
    /// Shows the dialog and returns true if registration was successful.
    /// </summary>
    public bool ShowDialog(Window owner)
    {
        Owner = owner;
        return ShowDialog() == true;
    }
}
