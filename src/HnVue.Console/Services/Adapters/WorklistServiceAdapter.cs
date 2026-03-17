using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IWorklistService.
/// SPEC-UI-001: FR-UI-02 Worklist Display.
/// </summary>
public sealed class WorklistServiceAdapter : GrpcAdapterBase, IWorklistService
{
    private readonly ILogger<WorklistServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="WorklistServiceAdapter"/>.
    /// </summary>
    public WorklistServiceAdapter(IConfiguration configuration, ILogger<WorklistServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorklistItem>> GetWorklistAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.WorklistService.WorklistServiceClient>();
            var grpcRequest = new HnVue.Ipc.QueryWorklistRequest
            {
                MaxResults = 100
            };

            var response = await client.QueryWorklistAsync(grpcRequest, cancellationToken: ct);

            return response.Entries.Select(e => new WorklistItem
            {
                ProcedureId = e.RequestedProcedureId,
                PatientId = e.Patient?.PatientId ?? string.Empty,
                PatientName = e.Patient != null ? $"{e.Patient.FamilyName} {e.Patient.GivenName}".Trim() : string.Empty,
                AccessionNumber = e.AccessionNumber,
                ScheduledProcedureStepDescription = e.RequestedProcedureDescription,
                ScheduledDateTime = ParseScheduledDateTime(e.ScheduledDate, e.ScheduledTime),
                BodyPart = string.Empty,
                Projection = string.Empty,
                Status = MapWorklistStatus(e.Status)
            }).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IWorklistService), nameof(GetWorklistAsync));
            return Array.Empty<WorklistItem>();
        }
    }

    /// <inheritdoc />
    public async Task<WorklistRefreshResult> RefreshWorklistAsync(WorklistRefreshRequest request, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.WorklistService.WorklistServiceClient>();
            var grpcRequest = new HnVue.Ipc.QueryWorklistRequest
            {
                MaxResults = 100
            };

            var response = await client.QueryWorklistAsync(grpcRequest, cancellationToken: ct);

            var items = response.Entries.Select(e => new WorklistItem
            {
                ProcedureId = e.RequestedProcedureId,
                PatientId = e.Patient?.PatientId ?? string.Empty,
                PatientName = e.Patient != null ? $"{e.Patient.FamilyName} {e.Patient.GivenName}".Trim() : string.Empty,
                AccessionNumber = e.AccessionNumber,
                ScheduledProcedureStepDescription = e.RequestedProcedureDescription,
                ScheduledDateTime = ParseScheduledDateTime(e.ScheduledDate, e.ScheduledTime),
                BodyPart = string.Empty,
                Projection = string.Empty,
                Status = MapWorklistStatus(e.Status)
            }).ToList();

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

    /// <inheritdoc />
    public async Task SelectWorklistItemAsync(string procedureId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.WorklistService.WorklistServiceClient>();
            var grpcRequest = new HnVue.Ipc.UpdateWorklistStatusRequest
            {
                WorklistEntryId = procedureId,
                NewStatus = HnVue.Ipc.WorklistStatus.InProgress
            };

            await client.UpdateWorklistStatusAsync(grpcRequest, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IWorklistService), nameof(SelectWorklistItemAsync));
        }
    }

    private static DateTimeOffset ParseScheduledDateTime(string date, string time)
    {
        if (DateOnly.TryParse(date, out var dateOnly) && TimeOnly.TryParse(time, out var timeOnly))
        {
            return new DateTimeOffset(dateOnly, timeOnly, TimeSpan.Zero);
        }
        if (DateOnly.TryParse(date, out dateOnly))
        {
            return new DateTimeOffset(dateOnly, TimeOnly.MinValue, TimeSpan.Zero);
        }
        return DateTimeOffset.MinValue;
    }

    private static WorklistStatus MapWorklistStatus(HnVue.Ipc.WorklistStatus protoStatus)
    {
        return protoStatus switch
        {
            HnVue.Ipc.WorklistStatus.Scheduled => WorklistStatus.Scheduled,
            HnVue.Ipc.WorklistStatus.InProgress => WorklistStatus.InProgress,
            HnVue.Ipc.WorklistStatus.Completed => WorklistStatus.Completed,
            HnVue.Ipc.WorklistStatus.Cancelled => WorklistStatus.Cancelled,
            HnVue.Ipc.WorklistStatus.Discontinued => WorklistStatus.Cancelled,
            _ => WorklistStatus.Scheduled
        };
    }
}
