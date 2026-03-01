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
        Assert.Equal("HnVue Console", viewModel.Title);
    }

    [Fact]
    public void Implements_INotifyPropertyChanged()
    {
        // Arrange
        var viewModel = new ShellViewModel();
        var callCount = 0;
        viewModel.PropertyChanged += (s, e) => callCount++;

        // Act
        viewModel.Title = "New Title";

        // Assert
        Assert.True(callCount > 0);
    }

    [Fact]
    public void Title_Can_Be_Set()
    {
        // Arrange
        var viewModel = new ShellViewModel();
        var changedProperties = GetChangedProperties(viewModel, () => viewModel.Title = "Test");

        // Assert
        Assert.Contains("Title", changedProperties);
        Assert.Equal("Test", viewModel.Title);
    }
}
