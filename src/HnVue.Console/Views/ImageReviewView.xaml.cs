using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace HnVue.Console.Views;

/// <summary>
/// Image review screen view.
/// SPEC-UI-001: FR-UI-03, 04, 05 Image Viewer, Measurement, QC.
/// </summary>
public partial class ImageReviewView : UserControl
{
    public ImageReviewView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.ImageReviewViewModel>();
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the Loaded event to initialize with a test image.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ImageReviewViewModel viewModel)
        {
            // Load a test image ID
            await viewModel.LoadImageAsync("TEST_IMAGE_001");
        }
    }
}
