using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IDoseService.
/// SPEC-ADAPTER-001: Dose tracking and radiation monitoring (IEC 62304 Class B/C).
/// @MX:NOTE Uses DoseService gRPC for dose recording, history, and alert streaming.
/// </summary>
public sealed class DoseServiceAdapter : GrpcAdapterBase, IDoseService
{
    private readonly ILogger<DoseServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DoseServiceAdapter"/>.
    /// </summary>
    public DoseServiceAdapter(IConfiguration configuration, ILogger<DoseServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DoseDisplay> GetCurrentDoseDisplayAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.DoseService.DoseServiceClient>();
            var response = await client.GetDoseSummaryAsync(
                new HnVue.Ipc.GetDoseSummaryRequest
                {
                    Period = HnVue.Ipc.SummaryPeriod.Last30Days
                },
                cancellationToken: ct);

            var doseValue = new DoseValue
            {
                Value = (decimal)response.CumulativeEffectiveDoseMsv,
                Unit = DoseUnit.MilliGray,
                MeasuredAt = DateTimeOffset.UtcNow
            };

            return new DoseDisplay
            {
                CurrentDose = doseValue,
                CumulativeDose = doseValue,
                StudyId = string.Empty,
                ExposureCount = response.ExamCount
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IDoseService), nameof(GetCurrentDoseDisplayAsync));
            var zeroDose = new DoseValue { Value = 0m, Unit = DoseUnit.MicroGray, MeasuredAt = DateTimeOffset.UtcNow };
            return new DoseDisplay
            {
                CurrentDose = zeroDose,
                CumulativeDose = zeroDose,
                StudyId = string.Empty,
                ExposureCount = 0
            };
        }
    }

    /// <inheritdoc />
    public Task<DoseAlertThreshold> GetAlertThresholdAsync(CancellationToken ct)
    {
        // @MX:TODO Proto does not expose threshold configuration directly.
        // Return default threshold values.
        return Task.FromResult(new DoseAlertThreshold
        {
            WarningThreshold = 100m,  // 100 mGy warning threshold
            ErrorThreshold = 200m,    // 200 mGy error threshold
            Unit = DoseUnit.MilliGray
        });
    }

    /// <inheritdoc />
    public Task SetAlertThresholdAsync(DoseAlertThreshold threshold, CancellationToken ct)
    {
        // @MX:TODO Proto does not expose threshold configuration directly.
        // This would require extension to DoseService proto.
        _logger.LogInformation("Dose alert threshold set: Warning={Warning}, Error={Error}",
            threshold.WarningThreshold, threshold.ErrorThreshold);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DoseUpdate> SubscribeDoseUpdatesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        HnVue.Ipc.DoseService.DoseServiceClient client;
        AsyncServerStreamingCall<HnVue.Ipc.DoseAlertEvent> call;

        try
        {
            client = CreateClient<HnVue.Ipc.DoseService.DoseServiceClient>();
            call = client.SubscribeDoseAlerts(new HnVue.Ipc.DoseAlertSubscribeRequest
            {
                IncludeAllPatients = true
            }, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IDoseService), nameof(SubscribeDoseUpdatesAsync));
            yield break;
        }

        await foreach (var alertEvent in call.ResponseStream.ReadAllAsync(ct))
        {
            var doseValue = new DoseValue
            {
                Value = (decimal)alertEvent.CurrentDoseMsv,
                Unit = DoseUnit.MilliGray,
                MeasuredAt = DateTimeOffset.UtcNow
            };

            yield return new DoseUpdate
            {
                NewDose = doseValue,
                CumulativeDose = doseValue,
                IsWarningThresholdExceeded = alertEvent.Level >= HnVue.Ipc.DoseAlertLevel.Warning,
                IsErrorThresholdExceeded = alertEvent.Level >= HnVue.Ipc.DoseAlertLevel.Critical
            };
        }
    }

    /// <inheritdoc />
    public Task ResetCumulativeDoseAsync(string studyId, CancellationToken ct)
    {
        // @MX:TODO Proto does not expose reset functionality directly.
        // This would typically be handled by starting a new study.
        _logger.LogInformation("Cumulative dose reset requested for study {StudyId}", studyId);
        return Task.CompletedTask;
    }
}
