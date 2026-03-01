using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HnVue.Console.Views.Panels;

/// <summary>
/// Image viewer panel with measurement support.
/// SPEC-UI-001: FR-UI-03 Image Viewer with measurement overlay.
/// </summary>
public partial class ImageViewerPanel : UserControl
{
    public ImageViewerPanel()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.ImageReviewViewModel>();
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles canvas click for measurement point placement.
    /// </summary>
    private void OnCanvasClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.ImageReviewViewModel viewModel &&
            sender is Canvas canvas)
        {
            var position = e.GetPosition(canvas);
            viewModel.HandleMeasurementClick(position.X, position.Y);
        }
    }
}
