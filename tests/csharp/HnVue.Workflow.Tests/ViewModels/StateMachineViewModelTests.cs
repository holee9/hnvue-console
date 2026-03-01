namespace HnVue.Workflow.Tests.ViewModels;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HnVue.Workflow.Events;
using HnVue.Workflow.StateMachine;
using HnVue.Workflow.ViewModels;
using Xunit;

/// <summary>
/// Tests for StateMachineViewModel.
/// SPEC-WORKFLOW-001 TASK-412: State Machine Visualization Component
/// </summary>
/// <remarks>
/// @MX:NOTE: TDD test suite for state machine visualization
/// Tests cover: Visual representation, current state highlighting, transition history
/// </remarks>
public class StateMachineViewModelTests
{
    /// <summary>
    /// TEST: ViewModel should initialize with all workflow states.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithAllStates()
    {
        // Act
        var viewModel = new StateMachineViewModel();

        // Assert
        Assert.NotNull(viewModel.States);
        Assert.Equal(10, viewModel.States.Count); // 10 workflow states
        Assert.NotNull(viewModel.CurrentState);
        Assert.NotNull(viewModel.TransitionHistory);
    }

    /// <summary>
    /// TEST: Initial state should be Idle.
    /// </summary>
    [Fact]
    public void Constructor_CurrentState_ShouldBeIdle()
    {
        // Act
        var viewModel = new StateMachineViewModel();

        // Assert
        Assert.NotNull(viewModel.CurrentState);
        Assert.Equal(WorkflowState.Idle, viewModel.CurrentState.State);
    }

    /// <summary>
    /// TEST: All states should be present in the correct order.
    /// </summary>
    [Fact]
    public void States_ShouldBeInCorrectOrder()
    {
        // Arrange
        var expectedOrder = new[]
        {
            WorkflowState.Idle,
            WorkflowState.WorklistSync,
            WorkflowState.PatientSelect,
            WorkflowState.ProtocolSelect,
            WorkflowState.PositionAndPreview,
            WorkflowState.ExposureTrigger,
            WorkflowState.QcReview,
            WorkflowState.MppsComplete,
            WorkflowState.PacsExport,
            WorkflowState.RejectRetake
        };

        // Act
        var viewModel = new StateMachineViewModel();

        // Assert
        for (int i = 0; i < expectedOrder.Length; i++)
        {
            Assert.Equal(expectedOrder[i], viewModel.States[i].State);
        }
    }

    /// <summary>
    /// TEST: StateChanged event should update current state.
    /// </summary>
    [Fact]
    public async Task OnWorkflowEvent_StateChanged_ShouldUpdateCurrentState()
    {
        // Arrange
        var viewModel = new StateMachineViewModel();
        var workflowEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            CurrentState = WorkflowState.ExposureTrigger,
            PreviousState = WorkflowState.PositionAndPreview
        };

        // Act
        viewModel.OnWorkflowEvent(workflowEvent);
        await Task.Delay(50); // Allow UI thread to process

        // Assert
        Assert.NotNull(viewModel.CurrentState);
        Assert.Equal(WorkflowState.ExposureTrigger, viewModel.CurrentState.State);
    }

    /// <summary>
    /// TEST: Transition history should record state changes.
    /// </summary>
    [Fact]
    public async Task OnWorkflowEvent_StateChanged_ShouldRecordTransitionHistory()
    {
        // Arrange
        var viewModel = new StateMachineViewModel();
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
        viewModel.OnWorkflowEvent(event1);
        await Task.Delay(50);
        viewModel.OnWorkflowEvent(event2);
        await Task.Delay(50);

        // Assert
        Assert.Equal(2, viewModel.TransitionHistory.Count);
        // Transitions are inserted at index 0, so most recent is first
        Assert.Equal(WorkflowState.ProtocolSelect, viewModel.TransitionHistory[0].ToState);
        Assert.Equal(WorkflowState.PatientSelect, viewModel.TransitionHistory[1].ToState);
    }

    /// <summary>
    /// TEST: Transition history should maintain maximum of 10 entries.
    /// </summary>
    [Fact]
    public async Task TransitionHistory_ShouldMaintainMaximum10Entries()
    {
        // Arrange
        var viewModel = new StateMachineViewModel();

        // Act - Add 15 transitions
        for (int i = 0; i < 15; i++)
        {
            var workflowEvent = new WorkflowEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                Type = WorkflowEventType.StateChanged,
                CurrentState = WorkflowState.ExposureTrigger,
                PreviousState = WorkflowState.Idle
            };
            viewModel.OnWorkflowEvent(workflowEvent);
            await Task.Delay(10);
        }

        // Assert
        Assert.Equal(10, viewModel.TransitionHistory.Count);
    }

    /// <summary>
    /// TEST: IsCurrent property should be true only for current state.
    /// </summary>
    [Fact]
    public void IsCurrent_ShouldBeTrueOnlyForCurrentState()
    {
        // Arrange
        var viewModel = new StateMachineViewModel();

        // Act & Assert
        foreach (var state in viewModel.States)
        {
            if (state.State == WorkflowState.Idle)
            {
                Assert.True(state.IsCurrent);
            }
            else
            {
                Assert.False(state.IsCurrent);
            }
        }
    }

    /// <summary>
    /// TEST: StateInfo should have display name.
    /// </summary>
    [Fact]
    public void StateInfo_ShouldHaveDisplayName()
    {
        // Arrange
        var viewModel = new StateMachineViewModel();

        // Act & Assert
        foreach (var state in viewModel.States)
        {
            Assert.False(string.IsNullOrWhiteSpace(state.DisplayName));
        }
    }

    /// <summary>
    /// TEST: TransitionInfo should have timestamp.
    /// </summary>
    [Fact]
    public async Task TransitionInfo_ShouldHaveTimestamp()
    {
        // Arrange
        var viewModel = new StateMachineViewModel();
        var workflowEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            CurrentState = WorkflowState.PatientSelect,
            PreviousState = WorkflowState.Idle
        };

        // Act
        viewModel.OnWorkflowEvent(workflowEvent);
        await Task.Delay(50);

        // Assert
        Assert.Single(viewModel.TransitionHistory);
        Assert.True(viewModel.TransitionHistory[0].Timestamp > DateTimeOffset.MinValue);
    }

    /// <summary>
    /// TEST: ClearHistory should reset transition history.
    /// </summary>
    [Fact]
    public async Task ClearHistory_ShouldResetTransitionHistory()
    {
        // Arrange
        var viewModel = new StateMachineViewModel();
        var workflowEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            CurrentState = WorkflowState.PatientSelect,
            PreviousState = WorkflowState.Idle
        };

        viewModel.OnWorkflowEvent(workflowEvent);
        await Task.Delay(50);

        // Act
        viewModel.ClearHistory();

        // Assert
        Assert.Empty(viewModel.TransitionHistory);
    }
}
