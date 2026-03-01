using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace HnVue.Console.Views;

/// <summary>
/// Acquisition screen view.
/// SPEC-UI-001: FR-UI-06, 07, 09, 10, 11.
/// </summary>
public partial class AcquisitionView : UserControl
{
    public AcquisitionView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.AcquisitionViewModel>();
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the Loaded event to automatically initialize the acquisition screen.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.AcquisitionViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
