namespace HnVue.Workflow.Events;

using System.Threading.Channels;

/// <summary>
/// Defines the contract for publishing workflow events to IPC subscribers.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Workflow event publisher - IPC event broadcasting
/// @MX:SPEC: SPEC-WORKFLOW-001 NFR-IPC-01
///
/// Provides asynchronous event streaming for UI components and other
/// subscribers to receive real-time workflow state updates.
/// </remarks>
public interface IWorkflowEventPublisher
{
    /// <summary>
    /// Publishes a workflow event to all subscribers.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishEventAsync(WorkflowEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a channel reader for subscribing to workflow events.
    /// </summary>
    /// <returns>A channel reader for receiving workflow events.</returns>
    /// <remarks>
    /// @MX:NOTE: Event subscription - read-only channel for consumers
    /// </remarks>
    ChannelReader<WorkflowEvent> Subscribe();

    /// <summary>
    /// Gets the current event queue depth.
    /// </summary>
    int QueueDepth { get; }
}

/// <summary>
/// Represents a workflow state change event.
/// </summary>
/// <remarks>
/// @MX:NOTE: Workflow event - base event type for state changes
/// </remarks>
public sealed record WorkflowEvent
{
    /// <summary>
    /// Gets the unique event identifier.
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Gets the event timestamp (UTC).
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the event type.
    /// </summary>
    public required WorkflowEventType Type { get; init; }

    /// <summary>
    /// Gets the study identifier, if applicable.
    /// </summary>
    public string? StudyId { get; init; }

    /// <summary>
    /// Gets the patient identifier, if applicable.
    /// </summary>
    public string? PatientId { get; init; }

    /// <summary>
    /// Gets the current workflow state.
    /// </summary>
    public StateMachine.WorkflowState? CurrentState { get; init; }

    /// <summary>
    /// Gets the previous workflow state, if applicable.
    /// </summary>
    public StateMachine.WorkflowState? PreviousState { get; init; }

    /// <summary>
    /// Gets the event data payload.
    /// </summary>
    public object? Data { get; init; }
}

/// <summary>
/// Represents the type of workflow event.
/// </summary>
/// <remarks>
/// @MX:NOTE: Workflow event type enumeration
/// </remarks>
public enum WorkflowEventType
{
    /// <summary>Workflow state changed.</summary>
    StateChanged,

    /// <summary>Patient selected.</summary>
    PatientSelected,

    /// <summary>Protocol selected.</summary>
    ProtocolSelected,

    /// <summary>Worklist synchronized.</summary>
    WorklistSynced,

    /// <summary>Exposure triggered.</summary>
    ExposureTriggered,

    /// <summary>Exposure completed.</summary>
    ExposureCompleted,

    /// <summary>Image acquired.</summary>
    ImageAcquired,

    /// <summary>Image accepted.</summary>
    ImageAccepted,

    /// <summary>Image rejected.</summary>
    ImageRejected,

    /// <summary>Study completed.</summary>
    StudyCompleted,

    /// <summary>Error occurred.</summary>
    Error,

    /// <summary>Warning issued.</summary>
    Warning
}

/// <summary>
/// In-memory implementation of workflow event publisher.
/// </summary>
/// <remarks>
/// @MX:NOTE: In-memory event bus - async channel-based broadcasting
/// </remarks>
public sealed class InMemoryWorkflowEventPublisher : IWorkflowEventPublisher, IDisposable
{
    private readonly Channel<WorkflowEvent> _eventChannel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWorkflowEventPublisher"/> class.
    /// </summary>
    public InMemoryWorkflowEventPublisher()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        };
        _eventChannel = Channel.CreateUnbounded<WorkflowEvent>(options);
        _cts = new CancellationTokenSource();

        // Start background processing task (currently a no-op, events are consumed by readers)
        _processingTask = Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task PublishEventAsync(WorkflowEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _eventChannel.Writer.WriteAsync(@event, cancellationToken);
        }
        catch (ChannelClosedException)
        {
            // Channel closed, ignore
        }
    }

    /// <inheritdoc/>
    public ChannelReader<WorkflowEvent> Subscribe()
    {
        return _eventChannel.Reader;
    }

    /// <inheritdoc/>
    public int QueueDepth => 0; // Unbounded channel

    /// <summary>
    /// Disposes the event publisher and closes the event channel.
    /// </summary>
    public void Dispose()
    {
        _cts.Cancel();
        _eventChannel.Writer.Complete();
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Task may have been cancelled
        }
        _cts.Dispose();
    }
}

/// <summary>
/// Extension methods for creating workflow events.
/// </summary>
/// <remarks>
/// @MX:NOTE: Workflow event extensions - fluent event creation
/// </remarks>
public static class WorkflowEventExtensions
{
    /// <summary>
    /// Creates a state changed event.
    /// </summary>
    /// <param name="previousState">The previous state.</param>
    /// <param name="currentState">The current state.</param>
    /// <param name="studyId">The study identifier.</param>
    /// <returns>A new workflow event.</returns>
    public static WorkflowEvent StateChanged(
        StateMachine.WorkflowState previousState,
        StateMachine.WorkflowState currentState,
        string? studyId = null)
    {
        return new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            StudyId = studyId,
            CurrentState = currentState,
            PreviousState = previousState
        };
    }

    /// <summary>
    /// Creates an exposure triggered event.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="patientId">The patient identifier.</param>
    /// <param name="data">Exposure parameters.</param>
    /// <returns>A new workflow event.</returns>
    public static WorkflowEvent ExposureTriggered(
        string studyId,
        string patientId,
        object? data = null)
    {
        return new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.ExposureTriggered,
            StudyId = studyId,
            PatientId = patientId,
            Data = data
        };
    }

    /// <summary>
    /// Creates an error event.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A new workflow event.</returns>
    /// <remarks>
    /// @MX:WARN: Error event - indicates workflow failure
    /// </remarks>
    public static WorkflowEvent Error(string studyId, string errorMessage)
    {
        return new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.Error,
            StudyId = studyId,
            Data = new { Message = errorMessage }
        };
    }
}
