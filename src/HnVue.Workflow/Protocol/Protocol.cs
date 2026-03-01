namespace HnVue.Workflow.Protocol;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Represents an X-ray imaging protocol with exposure parameters.
/// SPEC-WORKFLOW-001 FR-WF-08: Protocol definition and validation
/// IEC 62304 Class C - Safety-critical exposure parameters
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Protocol entity - defines X-ray exposure parameters
/// @MX:WARN: Safety-critical - incorrect parameters can cause patient overexposure
/// </remarks>
public sealed class Protocol
{
    private string _bodyPart = string.Empty;
    private string _projection = string.Empty;
    private decimal _kv;
    private decimal _ma;
    private int _exposureTimeMs;
    private string _deviceModel = string.Empty;

    /// <summary>
    /// Unique identifier for this protocol.
    /// </summary>
    public Guid ProtocolId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Body part for this protocol (e.g., "CHEST", "ABDOMEN", "PELVIS").
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to null, empty, or whitespace.</exception>
    public string BodyPart
    {
        get => _bodyPart;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("BodyPart cannot be null, empty, or whitespace.", nameof(BodyPart));
            }
            _bodyPart = value.Trim().ToUpperInvariant();
        }
    }

    /// <summary>
    /// Projection or view (e.g., "PA", "AP", "LATERAL").
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to null, empty, or whitespace.</exception>
    public string Projection
    {
        get => _projection;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Projection cannot be null, empty, or whitespace.", nameof(Projection));
            }
            _projection = value.Trim().ToUpperInvariant();
        }
    }

    /// <summary>
    /// Tube peak voltage in kilovolts (kVp).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to less than or equal to zero.</exception>
    public decimal Kv
    {
        get => _kv;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentException("Kv must be greater than 0.", nameof(Kv));
            }
            _kv = value;
        }
    }

    /// <summary>
    /// Tube current in milliamperes (mA).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to less than or equal to zero.</exception>
    public decimal Ma
    {
        get => _ma;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentException("Ma must be greater than 0.", nameof(Ma));
            }
            _ma = value;
        }
    }

    /// <summary>
    /// Exposure time in milliseconds.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to less than or equal to zero.</exception>
    public int ExposureTimeMs
    {
        get => _exposureTimeMs;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentException("ExposureTimeMs must be greater than 0.", nameof(ExposureTimeMs));
            }
            _exposureTimeMs = value;
        }
    }

    /// <summary>
    /// Automatic Exposure Control mode.
    /// </summary>
    public AecMode AecMode { get; set; } = AecMode.Disabled;

    /// <summary>
    /// Number of AEC chambers to use (0-3).
    /// </summary>
    public byte AecChambers { get; set; }

    /// <summary>
    /// Focal spot size (Small or Large).
    /// </summary>
    public FocusSize FocusSize { get; set; } = FocusSize.Large;

    /// <summary>
    /// Whether anti-scatter grid is used.
    /// </summary>
    public bool GridUsed { get; set; }

    /// <summary>
    /// Device model for this protocol (e.g., "HVG-3000").
    /// </summary>
    public string DeviceModel
    {
        get => _deviceModel;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("DeviceModel cannot be null, empty, or whitespace.", nameof(DeviceModel));
            }
            _deviceModel = value.Trim().ToUpperInvariant();
        }
    }

    /// <summary>
    /// Procedure codes mapped to this protocol (N-to-1 relationship).
    /// SPEC-WORKFLOW-001 FR-WF-08: N-to-1 procedure code to protocol mapping
    /// </summary>
    public string[] ProcedureCodes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether this protocol is active and available for use.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Created timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the composite key for this protocol (BodyPart|Projection|DeviceModel).
    /// SPEC-WORKFLOW-001 FR-WF-08: Protocol lookup by composite key
    /// </summary>
    [JsonIgnore]
    public string CompositeKey => $"{BodyPart}|{Projection}|{DeviceModel}";

    /// <summary>
    /// Calculates the mAs value for this protocol.
    /// Formula: mAs = kVp * mA * ExposureTime / 1000
    /// </summary>
    [JsonIgnore]
    public decimal CalculatedMas => Kv * Ma * ExposureTimeMs / 1000m;

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Protocol? left, Protocol? right) => !(left == right);

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Protocol? left, Protocol? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;
        return left.ProtocolId == right.ProtocolId;
    }

    public override bool Equals(object? obj) => obj is Protocol protocol && this == protocol;

    public override int GetHashCode() => ProtocolId.GetHashCode();
}

/// <summary>
/// Automatic Exposure Control mode.
/// </summary>
public enum AecMode
{
    /// <summary>AEC is disabled - manual exposure control.</summary>
    Disabled,

    /// <summary>AEC is enabled - automatic exposure termination.</summary>
    Enabled,

    /// <summary>AEC override - manual with AEC suggestion.</summary>
    Override
}

/// <summary>
/// Focal spot size.
/// </summary>
public enum FocusSize
{
    /// <summary>Small focal spot (e.g., 0.6mm).</summary>
    Small,

    /// <summary>Large focal spot (e.g., 1.2mm).</summary>
    Large
}
