using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using HnVue.Console.Models;

namespace HnVue.Console.Dialogs;

/// <summary>
/// Patient edit dialog.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public partial class PatientEditDialog : Window
{
    public PatientEditDialog()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.PatientEditViewModel>();
        DataContext = viewModel;

        // Close dialog when save succeeds
        if (viewModel != null)
        {
            viewModel.EditCompleted += (s, e) => Close();
        }
    }

    /// <summary>
    /// Shows the dialog for editing a patient and returns true if edit was successful.
    /// </summary>
    public bool ShowDialog(Window owner, Patient patient)
    {
        Owner = owner;

        if (DataContext is ViewModels.PatientEditViewModel viewModel)
        {
            viewModel.LoadPatient(patient);
        }

        return ShowDialog() == true;
    }
}
