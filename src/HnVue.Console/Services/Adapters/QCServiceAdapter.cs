using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IQCService.
/// SPEC-ADAPTER-001: Quality control review workflow using QCService gRPC.
/// @MX:NOTE Uses QCService gRPC for image QC review, status tracking, and action execution.
/// </summary>
public sealed class QCServiceAdapter : GrpcAdapterBase, IQCService
{
    private readonly ILogger<QCServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="QCServiceAdapter"/>.
    /// </summary>
    public QCServiceAdapter(IConfiguration configuration, ILogger<QCServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QCActionResult> AcceptImageAsync(string imageId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.QCService.QCServiceClient>();
            var response = await client.SubmitForQcReviewAsync(
                new HnVue.Ipc.SubmitForQcReviewRequest
                {
                    ImageId = imageId,
                    Priority = HnVue.Ipc.QcPriority.QcPriorityNormal,
                    SubmittedBy = "Operator" // @MX:TODO Get actual user ID
                },
                cancellationToken: ct);

            // Accept by performing QC decision
            var actionResponse = await client.PerformQcActionAsync(
                new HnVue.Ipc.PerformQcActionRequest
                {
                    QcReviewId = response.QcReviewId,
                    Decision = HnVue.Ipc.QcDecision.QcDecisionAccept,
                    PerformedBy = "Operator"
                },
                cancellationToken: ct);

            return new QCActionResult
            {
                Success = actionResponse.Success,
                ImageId = imageId,
                NewStatus = MapQCStatus(actionResponse.UpdatedReview.Status),
                ErrorMessage = actionResponse.Error?.Message ?? string.Empty
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IQCService), nameof(AcceptImageAsync));
            return new QCActionResult
            {
                Success = false,
                ImageId = imageId,
                NewStatus = QCStatus.Pending,
                ErrorMessage = ex.Status.StatusCode.ToString()
            };
        }
    }

    /// <inheritdoc />
    public async Task<QCActionResult> RejectImageAsync(string imageId, RejectionReason reason, string? notes, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.QCService.QCServiceClient>();

            // Get QC review ID first
            var statusResponse = await client.GetQcStatusAsync(
                new HnVue.Ipc.GetQcStatusRequest
                {
                    ImageId = imageId
                },
                cancellationToken: ct);

            var defect = new HnVue.Ipc.QcDefect
            {
                DefectType = MapRejectionReasonToDefectType(reason),
                Description = notes ?? $"Rejected: {reason}",
                RequiresRetake = true
            };

            var response = await client.PerformQcActionAsync(
                new HnVue.Ipc.PerformQcActionRequest
                {
                    QcReviewId = statusResponse.Review.QcReviewId,
                    Decision = HnVue.Ipc.QcDecision.QcDecisionRejectRetake,
                    Defects = { defect },
                    Notes = notes ?? string.Empty,
                    PerformedBy = "Operator"
                },
                cancellationToken: ct);

            return new QCActionResult
            {
                Success = response.Success,
                ImageId = imageId,
                NewStatus = MapQCStatus(response.UpdatedReview.Status),
                ErrorMessage = response.Error?.Message ?? string.Empty
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IQCService), nameof(RejectImageAsync));
            return new QCActionResult
            {
                Success = false,
                ImageId = imageId,
                NewStatus = QCStatus.Pending,
                ErrorMessage = ex.Status.StatusCode.ToString()
            };
        }
    }

    /// <inheritdoc />
    public async Task<QCActionResult> ReprocessImageAsync(string imageId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.QCService.QCServiceClient>();

            // Get QC review ID first
            var statusResponse = await client.GetQcStatusAsync(
                new HnVue.Ipc.GetQcStatusRequest
                {
                    ImageId = imageId
                },
                cancellationToken: ct);

            var response = await client.PerformQcActionAsync(
                new HnVue.Ipc.PerformQcActionRequest
                {
                    QcReviewId = statusResponse.Review.QcReviewId,
                    Decision = HnVue.Ipc.QcDecision.QcDecisionReprocess,
                    PerformedBy = "Operator"
                },
                cancellationToken: ct);

            return new QCActionResult
            {
                Success = response.Success,
                ImageId = imageId,
                NewStatus = MapQCStatus(response.UpdatedReview.Status),
                ErrorMessage = response.Error?.Message ?? string.Empty
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IQCService), nameof(ReprocessImageAsync));
            return new QCActionResult
            {
                Success = false,
                ImageId = imageId,
                NewStatus = QCStatus.Pending,
                ErrorMessage = ex.Status.StatusCode.ToString()
            };
        }
    }

    /// <inheritdoc />
    public async Task<QCStatus> GetQCStatusAsync(string imageId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.QCService.QCServiceClient>();
            var response = await client.GetQcStatusAsync(
                new HnVue.Ipc.GetQcStatusRequest
                {
                    ImageId = imageId
                },
                cancellationToken: ct);

            return MapQCStatus(response.Review.Status);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IQCService), nameof(GetQCStatusAsync));
            return QCStatus.Pending;
        }
    }

    /// <inheritdoc />
    public async Task<QCActionResult> ExecuteQCActionAsync(QCActionRequest request, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.QCService.QCServiceClient>();

            // Get QC review ID first
            var statusResponse = await client.GetQcStatusAsync(
                new HnVue.Ipc.GetQcStatusRequest
                {
                    ImageId = request.ImageId
                },
                cancellationToken: ct);

            var response = await client.PerformQcActionAsync(
                new HnVue.Ipc.PerformQcActionRequest
                {
                    QcReviewId = statusResponse.Review.QcReviewId,
                    Decision = MapQCActionToDecision(request.Action),
                    Notes = request.Notes ?? string.Empty,
                    PerformedBy = "Operator"
                },
                cancellationToken: ct);

            return new QCActionResult
            {
                Success = response.Success,
                ImageId = request.ImageId,
                NewStatus = MapQCStatus(response.UpdatedReview.Status),
                ErrorMessage = response.Error?.Message ?? string.Empty
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IQCService), nameof(ExecuteQCActionAsync));
            return new QCActionResult
            {
                Success = false,
                ImageId = request.ImageId,
                NewStatus = QCStatus.Pending,
                ErrorMessage = ex.Status.StatusCode.ToString()
            };
        }
    }

    private static QCStatus MapQCStatus(HnVue.Ipc.QcStatus protoStatus)
    {
        return protoStatus switch
        {
            HnVue.Ipc.QcStatus.QcStatusPending => QCStatus.Pending,
            HnVue.Ipc.QcStatus.QcStatusInReview => QCStatus.InReview,
            HnVue.Ipc.QcStatus.QcStatusAccepted => QCStatus.Accepted,
            HnVue.Ipc.QcStatus.QcStatusRejected => QCStatus.Rejected,
            HnVue.Ipc.QcStatus.QcStatusReprocessing => QCStatus.Reprocessing,
            _ => QCStatus.Pending
        };
    }

    private static HnVue.Ipc.QcDecision MapQCActionToDecision(QCAction action)
    {
        return action switch
        {
            QCAction.Accept => HnVue.Ipc.QcDecision.QcDecisionAccept,
            QCAction.Reject => HnVue.Ipc.QcDecision.QcDecisionRejectRetake,
            QCAction.Reprocess => HnVue.Ipc.QcDecision.QcDecisionReprocess,
            _ => HnVue.Ipc.QcDecision.QcDecisionUnspecified
        };
    }

    private static HnVue.Ipc.QcDefectType MapRejectionReasonToDefectType(RejectionReason reason)
    {
        return reason switch
        {
            RejectionReason.Motion => HnVue.Ipc.QcDefectType.QcDefectTypeMotionArtifact,
            RejectionReason.Positioning => HnVue.Ipc.QcDefectType.QcDefectTypePositioning,
            RejectionReason.ExposureError => HnVue.Ipc.QcDefectType.QcDefectTypeUnderexposure,
            RejectionReason.EquipmentArtifact => HnVue.Ipc.QcDefectType.QcDefectTypeArtifact,
            RejectionReason.Other => HnVue.Ipc.QcDefectType.QcDefectTypeOther,
            _ => HnVue.Ipc.QcDefectType.QcDefectTypeUnspecified
        };
    }
}
