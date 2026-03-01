using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace HnVue.Console.Views.Panels;

/// <summary>
/// Measurement tool panel.
/// SPEC-UI-001: FR-UI-04 Measurement Tools.
/// </summary>
public partial class MeasurementToolPanel : UserControl
{
    public MeasurementToolPanel()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.ImageReviewViewModel>();
        DataContext = viewModel;
    }
}
