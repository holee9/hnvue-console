using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for ShellViewModel.
/// SPEC-UI-001: FR-UI-00 Application shell infrastructure.
/// </summary>
public class ShellViewModelTests : ViewModelTestBase
{
    [Fact]
    public void Can_Be_Constructed()
    {
        // Arrange & Act
        var viewModel = new ShellViewModel();

        // Assert
        Assert.NotNull(viewModel);
        Assert.Equal("Patient", viewModel.CurrentView);
    }

    [Fact]
    public void Implements_INotifyPropertyChanged()
    {
        // Arrange
        var viewModel = new ShellViewModel();
        var callCount = 0;
        viewModel.PropertyChanged += (s, e) => callCount++;

        // Act
        viewModel.CurrentView = "Acquisition";

        // Assert
        Assert.True(callCount > 0);
    }

    [Fact]
    public void CurrentView_Can_Be_Set()
    {
        // Arrange
        var viewModel = new ShellViewModel();
        var changedProperties = GetChangedProperties(viewModel, () => viewModel.CurrentView = "Test");

        // Assert
        Assert.Contains("CurrentView", changedProperties);
        Assert.Equal("Test", viewModel.CurrentView);
    }

    [Fact]
    public void NavigateCommand_Updates_CurrentView()
    {
        // Arrange
        var viewModel = new ShellViewModel();

        // Act
        viewModel.NavigateCommand.Execute("Worklist");

        // Assert
        Assert.Equal("Worklist", viewModel.CurrentView);
    }

    [Fact]
    public void NavigateCommand_Cannot_Navigate_To_Acquisition_When_Error()
    {
        // Arrange
        var viewModel = new ShellViewModel();
        viewModel.OverallStatus = SystemStatus.Error;

        // Assert
        Assert.False(viewModel.NavigateCommand.CanExecute("Acquisition"));
    }
}
