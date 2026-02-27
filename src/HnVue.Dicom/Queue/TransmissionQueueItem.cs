namespace HnVue.Dicom.Queue;

/// <summary>
/// Represents a single DICOM object pending transmission in the retry queue.
/// Immutable record - use <c>with</c> expressions to produce updated instances.
/// </summary>
/// <param name="Id">Unique identifier for this queue item.</param>
/// <param name="SopInstanceUid">The DICOM SOP Instance UID of the object to transmit.</param>
/// <param name="FilePath">Absolute path to the DICOM file on disk.</param>
/// <param name="DestinationAeTitle">Called AE Title of the target SCP.</param>
/// <param name="Status">Current lifecycle status of this item.</param>
/// <param name="AttemptCount">Number of transmission attempts made so far (0 = never attempted).</param>
/// <param name="CreatedAt">UTC timestamp when this item was first enqueued.</param>
/// <param name="NextRetryAt">UTC timestamp of the next scheduled transmission attempt. Null when Pending (attempt immediately).</param>
/// <param name="LastAttemptAt">UTC timestamp of the most recent attempt. Null when never attempted.</param>
/// <param name="LastError">Human-readable error message from the most recent failed attempt. Null when never failed.</param>
public sealed record TransmissionQueueItem(
    Guid Id,
    string SopInstanceUid,
    string FilePath,
    string DestinationAeTitle,
    QueueItemStatus Status,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset? LastAttemptAt,
    string? LastError)
{
    /// <summary>
    /// Creates a new queue item in <see cref="QueueItemStatus.Pending"/> state,
    /// ready for immediate first transmission.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID of the DICOM object.</param>
    /// <param name="filePath">Absolute path to the DICOM file.</param>
    /// <param name="destinationAeTitle">Called AE Title of the target SCP.</param>
    /// <returns>A new queue item ready for enqueue.</returns>
    public static TransmissionQueueItem CreateNew(
        string sopInstanceUid,
        string filePath,
        string destinationAeTitle)
    {
        return new TransmissionQueueItem(
            Id: Guid.NewGuid(),
            SopInstanceUid: sopInstanceUid,
            FilePath: filePath,
            DestinationAeTitle: destinationAeTitle,
            Status: QueueItemStatus.Pending,
            AttemptCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            NextRetryAt: null,
            LastAttemptAt: null,
            LastError: null);
    }
}
