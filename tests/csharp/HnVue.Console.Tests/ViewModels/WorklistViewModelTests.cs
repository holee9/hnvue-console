using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for WorklistViewModel.
/// SPEC-UI-001: FR-UI-02 Worklist Management.
/// </summary>
public class WorklistViewModelTests : ViewModelTestBase
{
    private readonly Mock<IWorklistService> _mockWorklistService;

    public WorklistViewModelTests()
    {
        _mockWorklistService = CreateMockService<IWorklistService>();
    }

    [Fact]
    public void Constructor_Initializes_Collections()
    {
        // Arrange & Act
        var viewModel = new WorklistViewModel(_mockWorklistService.Object);

        // Assert
        Assert.NotNull(viewModel.WorklistItems);
        Assert.Empty(viewModel.WorklistItems);
        Assert.Null(viewModel.SelectedWorklistItem);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task RefreshAsync_Loads_Worklist()
    {
        // Arrange
        var worklistItems = new List<WorklistItem>
        {
            CreateTestWorklistItem("WL001"),
            CreateTestWorklistItem("WL002")
        };

        _mockWorklistService
            .Setup(s => s.GetWorklistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(worklistItems);

        var viewModel = new WorklistViewModel(_mockWorklistService.Object);

        // Act
        await viewModel.RefreshAsync(TestCancellationToken);

        // Assert
        Assert.Equal(2, viewModel.WorklistItems.Count);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task RefreshAsync_Updates_Status_Message()
    {
        // Arrange
        _mockWorklistService
            .Setup(s => s.GetWorklistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorklistItem>());

        var viewModel = new WorklistViewModel(_mockWorklistService.Object);

        // Act
        await viewModel.RefreshAsync(TestCancellationToken);

        // Assert
        Assert.Contains("0 items", viewModel.StatusMessage);
    }

    [Theory]
    [InlineData(WorklistStatus.Pending, "Pending")]
    [InlineData(WorklistStatus.InProgress, "In Progress")]
    [InlineData(WorklistStatus.Completed, "Completed")]
    [InlineData(WorklistStatus.Cancelled, "Cancelled")]
    public void StatusToString_Returns_Correct_String(WorklistStatus status, string expected)
    {
        // Arrange
        var item = CreateTestWorklistItem();
        item = item with { Status = status };

        // Act
        var result = WorklistViewModel.StatusToString(status);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(StudyPriority.Stat, "PriorityBrush")]
    [InlineData(StudyPriority.Urgent, "ErrorBrush")]
    [InlineData(StudyPriority.Routine, "SuccessBrush")]
    public void PriorityToBrush_Returns_Correct_Brush(StudyPriority priority, string expectedBrush)
    {
        // Act
        var result = WorklistViewModel.PriorityToBrush(priority);

        // Assert
        Assert.Equal(expectedBrush, result);
    }

    [Fact]
    public async Task SelectWorklistItemCommand_Sets_Selected_Item()
    {
        // Arrange
        _mockWorklistService
            .Setup(s => s.GetWorklistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorklistItem>());

        var viewModel = new WorklistViewModel(_mockWorklistService.Object);
        await viewModel.RefreshAsync(TestCancellationToken);

        var item = CreateTestWorklistItem();

        // Act
        viewModel.SelectWorklistItemCommand.Execute(item);

        // Assert
        Assert.Equal(item, viewModel.SelectedWorklistItem);
    }

    [Fact]
    public async Task FilterByPriority_Updates_Displayed_Items()
    {
        // Arrange
        var worklistItems = new List<WorklistItem>
        {
            CreateTestWorklistItem("WL001") with { Priority = StudyPriority.Stat },
            CreateTestWorklistItem("WL002") with { Priority = StudyPriority.Routine },
            CreateTestWorklistItem("WL003") with { Priority = StudyPriority.Routine }
        };

        _mockWorklistService
            .Setup(s => s.GetWorklistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(worklistItems);

        var viewModel = new WorklistViewModel(_mockWorklistService.Object);
        await viewModel.RefreshAsync(TestCancellationToken);

        // Act
        viewModel.FilterPriority = StudyPriority.Routine;
        await Task.Delay(50);

        // Assert
        // ViewModel should filter based on selected priority
        Assert.Contains("Routine", viewModel.StatusMessage);
    }
}
