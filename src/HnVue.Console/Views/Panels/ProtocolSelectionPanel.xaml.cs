using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace HnVue.Console.Views.Panels;

/// <summary>
/// Protocol selection control panel.
/// SPEC-UI-001: FR-UI-06 Protocol Selection.
/// </summary>
public partial class ProtocolSelectionPanel : UserControl
{
    public ProtocolSelectionPanel()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.ProtocolViewModel>();
        DataContext = viewModel;
    }

    /// <summary>
    /// Handles the Loaded event to automatically load body parts.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ProtocolViewModel viewModel)
        {
            await viewModel.LoadBodyPartsAsync();
        }
    }

    /// <summary>
    /// Handles body part selection changed to load projections.
    /// </summary>
    private async void OnBodyPartSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.ProtocolViewModel viewModel &&
            sender is ComboBox comboBox &&
            comboBox.SelectedItem is Models.BodyPart selectedBodyPart)
        {
            await viewModel.LoadProjectionsAsync(selectedBodyPart.Code);
        }
    }
}
