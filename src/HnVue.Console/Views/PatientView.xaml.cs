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

        // Subscribe to Unloaded event for disposal
        Loaded += (s, e) =>
        {
            Unloaded += OnUnloaded;
        };
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
