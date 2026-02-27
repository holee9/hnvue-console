namespace HnVue.Dicom.QueryRetrieve;

/// <summary>
/// A study-level result returned from a Study Root C-FIND query (FR-DICOM-06).
/// </summary>
public record StudyResult(
    string StudyInstanceUid,
    string? PatientId,
    string? PatientName,
    string? AccessionNumber,
    string? Modality,
    DateOnly? StudyDate,
    string? StudyDescription,
    int? NumberOfStudyRelatedSeries,
    int? NumberOfStudyRelatedInstances);
