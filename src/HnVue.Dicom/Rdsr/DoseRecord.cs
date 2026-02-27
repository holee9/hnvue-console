namespace HnVue.Dicom.Rdsr;

/// <summary>
/// Immutable record representing a single X-ray irradiation event within a study.
/// Used to populate DICOM SR TID 10003 (Irradiation Event X-Ray Data) content items.
/// </summary>
public record DoseRecord
{
    /// <summary>Unique identifier for this exposure event (internal).</summary>
    public Guid ExposureEventId { get; init; }

    /// <summary>DICOM UID assigned to this irradiation event for RDSR TID 10003.</summary>
    public required string IrradiationEventUid { get; init; }

    /// <summary>DICOM Study Instance UID this record belongs to.</summary>
    public required string StudyInstanceUid { get; init; }

    /// <summary>Patient ID associated with this exposure.</summary>
    public required string PatientId { get; init; }

    /// <summary>UTC timestamp when this exposure occurred.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>Peak kilo voltage at the generator (kV).</summary>
    public decimal KvpValue { get; init; }

    /// <summary>X-ray tube exposure in milliampere-seconds (mAs).</summary>
    public decimal MasValue { get; init; }

    /// <summary>X-ray filter material (maps to DICOM CID 10006).</summary>
    public string FilterMaterial { get; init; } = string.Empty;

    /// <summary>X-ray filter thickness in mm.</summary>
    public decimal FilterThicknessMm { get; init; }

    /// <summary>Source-to-image distance (SID) in mm.</summary>
    public decimal SidMm { get; init; }

    /// <summary>Collimated field width in mm.</summary>
    public decimal FieldWidthMm { get; init; }

    /// <summary>Collimated field height in mm.</summary>
    public decimal FieldHeightMm { get; init; }

    /// <summary>Calculated Dose Area Product in Gy·cm².</summary>
    public decimal CalculatedDapGyCm2 { get; init; }

    /// <summary>Measured Dose Area Product in Gy·cm² from a DAP meter. Null when not measured.</summary>
    public decimal? MeasuredDapGyCm2 { get; init; }

    /// <summary>Indicates whether this DAP value was calculated or directly measured.</summary>
    public DoseSource DoseSource { get; init; }

    /// <summary>Acquisition protocol name, if applicable.</summary>
    public string? AcquisitionProtocol { get; init; }

    /// <summary>Anatomic region code (SNOMED-CT) for this exposure.</summary>
    public string? BodyRegionCode { get; init; }

    /// <summary>Whether this exposure's dose exceeded the applicable Diagnostic Reference Level (DRL).</summary>
    public bool DrlExceedance { get; init; }

    /// <summary>
    /// Returns the effective DAP in Gy·cm²:
    /// uses <see cref="MeasuredDapGyCm2"/> when available, else <see cref="CalculatedDapGyCm2"/>.
    /// </summary>
    public decimal EffectiveDapGyCm2 =>
        DoseSource == DoseSource.Measured && MeasuredDapGyCm2.HasValue
            ? MeasuredDapGyCm2.Value
            : CalculatedDapGyCm2;
}

/// <summary>
/// Indicates whether a DAP value was obtained via calculation or direct measurement.
/// </summary>
public enum DoseSource
{
    /// <summary>DAP value was calculated from acquisition parameters.</summary>
    Calculated,

    /// <summary>DAP value was directly measured by a DAP meter.</summary>
    Measured
}

/// <summary>
/// Event notification when a dose study is closed and ready for RDSR export.
/// </summary>
public record StudyCompletedEvent
{
    /// <summary>DICOM Study Instance UID.</summary>
    public required string StudyInstanceUid { get; init; }

    /// <summary>Patient ID for this study.</summary>
    public required string PatientId { get; init; }

    /// <summary>Timestamp when the study was closed (UTC).</summary>
    public DateTime ClosedAtUtc { get; init; }

    /// <summary>Number of exposures in this study.</summary>
    public int ExposureCount { get; init; }

    /// <summary>Total accumulated DAP in Gy·cm² for this study.</summary>
    public decimal TotalDapGyCm2 { get; init; }
}
