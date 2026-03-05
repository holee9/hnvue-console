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
    public AuditLogView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetService<ViewModels.AuditLogViewModel>();
        DataContext = viewModel;

        // Initialize async
        if (viewModel != null)
        {
            Loaded += async (s, e) => await viewModel.InitializeAsync();
            Unloaded += OnUnloaded;
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
