namespace HnVue.Workflow.Tests.Events;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using HnVue.Workflow.Events;
using HnVue.Workflow.StateMachine;
using Xunit;

/// <summary>
/// Tests for WorkflowEventSubscriptionService.
/// SPEC-WORKFLOW-001 TASK-411: Workflow Event Subscription Service
/// </summary>
/// <remarks>
/// @MX:NOTE: TDD test suite for event subscription service
/// Tests cover: Observable pattern, event delivery timing, thread-safety, type-safe event data
/// </remarks>
public class WorkflowEventSubscriptionServiceTests
{
    /// <summary>
    /// TEST: Subscribe should return a channel reader that receives events.
    /// </summary>
    [Fact]
    public async Task Subscribe_ShouldReturnChannelReader_ThatReceivesEvents()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();
        var subscription = service.Subscribe();

        // Act
        var testEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            CurrentState = WorkflowState.Idle,
            PreviousState = WorkflowState.WorklistSync
        };

        await service.PublishAsync(testEvent);

        // Assert
        Assert.True(subscription.TryRead(out var receivedEvent));
        Assert.Equal(testEvent.EventId, receivedEvent.EventId);
        Assert.Equal(testEvent.Type, receivedEvent.Type);
        Assert.Equal(testEvent.CurrentState, receivedEvent.CurrentState);

        await service.DisposeAsync();
    }

    /// <summary>
    /// TEST: Event delivery should complete within 50ms.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ShouldDeliverEvent_Within50Ms()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();
        var subscription = service.Subscribe();
        var eventReceived = new TaskCompletionSource<bool>();

        // Start background task to wait for event
        _ = Task.Run(async () =>
        {
            await subscription.WaitToReadAsync();
            eventReceived.SetResult(true);
        });

        var testEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged,
            CurrentState = WorkflowState.Idle
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        await service.PublishAsync(testEvent);
        await eventReceived.Task;

        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"Event delivery took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");

        await service.DisposeAsync();
    }

    /// <summary>
    /// TEST: Multiple subscribers should all receive the same event.
    /// </summary>
    [Fact]
    public async Task Subscribe_MultipleSubscribers_ShouldAllReceiveEvents()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();
        var subscription1 = service.Subscribe();
        var subscription2 = service.Subscribe();
        var subscription3 = service.Subscribe();

        var testEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.ExposureTriggered,
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001"
        };

        // Act
        await service.PublishAsync(testEvent);

        // Assert
        Assert.True(await subscription1.WaitToReadAsync(default));
        Assert.True(await subscription2.WaitToReadAsync(default));
        Assert.True(await subscription3.WaitToReadAsync(default));

        var received1 = subscription1.TryRead(out var event1);
        var received2 = subscription2.TryRead(out var event2);
        var received3 = subscription3.TryRead(out var event3);

        Assert.True(received1);
        Assert.True(received2);
        Assert.True(received3);

        Assert.NotNull(event1);
        Assert.NotNull(event2);
        Assert.NotNull(event3);
        Assert.Equal(testEvent.EventId, event1.EventId);
        Assert.Equal(testEvent.EventId, event2.EventId);
        Assert.Equal(testEvent.EventId, event3.EventId);

        await service.DisposeAsync();
    }

    /// <summary>
    /// TEST: Thread-safe event dispatch from multiple publishers.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleParallelPublishers_ShouldBeThreadSafe()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();
        var subscription = service.Subscribe();
        var receivedEvents = new List<WorkflowEvent>();
        var eventCount = 100;

        // Start background task to collect events
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < eventCount; i++)
            {
                await subscription.WaitToReadAsync(default);
                if (subscription.TryRead(out var evt) && evt != null)
                {
                    lock (receivedEvents)
                    {
                        receivedEvents.Add(evt);
                    }
                }
            }
        });

        // Act - Publish from multiple threads in parallel
        var publishTasks = new List<Task>();
        for (int i = 0; i < eventCount; i++)
        {
            var index = i;
            publishTasks.Add(Task.Run(async () =>
            {
                var testEvent = new WorkflowEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = WorkflowEventType.StateChanged,
                    Data = index
                };
                await service.PublishAsync(testEvent);
            }));
        }

        await Task.WhenAll(publishTasks);

        // Wait for all events to be received
        await Task.Delay(500);

        // Assert
        lock (receivedEvents)
        {
            Assert.Equal(eventCount, receivedEvents.Count);
        }

        await service.DisposeAsync();
    }

    /// <summary>
    /// TEST: Unsubscribing should stop receiving events.
    /// </summary>
    [Fact]
    public async Task Unsubscribe_ShouldStopReceivingEvents()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();
        var subscription = service.Subscribe();

        var testEvent1 = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged
        };

        // Act
        await service.PublishAsync(testEvent1);
        Assert.True(subscription.TryRead(out var _)); // First event received

        // Unsubscribe
        service.Unsubscribe(subscription);

        var testEvent2 = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.ExposureCompleted
        };

        await service.PublishAsync(testEvent2);

        // Assert - Give some time for any potential event to arrive
        await Task.Delay(100);
        Assert.False(subscription.TryRead(out var _)); // No event after unsubscribe

        await service.DisposeAsync();
    }

    /// <summary>
    /// TEST: Event data should preserve type information.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ShouldPreserveEventDataTypes()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();
        var subscription = service.Subscribe();

        var testData = new
        {
            ExposureIndex = 5,
            Dose = 1.5,
            Reason = "Motion artifact"
        };

        var testEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.ImageRejected,
            Data = testData
        };

        // Act
        await service.PublishAsync(testEvent);
        var hasEvent = subscription.TryRead(out var receivedEvent);

        // Assert
        Assert.True(hasEvent);
        Assert.NotNull(receivedEvent);
        Assert.NotNull(receivedEvent.Data);
        var data = receivedEvent.Data;
        Assert.NotNull(data);

        await service.DisposeAsync();
    }

    /// <summary>
    /// TEST: GetSubscriberCount should return accurate count.
    /// </summary>
    [Fact]
    public async Task GetSubscriberCount_ShouldReturnAccurateCount()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();

        // Assert - Initially zero
        Assert.Equal(0, service.GetSubscriberCount());

        // Act - Add subscribers
        var sub1 = service.Subscribe();
        Assert.Equal(1, service.GetSubscriberCount());

        var sub2 = service.Subscribe();
        Assert.Equal(2, service.GetSubscriberCount());

        var sub3 = service.Subscribe();
        Assert.Equal(3, service.GetSubscriberCount());

        // Act - Remove subscribers
        service.Unsubscribe(sub2);
        Assert.Equal(2, service.GetSubscriberCount());

        service.Unsubscribe(sub1);
        Assert.Equal(1, service.GetSubscriberCount());

        service.Unsubscribe(sub3);
        Assert.Equal(0, service.GetSubscriberCount());

        await service.DisposeAsync();
    }

    /// <summary>
    /// TEST: Publishing after disposal should not throw.
    /// </summary>
    [Fact]
    public async Task PublishAsync_AfterDisposal_ShouldNotThrow()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();
        await service.DisposeAsync();

        var testEvent = new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = WorkflowEventType.StateChanged
        };

        // Act & Assert - Should not throw
        await service.PublishAsync(testEvent);
    }

    /// <summary>
    /// TEST: Subscribe after disposal should return a completed channel.
    /// </summary>
    [Fact]
    public async Task Subscribe_AfterDisposal_ShouldReturnCompletedChannel()
    {
        // Arrange
        var service = new WorkflowEventSubscriptionService();
        await service.DisposeAsync();

        // Act
        var subscription = service.Subscribe();

        // Assert - Channel should be completed
        Assert.True(subscription.Completion.IsCompleted);
    }
}
