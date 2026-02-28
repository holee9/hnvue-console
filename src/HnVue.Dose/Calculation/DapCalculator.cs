using HnVue.Dicom.Rdsr;
using HnVue.Dose.Interfaces;
using Microsoft.Extensions.Logging;

namespace HnVue.Dose.Calculation;

/// <summary>
/// Calculates Dose-Area Product (DAP) from exposure parameters.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of DAP calculation - core dose computation
/// @MX:REASON: Primary implementation of SPEC-DOSE-001 FR-DOSE-01
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-01, Section 4.1.1
///
/// DAP Calculation Formula:
/// DAP = K_air × A_field
/// Where:
///   K_air = k_factor × (kVp^n) × mAs / SID² × C_cal
///   A_field = width_cm × height_cm
///
/// Performance: Must complete within 200ms per SPEC-DOSE-001 NFR-DOSE-01.
/// Thread-safe: All operations are stateless and immutable.
/// </remarks>
public sealed class DapCalculator : IDoseCalculator
{
    private readonly CalibrationManager _calibrationManager;
    private readonly ILogger<DapCalculator> _logger;

    /// <summary>
    /// Initializes a new instance of the DapCalculator class.
    /// </summary>
    /// <param name="calibrationManager">Calibration manager for dose model parameters</param>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when calibrationManager or logger is null</exception>
    public DapCalculator(CalibrationManager calibrationManager, ILogger<DapCalculator> logger)
    {
        _calibrationManager = calibrationManager ?? throw new ArgumentNullException(nameof(calibrationManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculates DAP from exposure parameters and optional meter reading.
    /// </summary>
    /// <param name="parameters">Exposure parameters from HVG</param>
    /// <param name="meterDapGyCm2">Optional external DAP meter reading</param>
    /// <returns>DoseCalculationResult containing calculated and optional measured DAP</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when parameters are invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when calibration is not loaded</exception>
    public DoseCalculationResult Calculate(ExposureParameters parameters, decimal? meterDapGyCm2)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (!parameters.IsValid())
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "Exposure parameters are outside valid ranges.");
        }

        if (!_calibrationManager.IsCalibrated)
        {
            throw new InvalidOperationException("Calibration parameters not loaded. Cannot calculate DAP.");
        }

        var calibration = _calibrationManager.CurrentParameters;

        // Calculate field area in cm²
        var fieldWidthCm = parameters.FieldWidthMm / 10m;
        var fieldHeightCm = parameters.FieldHeightMm / 10m;
        var fieldAreaCm2 = fieldWidthCm * fieldHeightCm;

        // Calculate SID in cm
        var sidCm = parameters.SidMm / 10m;

        // Calculate air kerma at detector plane (mGy)
        // K_air = k_factor × (kVp^n) × mAs / SID² × C_cal
        var kvpPower = (decimal)Math.Pow(
            (double)parameters.KvpValue,
            (double)calibration.VoltageExponent);
        var airKermaMgy = calibration.KFactor
                        * kvpPower
                        * parameters.MasValue
                        / (sidCm * sidCm)
                        * calibration.CalibrationCoefficient;

        // Calculate DAP (Gy·cm²) = K_air (mGy) × A_field (cm²) / 1000
        var calculatedDapGyCm2 = airKermaMgy * fieldAreaCm2 / 1000m;

        // Determine dose source
        var doseSource = meterDapGyCm2.HasValue && meterDapGyCm2.Value > 0
            ? DoseSource.Measured
            : DoseSource.Calculated;

        _logger.LogDebug(
            "DAP Calculation - kVp: {Kvp} mAs: {Mas} SID: {Sid}cm Field: {Field}cm² K_air: {Kerma}mGy DAP: {Dap}Gy·cm² Source: {Source}",
            parameters.KvpValue, parameters.MasValue, sidCm, fieldAreaCm2, airKermaMgy, calculatedDapGyCm2, doseSource);

        return new DoseCalculationResult
        {
            CalculatedDapGyCm2 = calculatedDapGyCm2,
            MeasuredDapGyCm2 = meterDapGyCm2,
            DoseSource = doseSource,
            FieldAreaCm2 = fieldAreaCm2,
            AirKermaMgy = airKermaMgy
        };
    }
}
