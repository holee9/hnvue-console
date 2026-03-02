using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IExposureService.
/// TriggerExposureAsync and CancelExposureAsync use CommandService.
/// Other methods have no proto yet; return graceful defaults.
/// </summary>
public sealed class ExposureServiceAdapter : GrpcAdapterBase, IExposureService
{
    private readonly ILogger<ExposureServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ExposureServiceAdapter"/>.
    /// </summary>
    public ExposureServiceAdapter(IConfiguration configuration, ILogger<ExposureServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PreviewFrame> SubscribePreviewFramesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IExposureService), nameof(SubscribePreviewFramesAsync));
        yield break;
    }

    /// <inheritdoc />
    public Task<ExposureParameterRange> GetExposureRangesAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IExposureService), nameof(GetExposureRangesAsync));
        return Task.FromResult(new ExposureParameterRange
        {
            KvpRange = new IntRange { Min = 40, Max = 150 },
            MaRange = new IntRange { Min = 1, Max = 500 },
            TimeRangeMs = new IntRange { Min = 1, Max = 5000 },
            SidRangeCm = new IntRange { Min = 50, Max = 200 }
        });
    }

    /// <inheritdoc />
    public Task<ExposureParameters> GetExposureParametersAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IExposureService), nameof(GetExposureParametersAsync));
        return Task.FromResult(new ExposureParameters
        {
            KVp = 70,
            MA = 100,
            ExposureTimeMs = 100,
            SourceImageDistanceCm = 100,
            FocalSpotSize = FocalSpotSize.Small,
            IsAecMode = false
        });
    }

    /// <inheritdoc />
    public Task SetExposureParametersAsync(ExposureParameters parameters, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IExposureService), nameof(SetExposureParametersAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ExposureTriggerResult> TriggerExposureAsync(ExposureTriggerRequest request, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.CommandService.CommandServiceClient>();
            var grpcRequest = new HnVue.Ipc.StartExposureRequest
            {
                Parameters = new HnVue.Ipc.ExposureParameters
                {
                    Kv = request.Parameters.KVp,
                    Mas = request.Parameters.MA
                }
            };
            var response = await client.StartExposureAsync(grpcRequest, cancellationToken: ct);
            return new ExposureTriggerResult
            {
                Success = response.Success,
                ImageId = response.AcquisitionId.ToString(),
                ErrorMessage = null
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IExposureService), nameof(TriggerExposureAsync));
            return new ExposureTriggerResult
            {
                Success = false,
                ImageId = null,
                ErrorMessage = ex.Status.Detail
            };
        }
    }

    /// <inheritdoc />
    public async Task CancelExposureAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.CommandService.CommandServiceClient>();
            var grpcRequest = new HnVue.Ipc.AbortExposureRequest { AcquisitionId = 0 };
            await client.AbortExposureAsync(grpcRequest, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IExposureService), nameof(CancelExposureAsync));
        }
    }
}
