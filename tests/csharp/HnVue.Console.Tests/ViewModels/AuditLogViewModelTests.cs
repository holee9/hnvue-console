using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for AuditLogViewModel.
/// SPEC-UI-001: FR-UI-13 Audit Log Viewer.
/// </summary>
public class AuditLogViewModelTests : ViewModelTestBase
{
    private readonly Mock<IAuditLogService> _mockAuditLogService;

    public AuditLogViewModelTests()
    {
        _mockAuditLogService = CreateMockService<IAuditLogService>();
    }

    [Fact]
    public void Constructor_Initializes_Collections()
    {
        // Arrange & Act
        var viewModel = new AuditLogViewModel(_mockAuditLogService.Object);

        // Assert
        Assert.NotNull(viewModel.LogEntries);
        Assert.Empty(viewModel.LogEntries);
        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(50, viewModel.PageSize);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task InitializeAsync_Loads_Default_Logs()
    {
        // Arrange
        var logs = new List<AuditLogEntry>
        {
            new()
            {
                EntryId = "LOG001",
                Timestamp = DateTimeOffset.Now,
                EventType = AuditEventType.PatientRegistration,
                UserId = "user1",
                UserName = "Test User",
                EventDescription = "Test event",
                Outcome = AuditOutcome.Success
            }
        };

        var pagedResult = new PagedAuditLogResult
        {
            Entries = logs,
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 50,
            HasMorePages = false
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<AuditLogFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var viewModel = new AuditLogViewModel(_mockAuditLogService.Object);

        // Act
        await viewModel.InitializeAsync(TestCancellationToken);

        // Assert
        Assert.Single(viewModel.LogEntries);
        Assert.Equal(1, viewModel.TotalCount);
    }

    [Fact]
    public async Task SearchCommand_Applies_Filter()
    {
        // Arrange
        _mockAuditLogService
            .Setup(s => s.GetLogsPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<AuditLogFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedAuditLogResult
            {
                Entries = new List<AuditLogEntry>(),
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 50,
                HasMorePages = false
            });

        var viewModel = new AuditLogViewModel(_mockAuditLogService.Object);
        await viewModel.InitializeAsync(TestCancellationToken);

        viewModel.FilterEventType = AuditEventType.SystemError;
        viewModel.FilterOutcome = AuditOutcome.Failure;

        // Act
        viewModel.SearchCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        _mockAuditLogService.Verify(
            s => s.GetLogsPagedAsync(
                1, // Reset to page 1
                50,
                It.Is<AuditLogFilter>(f =>
                    f.EventType == AuditEventType.SystemError &&
                    f.Outcome == AuditOutcome.Failure),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NextPageCommand_Increments_Page()
    {
        // Arrange
        var pagedResult = new PagedAuditLogResult
        {
            Entries = new List<AuditLogEntry>(),
            TotalCount = 150,
            PageNumber = 2,
            PageSize = 50,
            HasMorePages = true
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsPagedAsync(2, 50, It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var viewModel = new AuditLogViewModel(_mockAuditLogService.Object);
        await viewModel.InitializeAsync(TestCancellationToken);

        // Reset mock
        _mockAuditLogService.Invocations.Clear();

        // Act
        viewModel.NextPageCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        Assert.Equal(2, viewModel.CurrentPage);
        _mockAuditLogService.Verify(
            s => s.GetLogsPagedAsync(2, 50, It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PreviousPageCommand_Decrements_Page()
    {
        // Arrange
        _mockAuditLogService
            .Setup(s => s.GetLogsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedAuditLogResult
            {
                Entries = new List<AuditLogEntry>(),
                TotalCount = 150,
                PageNumber = 1,
                PageSize = 50,
                HasMorePages = true
            });

        var viewModel = new AuditLogViewModel(_mockAuditLogService.Object);
        await viewModel.InitializeAsync(TestCancellationToken);

        // Set to page 2
        viewModel.CurrentPage = 2;
        _mockAuditLogService.Invocations.Clear();

        // Act
        viewModel.PreviousPageCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        Assert.Equal(1, viewModel.CurrentPage);
        _mockAuditLogService.Verify(
            s => s.GetLogsPagedAsync(1, 50, It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearFilterCommand_Resets_Filters()
    {
        // Arrange
        _mockAuditLogService
            .Setup(s => s.GetLogsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedAuditLogResult
            {
                Entries = new List<AuditLogEntry>(),
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 50,
                HasMorePages = false
            });

        var viewModel = new AuditLogViewModel(_mockAuditLogService.Object);
        await viewModel.InitializeAsync(TestCancellationToken);

        // Set some filters
        viewModel.FilterEventType = AuditEventType.PatientRegistration;
        viewModel.FilterOutcome = AuditOutcome.Warning;
        viewModel.FilterUserId = "testuser";

        // Act
        viewModel.ClearFilterCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        Assert.Null(viewModel.FilterEventType);
        Assert.Null(viewModel.FilterOutcome);
        Assert.Null(viewModel.FilterUserId);
        Assert.Equal(1, viewModel.CurrentPage);
    }

    [Theory]
    [InlineData(AuditOutcome.Success, "SuccessBrush")]
    [InlineData(AuditOutcome.Warning, "WarningBrush")]
    [InlineData(AuditOutcome.Failure, "ErrorBrush")]
    public void GetOutcomeBrushKey_Returns_Correct_Brush(AuditOutcome outcome, string expectedBrush)
    {
        // Act
        var result = AuditLogViewModel.GetOutcomeBrushKey(outcome);

        // Assert
        Assert.Equal(expectedBrush, result);
    }

    [Fact]
    public async Task ExportCommand_Calls_Export_Service()
    {
        // Arrange
        var exportData = new byte[] { 1, 2, 3 };

        _mockAuditLogService
            .Setup(s => s.ExportLogsAsync(It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exportData);

        var viewModel = new AuditLogViewModel(_mockAuditLogService.Object);
        await viewModel.InitializeAsync(TestCancellationToken);

        // Act
        viewModel.ExportCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        _mockAuditLogService.Verify(
            s => s.ExportLogsAsync(It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HasMorePages_Is_True_When_More_Pages_Exist()
    {
        // Arrange
        _mockAuditLogService
            .Setup(s => s.GetLogsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedAuditLogResult
            {
                Entries = new List<AuditLogEntry>(),
                TotalCount = 150,
                PageNumber = 1,
                PageSize = 50,
                HasMorePages = true
            });

        var viewModel = new AuditLogViewModel(_mockAuditLogService.Object);
        await viewModel.InitializeAsync(TestCancellationToken);

        // Act & Assert
        Assert.True(viewModel.HasMorePages);
        Assert.Equal(3, viewModel.TotalPages);
    }
}
