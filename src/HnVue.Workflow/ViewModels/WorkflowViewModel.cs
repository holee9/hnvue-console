namespace HnVue.Workflow.ViewModels;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using HnVue.Workflow.Events;

/// <summary>
/// Integration ViewModel that coordinates all workflow-related ViewModels.
/// SPEC-WORKFLOW-001 TASK-415: Workflow ViewModel Integration
/// </summary>
/// <remarks>
/// @MX:NOTE: Workflow view model integration - coordinates state machine, interlocks, dose
/// Subscribes to IWorkflowEventPublisher and updates all child ViewModels
/// Acts as the central coordinator for GUI workflow state
/// </remarks>
public sealed class WorkflowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task? _processingTask;
    private ChannelReader<WorkflowEvent>? _publisherChannel;

    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowViewModel"/> class.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Initialize child ViewModels and event processing infrastructure
    /// </remarks>
    public WorkflowViewModel()
    {
        StateMachine = new StateMachineViewModel();
        InterlockStatus = new InterlockStatusViewModel();
        DoseIndicator = new DoseIndicatorViewModel();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Gets the state machine ViewModel.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: State machine view model - displays workflow states and transitions
    /// </remarks>
    public StateMachineViewModel StateMachine { get; }

    /// <summary>
    /// Gets the interlock status ViewModel.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Interlock status view model - displays 9 safety interlocks
    /// </remarks>
    public InterlockStatusViewModel InterlockStatus { get; }

    /// <summary>
    /// Gets the dose indicator ViewModel.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Dose indicator view model - displays accumulated dose with limits
    /// </remarks>
    public DoseIndicatorViewModel DoseIndicator { get; }

    /// <summary>
    /// Starts listening to workflow events from the publisher.
    /// </summary>
    /// <param name="eventPublisher">The workflow event publisher.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Start event processing - subscribes to publisher channel
    /// Begins background task to process workflow events
    /// </remarks>
    public Task StartAsync(IWorkflowEventPublisher eventPublisher, CancellationToken cancellationToken = default)
    {
        // Subscribe to the publisher's channel
        _publisherChannel = eventPublisher.Subscribe();

        // Link the cancellation tokens
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationTokenSource.Token,
            cancellationToken);

        // Start the event processing loop
        _processingTask = ProcessEventsAsync(linkedCts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops listening to workflow events.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Stop event processing - cancels background tasks
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource.Cancel();

        if (_processingTask != null)
        {
            await _processingTask.WaitAsync(cancellationToken);
        }

        _publisherChannel = null;
    }

    /// <summary>
    /// Processes workflow events from the channel.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Process events loop - handles all workflow event types
    /// Updates child ViewModels based on event type and data
    /// </remarks>
    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        if (_publisherChannel == null)
        {
            return;
        }

        try
        {
            await foreach (var workflowEvent in _publisherChannel.ReadAllAsync(cancellationToken))
            {
                try
                {
                    ProcessWorkflowEvent(workflowEvent);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other events
                    Console.WriteLine($"Error processing workflow event: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellationToken is canceled
        }
    }

    /// <summary>
    /// Processes a single workflow event.
    /// </summary>
    /// <param name="workflowEvent">The workflow event to process.</param>
    /// <remarks>
    /// @MX:NOTE: Process single event - routes to appropriate child ViewModel
    /// StateChanged -> StateMachine, ExposureTriggered/Completed -> DoseIndicator,
    /// Error -> InterlockStatus
    /// </remarks>
    private void ProcessWorkflowEvent(WorkflowEvent workflowEvent)
    {
        switch (workflowEvent.Type)
        {
            case WorkflowEventType.StateChanged:
                StateMachine.OnWorkflowEvent(workflowEvent);
                break;

            case WorkflowEventType.ExposureTriggered:
            case WorkflowEventType.ExposureCompleted:
                UpdateDoseFromEvent(workflowEvent);
                break;

            case WorkflowEventType.Error:
                UpdateInterlockFromEvent(workflowEvent);
                break;

            // Other event types can be handled here as needed
        }
    }

    /// <summary>
    /// Updates the dose indicator from an exposure event.
    /// </summary>
    /// <param name="workflowEvent">The workflow event containing dose data.</param>
    /// <remarks>
    /// @MX:NOTE: Update dose indicator - extracts dose data from event payload
    /// Expected data format: { StudyTotalMGy: decimal, DailyTotalMGy: decimal }
    /// </remarks>
    private void UpdateDoseFromEvent(WorkflowEvent workflowEvent)
    {
        if (workflowEvent.Data != null)
        {
            try
            {
                // Use reflection to extract dose data from anonymous type
                var dataType = workflowEvent.Data.GetType();
                var studyTotalProperty = dataType.GetProperty("StudyTotalMGy");
                var dailyTotalProperty = dataType.GetProperty("DailyTotalMGy");

                if (studyTotalProperty != null && dailyTotalProperty != null)
                {
                    var studyTotalValue = studyTotalProperty.GetValue(workflowEvent.Data);
                    var dailyTotalValue = dailyTotalProperty.GetValue(workflowEvent.Data);

                    if (studyTotalValue is decimal studyTotal && dailyTotalValue is decimal dailyTotal)
                    {
                        DoseIndicator.UpdateDoseDisplay(studyTotal, dailyTotal);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting dose data: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Updates the interlock status from an error event.
    /// </summary>
    /// <param name="workflowEvent">The workflow event containing interlock data.</param>
    /// <remarks>
    /// @MX:NOTE: Update interlock status - extracts interlock data from event payload
    /// Expected data format: { InterlockIndex: int, Status: string }
    /// Status values: "Green", "Yellow", "Red"
    /// </remarks>
    private void UpdateInterlockFromEvent(WorkflowEvent workflowEvent)
    {
        if (workflowEvent.Data != null)
        {
            try
            {
                // Use reflection to extract interlock data from anonymous type
                var dataType = workflowEvent.Data.GetType();
                var interlockIndexProperty = dataType.GetProperty("InterlockIndex");
                var statusProperty = dataType.GetProperty("Status");

                if (interlockIndexProperty != null && statusProperty != null)
                {
                    var interlockIndexValue = interlockIndexProperty.GetValue(workflowEvent.Data);
                    var statusValue = statusProperty.GetValue(workflowEvent.Data);

                    if (interlockIndexValue is int interlockIndex && statusValue is string statusString)
                    {
                        var status = statusString switch
                        {
                            "Green" => ViewModels.InterlockStatus.Green,
                            "Yellow" => ViewModels.InterlockStatus.Yellow,
                            "Red" => ViewModels.InterlockStatus.Red,
                            _ => ViewModels.InterlockStatus.Green
                        };

                        InterlockStatus.UpdateInterlockStatus(interlockIndex, status);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting interlock data: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Disposes the view model and cancels background tasks.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Dispose pattern - cancels event processing
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        await StopAsync(default);
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
