using System.Text.Json;
using HnVue.Dicom.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HnVue.Dicom.Queue;

/// <summary>
/// JSON-file-backed persistent retry queue for DICOM transmissions.
/// Survives application restarts; recovers PENDING and RETRYING items on startup (NFR-REL-01).
/// Thread safety is provided via <see cref="SemaphoreSlim"/> for all file access.
/// </summary>
/// <remarks>
/// Storage layout: one JSON file per queue item in the configured directory.
/// File name pattern: {itemId}.json
/// On startup: items with status Pending or Retrying are re-loaded into the in-memory index.
/// </remarks>
// @MX:NOTE: [AUTO] Each queue item is a separate JSON file to avoid read-write contention on a
//           single large file. On high-throughput scenarios, consider SQLite (plan.md section 2.3).
// @MX:ANCHOR: [AUTO] ITransmissionQueue is the core resilience boundary for FR-DICOM-08.
// @MX:REASON: fan_in >= 3 expected (StorageScu, background retry worker, recovery on startup)
public sealed class TransmissionQueue : ITransmissionQueue, IAsyncDisposable
{
    private readonly RetryQueueOptions _options;
    private readonly ILogger<TransmissionQueue> _logger;
    private readonly string _storageDirectory;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly Dictionary<Guid, TransmissionQueueItem> _itemIndex = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of <see cref="TransmissionQueue"/> and recovers
    /// PENDING/RETRYING items from persistent storage.
    /// </summary>
    /// <param name="options">The DICOM service configuration containing retry queue settings.</param>
    /// <param name="logger">Logger for queue lifecycle events.</param>
    public TransmissionQueue(
        IOptions<DicomServiceOptions> options,
        ILogger<TransmissionQueue> logger)
    {
        _options = options.Value.RetryQueue;
        _logger = logger;

        // Resolve storage path: support %AppData% and environment variable expansion
        var rawPath = string.IsNullOrWhiteSpace(_options.StoragePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HnVue",
                "DicomQueue")
            : _options.StoragePath;

        _storageDirectory = Environment.ExpandEnvironmentVariables(rawPath);

        Directory.CreateDirectory(_storageDirectory);

        // Synchronous recovery on construction - acceptable for startup path
        RecoverItemsFromDisk();
    }

    /// <inheritdoc/>
    public async Task<TransmissionQueueItem> EnqueueAsync(
        string sopInstanceUid,
        string filePath,
        string destinationAeTitle,
        CancellationToken cancellationToken = default)
    {
        var item = TransmissionQueueItem.CreateNew(sopInstanceUid, filePath, destinationAeTitle);

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistItemAsync(item, cancellationToken).ConfigureAwait(false);
            _itemIndex[item.Id] = item;

            _logger.LogDebug(
                "Queue item enqueued (Id: {ItemId}, Destination: {Destination}, PendingCount: {Count})",
                item.Id,
                item.DestinationAeTitle,
                _itemIndex.Count(kvp => IsActiveStatus(kvp.Value.Status)));
        }
        finally
        {
            _fileLock.Release();
        }

        return item;
    }

    /// <inheritdoc/>
    public async Task<TransmissionQueueItem?> DequeueNextAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;

            var nextItem = _itemIndex.Values
                .Where(i => IsActiveStatus(i.Status))
                .Where(i => i.NextRetryAt == null || i.NextRetryAt <= now)
                .OrderBy(i => i.CreatedAt)
                .FirstOrDefault();

            return nextItem;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(
        Guid id,
        QueueItemStatus newStatus,
        int attemptCount,
        DateTimeOffset? nextRetryAt,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_itemIndex.TryGetValue(id, out var existing))
            {
                _logger.LogWarning("UpdateStatusAsync: item {ItemId} not found in index", id);
                return;
            }

            var updated = existing with
            {
                Status = newStatus,
                AttemptCount = attemptCount,
                NextRetryAt = nextRetryAt,
                LastAttemptAt = DateTimeOffset.UtcNow,
                LastError = lastError
            };

            await PersistItemAsync(updated, cancellationToken).ConfigureAwait(false);
            _itemIndex[id] = updated;

            _logger.LogDebug(
                "Queue item updated (Id: {ItemId}, Status: {Status}, AttemptCount: {AttemptCount})",
                id,
                newStatus,
                attemptCount);

            if (newStatus == QueueItemStatus.Failed)
            {
                _logger.LogWarning(
                    "Queue item reached FAILED terminal state (Id: {ItemId}, Destination: {Destination}, Attempts: {Attempts}). Operator intervention required.",
                    id,
                    updated.DestinationAeTitle,
                    attemptCount);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _itemIndex.Values.Count(i => IsActiveStatus(i.Status));
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Computes the next retry timestamp using exponential back-off.
    /// Formula: lastAttemptAt + (initialInterval * pow(backoffMultiplier, attemptCount))
    /// </summary>
    /// <param name="attemptCount">The number of attempts that have been made (0-based).</param>
    /// <param name="lastAttemptAt">The time of the most recent attempt.</param>
    /// <returns>UTC timestamp for the next retry.</returns>
    public DateTimeOffset ComputeNextRetryAt(int attemptCount, DateTimeOffset lastAttemptAt)
    {
        var intervalSeconds = _options.InitialIntervalSeconds
            * Math.Pow(_options.BackoffMultiplier, attemptCount);

        var cappedSeconds = Math.Min(intervalSeconds, _options.MaxIntervalSeconds);
        return lastAttemptAt.AddSeconds(cappedSeconds);
    }

    private void RecoverItemsFromDisk()
    {
        var recovered = 0;
        var skipped = 0;

        foreach (var filePath in Directory.EnumerateFiles(_storageDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var item = JsonSerializer.Deserialize<TransmissionQueueItem>(json, JsonOptions);

                if (item is null)
                {
                    _logger.LogWarning("Failed to deserialize queue item from {FilePath}", filePath);
                    skipped++;
                    continue;
                }

                // Only recover active items; terminal items stay on disk for audit
                if (IsActiveStatus(item.Status))
                {
                    _itemIndex[item.Id] = item;
                    recovered++;
                }
                else
                {
                    // Load terminal items into index for deduplication but skip them for retry
                    _itemIndex[item.Id] = item;
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading queue item from {FilePath}", filePath);
                skipped++;
            }
        }

        if (recovered > 0)
        {
            _logger.LogInformation(
                "Queue recovery complete: {Recovered} active item(s) recovered, {Skipped} terminal/unreadable item(s) skipped.",
                recovered,
                skipped);
        }
    }

    private async Task PersistItemAsync(TransmissionQueueItem item, CancellationToken cancellationToken)
    {
        var filePath = GetItemFilePath(item.Id);
        var json = JsonSerializer.Serialize(item, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }

    private string GetItemFilePath(Guid itemId)
    {
        return Path.Combine(_storageDirectory, $"{itemId:D}.json");
    }

    private static bool IsActiveStatus(QueueItemStatus status)
    {
        return status is QueueItemStatus.Pending or QueueItemStatus.Retrying;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _fileLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
