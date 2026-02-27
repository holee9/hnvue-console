namespace HnVue.Dicom.Queue;

/// <summary>
/// Persistent retry queue for DICOM transmission operations.
/// Provides durable storage that survives application restarts (FR-DICOM-08, NFR-REL-01).
/// </summary>
public interface ITransmissionQueue
{
    /// <summary>
    /// Enqueues a DICOM object for transmission.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID of the DICOM object.</param>
    /// <param name="filePath">Absolute path to the DICOM file on disk.</param>
    /// <param name="destinationAeTitle">Called AE Title of the target SCP.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created queue item.</returns>
    Task<TransmissionQueueItem> EnqueueAsync(
        string sopInstanceUid,
        string filePath,
        string destinationAeTitle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next item that is ready for transmission (status is Pending or Retrying
    /// and <see cref="TransmissionQueueItem.NextRetryAt"/> is in the past or null).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next eligible item, or null if no items are ready.</returns>
    Task<TransmissionQueueItem?> DequeueNextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status and retry metadata for a queue item.
    /// </summary>
    /// <param name="id">The unique ID of the item to update.</param>
    /// <param name="newStatus">The new status to apply.</param>
    /// <param name="attemptCount">Updated attempt count.</param>
    /// <param name="nextRetryAt">When the next retry should occur. Null for terminal states.</param>
    /// <param name="lastError">Error message from the most recent failure, or null on success.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateStatusAsync(
        Guid id,
        QueueItemStatus newStatus,
        int attemptCount,
        DateTimeOffset? nextRetryAt,
        string? lastError,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of items in non-terminal states (Pending or Retrying).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of active queue items.</returns>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}
