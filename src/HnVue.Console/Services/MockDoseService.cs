using System.Diagnostics;
using System.Runtime.CompilerServices;
using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock dose service for development.
/// SPEC-UI-001: Mock service for dose tracking.
/// </summary>
public class MockDoseService : IDoseService
{
    private DoseValue _cumulativeDose = new()
    {
        Value = 0.5m,
        Unit = DoseUnit.MilliGraySquareCm,
        MeasuredAt = DateTime.UtcNow
    };

    private DoseAlertThreshold _threshold = new()
    {
        WarningThreshold = 2.0m,
        ErrorThreshold = 5.0m,
        Unit = DoseUnit.MilliGraySquareCm
    };

    /// <inheritdoc/>
    public Task<DoseDisplay> GetCurrentDoseDisplayAsync(CancellationToken ct)
    {
        var display = new DoseDisplay
        {
            CurrentDose = new DoseValue
            {
                Value = 0.1m,
                Unit = DoseUnit.MilliGraySquareCm,
                MeasuredAt = DateTime.UtcNow
            },
            CumulativeDose = _cumulativeDose,
            StudyId = "MOCK_STUDY_001",
            ExposureCount = 0
        };

        Debug.WriteLine($"[MockDoseService] Getting dose display: Cumulative={display.CumulativeDose.Value}");
        return Task.FromResult(display);
    }

    /// <inheritdoc/>
    public Task<DoseAlertThreshold> GetAlertThresholdAsync(CancellationToken ct)
    {
        Debug.WriteLine($"[MockDoseService] Getting alert thresholds: Warning={_threshold.WarningThreshold}");
        return Task.FromResult(_threshold);
    }

    /// <inheritdoc/>
    public Task SetAlertThresholdAsync(DoseAlertThreshold threshold, CancellationToken ct)
    {
        _threshold = threshold;
        Debug.WriteLine($"[MockDoseService] Setting alert thresholds: Warning={threshold.WarningThreshold}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DoseUpdate> SubscribeDoseUpdatesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Debug.WriteLine("[MockDoseService] Starting dose update subscription");

        // Simulate periodic dose updates
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(5000, ct); // Update every 5 seconds

            // Simulate small dose increment
            _cumulativeDose = _cumulativeDose with
            {
                Value = _cumulativeDose.Value + 0.01m,
                MeasuredAt = DateTime.UtcNow
            };

            var update = new DoseUpdate
            {
                NewDose = new DoseValue
                {
                    Value = 0.01m,
                    Unit = DoseUnit.MilliGraySquareCm,
                    MeasuredAt = DateTime.UtcNow
                },
                CumulativeDose = _cumulativeDose,
                IsWarningThresholdExceeded = _cumulativeDose.Value > 2.0m,
                IsErrorThresholdExceeded = _cumulativeDose.Value > 5.0m
            };

            Debug.WriteLine($"[MockDoseService] Dose update: Cumulative={update.CumulativeDose.Value} mGy·cm²");
            yield return update;
        }
    }

    /// <inheritdoc/>
    public Task ResetCumulativeDoseAsync(string studyId, CancellationToken ct)
    {
        _cumulativeDose = _cumulativeDose with
        {
            Value = 0m,
            Unit = DoseUnit.MilliGraySquareCm,
            MeasuredAt = DateTime.UtcNow
        };

        Debug.WriteLine($"[MockDoseService] Resetting cumulative dose for study: {studyId}");
        return Task.CompletedTask;
    }
}
