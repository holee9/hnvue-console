namespace HnVue.Dose.Calculation;

/// <summary>
/// HVG tube-specific dose model parameters for DAP calculation.
/// </summary>
/// <remarks>
/// @MX:NOTE: Domain model for calibration parameters - signed configuration requirement
/// @MX:SPEC: SPEC-DOSE-001 Section 4.1.3
///
/// Contains calibration coefficients stored in tamper-evident configuration file.
/// Updated only through privileged calibration workflow with operator authorization.
/// </remarks>
public sealed record DoseModelParameters
{
    /// <summary>
    /// Gets the tube output coefficient (kV^-n × mAs^-1 × cm²).
    /// </summary>
    /// <remarks>
    /// Tube-specific output coefficient derived from factory calibration.
    /// Units: mGy·cm² / (kV^n × mAs)
    /// </remarks>
    public required decimal KFactor { get; init; }

    /// <summary>
    /// Gets the voltage exponent for kVp scaling.
    /// </summary>
    /// <remarks>
    /// Typically 2.5 for general radiography.
    /// Range: 2.0-3.0 depending on tube filtration and target material.
    /// </remarks>
    public required decimal VoltageExponent { get; init; }

    /// <summary>
    /// Gets the site-specific calibration coefficient.
    /// </summary>
    /// <remarks>
    /// Accounts for installation-specific factors (room geometry, scatter, etc.).
    /// Applied after base K_air calculation.
    /// </remarks>
    public required decimal CalibrationCoefficient { get; init; }

    /// <summary>
    /// Gets the tube configuration identifier.
    /// </summary>
    /// <remarks>
    /// Unique ID for the tube/calibration combination.
    /// Used for audit trail and calibration tracking.
    /// </remarks>
    public required string TubeId { get; init; }

    /// <summary>
    /// Gets the calibration timestamp.
    /// </summary>
    /// <remarks>
    /// When this calibration was performed/verified.
    /// Used for calibration expiry monitoring.
    /// </remarks>
    public required DateTime CalibrationDateUtc { get; init; }

    /// <summary>
    /// Validates the dose model parameters.
    /// </summary>
    /// <returns>True if all parameters are within valid ranges</returns>
    public bool IsValid()
    {
        return KFactor > 0m
            && VoltageExponent >= 2.0m && VoltageExponent <= 3.0m
            && CalibrationCoefficient > 0m && CalibrationCoefficient <= 10m
            && !string.IsNullOrWhiteSpace(TubeId);
    }
}
