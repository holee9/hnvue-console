namespace HnVue.Dose.Calculation;

/// <summary>
/// Result of DAP calculation including calculated and optional measured values.
/// </summary>
/// <remarks>
/// @MX:NOTE: Domain model for dose calculation result - immutable record type
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-01
///
/// Contains both calculated DAP and optional external meter reading.
/// Uses record type for immutability and value semantics.
/// </remarks>
public sealed record DoseCalculationResult
{
    /// <summary>
    /// Gets the calculated DAP value in Gy·cm².
    /// </summary>
    /// <remarks>
    /// Algorithm-derived DAP using HVG parameters and detector geometry.
    /// Formula: DAP = K_air × A_field
    /// Where K_air = k_factor × (kVp^n) × mAs / SID²
    /// </remarks>
    public required decimal CalculatedDapGyCm2 { get; init; }

    /// <summary>
    /// Gets the external DAP meter reading in Gy·cm², if available.
    /// </summary>
    /// <remarks>
    /// Null when external DAP meter is not connected or returns no data.
    /// When present, measured value takes precedence for display and RDSR.
    /// </remarks>
    public decimal? MeasuredDapGyCm2 { get; init; }

    /// <summary>
    /// Gets the dose source indicator.
    /// </summary>
    /// <remarks>
    /// Indicates whether DAP value is calculated or measured.
    /// Mapped to DICOM CID 10022 in RDSR generation.
    /// </remarks>
    public required HnVue.Dicom.Rdsr.DoseSource DoseSource { get; init; }

    /// <summary>
    /// Gets the effective field area in cm² used for calculation.
    /// </summary>
    /// <remarks>
    /// Calculated from field dimensions: A_field = width_cm × height_cm
    /// </remarks>
    public required decimal FieldAreaCm2 { get; init; }

    /// <summary>
    /// Gets the calculated air kerma at detector plane in mGy.
    /// </summary>
    /// <remarks>
    /// K_air = k_factor × (kVp^n) × mAs / SID²
    /// Used for intermediate calculation and audit trail.
    /// </remarks>
    public required decimal AirKermaMgy { get; init; }
}
