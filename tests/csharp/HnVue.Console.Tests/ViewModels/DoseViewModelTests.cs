using HnVue.Console.Models;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for DoseViewModel.
/// SPEC-UI-001: FR-UI-05 Dose Display and Monitoring.
/// </summary>
public class DoseViewModelTests : ViewModelTestBase
{
    private readonly Mock<HnVue.Console.Services.IDoseService> _mockDoseService;

    public DoseViewModelTests()
    {
        _mockDoseService = CreateMockService<HnVue.Console.Services.IDoseService>();
    }

    [Fact]
    public void Constructor_Initializes_Default_Values()
    {
        // Arrange & Act
        var viewModel = new DoseViewModel(_mockDoseService.Object);

        // Assert
        Assert.NotNull(viewModel);
    }

    [Fact]
    public async Task UpdateDoseDisplayAsync_Updates_Display_Values()
    {
        // Arrange
        var doseInfo = new DoseInformation
        {
            DoseAreaProduct = 1.5m,
            EntranceDose = 2.0m,
            ReferenceDose = 5.0m,
            AlertLevel = DoseAlertLevel.Normal
        };

        _mockDoseService
            .Setup(s => s.GetCurrentDoseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(doseInfo);

        var viewModel = new DoseViewModel(_mockDoseService.Object);

        // Act
        await viewModel.UpdateDoseDisplayAsync(TestCancellationToken);

        // Assert
        _mockDoseService.Verify(s => s.GetCurrentDoseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(DoseAlertLevel.Normal, "Normal", "SuccessBrush")]
    [InlineData(DoseAlertLevel.Warning, "Warning", "WarningBrush")]
    [InlineData(DoseAlertLevel.High, "High", "ErrorBrush")]
    [InlineData(DoseAlertLevel.Critical, "Critical", "ErrorBrush")]
    public void DoseAlertLevel_Maps_Correctly(
        DoseAlertLevel level, string expectedText, string expectedBrush)
    {
        // Act
        var text = DoseViewModel.DoseAlertLevelToString(level);
        var brush = DoseViewModel.DoseAlertLevelToBrush(level);

        // Assert
        Assert.Equal(expectedText, text);
        Assert.Equal(expectedBrush, brush);
    }

    [Fact]
    public async Task DosePercentage_Returns_Correct_Value()
    {
        // Arrange
        var doseInfo = new DoseInformation
        {
            DoseAreaProduct = 2.5m,
            ReferenceDose = 5.0m,
            EntranceDose = 0m,
            AlertLevel = DoseAlertLevel.Normal
        };

        _mockDoseService
            .Setup(s => s.GetCurrentDoseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(doseInfo);

        var viewModel = new DoseViewModel(_mockDoseService.Object);
        await viewModel.UpdateDoseDisplayAsync(TestCancellationToken);

        // Act
        var percentage = viewModel.CalculateDosePercentage(2.5m, 5.0m);

        // Assert
        Assert.Equal(50.0, percentage);
    }

    [Fact]
    public async Task StartDoseMonitoring_Begins_Monitoring()
    {
        // Arrange
        var tcs = new TaskCompletionSource<DoseInformation>();

        _mockDoseService
            .Setup(s => s.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var viewModel = new DoseViewModel(_mockDoseService.Object);

        // Act
        var task = viewModel.StartDoseMonitoringAsync(TestCancellationToken);

        // Assert
        _mockDoseService.Verify(s => s.StartMonitoringAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// Extension methods for DoseViewModel tests.
/// </summary>
public static class DoseViewModelTestExtensions
{
    public static double CalculateDosePercentage(this DoseViewModel _, double currentDose, double referenceDose)
    {
        return referenceDose > 0 ? (currentDose / referenceDose) * 100 : 0;
    }

    public static string DoseAlertLevelToString(DoseAlertLevel level) => level switch
    {
        DoseAlertLevel.Normal => "Normal",
        DoseAlertLevel.Warning => "Warning",
        DoseAlertLevel.High => "High",
        DoseAlertLevel.Critical => "Critical",
        _ => "Unknown"
    };

    public static string DoseAlertLevelToBrush(DoseAlertLevel level) => level switch
    {
        DoseAlertLevel.Normal => "SuccessBrush",
        DoseAlertLevel.Warning => "WarningBrush",
        DoseAlertLevel.High => "ErrorBrush",
        DoseAlertLevel.Critical => "ErrorBrush",
        _ => "DisabledTextBrush"
    };
}
