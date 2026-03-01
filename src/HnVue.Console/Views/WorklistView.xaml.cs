using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace HnVue.Console.Views;

/// <summary>
/// Worklist display view.
/// SPEC-UI-001: FR-UI-02 Worklist Display.
/// </summary>
public partial class WorklistView : UserControl
{
    public WorklistView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.WorklistViewModel>();
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the Loaded event to automatically refresh worklist.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.WorklistViewModel viewModel)
        {
            await viewModel.ActivateAsync();
        }
    }
}
