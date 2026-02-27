namespace HnVue.Dicom.StorageCommit;

/// <summary>
/// Event arguments for a Storage Commitment N-EVENT-REPORT response.
/// </summary>
public sealed class CommitmentReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the Transaction UID that matches the original N-ACTION request.
    /// </summary>
    public string TransactionUid { get; }

    /// <summary>
    /// Gets the list of SOP instances confirmed as committed by the SCP.
    /// </summary>
    public IReadOnlyList<(string SopClassUid, string SopInstanceUid)> CommittedItems { get; }

    /// <summary>
    /// Gets the list of SOP instances that the SCP failed to commit.
    /// Per FR-DICOM-05, any items here must be flagged for re-transmission.
    /// </summary>
    public IReadOnlyList<(string SopClassUid, string SopInstanceUid, ushort FailureReason)> FailedItems { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CommitmentReceivedEventArgs"/>.
    /// </summary>
    public CommitmentReceivedEventArgs(
        string transactionUid,
        IReadOnlyList<(string SopClassUid, string SopInstanceUid)> committedItems,
        IReadOnlyList<(string SopClassUid, string SopInstanceUid, ushort FailureReason)> failedItems)
    {
        TransactionUid = transactionUid;
        CommittedItems = committedItems;
        FailedItems = failedItems;
    }
}

/// <summary>
/// Provides Storage Commitment SCU operations per IHE SWF RAD-10.
/// Sends N-ACTION requests and listens for N-EVENT-REPORT confirmations.
/// SOP Class: 1.2.840.10008.1.3.10 (Storage Commitment Push Model)
/// </summary>
public interface IStorageCommitScu
{
    /// <summary>
    /// Raised when an N-EVENT-REPORT Storage Commitment response is received from the SCP.
    /// Callers MUST handle this event to detect failed items and trigger re-transmission per FR-DICOM-05.
    /// </summary>
    event EventHandler<CommitmentReceivedEventArgs> CommitmentReceived;

    /// <summary>
    /// Sends a Storage Commitment N-ACTION request to the configured SCP for the provided SOP instances.
    /// </summary>
    /// <remarks>
    /// This method sends the N-ACTION and returns immediately after the request is acknowledged.
    /// The actual commitment confirmation arrives asynchronously via the <see cref="CommitmentReceived"/> event,
    /// which may be raised on a background thread or after the SCP re-associates back to receive the N-EVENT-REPORT.
    /// The commitment state is maintained in memory for up to <see cref="Configuration.TimeoutOptions.StorageCommitmentWaitMs"/> milliseconds.
    /// </remarks>
    /// <param name="sopInstances">The SOP class / instance UID pairs to request commitment for.</param>
    /// <param name="cancellationToken">Token to cancel the N-ACTION send operation.</param>
    /// <returns>
    /// The Transaction UID generated for this commitment request.
    /// Callers should correlate this with the <see cref="CommitmentReceivedEventArgs.TransactionUid"/> in the event.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when StorageCommitScp is not configured.</exception>
    /// <exception cref="DicomStorageCommitException">Thrown when the N-ACTION request itself is rejected by the SCP.</exception>
    Task<string> RequestCommitAsync(
        IEnumerable<(string SopClassUid, string SopInstanceUid)> sopInstances,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a failure during a Storage Commitment N-ACTION request.
/// </summary>
public sealed class DicomStorageCommitException : Exception
{
    /// <summary>
    /// Gets the DICOM N-ACTION status code returned by the SCP.
    /// </summary>
    public ushort StatusCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DicomStorageCommitException"/>.
    /// </summary>
    public DicomStorageCommitException(ushort statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
