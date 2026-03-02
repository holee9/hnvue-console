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
        Assert.Null(viewModel.SelectedItem);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task RefreshCommand_Loads_Worklist()
    {
        // Arrange
        var worklistItems = new List<WorklistItem>
        {
            CreateTestWorklistItem("WL001"),
            CreateTestWorklistItem("WL002")
        };

        var refreshResult = new WorklistRefreshResult
        {
            Items = worklistItems,
            RefreshedAt = DateTimeOffset.Now
        };

        _mockWorklistService
            .Setup(s => s.RefreshWorklistAsync(It.IsAny<WorklistRefreshRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResult);

        var viewModel = new WorklistViewModel(_mockWorklistService.Object);

        // Act - AsyncRelayCommand uses Execute (async void internally)
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(200);

        // Assert
        Assert.Equal(2, viewModel.WorklistItems.Count);
        Assert.False(viewModel.IsLoading);
        _mockWorklistService.Verify(
            s => s.RefreshWorklistAsync(It.IsAny<WorklistRefreshRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshCommand_Handles_Error_And_Raises_ErrorOccurred()
    {
        // Arrange
        _mockWorklistService
            .Setup(s => s.RefreshWorklistAsync(It.IsAny<WorklistRefreshRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var viewModel = new WorklistViewModel(_mockWorklistService.Object);

        string? errorMessage = null;
        viewModel.ErrorOccurred += (s, msg) => errorMessage = msg;

        // Act - AsyncRelayCommand uses Execute (async void internally)
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(200);

        // Assert
        Assert.NotNull(errorMessage);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public void SelectProcedureCommand_SetsSelectedItem()
    {
        // Arrange
        var viewModel = new WorklistViewModel(_mockWorklistService.Object);
        var item = CreateTestWorklistItem("WL001");

        // Act
        viewModel.SelectProcedureCommand.Execute(item);

        // Assert
        Assert.Equal(item, viewModel.SelectedItem);
    }

    [Fact]
    public void SelectProcedureCommand_RaisesNavigationRequested()
    {
        // Arrange
        var viewModel = new WorklistViewModel(_mockWorklistService.Object);
        var item = CreateTestWorklistItem("WL001");

        string? navigationTarget = null;
        viewModel.NavigationRequested += (s, target) => navigationTarget = target;

        // Act
        viewModel.SelectProcedureCommand.Execute(item);

        // Assert
        Assert.Equal("Acquisition", navigationTarget);
    }

    [Fact]
    public void SelectProcedureCommand_CanExecute_WithWorklistItem()
    {
        // Arrange
        var viewModel = new WorklistViewModel(_mockWorklistService.Object);
        var item = CreateTestWorklistItem();

        // Assert
        Assert.True(viewModel.SelectProcedureCommand.CanExecute(item));
    }

    [Fact]
    public void SelectProcedureCommand_CannotExecute_WithNull()
    {
        // Arrange
        var viewModel = new WorklistViewModel(_mockWorklistService.Object);

        // Assert
        Assert.False(viewModel.SelectProcedureCommand.CanExecute(null));
    }

    [Fact]
    public async Task ActivateAsync_LoadsIfEmpty()
    {
        // Arrange
        var refreshResult = new WorklistRefreshResult
        {
            Items = new List<WorklistItem> { CreateTestWorklistItem() },
            RefreshedAt = DateTimeOffset.Now
        };

        _mockWorklistService
            .Setup(s => s.RefreshWorklistAsync(It.IsAny<WorklistRefreshRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResult);

        var viewModel = new WorklistViewModel(_mockWorklistService.Object);
        Assert.Empty(viewModel.WorklistItems);

        // Act
        await viewModel.ActivateAsync(TestCancellationToken);

        // Assert
        _mockWorklistService.Verify(
            s => s.RefreshWorklistAsync(It.IsAny<WorklistRefreshRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_DoesNotLoad_WhenItemsExist()
    {
        // Arrange
        var refreshResult = new WorklistRefreshResult
        {
            Items = new List<WorklistItem> { CreateTestWorklistItem() },
            RefreshedAt = DateTimeOffset.Now
        };

        _mockWorklistService
            .Setup(s => s.RefreshWorklistAsync(It.IsAny<WorklistRefreshRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResult);

        var viewModel = new WorklistViewModel(_mockWorklistService.Object);

        // Load once first
        await viewModel.ActivateAsync(TestCancellationToken);

        // Act - activate again
        await viewModel.ActivateAsync(TestCancellationToken);

        // Assert - should only have called RefreshWorklistAsync once total
        _mockWorklistService.Verify(
            s => s.RefreshWorklistAsync(It.IsAny<WorklistRefreshRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void WorklistItem_CreatedWith_CorrectFields()
    {
        // Arrange & Act
        var item = CreateTestWorklistItem("WL001");

        // Assert using actual WorklistItem fields
        Assert.Equal("WL001", item.ProcedureId);
        Assert.Equal("PT001", item.PatientId);
        Assert.Equal("Test Patient", item.PatientName);
        Assert.Equal("ACC001", item.AccessionNumber);
        Assert.Equal("Chest X-Ray", item.ScheduledProcedureStepDescription);
        Assert.Equal("CHEST", item.BodyPart);
        Assert.Equal("PA", item.Projection);
        Assert.Equal(WorklistStatus.Scheduled, item.Status);
    }

    [Fact]
    public void WorklistItem_WithExpression_UpdatesStatus()
    {
        // Arrange
        var item = CreateTestWorklistItem();

        // Act - use with expression to create modified copy
        var updatedItem = item with { Status = WorklistStatus.InProgress };

        // Assert
        Assert.Equal(WorklistStatus.InProgress, updatedItem.Status);
        Assert.Equal(WorklistStatus.Scheduled, item.Status);
    }
}
