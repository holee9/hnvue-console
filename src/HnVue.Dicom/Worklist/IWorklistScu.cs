namespace HnVue.Dicom.Worklist;

/// <summary>
/// Provides Modality Worklist SCU (Service Class User) operations.
/// Implements IHE SWF RAD-5 transaction via C-FIND against the configured Worklist SCP.
/// </summary>
public interface IWorklistScu
{
    /// <summary>
    /// Queries the Modality Worklist SCP for scheduled procedure steps matching the given criteria.
    /// </summary>
    /// <remarks>
    /// Sends a C-FIND request to the configured WorklistScp destination.
    /// Results are streamed as they are received (C-FIND PENDING responses).
    /// The async enumerable completes when C-FIND SUCCESS is received or the query fails.
    /// </remarks>
    /// <param name="query">The query parameters. Null fields are treated as wildcards.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// An asynchronous sequence of <see cref="WorklistItem"/> records matching the query.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when WorklistScp is not configured in <see cref="Configuration.DicomServiceOptions"/>.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when the C-FIND operation exceeds the configured DIMSE timeout.
    /// </exception>
    /// <exception cref="DicomWorklistException">
    /// Thrown when the SCP returns a Failure status code.
    /// </exception>
    IAsyncEnumerable<WorklistItem> QueryAsync(
        WorklistQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a failure returned by the Modality Worklist SCP during a C-FIND operation.
/// </summary>
public sealed class DicomWorklistException : Exception
{
    /// <summary>
    /// Gets the DICOM C-FIND status code returned by the SCP.
    /// </summary>
    public ushort StatusCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DicomWorklistException"/>.
    /// </summary>
    public DicomWorklistException(ushort statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
