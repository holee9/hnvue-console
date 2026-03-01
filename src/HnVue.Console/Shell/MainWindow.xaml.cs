using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace HnVue.Console.Shell;

/// <summary>
/// Shell window hosting navigation and content regions.
/// SPEC-UI-001 Shell Infrastructure.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.ShellViewModel>();
        DataContext = viewModel;
    }
}
