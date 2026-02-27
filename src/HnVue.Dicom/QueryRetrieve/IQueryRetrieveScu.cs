namespace HnVue.Dicom.QueryRetrieve;

/// <summary>
/// Study Root Query/Retrieve SCU operations (FR-DICOM-06, IHE PIR).
/// Provides C-FIND for study-level queries and C-MOVE to retrieve images.
/// </summary>
public interface IQueryRetrieveScu
{
    /// <summary>
    /// Performs a Study Root C-FIND query against the configured Query/Retrieve SCP
    /// and streams matching study-level results (FR-DICOM-06).
    /// </summary>
    /// <param name="query">Query parameters; null fields use DICOM wildcard matching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async sequence of matching <see cref="StudyResult"/> records.</returns>
    /// <exception cref="InvalidOperationException">Thrown when QueryRetrieveScp is not configured.</exception>
    IAsyncEnumerable<StudyResult> FindStudiesAsync(
        StudyQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a Study Root C-MOVE request to retrieve all instances of the specified study
    /// to the configured move destination AE Title (FR-DICOM-07).
    /// </summary>
    /// <param name="studyInstanceUid">The Study Instance UID to retrieve.</param>
    /// <param name="destinationAeTitle">
    /// The AE Title of the destination SCP that will receive the images via C-STORE.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when QueryRetrieveScp is not configured.</exception>
    /// <exception cref="DicomQueryRetrieveException">Thrown when the C-MOVE operation fails.</exception>
    Task MoveStudyAsync(
        string studyInstanceUid,
        string destinationAeTitle,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a failure returned by the Query/Retrieve SCP during a C-FIND or C-MOVE operation.
/// </summary>
public sealed class DicomQueryRetrieveException : Exception
{
    /// <summary>
    /// Gets the DICOM status code returned by the SCP.
    /// </summary>
    public ushort StatusCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DicomQueryRetrieveException"/>.
    /// </summary>
    public DicomQueryRetrieveException(ushort statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
