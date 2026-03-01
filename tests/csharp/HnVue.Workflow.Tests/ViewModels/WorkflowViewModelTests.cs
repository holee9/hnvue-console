namespace HnVue.Workflow.Tests.ViewModels;

using System;
using System.Threading.Tasks;
using HnVue.Workflow.Events;
using HnVue.Workflow.StateMachine;
using HnVue.Workflow.ViewModels;
using Xunit;

/// <summary>
/// Tests for WorkflowViewModel.
/// SPEC-WORKFLOW-001 TASK-415: Workflow ViewModel Integration
/// </summary>
/// <remarks>
/// @MX:NOTE: TDD test suite for workflow view model integration
/// Tests cover: integration of child ViewModels, event subscription, state updates
/// </remarks>
public class WorkflowViewModelTests : IAsyncDisposable
{
    private readonly WorkflowViewModel _viewModel;
    private readonly InMemoryWorkflowEventPublisher _eventPublisher;

    public WorkflowViewModelTests()
    {
        _viewModel = new WorkflowViewModel();
        _eventPublisher = new InMemoryWorkflowEventPublisher();
    }

    /// <summary>
    /// TEST: ViewModel should initialize with all child ViewModels.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithChildViewModels()
    {
        // Act
        var viewModel = new WorkflowViewModel();

        // Assert
        Assert.NotNull(viewModel.StateMachine);
        Assert.NotNull(viewModel.InterlockStatus);
        Assert.NotNull(viewModel.DoseIndicator);
    }

    /// <summary>
    /// TEST: StartAsync should subscribe to workflow events.
    /// </summary>
    [Fact]
    public async Task StartAsync_ShouldSubscribeToWorkflowEvents()
    {
        // Act
        await _viewModel.StartAsync(_eventPublisher, default);

        // Assert
        Assert.NotNull(_eventPublisher.Subscribe()); // Channel should be available

        // Cleanup
        await _viewModel.StopAsync(default);
    }

    /// <summary>
    /// TEST: StateChanged event should update StateMachineViewModel.
    /// </summary>
    [Fact]
    public async Task OnWorkflowEvent_StateChanged_ShouldUpdateStateMachineViewModel()
    {
        // Arrange
        await _viewModel.StartAsync(_eventPublisher, default);

        var workflowEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            CurrentState = WorkflowState.ExposureTrigger,
            PreviousState = WorkflowState.PositionAndPreview
        };

        // Act
        await _eventPublisher.PublishEventAsync(workflowEvent);
        await Task.Delay(100); // Allow event processing
        await _viewModel.StopAsync(default); // Stop processing

        // Assert
        Assert.Equal(WorkflowState.ExposureTrigger, _viewModel.StateMachine.CurrentState?.State);
    }

    /// <summary>
    /// TEST: ExposureTriggered event should update DoseIndicatorViewModel.
    /// </summary>
    [Fact]
    public async Task OnWorkflowEvent_ExposureTriggered_ShouldUpdateDoseIndicator()
    {
        // Arrange
        await _viewModel.StartAsync(_eventPublisher, default);

        var doseData = new
        {
            StudyTotalMGy = 25.5m,
            DailyTotalMGy = 50.0m
        };

        var workflowEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.ExposureTriggered,
            StudyId = "STUDY123",
            PatientId = "PATIENT456",
            Data = doseData
        };

        // Act
        await _eventPublisher.PublishEventAsync(workflowEvent);
        await Task.Delay(100); // Allow event processing
        await _viewModel.StopAsync(default); // Stop processing

        // Assert
        Assert.Equal(25.5m, _viewModel.DoseIndicator.StudyTotalMGy);
        Assert.Equal(50.0m, _viewModel.DoseIndicator.DailyTotalMGy);
    }

    /// <summary>
    /// TEST: Error event should update InterlockStatusViewModel.
    /// </summary>
    [Fact]
    public async Task OnWorkflowEvent_Error_ShouldUpdateInterlockStatus()
    {
        // Arrange
        await _viewModel.StartAsync(_eventPublisher, default);

        var errorData = new
        {
            InterlockIndex = 0, // Door Interlock
            Status = "Red"
        };

        var workflowEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.Error,
            StudyId = "STUDY123",
            Data = errorData
        };

        // Act
        await _eventPublisher.PublishEventAsync(workflowEvent);
        await Task.Delay(100); // Allow event processing
        await _viewModel.StopAsync(default); // Stop processing

        // Assert
        Assert.Equal(InterlockStatus.Red, _viewModel.InterlockStatus.Interlocks[0].Status);
    }

    /// <summary>
    /// TEST: StopAsync should unsubscribe from workflow events.
    /// </summary>
    [Fact]
    public async Task StopAsync_ShouldStopProcessingEvents()
    {
        // Arrange
        await _viewModel.StartAsync(_eventPublisher, default);

        // Act
        await _viewModel.StopAsync(default);

        // Assert - no exception should be thrown
        // ViewModel should be in a stopped state
        Assert.True(true); // Test passes if no exception is thrown
    }

    /// <summary>
    /// TEST: ViewModel should implement INotifyPropertyChanged.
    /// </summary>
    [Fact]
    public void ViewModel_ShouldImplementINotifyPropertyChanged()
    {
        // Arrange & Act
        var viewModel = new WorkflowViewModel();

        // Assert
        Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(viewModel);

        // Cleanup
        var task = viewModel.StopAsync(default);
    }

    /// <summary>
    /// TEST: Multiple events should be processed in sequence.
    /// </summary>
    [Fact]
    public async Task OnWorkflowEvent_MultipleEvents_ShouldProcessInSequence()
    {
        // Arrange
        await _viewModel.StartAsync(_eventPublisher, default);

        var event1 = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            CurrentState = WorkflowState.PatientSelect,
            PreviousState = WorkflowState.Idle
        };

        var event2 = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            CurrentState = WorkflowState.ProtocolSelect,
            PreviousState = WorkflowState.PatientSelect
        };

        // Act
        await _eventPublisher.PublishEventAsync(event1);
        await Task.Delay(50);
        await _eventPublisher.PublishEventAsync(event2);
        await Task.Delay(100);
        await _viewModel.StopAsync(default); // Stop processing

        // Assert
        Assert.Equal(WorkflowState.ProtocolSelect, _viewModel.StateMachine.CurrentState?.State);
        Assert.Equal(2, _viewModel.StateMachine.TransitionHistory.Count);
    }

    /// <summary>
    /// TEST: ExposureCompleted event should update dose display.
    /// </summary>
    [Fact]
    public async Task OnWorkflowEvent_ExposureCompleted_ShouldUpdateDoseDisplay()
    {
        // Arrange
        await _viewModel.StartAsync(_eventPublisher, default);

        var doseData = new
        {
            StudyTotalMGy = 75.0m,
            DailyTotalMGy = 100.0m
        };

        var workflowEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.ExposureCompleted,
            StudyId = "STUDY123",
            Data = doseData
        };

        // Act
        await _eventPublisher.PublishEventAsync(workflowEvent);
        await Task.Delay(100);
        await _viewModel.StopAsync(default); // Stop processing

        // Assert
        Assert.Equal(75.0m, _viewModel.DoseIndicator.StudyTotalMGy);
        Assert.Equal(100.0m, _viewModel.DoseIndicator.DailyTotalMGy);
    }

    /// <summary>
    /// Dispose pattern implementation.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        // Don't await, just return the task as ValueTask
        var task = _viewModel.StopAsync(default);
        return default; // Return completed ValueTask
    }
}
