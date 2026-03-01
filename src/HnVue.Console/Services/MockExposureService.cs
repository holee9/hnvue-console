using System.Diagnostics;
using System.Runtime.CompilerServices;
using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock exposure service for development.
/// SPEC-UI-001: Mock service for exposure parameter management.
/// </summary>
public class MockExposureService : IExposureService
{
    private ExposureParameters _currentParameters = new()
    {
        KVp = 120,
        MA = 100,
        ExposureTimeMs = 100,
        SourceImageDistanceCm = 100,
        FocalSpotSize = FocalSpotSize.Large,
        IsAecMode = false
    };

    private ExposureParameterRange _ranges = new()
    {
        KvpRange = new IntRange { Min = 40, Max = 150 },
        MaRange = new IntRange { Min = 10, Max = 630 },
        TimeRangeMs = new IntRange { Min = 1, Max = 5000 },
        SidRangeCm = new IntRange { Min = 100, Max = 180 }
    };

    /// <inheritdoc/>
    public async IAsyncEnumerable<PreviewFrame> SubscribePreviewFramesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Debug.WriteLine("[MockExposureService] Starting preview frame subscription");

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(100, ct); // 10 FPS

            // Simulate a small 64x64 grayscale frame
            var frameData = new byte[64 * 64];
            for (int i = 0; i < frameData.Length; i++)
            {
                frameData[i] = (byte)(i % 256); // Simple pattern
            }

            yield return new PreviewFrame
            {
                PixelData = frameData,
                Width = 64,
                Height = 64,
                BitsPerPixel = 8,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <inheritdoc/>
    public Task<ExposureParameterRange> GetExposureRangesAsync(CancellationToken ct)
    {
        Debug.WriteLine($"[MockExposureService] Getting exposure ranges");
        return Task.FromResult(_ranges);
    }

    /// <inheritdoc/>
    public Task<ExposureParameters> GetExposureParametersAsync(CancellationToken ct)
    {
        Debug.WriteLine($"[MockExposureService] Getting exposure parameters: kVp={_currentParameters.KVp}");
        return Task.FromResult(_currentParameters);
    }

    /// <inheritdoc/>
    public Task SetExposureParametersAsync(ExposureParameters parameters, CancellationToken ct)
    {
        _currentParameters = parameters;
        Debug.WriteLine($"[MockExposureService] Setting exposure parameters: kVp={parameters.KVp}, mA={parameters.MA}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ExposureTriggerResult> TriggerExposureAsync(ExposureTriggerRequest request, CancellationToken ct)
    {
        Debug.WriteLine($"[MockExposureService] Triggering exposure for study: {request.StudyId}");
        return Task.FromResult(new ExposureTriggerResult
        {
            Success = true,
            ImageId = $"IMG_{DateTime.UtcNow:yyyyMMddHHmmss}",
            ErrorMessage = null
        });
    }

    /// <inheritdoc/>
    public Task CancelExposureAsync(CancellationToken ct)
    {
        Debug.WriteLine("[MockExposureService] Canceling exposure");
        return Task.CompletedTask;
    }
}
