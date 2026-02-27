namespace HnVue.Dicom.Rdsr;

/// <summary>
/// Provides RDSR (X-Ray Radiation Dose SR) data to DICOM consumers.
/// Implemented by HnVue.Dose; consumed by HnVue.Dicom for RDSR generation and export.
///
/// Thread Safety: All methods are thread-safe for concurrent calls.
/// </summary>
public interface IRdsrDataProvider
{
    /// <summary>
    /// Retrieves a summary of accumulated dose data for a completed study.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="StudyDoseSummary"/> with cumulative metrics, or <c>null</c> if study not found.
    /// Returned data is immutable and safe for concurrent access.
    /// Returns null if studyInstanceUid has no recorded dose events.
    /// </returns>
    Task<StudyDoseSummary?> GetStudyDoseSummaryAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all exposure records for a study, in chronological order.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Read-only list of <see cref="DoseRecord"/> for each exposure, sorted by TimestampUtc ascending.
    /// Returns an empty list if no exposures are found.
    /// Records include both calculated and measured DAP values (if meter was used).
    /// </returns>
    Task<IReadOnlyList<DoseRecord>> GetStudyExposureRecordsAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that a study dose session is complete and may be queried for export.
    /// Called by the DOSE module when a study is closed; the DICOM module can then request RDSR generation.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID.</param>
    /// <param name="patientId">Patient ID for this study.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous completion notification.</returns>
    Task NotifyStudyClosedAsync(
        string studyInstanceUid,
        string patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Observable stream of study closure events for reactive RDSR generation.
    /// DICOM consumers subscribe to receive notifications when dose studies close,
    /// as an alternative to polling <see cref="GetStudyDoseSummaryAsync"/>.
    /// </summary>
    IObservable<StudyCompletedEvent> StudyClosed { get; }
}
