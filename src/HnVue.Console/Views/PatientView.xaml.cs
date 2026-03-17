using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace HnVue.Console.Views;

/// <summary>
/// Patient management view.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public partial class PatientView : UserControl
{
    public PatientView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.PatientViewModel>();
        DataContext = viewModel;

        // Subscribe to Loaded event to wire NavigationRequested after the visual tree is ready
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Handles the Loaded event to wire navigation and disposal.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Unloaded += OnUnloaded;

        // Wire PatientViewModel.NavigationRequested to ShellViewModel navigation
        if (DataContext is ViewModels.PatientViewModel patientVm)
        {
            var window = Window.GetWindow(this);
            if (window?.DataContext is ViewModels.ShellViewModel shellVm)
            {
                patientVm.NavigationRequested += (_, viewName) =>
                    shellVm.NavigateCommand.Execute(viewName);
            }
        }
    }

    /// <summary>
    /// Handles the Unloaded event to dispose the ViewModel.
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
