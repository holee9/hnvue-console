using HnVue.Dicom.Rdsr;
using HnVue.Dose.Exceptions;
using HnVue.Dose.Recording;

namespace HnVue.Dose.Interfaces;

/// <summary>
/// Persists and retrieves dose records to/from non-volatile storage.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Public API for dose record persistence - atomic write requirement
/// @MX:REASON: Critical interface ensuring no data loss on crash (SPEC-DOSE-001 NFR-DOSE-02)
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-01, NFR-DOSE-02
///
/// Implementation must use write-ahead logging or equivalent atomic mechanism
/// to guarantee dose record integrity across unexpected process termination.
/// </remarks>
public interface IDoseRecordRepository
{
    /// <summary>
    /// Persists a dose record atomically to non-volatile storage.
    /// </summary>
    /// <param name="record">The dose record to persist</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async persistence operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when record is null</exception>
    /// <exception cref="DoseRecordPersistenceException">Thrown when persistence fails (do not swallow)</exception>
    /// <remarks>
    /// Must complete within 1 second per SPEC-DOSE-001 NFR-DOSE-02.
    /// Atomic write: All-or-nothing persistence; no partial records on crash.
    /// </remarks>
    Task PersistAsync(DoseRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all dose records for a specific study.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Read-only list of dose records for the study</returns>
    /// <exception cref="ArgumentNullException">Thrown when studyInstanceUid is null or empty</exception>
    /// <exception cref="DoseRecordPersistenceException">Thrown when retrieval fails</exception>
    Task<IReadOnlyList<DoseRecord>> GetByStudyAsync(string studyInstanceUid, CancellationToken cancellationToken = default);
}
