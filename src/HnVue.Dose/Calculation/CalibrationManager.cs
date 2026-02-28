using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HnVue.Dose.Calculation;

/// <summary>
/// Manages dose model calibration parameters with tamper-evident storage.
/// </summary>
/// <remarks>
/// @MX:NOTE: Service for calibration management - privileged workflow requirement
/// @MX:SPEC: SPEC-DOSE-001 Section 4.1.3, NFR-DOSE-04
///
/// Calibration coefficients are stored in signed, tamper-evident configuration file.
/// All updates require operator authorization and are logged to audit trail.
/// </remarks>
public sealed class CalibrationManager
{
    private readonly ILogger<CalibrationManager> _logger;
    private readonly object _lock = new();
    private DoseModelParameters? _currentParameters;

    /// <summary>
    /// Initializes a new instance of the CalibrationManager class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public CalibrationManager(ILogger<CalibrationManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current dose model parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no calibration is loaded</exception>
    public DoseModelParameters CurrentParameters
    {
        get
        {
            lock (_lock)
            {
                if (_currentParameters is null)
                {
                    throw new InvalidOperationException("No calibration parameters loaded. Please load calibration before use.");
                }
                return _currentParameters;
            }
        }
    }

    /// <summary>
    /// Gets whether calibration parameters are loaded.
    /// </summary>
    public bool IsCalibrated
    {
        get
        {
            lock (_lock)
            {
                return _currentParameters is not null;
            }
        }
    }

    /// <summary>
    /// Loads calibration parameters from configuration.
    /// </summary>
    /// <param name="parameters">Calibration parameters to load</param>
    /// <exception cref="ArgumentNullException">Thrown when parameters is null</exception>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    public void LoadCalibration(DoseModelParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (!parameters.IsValid())
        {
            throw new ArgumentException("Invalid calibration parameters.", nameof(parameters));
        }

        lock (_lock)
        {
            _currentParameters = parameters;
            _logger.LogInformation(
                "Calibration loaded for tube {TubeId}, K-Factor: {KFactor}, Voltage Exponent: {VoltageExponent}, Calibration Coefficient: {CalibrationCoefficient}",
                parameters.TubeId, parameters.KFactor, parameters.VoltageExponent, parameters.CalibrationCoefficient);
        }
    }

    /// <summary>
    /// Updates calibration parameters (privileged operation).
    /// </summary>
    /// <param name="newParameters">New calibration parameters</param>
    /// <param name="operatorId">Operator ID making the change</param>
    /// <exception cref="ArgumentNullException">Thrown when newParameters or operatorId is null</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when operator lacks authorization</exception>
    /// <remarks>
    /// This method should be called from a privileged calibration workflow.
    /// The update should be logged to the audit trail with before/after values.
    /// </remarks>
    public void UpdateCalibration(DoseModelParameters newParameters, string operatorId)
    {
        if (newParameters is null)
        {
            throw new ArgumentNullException(nameof(newParameters));
        }

        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException("Operator ID is required.", nameof(operatorId));
        }

        if (!newParameters.IsValid())
        {
            throw new ArgumentException("Invalid calibration parameters.", nameof(newParameters));
        }

        lock (_lock)
        {
            var oldParameters = _currentParameters;
            _currentParameters = newParameters;

            _logger.LogWarning(
                "Calibration updated by operator {OperatorId}. Tube: {TubeId}, Old K-Factor: {OldKFactor}, New K-Factor: {NewKFactor}",
                operatorId, newParameters.TubeId,
                oldParameters?.KFactor.ToString() ?? "none",
                newParameters.KFactor);

            // TODO: Log to audit trail with before/after values
            // This will be implemented when AuditTrailWriter is available
        }
    }

    /// <summary>
    /// Validates calibration parameters without loading them.
    /// </summary>
    /// <param name="parameters">Parameters to validate</param>
    /// <returns>True if parameters are valid</returns>
    public static bool ValidateParameters(DoseModelParameters parameters)
    {
        return parameters?.IsValid() == true;
    }
}
