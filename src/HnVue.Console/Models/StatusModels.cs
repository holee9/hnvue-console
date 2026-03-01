namespace HnVue.Console.Models;

/// <summary>
/// Component status information.
/// SPEC-UI-001: FR-UI-12 System Status Dashboard.
/// </summary>
public record ComponentStatus
{
    public required string ComponentId { get; init; }
    public required ComponentType Type { get; init; }
    public required ComponentHealth Health { get; init; }
    public required string StatusMessage { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Component type enumeration.
/// </summary>
public enum ComponentType
{
    XrayGenerator,
    Detector,
    Collimator,
    Network,
    DicomService,
    CoreEngine,
    DoseService
}

/// <summary>
/// Component health enumeration.
/// </summary>
public enum ComponentHealth
{
    Healthy,
    Degraded,
    Error,
    Offline,
    Unknown
}

/// <summary>
/// Overall system status.
/// </summary>
public record SystemOverallStatus
{
    public required ComponentHealth OverallHealth { get; init; }
    public required IReadOnlyList<ComponentStatus> ComponentStatuses { get; init; }
    public required bool CanInitiateExposure { get; init; }
    public required string[] ActiveAlerts { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Status update notification.
/// </summary>
public record StatusUpdate
{
    public required ComponentStatus Component { get; init; }
    public bool RequiresExposureHalt { get; init; }
}
