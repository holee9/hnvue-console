using FluentAssertions;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Queue;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace HnVue.Dicom.Tests.Queue;

/// <summary>
/// Unit tests for the actual TransmissionQueue implementation class.
/// SPEC-DICOM-001 AC-01 Scenario 1.3 and AC-06: Retry Queue Resilience.
/// Uses isolated temp directories for file I/O; cleaned up in Dispose.
/// </summary>
public class TransmissionQueueTests : IDisposable
{
    private readonly string _testStoragePath;
    private readonly DicomServiceOptions _options;

    public TransmissionQueueTests()
    {
        _testStoragePath = Path.Combine(
            Path.GetTempPath(), "HnVueDicomTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testStoragePath);

        _options = new DicomServiceOptions
        {
            RetryQueue = new RetryQueueOptions
            {
                StoragePath = _testStoragePath,
                InitialIntervalSeconds = 30,
                BackoffMultiplier = 2.0,
                MaxIntervalSeconds = 3600,
                MaxRetryCount = 5
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testStoragePath))
        {
            Directory.Delete(_testStoragePath, recursive: true);
        }
    }

    private TransmissionQueue CreateSut()
    {
        return new TransmissionQueue(
            Options.Create(_options),
            NullLogger<TransmissionQueue>.Instance);
    }

    // EnqueueAsync creates a JSON file on disk and returns the item with correct fields
    [Fact]
    public async Task EnqueueAsync_NewItem_CreatesJsonFileAndReturnsCorrectItem()
    {
        // Arrange
        await using var sut = CreateSut();
        var sopInstanceUid = "1.2.3.4.5.100";
        var filePath = "/data/dicom/image.dcm";
        var destinationAeTitle = "ORTHANC";

        // Act
        var result = await sut.EnqueueAsync(sopInstanceUid, filePath, destinationAeTitle);

        // Assert: returned item
        result.Should().NotBeNull();
        result.SopInstanceUid.Should().Be(sopInstanceUid);
        result.FilePath.Should().Be(filePath);
        result.DestinationAeTitle.Should().Be(destinationAeTitle);
        result.Status.Should().Be(QueueItemStatus.Pending);
        result.AttemptCount.Should().Be(0, "new items have never been attempted");
        result.Id.Should().NotBe(Guid.Empty);

        // Assert: JSON file created on disk
        var jsonFile = Path.Combine(_testStoragePath, $"{result.Id:D}.json");
        File.Exists(jsonFile).Should().BeTrue(
            "EnqueueAsync must persist the queue item as a JSON file");
    }

    // EnqueueAsync JSON file contains correct serialized data
    [Fact]
    public async Task EnqueueAsync_NewItem_JsonFileContainsCorrectData()
    {
        // Arrange
        await using var sut = CreateSut();
        var sopInstanceUid = "1.2.3.4.5.101";
        var filePath = "/data/dicom/study.dcm";
        var destinationAeTitle = "PACS1";

        // Act
        var result = await sut.EnqueueAsync(sopInstanceUid, filePath, destinationAeTitle);

        // Assert: parse JSON from disk
        var jsonFile = Path.Combine(_testStoragePath, $"{result.Id:D}.json");
        var json = await File.ReadAllTextAsync(jsonFile);
        var deserialized = JsonSerializer.Deserialize<TransmissionQueueItem>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        deserialized.Should().NotBeNull();
        deserialized!.SopInstanceUid.Should().Be(sopInstanceUid);
        deserialized.DestinationAeTitle.Should().Be(destinationAeTitle);
        deserialized.Status.Should().Be(QueueItemStatus.Pending);
    }

    // DequeueNextAsync returns the oldest pending item (FIFO by CreatedAt)
    [Fact]
    public async Task DequeueNextAsync_WithPendingItems_ReturnsOldestItem()
    {
        // Arrange: enqueue two items with small delay to ensure distinct CreatedAt
        await using var sut = CreateSut();
        var first = await sut.EnqueueAsync("1.2.3.4.5.200", "/data/first.dcm", "PACS");
        await Task.Delay(10);
        await sut.EnqueueAsync("1.2.3.4.5.201", "/data/second.dcm", "PACS");

        // Act
        var dequeued = await sut.DequeueNextAsync();

        // Assert
        dequeued.Should().NotBeNull("there are pending items");
        dequeued!.Id.Should().Be(first.Id,
            "DequeueNextAsync must return the oldest pending item (FIFO by CreatedAt)");
    }

    // DequeueNextAsync returns null for empty queue
    [Fact]
    public async Task DequeueNextAsync_WhenQueueEmpty_ReturnsNull()
    {
        // Arrange
        await using var sut = CreateSut();

        // Act
        var result = await sut.DequeueNextAsync();

        // Assert
        result.Should().BeNull("empty queue returns null");
    }

    // DequeueNextAsync skips items with future NextRetryAt
    [Fact]
    public async Task DequeueNextAsync_ItemWithFutureRetryAt_ReturnsNull()
    {
        // Arrange
        await using var sut = CreateSut();
        var item = await sut.EnqueueAsync("1.2.3.4.5.202", "/data/test.dcm", "PACS");

        await sut.UpdateStatusAsync(
            item.Id,
            QueueItemStatus.Retrying,
            attemptCount: 1,
            nextRetryAt: DateTimeOffset.UtcNow.AddHours(1),
            lastError: "Connection refused");

        // Act
        var result = await sut.DequeueNextAsync();

        // Assert
        result.Should().BeNull("items with future NextRetryAt must not be returned");
    }

    // UpdateStatusAsync persists the new status to the JSON file
    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus_PersistsToFile()
    {
        // Arrange
        await using var sut = CreateSut();
        var item = await sut.EnqueueAsync("1.2.3.4.5.300", "/data/image.dcm", "PACS");

        // Act
        await sut.UpdateStatusAsync(
            item.Id,
            QueueItemStatus.Retrying,
            attemptCount: 1,
            nextRetryAt: DateTimeOffset.UtcNow.AddSeconds(30),
            lastError: "DICOM association failed");

        // Assert: parse JSON from disk
        var jsonFile = Path.Combine(_testStoragePath, $"{item.Id:D}.json");
        var json = await File.ReadAllTextAsync(jsonFile);
        var updated = JsonSerializer.Deserialize<TransmissionQueueItem>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(QueueItemStatus.Retrying,
            "UpdateStatusAsync must persist the new status to disk");
        updated.AttemptCount.Should().Be(1);
        updated.LastError.Should().Be("DICOM association failed");
    }

    // ComputeNextRetryAt uses exponential backoff formula: initial * multiplier^attempt
    [Theory]
    [InlineData(0, 30)]    // 30 * 2^0 = 30s
    [InlineData(1, 60)]    // 30 * 2^1 = 60s
    [InlineData(2, 120)]   // 30 * 2^2 = 120s
    [InlineData(3, 240)]   // 30 * 2^3 = 240s
    public async Task ComputeNextRetryAt_ExponentialBackoff_ReturnsCorrectInterval(
        int attemptCount, int expectedSeconds)
    {
        // Arrange
        await using var sut = CreateSut();
        var lastAttemptAt = DateTimeOffset.UtcNow;

        // Act
        var nextRetry = sut.ComputeNextRetryAt(attemptCount, lastAttemptAt);

        // Assert
        var actualSeconds = (nextRetry - lastAttemptAt).TotalSeconds;
        actualSeconds.Should().BeApproximately(expectedSeconds, precision: 1.0,
            $"attempt {attemptCount} must use delay {expectedSeconds}s per exponential backoff formula");
    }

    // ComputeNextRetryAt caps at MaxIntervalSeconds
    [Fact]
    public async Task ComputeNextRetryAt_WhenBackoffExceedsMax_CapsAtMaxInterval()
    {
        // Arrange: after 7 attempts: 30 * 2^7 = 3840 > 3600 (MaxIntervalSeconds)
        await using var sut = CreateSut();
        var lastAttemptAt = DateTimeOffset.UtcNow;

        // Act
        var nextRetry = sut.ComputeNextRetryAt(7, lastAttemptAt);

        // Assert
        var actualSeconds = (nextRetry - lastAttemptAt).TotalSeconds;
        actualSeconds.Should().BeApproximately(3600, precision: 1.0,
            "backoff interval must be capped at MaxIntervalSeconds (3600s) per AC-06");
    }

    // GetPendingCountAsync counts only active (Pending/Retrying) items
    [Fact]
    public async Task GetPendingCountAsync_WithCompletedAndActiveItems_CountsOnlyActive()
    {
        // Arrange: enqueue 3 items, complete one
        await using var sut = CreateSut();
        var item1 = await sut.EnqueueAsync("1.2.3.4.5.400", "/data/1.dcm", "PACS");
        await sut.EnqueueAsync("1.2.3.4.5.401", "/data/2.dcm", "PACS");
        await sut.EnqueueAsync("1.2.3.4.5.402", "/data/3.dcm", "PACS");

        await sut.UpdateStatusAsync(item1.Id, QueueItemStatus.Complete, 1, null, null);

        // Act
        var count = await sut.GetPendingCountAsync();

        // Assert
        count.Should().Be(2, "only active items (Pending/Retrying) count toward pending count");
    }

    // Recovery: new instance picks up Pending items from disk (survives restart)
    [Fact]
    public async Task Constructor_WithPendingItemsOnDisk_RecoversPendingItems()
    {
        // Arrange: first instance enqueues an item
        TransmissionQueueItem enqueuedItem;
        await using (var sut1 = CreateSut())
        {
            enqueuedItem = await sut1.EnqueueAsync("1.2.3.4.5.500", "/data/recover.dcm", "PACS");
        }

        // Act: second instance uses same storage path
        await using var sut2 = CreateSut();
        var count = await sut2.GetPendingCountAsync();
        var dequeued = await sut2.DequeueNextAsync();

        // Assert
        count.Should().Be(1, "new instance must recover Pending items from disk on startup");
        dequeued.Should().NotBeNull("recovered item must be available for dequeue");
        dequeued!.Id.Should().Be(enqueuedItem.Id);
        dequeued.SopInstanceUid.Should().Be("1.2.3.4.5.500");
    }

    // Recovery: terminal items are not counted as active after restart
    [Fact]
    public async Task Constructor_WithCompletedItemsOnDisk_DoesNotCountAsActive()
    {
        // Arrange: first instance enqueues then completes an item
        await using (var sut1 = CreateSut())
        {
            var item = await sut1.EnqueueAsync("1.2.3.4.5.501", "/data/done.dcm", "PACS");
            await sut1.UpdateStatusAsync(item.Id, QueueItemStatus.Complete, 1, null, null);
        }

        // Act: second instance
        await using var sut2 = CreateSut();
        var count = await sut2.GetPendingCountAsync();

        // Assert
        count.Should().Be(0, "terminal (Complete) items must not be counted as active after recovery");
    }

    // TransmissionQueueItem.CreateNew produces correct defaults
    [Fact]
    public void TransmissionQueueItem_CreateNew_ProducesCorrectDefaults()
    {
        // Arrange & Act
        var item = TransmissionQueueItem.CreateNew("1.2.3.4.5.999", "/data/test.dcm", "PACS1");

        // Assert
        item.Id.Should().NotBe(Guid.Empty);
        item.Status.Should().Be(QueueItemStatus.Pending);
        item.AttemptCount.Should().Be(0);
        item.NextRetryAt.Should().BeNull("new items are attempted immediately");
        item.LastAttemptAt.Should().BeNull("new items have never been attempted");
        item.LastError.Should().BeNull();
        item.SopInstanceUid.Should().Be("1.2.3.4.5.999");
        item.FilePath.Should().Be("/data/test.dcm");
        item.DestinationAeTitle.Should().Be("PACS1");
    }
}
