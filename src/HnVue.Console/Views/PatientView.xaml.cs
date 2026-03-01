using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace HnVue.Console.Views;

/// <summary>
/// Patient management view.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public partial class PatientView : UserControl
{
    public PatientView()
    {
        InitializeComponent();

        // Resolve ViewModel from DI container
        var viewModel = App.ServiceProvider?.GetRequiredService<ViewModels.PatientViewModel>();
        DataContext = viewModel;
    }
}
