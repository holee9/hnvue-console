using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for AECViewModel.
/// SPEC-UI-001: FR-UI-04 Automatic Exposure Control.
/// </summary>
public class AECViewModelTests : ViewModelTestBase
{
    private readonly Mock<HnVue.Console.Services.IAECService> _mockAecService;

    public AECViewModelTests()
    {
        _mockAecService = CreateMockService<HnVue.Console.Services.IAECService>();
    }

    [Fact]
    public void Constructor_Initializes_Default_Values()
    {
        // Arrange & Act
        var viewModel = new AECViewModel(_mockAecService.Object);

        // Assert
        Assert.NotNull(viewModel);
        Assert.False(viewModel.IsAecEnabled);
        Assert.NotNull(viewModel.ToggleAecCommand);
    }

    [Fact]
    public async Task ToggleAecCommand_Enables_When_Disabled()
    {
        // Arrange
        _mockAecService
            .Setup(s => s.EnableAECAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new AECViewModel(_mockAecService.Object);
        Assert.False(viewModel.IsAecEnabled);

        // Act - execute toggle when AEC is disabled
        viewModel.ToggleAecCommand.Execute(null);

        // Wait briefly for async void to complete
        await Task.Delay(50);

        // Assert - EnableAECAsync should have been called
        _mockAecService.Verify(s => s.EnableAECAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleAecCommand_Disables_When_Enabled()
    {
        // Arrange
        _mockAecService
            .Setup(s => s.EnableAECAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAecService
            .Setup(s => s.DisableAECAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new AECViewModel(_mockAecService.Object);

        // Set AEC to enabled state first
        await viewModel.SetAecModeAsync(true);
        Assert.True(viewModel.IsAecEnabled);

        // Act - execute toggle when AEC is enabled
        viewModel.ToggleAecCommand.Execute(null);

        // Wait briefly for async void to complete
        await Task.Delay(50);

        // Assert - DisableAECAsync should have been called
        _mockAecService.Verify(s => s.DisableAECAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SetAecModeAsync_Enables_AEC()
    {
        // Arrange
        _mockAecService
            .Setup(s => s.EnableAECAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new AECViewModel(_mockAecService.Object);

        // Act
        await viewModel.SetAecModeAsync(true);

        // Assert
        _mockAecService.Verify(s => s.EnableAECAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(viewModel.IsAecEnabled);
    }

    [Fact]
    public async Task SetAecModeAsync_Disables_AEC()
    {
        // Arrange
        _mockAecService
            .Setup(s => s.EnableAECAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAecService
            .Setup(s => s.DisableAECAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new AECViewModel(_mockAecService.Object);
        await viewModel.SetAecModeAsync(true);

        // Act
        await viewModel.SetAecModeAsync(false);

        // Assert
        _mockAecService.Verify(s => s.DisableAECAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(viewModel.IsAecEnabled);
    }

    [Fact]
    public void IsAecEnabled_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new AECViewModel(_mockAecService.Object);
        var changedProperties = GetChangedProperties(viewModel, () => viewModel.IsAecEnabled = true);

        // Assert
        Assert.Contains("IsAecEnabled", changedProperties);
    }
}
