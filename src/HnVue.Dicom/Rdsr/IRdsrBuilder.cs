using Dicom;

namespace HnVue.Dicom.Rdsr;

/// <summary>
/// Builds a complete DICOM RDSR (X-Ray Radiation Dose SR) dataset conforming to
/// DICOM SR TID 10001 (Projection X-Ray Radiation Dose) and TID 10003 (Irradiation Event X-Ray Data).
/// SOP Class UID: 1.2.840.10008.5.1.4.1.1.88.67 (X-Ray Radiation Dose SR Storage)
/// </summary>
public interface IRdsrBuilder
{
    /// <summary>
    /// Builds a complete DICOM RDSR file from dose summary and per-exposure records.
    /// </summary>
    /// <param name="studySummary">Accumulated dose summary from the DOSE module.</param>
    /// <param name="exposures">Per-exposure records from the DOSE module, in chronological order.</param>
    /// <returns>A complete <see cref="DicomFile"/> ready for C-STORE transmission.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="studySummary"/> or <paramref name="exposures"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the SR document cannot be constructed due to missing mandatory fields.
    /// </exception>
    DicomFile Build(StudyDoseSummary studySummary, IReadOnlyList<DoseRecord> exposures);
}
