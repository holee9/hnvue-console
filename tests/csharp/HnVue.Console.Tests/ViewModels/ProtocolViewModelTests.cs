using HnVue.Console.Models;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
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
        Assert.NotNull(viewModel.Protocols);
        Assert.Null(viewModel.SelectedProtocol);
    }

    [Fact]
    public async Task LoadProtocolsAsync_Populates_Protocol_List()
    {
        // Arrange
        var protocols = new List<Protocol>
        {
            CreateTestProtocol("PROTO001"),
            CreateTestProtocol("PROTO002")
        };

        _mockProtocolService
            .Setup(s => s.GetProtocolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(protocols);

        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);

        // Act
        await viewModel.LoadProtocolsAsync(TestCancellationToken);

        // Assert
        Assert.Equal(2, viewModel.Protocols.Count);
    }

    [Fact]
    public void SelectProtocolCommand_Sets_Selected_Protocol()
    {
        // Arrange
        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);
        var protocol = CreateTestProtocol();

        // Act
        viewModel.SelectProtocolCommand.Execute(protocol);

        // Assert
        Assert.Equal(protocol, viewModel.SelectedProtocol);
    }

    [Fact]
    public async Task SelectedProtocol_Raises_PropertyChanged()
    {
        // Arrange
        _mockProtocolService
            .Setup(s => s.GetProtocolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Protocol>());

        var viewModel = new ProtocolViewModel(_mockProtocolService.Object);
        await viewModel.LoadProtocolsAsync(TestCancellationToken);
        var protocol = CreateTestProtocol();

        var changedProperties = GetChangedProperties(viewModel, () => viewModel.SelectedProtocol = protocol);

        // Assert
        Assert.Contains("SelectedProtocol", changedProperties);
    }

    [Theory]
    [InlineData(FocalSpotSize.Small, "0.6")]
    [InlineData(FocalSpotSize.Large, "1.2")]
    [InlineData(FocalSpotSize.Micro, "0.3")]
    public void FocalSpotSizeToString_Returns_Correct_Value(FocalSpotSize size, string expected)
    {
        // Act
        var result = ProtocolViewModel.FocalSpotSizeToString(size);

        // Assert
        Assert.Equal(expected, result);
    }
}
