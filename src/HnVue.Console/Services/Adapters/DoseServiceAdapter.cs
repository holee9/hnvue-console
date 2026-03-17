using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IDoseService.
/// SPEC-UI-001: FR-UI-10 Dose Display.
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
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(GetCurrentDoseDisplayAsync));
        await Task.CompletedTask;
        return new DoseDisplay
        {
            CurrentDose = new DoseValue
            {
                Value = 0,
                Unit = DoseUnit.MilliGray,
                MeasuredAt = DateTimeOffset.UtcNow
            },
            CumulativeDose = new DoseValue
            {
                Value = 0,
                Unit = DoseUnit.MilliGray,
                MeasuredAt = DateTimeOffset.UtcNow
            },
            StudyId = string.Empty,
            ExposureCount = 0
        };
    }

    /// <inheritdoc />
    public async Task<DoseAlertThreshold> GetAlertThresholdAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(GetAlertThresholdAsync));
        await Task.CompletedTask;
        return new DoseAlertThreshold
        {
            WarningThreshold = 10m,
            ErrorThreshold = 20m,
            Unit = DoseUnit.MilliGray
        };
    }

    /// <inheritdoc />
    public async Task SetAlertThresholdAsync(DoseAlertThreshold threshold, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(SetAlertThresholdAsync));
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DoseUpdate> SubscribeDoseUpdatesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(SubscribeDoseUpdatesAsync));
        await Task.CompletedTask;
        yield break;
    }

    /// <inheritdoc />
    public async Task ResetCumulativeDoseAsync(string studyId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(ResetCumulativeDoseAsync));
        await Task.CompletedTask;
    }
}
