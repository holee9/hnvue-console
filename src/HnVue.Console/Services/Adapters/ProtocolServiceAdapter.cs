using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IProtocolService.
/// SPEC-UI-001: FR-UI-06 Protocol Selection.
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
    public async Task<IReadOnlyList<BodyPart>> GetBodyPartsAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IProtocolService), nameof(GetBodyPartsAsync));
        await Task.CompletedTask;
        return Array.Empty<BodyPart>();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Projection>> GetProjectionsAsync(string bodyPartCode, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ProtocolService.ProtocolServiceClient>();
            var grpcRequest = new HnVue.Ipc.ListProtocolsRequest
            {
                BodyPart = bodyPartCode
            };

            var response = await client.ListProtocolsAsync(grpcRequest, cancellationToken: ct);

            // Group by projection to get unique projections
            var projections = response.Protocols
                .Select(p => new Projection
                {
                    Code = p.Projection,
                    DisplayName = p.Projection,
                    DisplayNameKorean = p.Projection
                })
                .DistinctBy(p => p.Code)
                .ToList();

            return projections;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IProtocolService), nameof(GetProjectionsAsync));
            return Array.Empty<Projection>();
        }
    }

    /// <inheritdoc />
    public async Task<ProtocolPreset?> GetProtocolPresetAsync(string bodyPartCode, string projectionCode, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ProtocolService.ProtocolServiceClient>();
            var grpcRequest = new HnVue.Ipc.ListProtocolsRequest
            {
                BodyPart = bodyPartCode,
                Projection = projectionCode
            };

            var response = await client.ListProtocolsAsync(grpcRequest, cancellationToken: ct);

            var protocol = response.Protocols.FirstOrDefault();
            if (protocol == null)
            {
                return null;
            }

            return new ProtocolPreset
            {
                ProtocolId = protocol.ProtocolId,
                BodyPartCode = protocol.BodyPart,
                ProjectionCode = protocol.Projection,
                DefaultExposure = new ExposureParameters
                {
                    KVp = (int)protocol.DefaultParameters.Kvp,
                    MA = (int)protocol.DefaultParameters.Mas,
                    ExposureTimeMs = (int)protocol.DefaultParameters.ExposureTimeMs,
                    SourceImageDistanceCm = (int)protocol.DefaultParameters.SourceImageDistanceCm,
                    FocalSpotSize = MapFocalSpotSize(protocol.DefaultParameters.FocalSpotSize),
                    IsAecMode = protocol.DefaultParameters.AecEnabled
                }
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IProtocolService), nameof(GetProtocolPresetAsync));
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProtocolSelectionResult> SelectProtocolAsync(ProtocolSelection selection, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ProtocolService.ProtocolServiceClient>();

            // First get the protocol to apply
            var listRequest = new HnVue.Ipc.ListProtocolsRequest
            {
                BodyPart = selection.BodyPartCode,
                Projection = selection.ProjectionCode
            };

            var listResponse = await client.ListProtocolsAsync(listRequest, cancellationToken: ct);
            var protocol = listResponse.Protocols.FirstOrDefault();

            if (protocol == null)
            {
                return new ProtocolSelectionResult
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
                            FocalSpotSize = FocalSpotSize.Small,
                            IsAecMode = false
                        }
                    },
                    IsAecRecommended = false
                };
            }

            return new ProtocolSelectionResult
            {
                Preset = new ProtocolPreset
                {
                    ProtocolId = protocol.ProtocolId,
                    BodyPartCode = protocol.BodyPart,
                    ProjectionCode = protocol.Projection,
                    DefaultExposure = new ExposureParameters
                    {
                        KVp = (int)protocol.DefaultParameters.Kvp,
                        MA = (int)protocol.DefaultParameters.Mas,
                        ExposureTimeMs = (int)protocol.DefaultParameters.ExposureTimeMs,
                        SourceImageDistanceCm = (int)protocol.DefaultParameters.SourceImageDistanceCm,
                        FocalSpotSize = MapFocalSpotSize(protocol.DefaultParameters.FocalSpotSize),
                        IsAecMode = protocol.DefaultParameters.AecEnabled
                    }
                },
                IsAecRecommended = protocol.DefaultParameters.AecEnabled
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IProtocolService), nameof(SelectProtocolAsync));
            return new ProtocolSelectionResult
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
                        FocalSpotSize = FocalSpotSize.Small,
                        IsAecMode = false
                    }
                },
                IsAecRecommended = false
            };
        }
    }

    private static FocalSpotSize MapFocalSpotSize(string focalSpotSize)
    {
        return focalSpotSize?.ToUpperInvariant() switch
        {
            "SMALL" => FocalSpotSize.Small,
            "LARGE" => FocalSpotSize.Large,
            "FINE" => FocalSpotSize.Fine,
            "COARSE" => FocalSpotSize.Coarse,
            _ => FocalSpotSize.Small
        };
    }
}
