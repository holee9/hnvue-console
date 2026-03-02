using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IDoseService.
/// No gRPC proto defined yet; returns graceful defaults.
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
    public Task<DoseDisplay> GetCurrentDoseDisplayAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(GetCurrentDoseDisplayAsync));
        var zeroDose = new DoseValue { Value = 0m, Unit = DoseUnit.MicroGray, MeasuredAt = DateTimeOffset.UtcNow };
        return Task.FromResult(new DoseDisplay
        {
            CurrentDose = zeroDose,
            CumulativeDose = zeroDose,
            StudyId = string.Empty,
            ExposureCount = 0
        });
    }

    /// <inheritdoc />
    public Task<DoseAlertThreshold> GetAlertThresholdAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(GetAlertThresholdAsync));
        return Task.FromResult(new DoseAlertThreshold
        {
            WarningThreshold = 0m,
            ErrorThreshold = 0m,
            Unit = DoseUnit.MicroGray
        });
    }

    /// <inheritdoc />
    public Task SetAlertThresholdAsync(DoseAlertThreshold threshold, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(SetAlertThresholdAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DoseUpdate> SubscribeDoseUpdatesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(SubscribeDoseUpdatesAsync));
        yield break;
    }

    /// <inheritdoc />
    public Task ResetCumulativeDoseAsync(string studyId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IDoseService), nameof(ResetCumulativeDoseAsync));
        return Task.CompletedTask;
    }
}
