namespace HnVue.Ipc.Client;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using HnVue.Workflow.Events;

/// <summary>
/// Client-side subscriber for workflow events from the Workflow Engine.
/// SPEC-WORKFLOW-001 TASK-411: Workflow Event Subscription Service
/// </summary>
/// <remarks>
/// @MX:NOTE: IPC workflow event subscriber - bridges backend events to frontend
/// Provides subscription management for GUI components to receive workflow state changes
/// </remarks>
public sealed class WorkflowEventSubscriber : IAsyncDisposable
{
    private readonly ChannelReader<WorkflowEvent> _reader;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowEventSubscriber"/> class.
    /// </summary>
    /// <param name="reader">The channel reader for receiving events.</param>
    public WorkflowEventSubscriber(ChannelReader<WorkflowEvent> reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _cts = new CancellationTokenSource();
        _isDisposed = false;

        // Start background processing task
        _processingTask = ProcessEventsAsync(_cts.Token);
    }

    /// <summary>
    /// Event raised when a workflow event is received.
    /// </summary>
    public event EventHandler<WorkflowEvent>? EventReceived;

    /// <summary>
    /// Gets the reader for directly consuming events.
    /// </summary>
    public ChannelReader<WorkflowEvent> Reader => _reader;

    /// <summary>
    /// Processes workflow events in the background.
    /// </summary>
    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var workflowEvent in _reader.ReadAllAsync(cancellationToken))
        {
            // Raise event for subscribers
            EventReceived?.Invoke(this, workflowEvent);
        }
    }

    /// <summary>
    /// Disposes the subscriber.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _cts.Cancel();
        _cts.Dispose();

        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }
}
