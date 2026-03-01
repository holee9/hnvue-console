namespace HnVue.Workflow.Protocol;

using System;
using System.Collections.Generic;

// Legacy validation classes - preserved for backward compatibility with existing tests
// TODO: Migrate tests to use new ProtocolRepository and DeviceSafetyLimits classes

/// <summary>
/// Validates protocol parameters and exposure settings.
/// Legacy class - use DeviceSafetyLimits for protocol validation.
/// </summary>
/// <remarks>
/// @MX:LEGACY: Protocol validator - legacy class, use DeviceSafetyLimits instead
/// </remarks>
public sealed class ProtocolValidator
{
    /// <summary>
    /// Validates exposure parameters against protocol constraints.
    /// </summary>
    /// <param name="protocol">The protocol definition.</param>
    /// <param name="kv">Tube peak voltage in kV.</param>
    /// <param name="ma">Tube current in mA.</param>
    /// <param name="ms">Exposure time in ms.</param>
    /// <returns>The validation result.</returns>
    public ProtocolValidationResult ValidateExposure(Protocol protocol, decimal kv, decimal ma, int ms)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate kV range (protocol doesn't have min/max, use clinical limits)
        if (kv < 40 || kv > 150)
        {
            errors.Add($"kV {kv} outside clinical range [40, 150]");
        }

        // Validate mA range
        if (ma < 10 || ma > 1000)
        {
            errors.Add($"mA {ma} outside clinical range [10, 1000]");
        }

        // Validate ms range
        if (ms < 1 || ms > 2000)
        {
            errors.Add($"ms {ms} outside clinical range [1, 2000]");
        }

        // Validate mAs
        var mas = protocol.CalculatedMas;
        if (mas < 1 || mas > 1000)
        {
            errors.Add($"mAs {mas:F2} outside clinical range [1, 1000]");
        }

        return new ProtocolValidationResult
        {
            IsValid = errors.Count == 0,
            ProtocolId = protocol.ProtocolId.ToString(),
            Kv = (int)kv,
            Ma = (int)ma,
            Ms = ms,
            Mas = mas,
            Errors = errors.ToArray(),
            Warnings = warnings.ToArray()
        };
    }

    /// <summary>
    /// Validates protocol compatibility with body part and view.
    /// </summary>
    /// <param name="protocol">The protocol definition.</param>
    /// <param name="bodyPart">The body part.</param>
    /// <param name="projection">The projection/view.</param>
    /// <returns>True if compatible; false otherwise.</returns>
    public bool IsCompatible(Protocol protocol, string bodyPart, string projection)
    {
        // Check body part match (case-insensitive)
        if (!string.Equals(protocol.BodyPart, bodyPart, StringComparison.OrdinalIgnoreCase))
        {
            // Try partial match
            if (!protocol.BodyPart.Contains(bodyPart, StringComparison.OrdinalIgnoreCase) &&
                !bodyPart.Contains(protocol.BodyPart, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Maps procedure codes to DICOM SOP Class UIDs.
/// </summary>
/// <remarks>
/// @MX:NOTE: Procedure code mapper - DICOM code mapping
/// </remarks>
public sealed class ProcedureCodeMapper
{
    private readonly Dictionary<string, string> _codeToSopClassMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcedureCodeMapper"/> class.
    /// </summary>
    public ProcedureCodeMapper()
    {
        _codeToSopClassMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common procedure codes to SOP Class mappings
            { "CHEST_AP", "1.2.840.10008.5.1.4.1.1.2.1.1" },  // CR Image Storage
            { "CHEST_PA", "1.2.840.10008.5.1.4.1.1.2.1.1" },
            { "ABDOMEN_AP", "1.2.840.10008.5.1.4.1.1.2.1.1" },
            { "EXTREMITY_AP", "1.2.840.10008.5.1.4.1.1.2.1.1" }
        };
    }

    /// <summary>
    /// Gets the SOP Class UID for a procedure code.
    /// </summary>
    /// <param name="procedureCode">The procedure code.</param>
    /// <returns>The SOP Class UID, or null if not found.</returns>
    public string? GetSopClassUid(string procedureCode)
    {
        return _codeToSopClassMap.GetValueOrDefault(procedureCode);
    }
}
