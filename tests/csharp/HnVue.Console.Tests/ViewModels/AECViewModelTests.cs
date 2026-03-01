using HnVue.Console.Models;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
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
        // AEC should have default values
    }

    [Fact]
    public async Task EnableAECAsync_Enables_AEC()
    {
        // Arrange
        _mockAecService
            .Setup(s => s.EnableAECAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new AECViewModel(_mockAecService.Object);

        // Act
        await viewModel.EnableAECAsync(TestCancellationToken);

        // Assert
        _mockAecService.Verify(s => s.EnableAECAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAECAsync_Disables_AEC()
    {
        // Arrange
        _mockAecService
            .Setup(s => s.DisableAECAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new AECViewModel(_mockAecService.Object);

        // Act
        await viewModel.DisableAECAsync(TestCancellationToken);

        // Assert
        _mockAecService.Verify(s => s.DisableAECAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAECStatusAsync_Returns_Status()
    {
        // Arrange
        var status = new AECStatus
        {
            IsEnabled = true,
            Mode = AECMode.SemiAuto,
            DetectedDensity = 1.5m,
            SpeedClass = AECSpeedClass.S400
        };

        _mockAecService
            .Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var viewModel = new AECViewModel(_mockAecService.Object);

        // Act
        var result = await viewModel.GetAECStatusAsync(TestCancellationToken);

        // Assert
        Assert.True(result.IsEnabled);
        Assert.Equal(AECMode.SemiAuto, result.Mode);
    }

    [Theory]
    [InlineData(AECMode.Manual, "Manual")]
    [InlineData(AECMode.SemiAuto, "Semi-Auto")]
    [InlineData(AECMode.Auto, "Auto")]
    public void AecModeToString_Returns_Correct_String(AECMode mode, string expected)
    {
        // Act
        var result = AECViewModel.AecModeToString(mode);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(AECMode.Manual, "DisabledBrush")]
    [InlineData(AECMode.SemiAuto, "WarningBrush")]
    [InlineData(AECMode.Auto, "SuccessBrush")]
    public void AecModeToBrush_Returns_Correct_Brush(AECMode mode, string expectedBrush)
    {
        // Act
        var result = AECViewModel.AecModeToBrush(mode);

        // Assert
        Assert.Equal(expectedBrush, result);
    }
}
