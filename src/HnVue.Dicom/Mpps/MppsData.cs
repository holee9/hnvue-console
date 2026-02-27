namespace HnVue.Dicom.Mpps;

/// <summary>
/// MPPS status values as defined in DICOM PS3.3 C.7.6.3.
/// </summary>
public enum MppsStatus
{
    /// <summary>Procedure step is in progress.</summary>
    InProgress,

    /// <summary>Procedure step has been completed.</summary>
    Completed,

    /// <summary>Procedure step has been discontinued before completion.</summary>
    Discontinued
}

/// <summary>
/// Represents exposure data associated with a performed procedure step.
/// </summary>
/// <param name="SeriesInstanceUid">Series Instance UID (0020,000E) for the exposure series.</param>
/// <param name="SopClassUid">SOP Class UID of the image produced (e.g., DX SOP class).</param>
/// <param name="SopInstanceUid">SOP Instance UID of the image produced.</param>
public record ExposureData(
    string SeriesInstanceUid,
    string SopClassUid,
    string SopInstanceUid);

/// <summary>
/// Data payload for Modality Performed Procedure Step (MPPS) operations.
/// Used for N-CREATE (start) and N-SET (update/complete/discontinue) requests.
/// Maps to MPPS IOD attributes per DICOM PS3.3 B.17 and IHE SWF RAD-6/RAD-7.
/// </summary>
/// <param name="PatientId">Patient ID (0010,0020). Required.</param>
/// <param name="StudyInstanceUid">Study Instance UID (0020,000D). Required.</param>
/// <param name="SeriesInstanceUid">Series Instance UID (0020,000E) for the performed series. Required.</param>
/// <param name="PerformedProcedureStepId">Performed Procedure Step ID (0040,0253). Required.</param>
/// <param name="PerformedProcedureStepDescription">Performed Procedure Step Description (0040,0254). Required.</param>
/// <param name="StartDateTime">Performed Procedure Step Start Date and Time (0040,0244 / 0040,0245). Required.</param>
/// <param name="EndDateTime">Performed Procedure Step End Date and Time (0040,0250 / 0040,0251). Null if step is not yet completed or discontinued.</param>
/// <param name="Status">Current MPPS status. Maps to Performed Procedure Step Status (0040,0252).</param>
/// <param name="ExposureData">List of series and image references for COMPLETED status per FR-DICOM-04.</param>
public record MppsData(
    string PatientId,
    string StudyInstanceUid,
    string SeriesInstanceUid,
    string PerformedProcedureStepId,
    string PerformedProcedureStepDescription,
    DateTime StartDateTime,
    DateTime? EndDateTime,
    MppsStatus Status,
    IReadOnlyList<ExposureData> ExposureData);
