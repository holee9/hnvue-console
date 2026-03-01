namespace HnVue.Dicom.Storage;

/// <summary>
/// Status of a PACS export operation.
/// </summary>
/// <remarks>
/// @MX:NOTE Export status tracking - Tracks lifecycle of PACS exports
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-408
/// </remarks>
public enum PacsExportStatus
{
    /// <summary>Export is pending.</summary>
    Pending,

    /// <summary>Export is in progress.</summary>
    InProgress,

    /// <summary>Export succeeded.</summary>
    Succeeded,

    /// <summary>Export failed after all retries.</summary>
    Failed,

    /// <summary>Export was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Result of a PACS export operation.
/// </summary>
/// <remarks>
/// @MX:NOTE Error handling - Export results provide status and error information
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-408
/// </remarks>
public record PacsExportResult
{
    /// <summary>
    /// Gets the export status.
    /// </summary>
    public required PacsExportStatus ExportStatus { get; init; }

    /// <summary>
    /// Gets the error message if the export failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets whether the export succeeded.
    /// </summary>
    public bool IsSuccess => ExportStatus == PacsExportStatus.Succeeded;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PacsExportResult Success() =>
        new()
        {
            ExportStatus = PacsExportStatus.Succeeded,
            ErrorMessage = null
        };

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static PacsExportResult Failure(string errorMessage) =>
        new()
        {
            ExportStatus = PacsExportStatus.Failed,
            ErrorMessage = errorMessage
        };

    /// <summary>
    /// Creates a pending result (queued for retry).
    /// </summary>
    public static PacsExportResult Pending(string errorMessage) =>
        new()
        {
            ExportStatus = PacsExportStatus.Pending,
            ErrorMessage = errorMessage
        };
}
