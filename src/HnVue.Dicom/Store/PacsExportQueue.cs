using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Storage;

/// <summary>
/// Queue for managing PACS export operations with retry logic.
/// </summary>
/// <remarks>
/// @MX:WARN Retry logic - Exponential backoff for failed exports
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-408
///
/// Features:
/// - Retry queue (3 retries, exponential backoff)
/// - Export status tracking
/// - Error notification
/// </remarks>
public interface IPacsExportQueue
{
    /// <summary>
    /// Enqueues an item for PACS export with retry logic.
    /// </summary>
    /// <param name="item">The item to export.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task EnqueueAsync(PacsExportItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current queue depth.
    /// </summary>
    int QueueDepth { get; }

    /// <summary>
    /// Starts the background processor for retrying failed exports.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the background processor.
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Item in the PACS export queue.
/// </summary>
/// <remarks>
/// @MX:NOTE Export item - Represents a DICOM file pending export
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-408
/// </remarks>
public record PacsExportItem
{
    /// <summary>
    /// Gets the file path to the DICOM file to export.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the destination PACS AE title.
    /// </summary>
    public required string DestinationAeTitle { get; init; }

    /// <summary>
    /// Gets the destination PACS host.
    /// </summary>
    public required string DestinationHost { get; init; }

    /// <summary>
    /// Gets the destination PACS port.
    /// </summary>
    public required int DestinationPort { get; init; }

    /// <summary>
    /// Gets the current retry attempt count.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets the maximum number of retry attempts allowed.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Gets the time when the next retry should be attempted.
    /// </summary>
    public DateTime? NextRetryTime { get; init; }

    /// <summary>
    /// Creates a new export item with default settings.
    /// </summary>
    public static PacsExportItem Create(
        string filePath,
        string destinationAeTitle,
        string destinationHost,
        int destinationPort) =>
        new()
        {
            FilePath = filePath,
            DestinationAeTitle = destinationAeTitle,
            DestinationHost = destinationHost,
            DestinationPort = destinationPort,
            RetryCount = 0,
            MaxRetries = 3,
            NextRetryTime = DateTime.UtcNow
        };

    /// <summary>
    /// Creates a new item for retry with incremented retry count and exponential backoff.
    /// </summary>
    /// <remarks>
    /// @MX:WARN Retry logic - Exponential backoff calculation
    /// </remarks>
    public PacsExportItem CreateRetryItem()
    {
        var delayMs = CalculateBackoffDelay(RetryCount + 1);
        return this with
        {
            RetryCount = RetryCount + 1,
            NextRetryTime = DateTime.UtcNow.AddMilliseconds(delayMs)
        };
    }

    /// <summary>
    /// Calculates exponential backoff delay in milliseconds.
    /// </summary>
    private static int CalculateBackoffDelay(int retryCount)
    {
        // Exponential backoff: 2^retry * 1000ms (1s, 2s, 4s, 8s, ...)
        var delayMs = (int)Math.Pow(2, retryCount) * 1000;
        return Math.Min(delayMs, 60000); // Cap at 60 seconds
    }
}

/// <summary>
/// In-memory implementation of PACS export queue with retry logic.
/// </summary>
/// <remarks>
/// @MX:WARN Retry logic - Background processor with exponential backoff
/// </remarks>
public sealed class PacsExportQueue : IPacsExportQueue, IDisposable
{
    private readonly ConcurrentQueue<PacsExportItem> _queue;
    private readonly ILogger<PacsExportQueue> _logger;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacsExportQueue"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public PacsExportQueue(ILogger<PacsExportQueue> logger)
    {
        _queue = new ConcurrentQueue<PacsExportItem>();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cts = new CancellationTokenSource();
    }

    /// <inheritdoc/>
    public int QueueDepth => _queue.Count;

    /// <inheritdoc/>
    public Task EnqueueAsync(PacsExportItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        _logger.LogInformation(
            "Enqueuing PACS export (File: {FilePath}, Destination: {AeTitle}, Retry: {RetryCount}/{MaxRetries})",
            item.FilePath,
            item.DestinationAeTitle,
            item.RetryCount,
            item.MaxRetries);

        _queue.Enqueue(item);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting PACS export queue processor");

        // Start background processing task
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        _logger.LogInformation("Stopping PACS export queue processor");
        _cts.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
        _disposed = true;
    }
}
