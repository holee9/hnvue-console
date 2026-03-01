using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace HnVue.Console.Views;

/// <summary>
/// Configuration view.
/// SPEC-UI-001: FR-UI-08 System Configuration.
/// </summary>
public partial class ConfigurationView : UserControl
{
    public ConfigurationView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetService<ViewModels.ConfigurationViewModel>();
        DataContext = viewModel;

        // Initialize async
        if (viewModel != null)
        {
            Loaded += async (s, e) => await viewModel.InitializeAsync();
        }
    }
}
