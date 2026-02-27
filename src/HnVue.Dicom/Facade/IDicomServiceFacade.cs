using HnVue.Dicom.Iod;
using HnVue.Dicom.Mpps;
using HnVue.Dicom.Rdsr;
using HnVue.Dicom.Worklist;

namespace HnVue.Dicom.Facade;

/// <summary>
/// Single entry point aggregating all DICOM services for HnVue.Core callers.
/// Abstracts SCU orchestration, IOD construction, and retry queue management
/// behind a domain-oriented API surface.
/// </summary>
public interface IDicomServiceFacade
{
    /// <summary>
    /// Builds a DICOM image file from the provided image data and transmits it to all configured
    /// Storage SCP destinations via C-STORE (FR-DICOM-01).
    /// The appropriate IOD builder (DX or CR) is selected based on <see cref="DicomImageData.Modality"/>.
    /// </summary>
    /// <param name="imageData">The image data record containing all mandatory DICOM attributes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The SOP Instance UID of the stored DICOM object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageData"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the modality in <paramref name="imageData"/> is not supported.</exception>
    Task<string> StoreImageAsync(DicomImageData imageData, CancellationToken ct = default);

    /// <summary>
    /// Queries the Modality Worklist SCP and returns all matching scheduled procedure steps
    /// (FR-DICOM-03, IHE SWF RAD-5).
    /// </summary>
    /// <param name="query">The worklist query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching <see cref="WorklistItem"/> records.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WorklistScp is not configured.</exception>
    Task<IList<WorklistItem>> FetchWorklistAsync(WorklistQuery query, CancellationToken ct = default);

    /// <summary>
    /// Creates a Modality Performed Procedure Step in IN PROGRESS status (FR-DICOM-04, IHE SWF RAD-6).
    /// </summary>
    /// <param name="data">The MPPS data for the new procedure step.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The SOP Instance UID of the created MPPS object.</returns>
    Task<string> StartProcedureStepAsync(MppsData data, CancellationToken ct = default);

    /// <summary>
    /// Updates a Modality Performed Procedure Step to COMPLETED status (FR-DICOM-04, IHE SWF RAD-7).
    /// </summary>
    /// <param name="mppsUid">The SOP Instance UID returned by <see cref="StartProcedureStepAsync"/>.</param>
    /// <param name="data">Updated MPPS data with end time and exposure references.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CompleteProcedureStepAsync(string mppsUid, MppsData data, CancellationToken ct = default);

    /// <summary>
    /// Sends a Storage Commitment N-ACTION request for the specified SOP Instance UIDs (FR-DICOM-05).
    /// </summary>
    /// <param name="sopInstanceUids">Collection of SOP Instance UIDs to request commitment for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Transaction UID generated for this commitment request.</returns>
    Task<string> RequestStorageCommitAsync(IEnumerable<string> sopInstanceUids, CancellationToken ct = default);

    /// <summary>
    /// Generates an RDSR for a completed dose study and transmits it to all configured Storage SCP destinations.
    /// Retrieves dose data from <paramref name="provider"/>, builds the SR document, then C-STOREs it (IHE REM).
    /// </summary>
    /// <param name="studyInstanceUid">The Study Instance UID identifying the completed dose study.</param>
    /// <param name="provider">The RDSR data provider (implemented by HnVue.Dose module).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="RdsrExportResult"/> describing success or failure of the export operation.
    /// </returns>
    Task<RdsrExportResult> ExportStudyDoseAsync(
        string studyInstanceUid,
        IRdsrDataProvider provider,
        CancellationToken ct = default);
}

/// <summary>
/// Result of an RDSR export operation.
/// </summary>
public record RdsrExportResult
{
    /// <summary>Indicates whether the RDSR was successfully built and transmitted.</summary>
    public bool Success { get; init; }

    /// <summary>SOP Instance UID of the generated RDSR, when successful.</summary>
    public string? RdsrSopInstanceUid { get; init; }

    /// <summary>Human-readable error message when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; init; }
}
