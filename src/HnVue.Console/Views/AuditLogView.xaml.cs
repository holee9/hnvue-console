using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace HnVue.Console.Views;

/// <summary>
/// Audit log view.
/// SPEC-UI-001: FR-UI-13 Audit Log Viewer.
/// </summary>
public partial class AuditLogView : UserControl
{
    private ViewModels.AuditLogViewModel? _viewModel;

    public AuditLogView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        _viewModel = App.ServiceProvider?.GetService<ViewModels.AuditLogViewModel>();
        DataContext = _viewModel;
    }

    /// <summary>
    /// Handles the Loaded event to initialize the view.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.InitializeAsync();
        }
    }

    /// <summary>
    /// Handles the Unloaded event to dispose the ViewModel.
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
