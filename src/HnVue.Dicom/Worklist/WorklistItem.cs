namespace HnVue.Dicom.Worklist;

/// <summary>
/// Represents the scheduled procedure step details from a Modality Worklist response.
/// Maps to the Scheduled Procedure Step Sequence (0040,0100) in the worklist response.
/// </summary>
/// <param name="StepId">Scheduled Procedure Step ID (0040,0009).</param>
/// <param name="Description">Scheduled Procedure Step Description (0040,0007).</param>
/// <param name="DateTime">Scheduled Procedure Step Start DateTime, combining tags (0040,0002) and (0040,0003).</param>
/// <param name="PerformingPhysician">Scheduled Performing Physician's Name (0040,0006). May be null.</param>
/// <param name="Modality">Modality (0008,0060).</param>
public record ScheduledProcedureStep(
    string StepId,
    string Description,
    DateTime? DateTime,
    string? PerformingPhysician,
    string Modality);

/// <summary>
/// Represents a single Modality Worklist response item returned by a C-FIND query.
/// Implements mandatory attributes per SPEC-DICOM-001 FR-DICOM-03 and IHE SWF RAD-5.
/// </summary>
/// <param name="PatientId">Patient ID (0010,0020). Required.</param>
/// <param name="PatientName">Patient's Name (0010,0010). Required.</param>
/// <param name="BirthDate">Patient's Birth Date (0010,0030). Null if not present in response.</param>
/// <param name="PatientSex">Patient's Sex (0010,0040). Null if not present in response.</param>
/// <param name="StudyInstanceUid">Study Instance UID (0020,000D). Null if not assigned yet.</param>
/// <param name="AccessionNumber">Accession Number (0008,0050). Required per IHE SWF.</param>
/// <param name="RequestedProcedureId">Requested Procedure ID (0040,1001). Required.</param>
/// <param name="ScheduledProcedureStep">The scheduled procedure step details from the response sequence.</param>
public record WorklistItem(
    string PatientId,
    string PatientName,
    DateOnly? BirthDate,
    string? PatientSex,
    string? StudyInstanceUid,
    string AccessionNumber,
    string RequestedProcedureId,
    ScheduledProcedureStep ScheduledProcedureStep);
