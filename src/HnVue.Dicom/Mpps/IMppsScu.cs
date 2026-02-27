namespace HnVue.Dicom.Mpps;

/// <summary>
/// Provides Modality Performed Procedure Step (MPPS) SCU operations.
/// Implements IHE SWF RAD-6 (IN PROGRESS) and RAD-7 (COMPLETED / DISCONTINUED)
/// using N-CREATE and N-SET against the configured MPPS SCP.
/// SOP Class: 1.2.840.10008.3.1.2.3.3
/// </summary>
public interface IMppsScu
{
    /// <summary>
    /// Creates a new Modality Performed Procedure Step in IN PROGRESS status (IHE SWF RAD-6).
    /// Sends an N-CREATE request to the configured MPPS SCP.
    /// </summary>
    /// <param name="data">The MPPS data for the new procedure step.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The SOP Instance UID of the created MPPS object. Must be retained for subsequent N-SET calls.</returns>
    /// <exception cref="InvalidOperationException">Thrown when MppsScp is not configured.</exception>
    /// <exception cref="DicomMppsException">Thrown when the SCP returns an N-CREATE failure status.</exception>
    Task<string> CreateProcedureStepAsync(MppsData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the Modality Performed Procedure Step to IN PROGRESS status via N-SET.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID returned by <see cref="CreateProcedureStepAsync"/>.</param>
    /// <param name="data">Updated MPPS data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="DicomMppsException">Thrown when the SCP returns an N-SET failure status.</exception>
    Task SetProcedureStepInProgressAsync(string sopInstanceUid, MppsData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the Modality Performed Procedure Step via N-SET with COMPLETED status (IHE SWF RAD-7).
    /// The <paramref name="data"/> must include <see cref="MppsData.ExposureData"/> with series and image references.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID returned by <see cref="CreateProcedureStepAsync"/>.</param>
    /// <param name="data">MPPS data including EndDateTime and ExposureData for the completed step.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="DicomMppsException">Thrown when the SCP returns an N-SET failure status.</exception>
    Task CompleteProcedureStepAsync(string sopInstanceUid, MppsData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discontinues the Modality Performed Procedure Step via N-SET with DISCONTINUED status.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID returned by <see cref="CreateProcedureStepAsync"/>.</param>
    /// <param name="reason">Human-readable reason for discontinuation. Included in the dataset comment.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="DicomMppsException">Thrown when the SCP returns an N-SET failure status.</exception>
    Task DiscontinueProcedureStepAsync(string sopInstanceUid, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a failure returned by the MPPS SCP during an N-CREATE or N-SET operation.
/// </summary>
public sealed class DicomMppsException : Exception
{
    /// <summary>
    /// Gets the DICOM N-CREATE or N-SET status code returned by the SCP.
    /// </summary>
    public ushort StatusCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DicomMppsException"/>.
    /// </summary>
    public DicomMppsException(ushort statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
