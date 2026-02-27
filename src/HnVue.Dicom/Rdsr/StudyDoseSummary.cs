namespace HnVue.Dicom.Rdsr;

/// <summary>
/// Immutable summary of accumulated radiation dose for a completed study.
/// Designed for RDSR SR Document and IHE REM Actor "Dose Reporter" compliance.
/// </summary>
public record StudyDoseSummary
{
    /// <summary>DICOM Study Instance UID (primary key).</summary>
    public required string StudyInstanceUid { get; init; }

    /// <summary>Patient ID associated with this study.</summary>
    public required string PatientId { get; init; }

    /// <summary>Patient Name (required for RDSR Patient Module).</summary>
    public string? PatientName { get; init; }

    /// <summary>Patient Birth Date (optional for RDSR).</summary>
    public DateTime? PatientBirthDate { get; init; }

    /// <summary>Patient Sex (M/F/O for RDSR).</summary>
    public string? PatientSex { get; init; }

    /// <summary>Modality code (DX, CR, etc.).</summary>
    public required string Modality { get; init; }

    /// <summary>Examination description or protocol name.</summary>
    public string? ExaminationDescription { get; init; }

    /// <summary>Anatomic region code (SNOMED-CT).</summary>
    public string? BodyRegionCode { get; init; }

    /// <summary>Total cumulative DAP in Gy·cm² (sum of all exposures).</summary>
    public decimal TotalDapGyCm2 { get; init; }

    /// <summary>Total number of X-ray exposures in this study.</summary>
    public int ExposureCount { get; init; }

    /// <summary>Study start timestamp (UTC).</summary>
    public DateTime StudyStartTimeUtc { get; init; }

    /// <summary>Study end/close timestamp (UTC).</summary>
    public DateTime StudyEndTimeUtc { get; init; }

    /// <summary>Accession Number (optional, from HIS/RIS).</summary>
    public string? AccessionNumber { get; init; }

    /// <summary>Whether cumulative dose exceeded DRL for this examination type.</summary>
    public bool DrlExceeded { get; init; }

    /// <summary>Name of configured AE Title that performed this study.</summary>
    public string? PerformedStationAeTitle { get; init; }
}
