using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// QC service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-05 Image Quality Control.
/// </summary>
public interface IQCService
{
    /// <summary>
    /// Accepts an image.
    /// </summary>
    Task<QCActionResult> AcceptImageAsync(string imageId, CancellationToken ct);

    /// <summary>
    /// Rejects an image with a reason.
    /// </summary>
    Task<QCActionResult> RejectImageAsync(string imageId, RejectionReason reason, string? notes, CancellationToken ct);

    /// <summary>
    /// Requests image reprocessing.
    /// </summary>
    Task<QCActionResult> ReprocessImageAsync(string imageId, CancellationToken ct);

    /// <summary>
    /// Gets QC status for an image.
    /// </summary>
    Task<QCStatus> GetQCStatusAsync(string imageId, CancellationToken ct);

    /// <summary>
    /// Executes a QC action.
    /// </summary>
    Task<QCActionResult> ExecuteQCActionAsync(QCActionRequest request, CancellationToken ct);
}
