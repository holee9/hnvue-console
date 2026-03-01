using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace HnVue.Console.Views.Panels;

/// <summary>
/// Exposure parameter control panel.
/// SPEC-UI-001: FR-UI-07 Exposure Parameter Display.
/// </summary>
public partial class ExposureParameterPanel : UserControl
{
    public ExposureParameterPanel()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.ExposureParameterViewModel>();
        DataContext = viewModel;
    }
}
