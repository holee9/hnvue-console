namespace HnVue.Workflow.Protocol;

using HnVue.Workflow.Safety;

/// <summary>
/// Device safety limits for protocol validation.
/// SPEC-WORKFLOW-001 FR-WF-09: Enforce safety limits on protocol save
/// SPEC-WORKFLOW-001 Safety-02: No parameters outside device-specific limits
/// IEC 62304 Class C - Safety-critical exposure limits
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Device safety limits - protocol parameter validation
/// @MX:WARN: Safety-critical - prevents protocol creation with dangerous parameters
/// </remarks>
public sealed class DeviceSafetyLimits
{
    /// <summary>
    /// Minimum tube peak voltage in kVp.
    /// </summary>
    public decimal MinKvp { get; set; } = 40;

    /// <summary>
    /// Maximum tube peak voltage in kVp.
    /// </summary>
    public decimal MaxKvp { get; set; } = 150;

    /// <summary>
    /// Minimum tube current in mA.
    /// </summary>
    public decimal MinMa { get; set; } = 1;

    /// <summary>
    /// Maximum tube current in mA.
    /// </summary>
    public decimal MaxMa { get; set; } = 500;

    /// <summary>
    /// Maximum exposure time in milliseconds.
    /// </summary>
    public int MaxExposureTimeMs { get; set; } = 3000;

    /// <summary>
    /// Maximum mAs (milliampere-seconds).
    /// Calculated as: kVp * mA * ExposureTime / 1000
    /// Default: 2000 mAs accommodates realistic clinical protocols (e.g., 120kVp * 100mA * 100ms = 1200 mAs)
    /// </summary>
    public decimal MaxMas { get; set; } = 2000;

    /// <summary>
    /// Validates a protocol's exposure parameters against these safety limits.
    /// SPEC-WORKFLOW-001 FR-WF-09: Enforce safety limits on protocol save
    /// </summary>
    /// <param name="protocol">The protocol to validate.</param>
    /// <returns>Validation result with any violations.</returns>
    public ProtocolValidationResult Validate(Protocol protocol)
    {
        var errors = new System.Collections.Generic.List<string>();

        // Validate kVp
        if (protocol.Kv < MinKvp || protocol.Kv > MaxKvp)
        {
            errors.Add($"kVp {protocol.Kv} is outside allowed range [{MinKvp}, {MaxKvp}]");
        }

        // Validate mA
        if (protocol.Ma < MinMa || protocol.Ma > MaxMa)
        {
            errors.Add($"mA {protocol.Ma} is outside allowed range [{MinMa}, {MaxMa}]");
        }

        // Validate exposure time
        if (protocol.ExposureTimeMs > MaxExposureTimeMs)
        {
            errors.Add($"Exposure time {protocol.ExposureTimeMs}ms exceeds maximum {MaxExposureTimeMs}ms");
        }

        // Validate mAs
        var calculatedMas = protocol.CalculatedMas;
        if (calculatedMas > MaxMas)
        {
            errors.Add($"Calculated mAs {calculatedMas:F2} exceeds maximum {MaxMas}");
        }

        return new ProtocolValidationResult
        {
            IsValid = errors.Count == 0,
            ProtocolId = protocol.ProtocolId.ToString(),
            Kv = (int)protocol.Kv,
            Ma = (int)protocol.Ma,
            Ms = protocol.ExposureTimeMs,
            Mas = calculatedMas,
            Errors = errors.ToArray(),
            Warnings = Array.Empty<string>()
        };
    }

    /// <summary>
    /// Creates a DeviceSafetyLimits instance from an IDeviceSafetyLimits.
    /// </summary>
    public static DeviceSafetyLimits FromInterface(IDeviceSafetyLimits limits)
    {
        return new DeviceSafetyLimits
        {
            MinKvp = limits.MinKvp,
            MaxKvp = limits.MaxKvp,
            MinMa = limits.MinMa,
            MaxMa = limits.MaxMa,
            MaxExposureTimeMs = limits.MaxExposureTime,
            MaxMas = limits.MaxMas
        };
    }
}

/// <summary>
/// Protocol validation result.
/// </summary>
/// <remarks>
/// @MX:NOTE: Protocol validation result - validation outcome
/// </remarks>
public sealed class ProtocolValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether validation passed.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets or sets the protocol identifier.
    /// </summary>
    public required string ProtocolId { get; init; }

    /// <summary>
    /// Gets or sets the tube peak voltage in kV.
    /// </summary>
    public required int Kv { get; init; }

    /// <summary>
    /// Gets or sets the tube current in mA.
    /// </summary>
    public required int Ma { get; init; }

    /// <summary>
    /// Gets or sets the exposure time in ms.
    /// </summary>
    public required int Ms { get; init; }

    /// <summary>
    /// Gets or sets the calculated mAs value.
    /// </summary>
    public required decimal Mas { get; init; }

    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    public required string[] Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the validation warnings.
    /// </summary>
    public required string[] Warnings { get; init; } = Array.Empty<string>();
}
