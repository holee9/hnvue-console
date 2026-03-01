using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace HnVue.Console.Views.Panels;

/// <summary>
/// QC action panel for image quality control.
/// SPEC-UI-001: FR-UI-05 Image Quality Control.
/// </summary>
public partial class QCActionPanel : UserControl
{
    public QCActionPanel()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.ImageReviewViewModel>();
        DataContext = viewModel;
    }
}
