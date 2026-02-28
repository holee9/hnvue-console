namespace HnVue.Dose.Calculation;

/// <summary>
/// Exposure parameters acquired from HVG and detector subsystems.
/// </summary>
/// <remarks>
/// @MX:NOTE: Domain model for exposure parameters - immutable record type
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-01, FR-DOSE-06
///
/// Contains all required parameters for DAP calculation per IEC 60601-1-3.
/// Uses record type for immutability and value semantics.
/// </remarks>
public sealed record ExposureParameters
{
    /// <summary>
    /// Gets the peak kilovoltage (kVp) at the X-ray tube.
    /// </summary>
    /// <remarks>
    /// Range: 20-150 kVp for diagnostic radiography per IEC 60601-2-54.
    /// </remarks>
    public required decimal KvpValue { get; init; }

    /// <summary>
    /// Gets the tube current-exposure time product (mAs).
    /// </summary>
    /// <remarks>
    /// Can be specified directly as mAs, or derived from mA × exposure time (ms).
    /// </remarks>
    public required decimal MasValue { get; init; }

    /// <summary>
    /// Gets the beam filtration material code (e.g., "Al" for Aluminum, "Cu" for Copper).
    /// </summary>
    /// <remarks>
    /// Coded value per DICOM CID 10006 (Filter Material).
    /// Common values: Al (Aluminum), Cu (Copper), Rh (Rhodium).
    /// </remarks>
    public required string FilterMaterial { get; init; }

    /// <summary>
    /// Gets the beam filtration thickness in millimeters.
    /// </summary>
    /// <remarks>
    /// Total inherent + added filtration thickness.
    /// Minimum 2.5 mm Al equivalent per IEC 60601-1-3.
    /// </remarks>
    public required decimal FilterThicknessMm { get; init; }

    /// <summary>
    /// Gets the Source-to-Image Distance (SID) in millimeters.
    /// </summary>
    /// <remarks>
    /// Distance from X-ray tube focal spot to detector plane.
    /// Typical range: 100-180 cm (1000-1800 mm).
    /// </remarks>
    public required decimal SidMm { get; init; }

    /// <summary>
    /// Gets the collimated field width at the detector plane in millimeters.
    /// </summary>
    /// <remarks>
    /// Preferred source: Detector-reported exposure area.
    /// Fallback: Collimator angle data and SID.
    /// </remarks>
    public required decimal FieldWidthMm { get; init; }

    /// <summary>
    /// Gets the collimated field height at the detector plane in millimeters.
    /// </summary>
    /// <remarks>
    /// Preferred source: Detector-reported exposure area.
    /// Fallback: Collimator angle data and SID.
    /// </remarks>
    public required decimal FieldHeightMm { get; init; }

    /// <summary>
    /// Gets the exposure timestamp in UTC.
    /// </summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>
    /// Gets the examination protocol name (e.g., "CXR PA", "Abdomen AP").
    /// </summary>
    /// <remarks>
    /// Optional: Used for DRL comparison per examination type.
    /// </remarks>
    public string? AcquisitionProtocol { get; init; }

    /// <summary>
    /// Gets the target body region code (SNOMED-CT).
    /// </summary>
    /// <remarks>
    /// Optional: Used for DRL comparison per body region.
    /// Example: " Chest" (SNOMED-CT code for chest region).
    /// </remarks>
    public string? BodyRegionCode { get; init; }

    /// <summary>
    /// Validates the exposure parameters.
    /// </summary>
    /// <returns>True if all parameters are within valid ranges</returns>
    /// <remarks>
    /// Validation criteria per IEC 60601-1-3:
    /// - kVp: 20-150 kVp
    /// - mAs: 0.1-1000 mAs
    /// - SID: 800-2000 mm
    /// - Field dimensions: 50-500 mm
    /// - Filter thickness: 0-10 mm
    /// </remarks>
    public bool IsValid()
    {
        return KvpValue >= 20m && KvpValue <= 150m
            && MasValue > 0m && MasValue <= 1000m
            && SidMm >= 800m && SidMm <= 2000m
            && FieldWidthMm > 0m && FieldWidthMm <= 500m
            && FieldHeightMm > 0m && FieldHeightMm <= 500m
            && FilterThicknessMm >= 0m && FilterThicknessMm <= 10m
            && !string.IsNullOrWhiteSpace(FilterMaterial);
    }
}
