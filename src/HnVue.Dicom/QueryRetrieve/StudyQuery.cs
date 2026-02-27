using HnVue.Dicom.Worklist;

namespace HnVue.Dicom.QueryRetrieve;

/// <summary>
/// Query parameters for Study Root C-FIND requests (FR-DICOM-06, IHE PIR).
/// All fields are optional; null fields use DICOM wildcard matching.
/// </summary>
public record StudyQuery
{
    /// <summary>
    /// Gets or sets the Patient ID to match (exact or wildcard).
    /// </summary>
    public string? PatientId { get; init; }

    /// <summary>
    /// Gets or sets the Accession Number to match (exact or wildcard).
    /// </summary>
    public string? AccessionNumber { get; init; }

    /// <summary>
    /// Gets or sets the Study Instance UID to retrieve a specific study.
    /// </summary>
    public string? StudyInstanceUid { get; init; }

    /// <summary>
    /// Gets or sets the modality filter applied to the Modalities In Study attribute.
    /// </summary>
    public string? Modality { get; init; }

    /// <summary>
    /// Gets or sets the study date range for filtering (DICOM DA range format: yyyyMMdd-yyyyMMdd).
    /// When null, no date filter is applied.
    /// </summary>
    public DateRange? StudyDate { get; init; }
}
