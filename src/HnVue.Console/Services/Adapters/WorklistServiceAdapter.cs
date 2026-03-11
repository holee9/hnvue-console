using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IWorklistService.
/// SPEC-ADAPTER-001: DICOM Modality Worklist (MWL) integration.
/// @MX:NOTE Uses WorklistService gRPC for MWL query and status updates.
/// </summary>
public sealed class WorklistServiceAdapter : GrpcAdapterBase, IWorklistService
{
    private readonly ILogger<WorklistServiceAdapter> _logger;

    public WorklistServiceAdapter(IConfiguration configuration, ILogger<WorklistServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorklistItem>> GetWorklistAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.WorklistService.WorklistServiceClient>();
            var response = await client.QueryWorklistAsync(
                new HnVue.Ipc.QueryWorklistRequest { MaxResults = 100 },
                cancellationToken: ct);
            return response.Entries.Select(MapToWorklistItem).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IWorklistService), nameof(GetWorklistAsync));
            return Array.Empty<WorklistItem>();
        }
    }

    public async Task<WorklistRefreshResult> RefreshWorklistAsync(WorklistRefreshRequest request, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.WorklistService.WorklistServiceClient>();
            var grpcRequest = new HnVue.Ipc.QueryWorklistRequest { MaxResults = 100 };

            if (request.Since.HasValue)
            {
                grpcRequest.ScheduledDateStart = request.Since.Value.ToString("yyyy-MM-dd");
            }

            var response = await client.QueryWorklistAsync(grpcRequest, cancellationToken: ct);
            var items = response.Entries.Select(MapToWorklistItem).ToList();

            return new WorklistRefreshResult
            {
                Items = items,
                RefreshedAt = DateTimeOffset.UtcNow
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IWorklistService), nameof(RefreshWorklistAsync));
            return new WorklistRefreshResult
            {
                Items = Array.Empty<WorklistItem>(),
                RefreshedAt = DateTimeOffset.UtcNow
            };
        }
    }

    public async Task SelectWorklistItemAsync(string procedureId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.WorklistService.WorklistServiceClient>();
            await client.UpdateWorklistStatusAsync(
                new HnVue.Ipc.UpdateWorklistStatusRequest
                {
                    WorklistEntryId = procedureId,
                    NewStatus = HnVue.Ipc.WorklistStatus.InProgress
                },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IWorklistService), nameof(SelectWorklistItemAsync));
        }
    }

    /// <summary>
    /// @MX:ANCHOR Proto to domain mapping for WorklistItem.
    /// </summary>
    private static WorklistItem MapToWorklistItem(HnVue.Ipc.WorklistEntry entry)
    {
        var patientName = entry.Patient is not null
            ? $"{entry.Patient.FamilyName}^{entry.Patient.GivenName}"
            : string.Empty;

        var scheduledDate = DateTimeOffset.TryParse($"{entry.ScheduledDate} {entry.ScheduledTime}", out var dt)
            ? dt
            : DateTimeOffset.UtcNow;

        return new WorklistItem
        {
            ProcedureId = entry.WorklistEntryId,
            PatientId = entry.Patient?.PatientId ?? string.Empty,
            PatientName = patientName,
            AccessionNumber = entry.AccessionNumber,
            ScheduledProcedureStepDescription = entry.StudyDescription,
            ScheduledDateTime = scheduledDate,
            BodyPart = entry.StudyDescription,
            Projection = entry.Modality,
            Status = MapFromProtoStatus(entry.Status)
        };
    }

    private static WorklistStatus MapFromProtoStatus(HnVue.Ipc.WorklistStatus status) => status switch
    {
        HnVue.Ipc.WorklistStatus.Scheduled => WorklistStatus.Scheduled,
        HnVue.Ipc.WorklistStatus.InProgress => WorklistStatus.InProgress,
        HnVue.Ipc.WorklistStatus.Completed => WorklistStatus.Completed,
        HnVue.Ipc.WorklistStatus.Cancelled or HnVue.Ipc.WorklistStatus.Discontinued => WorklistStatus.Cancelled,
        _ => WorklistStatus.Scheduled
    };
}
