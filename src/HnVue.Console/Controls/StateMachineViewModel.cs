namespace HnVue.Console.Controls;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HnVue.Workflow.Events;
using HnVue.Workflow.StateMachine;

/// <summary>
/// ViewModel for state machine visualization component.
/// SPEC-WORKFLOW-001 TASK-412: State Machine Visualization Component
/// </summary>
/// <remarks>
/// @MX:NOTE: State machine visualization - displays workflow states and transitions
/// Provides real-time state highlighting and transition history tracking
/// </remarks>
public sealed class StateMachineViewModel : INotifyPropertyChanged
{
    private const int MaxTransitionHistory = 10;
    private StateInfo? _currentState;

    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineViewModel"/> class.
    /// </summary>
    public StateMachineViewModel()
    {
        States = new ObservableCollection<StateInfo>
        {
            new StateInfo(WorkflowState.Idle, "Idle"),
            new StateInfo(WorkflowState.WorklistSync, "Worklist Sync"),
            new StateInfo(WorkflowState.PatientSelect, "Patient Select"),
            new StateInfo(WorkflowState.ProtocolSelect, "Protocol Select"),
            new StateInfo(WorkflowState.PositionAndPreview, "Position & Preview"),
            new StateInfo(WorkflowState.ExposureTrigger, "Exposure Trigger"),
            new StateInfo(WorkflowState.QcReview, "QC Review"),
            new StateInfo(WorkflowState.MppsComplete, "MPPS Complete"),
            new StateInfo(WorkflowState.PacsExport, "PACS Export"),
            new StateInfo(WorkflowState.RejectRetake, "Reject / Retake")
        };

        TransitionHistory = new ObservableCollection<TransitionInfo>();
        CurrentState = States[0]; // Initial state is Idle
    }

    /// <summary>
    /// Gets the collection of workflow states.
    /// </summary>
    public ObservableCollection<StateInfo> States { get; }

    /// <summary>
    /// Gets or sets the current workflow state.
    /// </summary>
    public StateInfo? CurrentState
    {
        get => _currentState;
        set
        {
            if (_currentState != value)
            {
                // Update IsCurrent for old state
                if (_currentState != null)
                {
                    _currentState.IsCurrent = false;
                }

                _currentState = value;

                // Update IsCurrent for new state
                if (_currentState != null)
                {
                    _currentState.IsCurrent = true;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStateDisplayName));
            }
        }
    }

    /// <summary>
    /// Gets the display name of the current state.
    /// </summary>
    public string? CurrentStateDisplayName => CurrentState?.DisplayName;

    /// <summary>
    /// Gets the transition history.
    /// </summary>
    public ObservableCollection<TransitionInfo> TransitionHistory { get; }

    /// <summary>
    /// Handles workflow events and updates state machine visualization.
    /// </summary>
    /// <param name="workflowEvent">The workflow event to process.</param>
    public void OnWorkflowEvent(WorkflowEvent workflowEvent)
    {
        if (workflowEvent.Type == WorkflowEventType.StateChanged &&
            workflowEvent.CurrentState.HasValue)
        {
            // Update current state
            var newState = States.FindState(workflowEvent.CurrentState.Value);
            if (newState != null)
            {
                CurrentState = newState;
            }

            // Add to transition history
            if (workflowEvent.PreviousState.HasValue)
            {
                var transition = new TransitionInfo(
                    workflowEvent.PreviousState.Value,
                    workflowEvent.CurrentState.Value,
                    workflowEvent.Timestamp);

                TransitionHistory.Insert(0, transition);

                // Maintain maximum history size
                while (TransitionHistory.Count > MaxTransitionHistory)
                {
                    TransitionHistory.RemoveAt(TransitionHistory.Count - 1);
                }
            }
        }
    }

    /// <summary>
    /// Clears the transition history.
    /// </summary>
    public void ClearHistory()
    {
        TransitionHistory.Clear();
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Information about a workflow state.
/// </summary>
public sealed class StateInfo : INotifyPropertyChanged
{
    private bool _isCurrent;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateInfo"/> class.
    /// </summary>
    /// <param name="state">The workflow state.</param>
    /// <param name="displayName">The display name.</param>
    public StateInfo(WorkflowState state, string displayName)
    {
        State = state;
        DisplayName = displayName;
        _isCurrent = false;
    }

    /// <summary>
    /// Gets the workflow state.
    /// </summary>
    public WorkflowState State { get; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the current state.
    /// </summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent != value)
            {
                _isCurrent = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Information about a state transition.
/// </summary>
public sealed record TransitionInfo(
    WorkflowState FromState,
    WorkflowState ToState,
    DateTimeOffset Timestamp
);

/// <summary>
/// Extension methods for StateInfo collection.
/// </summary>
internal static class StateInfoExtensions
{
    /// <summary>
    /// Finds a state by workflow state value.
    /// </summary>
    public static StateInfo? FindState(this IList<StateInfo> states, WorkflowState state)
    {
        foreach (var s in states)
        {
            if (s.State == state)
            {
                return s;
            }
        }
        return null;
    }
}
