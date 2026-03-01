using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace HnVue.Console.Views.Panels;

/// <summary>
/// AEC (Automatic Exposure Control) toggle panel.
/// SPEC-UI-001: FR-UI-08 AEC Toggle.
/// </summary>
public partial class AECTogglePanel : UserControl
{
    public AECTogglePanel()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.AECViewModel>();
        DataContext = viewModel;
    }
}
