using HnVue.Console.Models;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
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
        Assert.NotNull(viewModel.DoseDisplay);
        Assert.False(viewModel.HasAlert);
        Assert.NotNull(viewModel.AcknowledgeAlertCommand);
    }

    [Fact]
    public void AcknowledgeAlertCommand_ClearsAlert()
    {
        // Arrange
        var viewModel = new DoseViewModel(_mockDoseService.Object);
        viewModel.HasAlert = true;

        // Verify CanExecute is true when alert is active
        // Note: command's canExecute checks _hasAlert backing field,
        // so we set it via the property which updates the field
        viewModel.AcknowledgeAlertCommand.RaiseCanExecuteChanged();

        // Act
        viewModel.AcknowledgeAlertCommand.Execute(null);

        // Assert
        Assert.False(viewModel.HasAlert);
    }

    [Fact]
    public async Task ResetCumulativeDoseAsync_Calls_Service()
    {
        // Arrange
        _mockDoseService
            .Setup(s => s.ResetCumulativeDoseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new DoseViewModel(_mockDoseService.Object);

        // Act
        await viewModel.ResetCumulativeDoseAsync("STUDY001", TestCancellationToken);

        // Assert
        _mockDoseService.Verify(
            s => s.ResetCumulativeDoseAsync("STUDY001", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void CurrentDoseValue_Returns_Formatted_String()
    {
        // Arrange
        var viewModel = new DoseViewModel(_mockDoseService.Object);

        // Act
        var result = viewModel.CurrentDoseValue;

        // Assert - should return a formatted string (not null or empty)
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void CumulativeDoseValue_Returns_Formatted_String()
    {
        // Arrange
        var viewModel = new DoseViewModel(_mockDoseService.Object);

        // Act
        var result = viewModel.CumulativeDoseValue;

        // Assert - should return a formatted string (not null or empty)
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void HasAlert_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new DoseViewModel(_mockDoseService.Object);

        // Act
        var changedProperties = GetChangedProperties(viewModel, () => viewModel.HasAlert = true);

        // Assert
        Assert.Contains("HasAlert", changedProperties);
    }

    [Fact]
    public void AlertThreshold_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new DoseViewModel(_mockDoseService.Object);
        var newThreshold = new DoseAlertThreshold
        {
            WarningThreshold = 3.0m,
            ErrorThreshold = 7.0m,
            Unit = DoseUnit.MilliGray
        };

        // Act
        var changedProperties = GetChangedProperties(viewModel, () => viewModel.AlertThreshold = newThreshold);

        // Assert
        Assert.Contains("AlertThreshold", changedProperties);
    }
}
