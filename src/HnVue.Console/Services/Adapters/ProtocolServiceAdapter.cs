using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IProtocolService.
/// No gRPC proto defined yet; returns graceful defaults.
/// </summary>
public sealed class ProtocolServiceAdapter : GrpcAdapterBase, IProtocolService
{
    private readonly ILogger<ProtocolServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ProtocolServiceAdapter"/>.
    /// </summary>
    public ProtocolServiceAdapter(IConfiguration configuration, ILogger<ProtocolServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BodyPart>> GetBodyPartsAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IProtocolService), nameof(GetBodyPartsAsync));
        return Task.FromResult<IReadOnlyList<BodyPart>>(Array.Empty<BodyPart>());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Projection>> GetProjectionsAsync(string bodyPartCode, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IProtocolService), nameof(GetProjectionsAsync));
        return Task.FromResult<IReadOnlyList<Projection>>(Array.Empty<Projection>());
    }

    /// <inheritdoc />
    public Task<ProtocolPreset?> GetProtocolPresetAsync(string bodyPartCode, string projectionCode, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IProtocolService), nameof(GetProtocolPresetAsync));
        return Task.FromResult<ProtocolPreset?>(null);
    }

    /// <inheritdoc />
    public Task<ProtocolSelectionResult> SelectProtocolAsync(ProtocolSelection selection, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IProtocolService), nameof(SelectProtocolAsync));
        return Task.FromResult(new ProtocolSelectionResult
        {
            Preset = new ProtocolPreset
            {
                ProtocolId = string.Empty,
                BodyPartCode = selection.BodyPartCode,
                ProjectionCode = selection.ProjectionCode,
                DefaultExposure = new ExposureParameters
                {
                    KVp = 70,
                    MA = 100,
                    ExposureTimeMs = 100,
                    SourceImageDistanceCm = 100,
                    FocalSpotSize = FocalSpotSize.Small
                }
            },
            IsAecRecommended = false
        });
    }
}
