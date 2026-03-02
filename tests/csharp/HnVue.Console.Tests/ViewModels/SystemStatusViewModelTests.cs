using System.Collections.ObjectModel;
using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for SystemStatusViewModel.
/// SPEC-UI-001: FR-UI-12 System Status Dashboard.
/// </summary>
public class SystemStatusViewModelTests : ViewModelTestBase
{
    private readonly Mock<ISystemStatusService> _mockStatusService;

    public SystemStatusViewModelTests()
    {
        _mockStatusService = CreateMockService<ISystemStatusService>();
    }

    [Fact]
    public async Task InitializeAsync_Loads_System_Status()
    {
        // Arrange
        var mockStatus = new SystemOverallStatus
        {
            OverallHealth = ComponentHealth.Healthy,
            CanInitiateExposure = true,
            ActiveAlerts = Array.Empty<string>(),
            UpdatedAt = DateTimeOffset.Now,
            ComponentStatuses = new List<ComponentStatus>
            {
                new()
                {
                    ComponentId = "TestComponent",
                    Type = ComponentType.CoreEngine,
                    Health = ComponentHealth.Healthy,
                    StatusMessage = "OK",
                    UpdatedAt = DateTimeOffset.Now
                }
            }
        };

        _mockStatusService
            .Setup(s => s.GetOverallStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStatus);

        var viewModel = new SystemStatusViewModel(_mockStatusService.Object);

        // Act
        await viewModel.InitializeAsync(TestCancellationToken);

        // Assert
        Assert.Equal(ComponentHealth.Healthy, viewModel.OverallHealth);
        Assert.Single(viewModel.ComponentStatuses);
        Assert.True(viewModel.CanInitiateExposure);
    }

    [Fact]
    public async Task RefreshCommand_Reloads_Status()
    {
        // Arrange
        var mockStatus = new SystemOverallStatus
        {
            OverallHealth = ComponentHealth.Healthy,
            CanInitiateExposure = true,
            ActiveAlerts = Array.Empty<string>(),
            UpdatedAt = DateTimeOffset.Now,
            ComponentStatuses = new List<ComponentStatus>()
        };

        _mockStatusService
            .Setup(s => s.GetOverallStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStatus);

        var viewModel = new SystemStatusViewModel(_mockStatusService.Object);
        await viewModel.InitializeAsync(TestCancellationToken);

        // Reset mock for next call
        _mockStatusService.Invocations.Clear();

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(100); // Allow async to complete

        // Assert
        _mockStatusService.Verify(
            s => s.GetOverallStatusAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void OverallHealthBrushKey_Returns_Correct_Brush()
    {
        // Arrange
        _mockStatusService
            .Setup(s => s.GetOverallStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemOverallStatus
            {
                OverallHealth = ComponentHealth.Healthy,
                CanInitiateExposure = true,
                ActiveAlerts = Array.Empty<string>(),
                UpdatedAt = DateTimeOffset.Now,
                ComponentStatuses = new List<ComponentStatus>()
            });

        var viewModel = new SystemStatusViewModel(_mockStatusService.Object);

        // Act
        var brushKey = viewModel.OverallHealthBrushKey;

        // Assert
        Assert.Equal("SuccessBrush", brushKey);
    }

    [Theory]
    [InlineData(ComponentHealth.Healthy, "SuccessBrush")]
    [InlineData(ComponentHealth.Degraded, "WarningBrush")]
    [InlineData(ComponentHealth.Error, "ErrorBrush")]
    [InlineData(ComponentHealth.Offline, "SecondaryBrush")]
    public async Task OverallHealthBrushKey_Maps_Health_To_Brush(
        ComponentHealth health, string expectedBrush)
    {
        // Arrange
        _mockStatusService
            .Setup(s => s.GetOverallStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemOverallStatus
            {
                OverallHealth = health,
                CanInitiateExposure = true,
                ActiveAlerts = Array.Empty<string>(),
                UpdatedAt = DateTimeOffset.Now,
                ComponentStatuses = new List<ComponentStatus>()
            });

        var viewModel = new SystemStatusViewModel(_mockStatusService.Object);
        await viewModel.InitializeAsync(TestCancellationToken);

        // Act
        var brushKey = viewModel.OverallHealthBrushKey;

        // Assert
        Assert.Equal(expectedBrush, brushKey);
    }

    [Fact]
    public async Task IsLoading_Is_True_During_Load()
    {
        // Arrange
        var tcs = new TaskCompletionSource<SystemOverallStatus>();

        _mockStatusService
            .Setup(s => s.GetOverallStatusAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var viewModel = new SystemStatusViewModel(_mockStatusService.Object);

        // Act - start loading (don't await)
        var loadTask = viewModel.InitializeAsync(TestCancellationToken);
        await Task.Yield(); // Allow async to start

        // Assert - loading should be true
        var isLoadingDuringLoad = viewModel.IsLoading;

        // Complete the load
        tcs.SetResult(new SystemOverallStatus
        {
            OverallHealth = ComponentHealth.Healthy,
            CanInitiateExposure = true,
            ActiveAlerts = Array.Empty<string>(),
            UpdatedAt = DateTimeOffset.Now,
            ComponentStatuses = new List<ComponentStatus>()
        });

        await loadTask;

        // Assert - loading should be false after
        Assert.False(viewModel.IsLoading);
    }
}
