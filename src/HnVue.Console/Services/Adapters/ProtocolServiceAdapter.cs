using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IProtocolService.
/// SPEC-ADAPTER-001: Protocol selection and validation using ProtocolService gRPC.
/// @MX:NOTE Uses ProtocolService gRPC for protocol listing, validation, and application.
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
        try
        {
            var client = CreateClient<HnVue.Ipc.ProtocolService.ProtocolServiceClient>();
            var response = await client.ListProtocolsAsync(
                new HnVue.Ipc.ListProtocolsRequest
                {
                    IncludeCustom = true
                },
                cancellationToken: ct);

            // Extract unique body parts from protocols
            var bodyParts = response.Protocols
                .Select(p => p.BodyPart)
                .Distinct()
                .Where(bp => !string.IsNullOrEmpty(bp))
                .Select(bp => new BodyPart
                {
                    Code = bp,
                    DisplayName = bp,
                    DisplayNameKorean = MapBodyPartToKorean(bp)
                })
                .ToList();

            return bodyParts.AsReadOnly();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IProtocolService), nameof(GetBodyPartsAsync));
            return Array.Empty<BodyPart>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Projection>> GetProjectionsAsync(string bodyPartCode, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ProtocolService.ProtocolServiceClient>();
            var response = await client.ListProtocolsAsync(
                new HnVue.Ipc.ListProtocolsRequest
                {
                    BodyPart = bodyPartCode,
                    IncludeCustom = true
                },
                cancellationToken: ct);

            // Extract unique projections from protocols for this body part
            var projections = response.Protocols
                .Where(p => p.BodyPart == bodyPartCode)
                .Select(p => p.Projection)
                .Distinct()
                .Where(proj => !string.IsNullOrEmpty(proj))
                .Select(proj => new Projection
                {
                    Code = proj,
                    DisplayName = proj,
                    DisplayNameKorean = MapProjectionToKorean(proj)
                })
                .ToList();

            return projections.AsReadOnly();
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
            var response = await client.ListProtocolsAsync(
                new HnVue.Ipc.ListProtocolsRequest
                {
                    BodyPart = bodyPartCode,
                    Projection = projectionCode,
                    IncludeCustom = true
                },
                cancellationToken: ct);

            var protocol = response.Protocols
                .FirstOrDefault(p => p.BodyPart == bodyPartCode && p.Projection == projectionCode);

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
            var preset = await GetProtocolPresetAsync(selection.BodyPartCode, selection.ProjectionCode, ct);

            if (preset == null)
            {
                // Return default preset if not found
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

            // Validate protocol
            var response = await client.ValidateProtocolAsync(
                new HnVue.Ipc.ValidateProtocolRequest
                {
                    ProtocolId = preset.ProtocolId
                },
                cancellationToken: ct);

            return new ProtocolSelectionResult
            {
                Preset = preset,
                IsAecRecommended = preset.DefaultExposure.IsAecMode
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

    private static FocalSpotSize MapFocalSpotSize(string? protoValue)
    {
        return protoValue?.ToUpperInvariant() switch
        {
            "SMALL" or "FINE" => FocalSpotSize.Small,
            "LARGE" or "COARSE" => FocalSpotSize.Large,
            _ => FocalSpotSize.Small
        };
    }

    private static string MapBodyPartToKorean(string code)
    {
        return code?.ToUpperInvariant() switch
        {
            "CHEST" => "흉부",
            "ABDOMEN" => "복부",
            "PELVIS" => "골반",
            "SKULL" => "두부",
            "SPINE" => "척추",
            "EXTREMITY" => "사지",
            "HAND" => "손",
            "FOOT" => "발",
            _ => code ?? ""
        };
    }

    private static string MapProjectionToKorean(string code)
    {
        return code?.ToUpperInvariant() switch
        {
            "AP" => "전후방",
            "PA" => "후전방",
            "LATERAL" => "측면",
            "OBLIQUE" => "사면",
            "AXIAL" => "축방향",
            _ => code ?? ""
        };
    }
}
