using HnVue.Dose.Calculation;

namespace HnVue.Dose.Interfaces;

/// <summary>
/// Calculates Dose-Area Product (DAP) from exposure parameters.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Public API for DAP calculation - core dose computation interface
/// @MX:REASON: Primary interface for dose calculation, used by all exposure workflows
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-01
///
/// Performance requirement: Must complete within 200ms per SPEC-DOSE-001 NFR-DOSE-01.
/// Thread-safe: Callable from multiple threads concurrently.
/// </remarks>
public interface IDoseCalculator
{
    /// <summary>
    /// Calculates DAP from exposure parameters and optional meter reading.
    /// </summary>
    /// <param name="parameters">Exposure parameters from HVG (kVp, mAs, filtration, field geometry)</param>
    /// <param name="meterDapGyCm2">Optional external DAP meter reading in Gy·cm²</param>
    /// <returns>DoseCalculationResult containing calculated DAP, optional measured DAP, and DoseSource indicator</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when parameter values are outside valid ranges</exception>
    DoseCalculationResult Calculate(ExposureParameters parameters, decimal? meterDapGyCm2);
}
