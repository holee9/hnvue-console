using System.Diagnostics;
using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock QC service for development.
/// SPEC-UI-001: Mock service for quality control.
/// </summary>
public class MockQCService : IQCService
{
    private readonly Dictionary<string, QCStatus> _imageStatus = new();

    /// <inheritdoc/>
    public Task<QCActionResult> AcceptImageAsync(string imageId, CancellationToken ct)
    {
        _imageStatus[imageId] = QCStatus.Accepted;

        Debug.WriteLine($"[MockQCService] Accepted image: {imageId}");

        return Task.FromResult(new QCActionResult
        {
            Success = true,
            ImageId = imageId,
            NewStatus = QCStatus.Accepted
        });
    }

    /// <inheritdoc/>
    public Task<QCActionResult> RejectImageAsync(string imageId, RejectionReason reason, string? notes, CancellationToken ct)
    {
        _imageStatus[imageId] = QCStatus.Rejected;

        Debug.WriteLine($"[MockQCService] Rejected image: {imageId} - Reason: {reason}, Notes: {notes}");

        return Task.FromResult(new QCActionResult
        {
            Success = true,
            ImageId = imageId,
            NewStatus = QCStatus.Rejected,
            ErrorMessage = $"Rejected: {reason}"
        });
    }

    /// <inheritdoc/>
    public Task<QCActionResult> ReprocessImageAsync(string imageId, CancellationToken ct)
    {
        _imageStatus[imageId] = QCStatus.Reprocessed;

        Debug.WriteLine($"[MockQCService] Requested reprocessing for: {imageId}");

        return Task.FromResult(new QCActionResult
        {
            Success = true,
            ImageId = imageId,
            NewStatus = QCStatus.Reprocessed
        });
    }

    /// <inheritdoc/>
    public Task<QCStatus> GetQCStatusAsync(string imageId, CancellationToken ct)
    {
        if (_imageStatus.TryGetValue(imageId, out var status))
        {
            return Task.FromResult(status);
        }

        return Task.FromResult(QCStatus.Pending);
    }

    /// <inheritdoc/>
    public Task<QCActionResult> ExecuteQCActionAsync(QCActionRequest request, CancellationToken ct)
    {
        return request.Action switch
        {
            QCAction.Accept => AcceptImageAsync(request.ImageId, ct),
            QCAction.Reject => RejectImageAsync(request.ImageId, RejectionReason.Other, request.RejectionReason, ct),
            QCAction.Reprocess => ReprocessImageAsync(request.ImageId, ct),
            _ => Task.FromResult(new QCActionResult { Success = false, ImageId = request.ImageId })
        };
    }
}
