using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace HnVue.Console.Views.Panels;

/// <summary>
/// Dose display monitoring panel.
/// SPEC-UI-001: FR-UI-10 Dose Display.
/// </summary>
public partial class DoseDisplayPanel : UserControl
{
    public DoseDisplayPanel()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.DoseViewModel>();
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the Loaded event to automatically start dose updates.
    /// </summary>
    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.DoseViewModel viewModel)
        {
            await viewModel.StartDoseUpdatesAsync();
        }
    }
}
