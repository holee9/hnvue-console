namespace HnVue.Console.Models;

/// <summary>
/// Quality Control action type.
/// SPEC-UI-001: FR-UI-05 Image Quality Control.
/// </summary>
public enum QCAction
{
    Accept,
    Reject,
    Reprocess
}

/// <summary>
/// QC action request.
/// </summary>
public record QCActionRequest
{
    public required string ImageId { get; init; }
    public required QCAction Action { get; init; }
    public string? RejectionReason { get; init; }
}

/// <summary>
/// QC action result.
/// </summary>
public record QCActionResult
{
    public required bool Success { get; init; }
    public required string ImageId { get; init; }
    public QCStatus NewStatus { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Image QC status.
/// </summary>
public enum QCStatus
{
    Pending,
    Accepted,
    Rejected,
    Reprocessed
}

/// <summary>
/// Rejection reason enumeration.
/// </summary>
public enum RejectionReason
{
    PatientMotion,
    ExposureError,
    PositioningError,
    Artifact,
    EquipmentMalfunction,
    WrongProtocol,
    Duplicate,
    Other
}
