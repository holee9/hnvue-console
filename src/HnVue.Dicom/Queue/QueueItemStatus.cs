namespace HnVue.Dicom.Queue;

/// <summary>
/// Represents the lifecycle state of a transmission queue item.
/// </summary>
public enum QueueItemStatus
{
    /// <summary>
    /// Item is waiting for its first transmission attempt or scheduled retry.
    /// </summary>
    Pending,

    /// <summary>
    /// Item has failed at least once and is scheduled for a retry attempt.
    /// </summary>
    Retrying,

    /// <summary>
    /// Item has been successfully transmitted to all configured destinations.
    /// Terminal state - item is retained for audit.
    /// </summary>
    Complete,

    /// <summary>
    /// Item has exhausted all retry attempts and cannot be automatically recovered.
    /// Terminal state - operator intervention required.
    /// </summary>
    Failed
}
