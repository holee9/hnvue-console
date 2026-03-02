using HnVue.Console.Models;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for ProtocolViewModel.
/// SPEC-UI-001: FR-UI-03 Protocol Selection.
/// </summary>
public class ProtocolViewModelTests : ViewModelTestBase
{
    private readonly Mock<HnVue.Console.Services.IProtocolService> _mockProtocolService;

    public ProtocolViewModelTests()
    {
        _mockProtocolService = CreateMockService<HnVue.Console.Services.IProtocolService>();
    }

    [Fact]
    public void Constructor_Initializes_Collections()
    {
        // Arrange & Act
        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);

        // Assert
        Assert.NotNull(viewModel.BodyParts);
        Assert.NotNull(viewModel.Projections);
        Assert.Empty(viewModel.BodyParts);
        Assert.Empty(viewModel.Projections);
        Assert.Null(viewModel.SelectedProtocol);
    }

    [Fact]
    public async Task LoadBodyPartsAsync_Populates_BodyParts()
    {
        // Arrange
        var bodyParts = new List<BodyPart>
        {
            new BodyPart { Code = "CHEST", DisplayName = "Chest", DisplayNameKorean = "흉부" },
            new BodyPart { Code = "ABD", DisplayName = "Abdomen", DisplayNameKorean = "복부" }
        };

        _mockProtocolService
            .Setup(s => s.GetBodyPartsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bodyParts);

        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);

        // Act
        await viewModel.LoadBodyPartsAsync(TestCancellationToken);

        // Assert
        Assert.Equal(2, viewModel.BodyParts.Count);
        _mockProtocolService.Verify(s => s.GetBodyPartsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadProjectionsAsync_Populates_Projections()
    {
        // Arrange
        var projections = new List<Projection>
        {
            new Projection { Code = "PA", DisplayName = "PA", DisplayNameKorean = "전후방" },
            new Projection { Code = "LAT", DisplayName = "Lateral", DisplayNameKorean = "측면" }
        };

        _mockProtocolService
            .Setup(s => s.GetProjectionsAsync("CHEST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(projections);

        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);

        // Act
        await viewModel.LoadProjectionsAsync("CHEST", TestCancellationToken);

        // Assert
        Assert.Equal(2, viewModel.Projections.Count);
        _mockProtocolService.Verify(
            s => s.GetProjectionsAsync("CHEST", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void SelectedProtocol_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);
        var protocol = CreateTestProtocol();

        // Act
        var changedProperties = GetChangedProperties(viewModel, () => viewModel.SelectedProtocol = protocol);

        // Assert
        Assert.Contains("SelectedProtocol", changedProperties);
    }

    [Fact]
    public void SelectedProtocol_WhenSet_UpdatesValue()
    {
        // Arrange
        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);
        var protocol = CreateTestProtocol();

        // Act
        viewModel.SelectedProtocol = protocol;

        // Assert
        Assert.Equal(protocol, viewModel.SelectedProtocol);
    }

    [Fact]
    public void SelectedProtocol_WhenSetToNull_ClearsValue()
    {
        // Arrange
        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);
        var protocol = CreateTestProtocol();
        viewModel.SelectedProtocol = protocol;

        // Act
        viewModel.SelectedProtocol = null;

        // Assert
        Assert.Null(viewModel.SelectedProtocol);
    }

    [Fact]
    public void SelectProtocolCommand_CanExecute_WhenProtocolSelected()
    {
        // Arrange
        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);
        var protocol = CreateTestProtocol();

        // Act - set a protocol preset (this also sets the internal _selectedProtocol)
        viewModel.SelectedProtocol = protocol;

        // Assert - CanExecute should be true when a protocol is selected and not loading
        Assert.True(viewModel.SelectProtocolCommand.CanExecute(null));
    }

    [Fact]
    public void SelectProtocolCommand_CannotExecute_WhenNoProtocolSelected()
    {
        // Arrange
        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);

        // Assert - CanExecute should be false when no protocol is selected
        Assert.False(viewModel.SelectProtocolCommand.CanExecute(null));
    }

    [Fact]
    public async Task GetPresetAsync_Returns_ProtocolPreset()
    {
        // Arrange
        var preset = CreateTestProtocol();

        _mockProtocolService
            .Setup(s => s.GetProtocolPresetAsync("CHEST", "PA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(preset);

        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);

        // Act
        var result = await viewModel.GetPresetAsync("CHEST", "PA", TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(preset.ProtocolId, result.ProtocolId);
    }
}
