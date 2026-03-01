namespace HnVue.Console.Models;

/// <summary>
/// Dose value with units.
/// SPEC-UI-001: FR-UI-10 Dose Display.
/// </summary>
public record DoseValue
{
    public required decimal Value { get; init; }
    public required DoseUnit Unit { get; init; }
    public required DateTimeOffset MeasuredAt { get; init; }
}

/// <summary>
/// Dose unit enumeration.
/// </summary>
public enum DoseUnit
{
    MicroGray,        // µGy
    MilliGray,         // mGy
    MicroGraySquareCm, // µGy·cm² (DAP)
    MilliGraySquareCm  // mGy·cm² (DAP)
}

/// <summary>
/// Dose display information.
/// </summary>
public record DoseDisplay
{
    public required DoseValue CurrentDose { get; init; }
    public required DoseValue CumulativeDose { get; init; }
    public required string StudyId { get; init; }
    public int ExposureCount { get; init; }
}

/// <summary>
/// Dose alert threshold configuration.
/// </summary>
public record DoseAlertThreshold
{
    public required decimal WarningThreshold { get; init; }
    public required decimal ErrorThreshold { get; init; }
    public required DoseUnit Unit { get; init; }
}

/// <summary>
/// Dose update notification.
/// </summary>
public record DoseUpdate
{
    public required DoseValue NewDose { get; init; }
    public required DoseValue CumulativeDose { get; init; }
    public bool IsWarningThresholdExceeded { get; init; }
    public bool IsErrorThresholdExceeded { get; init; }
}
