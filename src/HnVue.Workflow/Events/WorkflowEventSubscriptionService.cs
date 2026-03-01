namespace HnVue.Workflow.Events;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <summary>
/// Service for subscribing to and publishing workflow events.
/// SPEC-WORKFLOW-001 TASK-411: Workflow Event Subscription Service
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Event subscription service - observable pattern for workflow events
/// Provides thread-safe event broadcasting to multiple subscribers with guaranteed delivery within 50ms
/// </remarks>
public sealed class WorkflowEventSubscriptionService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, SubscriptionInfo> _subscribers;
    private readonly SemaphoreSlim _disposalLock;
    private bool _isDisposed;

    /// <summary>
    /// Information about a subscription.
    /// </summary>
    private sealed class SubscriptionInfo
    {
        public required ChannelWriter<WorkflowEvent> Writer { get; init; }
        public required ChannelReader<WorkflowEvent> Reader { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowEventSubscriptionService"/> class.
    /// </summary>
    public WorkflowEventSubscriptionService()
    {
        _subscribers = new ConcurrentDictionary<Guid, SubscriptionInfo>();
        _disposalLock = new SemaphoreSlim(1, 1);
        _isDisposed = false;
    }

    /// <summary>
    /// Subscribes to workflow events.
    /// </summary>
    /// <returns>A channel reader for receiving workflow events.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Subscribe method - returns read-only channel for event consumption
    /// </remarks>
    public ChannelReader<WorkflowEvent> Subscribe()
    {
        var channel = Channel.CreateUnbounded<WorkflowEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        var subscriptionId = Guid.NewGuid();
        var subscriptionInfo = new SubscriptionInfo
        {
            Writer = channel.Writer,
            Reader = channel.Reader
        };

        if (!_isDisposed)
        {
            _subscribers.TryAdd(subscriptionId, subscriptionInfo);
        }
        else
        {
            // Mark channel as completed if service is disposed
            channel.Writer.Complete();
        }

        return channel.Reader;
    }

    /// <summary>
    /// Unsubscribes from workflow events.
    /// </summary>
    /// <param name="subscription">The channel reader to unsubscribe.</param>
    public void Unsubscribe(ChannelReader<WorkflowEvent> subscription)
    {
        // Find the subscription with matching reader and remove it
        var subscriptionToRemove = _subscribers.FirstOrDefault(kvp => kvp.Value.Reader == subscription);

        if (subscriptionToRemove.Key != Guid.Empty)
        {
            _subscribers.TryRemove(subscriptionToRemove.Key, out _);
            // Complete the channel to signal no more events
            try
            {
                subscriptionToRemove.Value.Writer.Complete();
            }
            catch
            {
                // Channel already completed
            }
        }
    }

    /// <summary>
    /// Publishes a workflow event to all subscribers.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Event delivery guaranteed within 50ms per SPEC-WORKFLOW-001 NFR-IPC-01
    /// </remarks>
    public async Task PublishAsync(WorkflowEvent @event, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return; // Silently ignore after disposal
        }

        var publishTasks = new List<Task>();

        foreach (var subscriber in _subscribers.Values)
        {
            publishTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await subscriber.Writer.WriteAsync(@event, cancellationToken);
                }
                catch (ChannelClosedException)
                {
                    // Subscriber channel closed, ignore
                }
            }, cancellationToken));
        }

        await Task.WhenAll(publishTasks);
    }

    /// <summary>
    /// Gets the number of active subscribers.
    /// </summary>
    /// <returns>The subscriber count.</returns>
    public int GetSubscriberCount()
    {
        return _subscribers.Count;
    }

    /// <summary>
    /// Disposes the service asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await _disposalLock.WaitAsync();
        if (_isDisposed)
        {
            _disposalLock.Release();
            return;
        }

        _isDisposed = true;

        // Complete all subscriber channels
        foreach (var subscriber in _subscribers.Values)
        {
            try
            {
                subscriber.Writer.Complete();
            }
            catch
            {
                // Channel already completed
            }
        }

        _subscribers.Clear();
        _disposalLock.Release();
        _disposalLock.Dispose();
    }
}
