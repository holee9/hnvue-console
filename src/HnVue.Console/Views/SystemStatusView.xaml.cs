using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace HnVue.Console.Views;

/// <summary>
/// System status view.
/// SPEC-UI-001: FR-UI-12 System Status Dashboard.
/// </summary>
public partial class SystemStatusView : UserControl
{
    public SystemStatusView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetService<ViewModels.SystemStatusViewModel>();
        DataContext = viewModel;

        // Initialize async
        if (viewModel != null)
        {
            Loaded += async (s, e) => await viewModel.InitializeAsync();
        }
    }
}
