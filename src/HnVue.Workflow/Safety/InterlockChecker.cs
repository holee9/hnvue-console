namespace HnVue.Workflow.Safety;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hardware interlock checker for safety-critical exposure validation.
///
/// SPEC-WORKFLOW-001 Section 5: Safety Interlocks
/// SPEC-WORKFLOW-001 Safety-01: No exposure without all interlocks passing
/// SPEC-WORKFLOW-001 FR-WF-04-b: Confirm all 9 hardware interlocks before exposure
///
/// IEC 62304 Class C - Safety-critical component for X-ray exposure control.
/// </summary>
// @MX:ANCHOR: Hardware interlock verification before every exposure
// @MX:REASON: Safety-critical - prevents X-ray exposure when unsafe conditions exist. High fan_in from all exposure paths.
public class InterlockChecker
{
    private readonly ILogger<InterlockChecker> _logger;
    private readonly ISafetyInterlock _safetyInterlock;

    // Interlock definitions from SPEC-WORKFLOW-001 Section 5.1
    private static readonly (string Id, string Name, bool RequiredValue)[] InterlockDefinitions = new[]
    {
        ("IL-01", "door_closed", true),
        ("IL-02", "emergency_stop_clear", true),
        ("IL-03", "thermal_normal", true),
        ("IL-04", "generator_ready", true),
        ("IL-05", "detector_ready", true),
        ("IL-06", "collimator_valid", true),
        ("IL-07", "table_locked", true),
        ("IL-08", "dose_within_limits", true),
        ("IL-09", "aec_configured", true)
    };

    public InterlockChecker(ILogger<InterlockChecker> logger, ISafetyInterlock safetyInterlock)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _safetyInterlock = safetyInterlock ?? throw new ArgumentNullException(nameof(safetyInterlock));
    }

    /// <summary>
    /// Checks all 9 hardware interlocks before exposure.
    /// SPEC-WORKFLOW-001 FR-WF-04-b: Confirm all nine hardware interlocks
    /// SPEC-WORKFLOW-001 Safety-01: No exposure command if any interlock not in required state
    /// </summary>
    public async Task<InterlockCheckResult> CheckAllInterlocksAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SAFETY: Checking all hardware interlocks before exposure");

        InterlockStatus status;
        try
        {
            status = await _safetyInterlock.CheckAllInterlocksAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Timeout is treated as interlock failure (safety-critical conservative approach)
            _logger.LogError("SAFETY: Interlock check timed out - treating as failure");
            return new InterlockCheckResult
            {
                AllPassed = false,
                FailedInterlocks = new[] { "TIMEOUT" },
                FailedInterlocksWithDescription = new[] { "TIMEOUT: Interlock check timed out" },
                InterlockDetails = new Dictionary<string, bool>(),
                CheckedAt = DateTime.UtcNow
            };
        }

        var failedInterlocks = new List<string>();
        var failedInterlocksWithDescriptions = new List<string>();
        var interlockDetails = new Dictionary<string, bool>();

        // Check each interlock
        foreach (var (id, name, requiredValue) in InterlockDefinitions)
        {
            bool actualValue = GetInterlockValue(status, name);
            interlockDetails[name] = actualValue;

            if (actualValue != requiredValue)
            {
                failedInterlocks.Add(id);
                failedInterlocksWithDescriptions.Add($"{id}: {name}");
                _logger.LogWarning("SAFETY: Interlock {InterlockId} ({InterlockName}) failed - required: {Required}, actual: {Actual}",
                    id, name, requiredValue, actualValue);
            }
        }

        var result = new InterlockCheckResult
        {
            AllPassed = failedInterlocks.Count == 0,
            FailedInterlocks = failedInterlocks.ToArray(),
            FailedInterlocksWithDescription = failedInterlocksWithDescriptions.ToArray(),
            InterlockDetails = interlockDetails,
            CheckedAt = DateTime.UtcNow
        };

        if (result.AllPassed)
        {
            _logger.LogInformation("SAFETY: All {Count} hardware interlocks passed", InterlockDefinitions.Length);
        }
        else
        {
            _logger.LogError("SAFETY: {FailedCount} of {TotalCount} interlocks failed - exposure blocked",
                failedInterlocks.Count, InterlockDefinitions.Length);
        }

        return result;
    }

    private static bool GetInterlockValue(InterlockStatus status, string name)
    {
        return name switch
        {
            "door_closed" => status.door_closed,
            "emergency_stop_clear" => status.emergency_stop_clear,
            "thermal_normal" => status.thermal_normal,
            "generator_ready" => status.generator_ready,
            "detector_ready" => status.detector_ready,
            "collimator_valid" => status.collimator_valid,
            "table_locked" => status.table_locked,
            "dose_within_limits" => status.dose_within_limits,
            "aec_configured" => status.aec_configured,
            _ => throw new ArgumentException($"Unknown interlock: {name}", nameof(name))
        };
    }
}

/// <summary>
/// Result of hardware interlock check.
/// </summary>
public class InterlockCheckResult
{
    /// <summary>
    /// Whether all 9 interlocks passed.
    /// </summary>
    public bool AllPassed { get; init; }

    /// <summary>
    /// Alias for AllPassed - provides a more readable property name.
    /// </summary>
    public bool IsSafe => AllPassed;

    /// <summary>
    /// List of failed interlock IDs only (e.g., "IL-01", "IL-02").
    /// Empty if all passed.
    /// </summary>
    public string[] FailedInterlocks { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of failed interlock identifiers with descriptions (e.g., "IL-01: door_closed").
    /// Empty if all passed.
    /// </summary>
    public string[] FailedInterlocksWithDescription { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Full interlock status details.
    /// </summary>
    public Dictionary<string, bool> InterlockDetails { get; init; } = new();

    /// <summary>
    /// Timestamp when the check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; }

    /// <summary>
    /// Human-readable summary of the check result.
    /// </summary>
    public string GetSummary()
    {
        if (AllPassed)
        {
            return "All hardware interlocks passed";
        }

        return $"Interlocks failed: {string.Join(", ", FailedInterlocks)}";
    }
}

/// <summary>
/// Hardware safety interlock interface.
/// Provided by HAL layer to query actual hardware states.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Safety interlock interface - pre-exposure safety verification
/// @MX:REASON: IEC 62304 Class C - Safety-critical component
/// @MX:SPEC: SPEC-WORKFLOW-001 NFR-SAFETY-01
/// </remarks>
public interface ISafetyInterlock
{
    /// <summary>
    /// Queries all 9 hardware interlocks atomically.
    /// SPEC-WORKFLOW-001 Section 5.1: Complete within 10ms
    /// </summary>
    /// <remarks>
    /// @MX:ANCHOR: Pre-exposure safety gate - must return true before exposure
    /// @MX:WARN: Safety-critical - failure prevents exposure
    /// </remarks>
    Task<InterlockStatus> CheckAllInterlocksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether exposure is currently blocked by any interlock.
    /// Returns true if ANY interlock is in unsafe state (false).
    /// </summary>
    /// <remarks>
    /// @MX:ANCHOR: IsExposureBlockedAsync - safety gate for exposure control
    /// @MX:WARN: Safety-critical - returns true when any interlock is unsafe
    /// </remarks>
    Task<bool> IsExposureBlockedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the state of an individual interlock.
    /// Used for testing and simulator control.
    /// </summary>
    /// <param name="interlockName">The name of the interlock to set.</param>
    /// <param name="enabled">True to enable (safe), false to disable (unsafe).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: SetInterlockStateAsync - testing method for interlock control
    /// @MX:WARN: Safety-critical - directly affects exposure blocking
    /// </remarks>
    Task SetInterlockStateAsync(string interlockName, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts the system into emergency standby mode.
    /// SPEC-WORKFLOW-001 Section 5.2: Emergency stop override
    /// </summary>
    /// <remarks>
    /// @MX:ANCHOR: Emergency standby - immediately disables exposure capability
    /// @MX:WARN: Safety-critical - emergency stop activation
    /// </remarks>
    Task EmergencyStandbyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Hardware interlock status for all 9 interlocks.
/// SPEC-WORKFLOW-001 Section 5.1: Interlock Chain
/// </summary>
public class InterlockStatus
{
    /// <summary>IL-01: X-ray room door closed</summary>
    public bool door_closed { get; set; }

    /// <summary>IL-02: Emergency stop not activated</summary>
    public bool emergency_stop_clear { get; set; }

    /// <summary>IL-03: Temperature normal (not overheated)</summary>
    public bool thermal_normal { get; set; }

    /// <summary>IL-04: High-voltage generator ready</summary>
    public bool generator_ready { get; set; }

    /// <summary>IL-05: Flat-panel detector ready</summary>
    public bool detector_ready { get; set; }

    /// <summary>IL-06: Collimator position valid</summary>
    public bool collimator_valid { get; set; }

    /// <summary>IL-07: Patient table locked</summary>
    public bool table_locked { get; set; }

    /// <summary>IL-08: Cumulative dose within limits</summary>
    public bool dose_within_limits { get; set; }

    /// <summary>IL-09: AEC (Automatic Exposure Control) configured</summary>
    public bool aec_configured { get; set; }
}

/// <summary>
/// Exposure parameter safety validator.
/// SPEC-WORKFLOW-001 Section 5.2: Parameter Safety Validation
/// </summary>
public class ParameterSafetyValidator
{
    private readonly ILogger<ParameterSafetyValidator> _logger;
    private readonly IDeviceSafetyLimits _safetyLimits;

    public ParameterSafetyValidator(ILogger<ParameterSafetyValidator> logger, IDeviceSafetyLimits safetyLimits)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _safetyLimits = safetyLimits ?? throw new ArgumentNullException(nameof(safetyLimits));
    }

    /// <summary>
    /// Validates exposure parameters against device safety limits.
    /// SPEC-WORKFLOW-001 Section 5.2: Parameter Safety Validation
    /// SPEC-WORKFLOW-001 Safety-02: No parameters outside device-specific limits
    /// </summary>
    public ExposureParameterValidationResult ValidateExposureParameters(ExposureParameters parameters, decimal accumulatedStudyDap = 0)
    {
        _logger.LogInformation("SAFETY: Validating exposure parameters against safety limits");

        var result = new ExposureParameterValidationResult { IsValid = true };

        // Validate kVp (SPEC Section 5.2)
        if (parameters.Kv < _safetyLimits.MinKvp || parameters.Kv > _safetyLimits.MaxKvp)
        {
            result.Violations.Add(new ParameterViolation
            {
                Parameter = "kVp",
                Reason = $"kVp {parameters.Kv} is outside allowed range [{_safetyLimits.MinKvp}, {_safetyLimits.MaxKvp}]"
            });
            result.IsValid = false;
        }

        // Validate mA (SPEC Section 5.2)
        if (parameters.Ma < _safetyLimits.MinMa || parameters.Ma > _safetyLimits.MaxMa)
        {
            result.Violations.Add(new ParameterViolation
            {
                Parameter = "mA",
                Reason = $"mA {parameters.Ma} is outside allowed range [{_safetyLimits.MinMa}, {_safetyLimits.MaxMa}]"
            });
            result.IsValid = false;
        }

        // Validate exposure time (SPEC Section 5.2)
        if (parameters.ExposureTimeMs > _safetyLimits.MaxExposureTime)
        {
            result.Violations.Add(new ParameterViolation
            {
                Parameter = "ExposureTime",
                Reason = $"Exposure time {parameters.ExposureTimeMs}ms exceeds maximum {_safetyLimits.MaxExposureTime}ms"
            });
            result.IsValid = false;
        }

        // Validate mAs (SPEC Section 5.2: kVp * mA * ExposureTime/1000 <= MaxMas)
        var mas = parameters.Kv * parameters.Ma * parameters.ExposureTimeMs / 1000;
        if (mas > _safetyLimits.MaxMas)
        {
            result.Violations.Add(new ParameterViolation
            {
                Parameter = "mAs",
                Reason = $"Calculated mAs {mas} exceeds maximum {_safetyLimits.MaxMas}"
            });
            result.IsValid = false;
        }

        // Check DAP warning level (soft limit - SPEC Section 5.2)
        if (accumulatedStudyDap > 0)
        {
            // Estimate DAP for this exposure (simplified calculation)
            var estimatedDap = mas * 0.1m; // Rough estimate
            var newTotalDap = accumulatedStudyDap + estimatedDap;

            if (newTotalDap > _safetyLimits.DapWarningLevel)
            {
                result.Warnings.Add(new ParameterWarning
                {
                    Parameter = "DAP",
                    Reason = $"Cumulative study DAP ({newTotalDap} cGycm) exceeds warning level ({_safetyLimits.DapWarningLevel} cGycm)"
                });
            }
        }

        return result;
    }
}

/// <summary>
/// Device safety limits configuration.
/// SPEC-WORKFLOW-001 Section 5.2: Parameter Safety Validation
/// </summary>
public interface IDeviceSafetyLimits
{
    decimal MinKvp { get; }
    decimal MaxKvp { get; }
    decimal MinMa { get; }
    decimal MaxMa { get; }
    int MaxExposureTime { get; }
    decimal MaxMas { get; }
    decimal DapWarningLevel { get; }
}

/// <summary>
/// Default device safety limits implementation.
/// </summary>
public class DeviceSafetyLimits : IDeviceSafetyLimits
{
    public decimal MinKvp { get; set; } = 40;
    public decimal MaxKvp { get; set; } = 150;
    public decimal MinMa { get; set; } = 1;
    public decimal MaxMa { get; set; } = 500;
    public int MaxExposureTime { get; set; } = 3000;
    public decimal MaxMas { get; set; } = 2000; // Updated to accommodate realistic clinical protocols
    public decimal DapWarningLevel { get; set; } = 50000;
}

/// <summary>
/// Exposure parameter validation result.
/// </summary>
public class ExposureParameterValidationResult
{
    public bool IsValid { get; set; }
    public List<ParameterViolation> Violations { get; set; } = new();
    public List<ParameterWarning> Warnings { get; set; } = new();
    public bool HasWarnings => Warnings.Count > 0;
}

/// <summary>
/// Parameter violation (hard limit - blocks exposure).
/// </summary>
public class ParameterViolation
{
    public string Parameter { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Parameter warning (soft limit - allows exposure with notification).
/// </summary>
public class ParameterWarning
{
    public string Parameter { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Exposure parameters for validation.
/// </summary>
public class ExposureParameters
{
    public decimal Kv { get; set; }
    public decimal Ma { get; set; }
    public int ExposureTimeMs { get; set; }
}
