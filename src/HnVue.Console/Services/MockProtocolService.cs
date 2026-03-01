using System.Diagnostics;
using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock protocol service for development.
/// SPEC-UI-001: Mock service for protocol management.
/// </summary>
public class MockProtocolService : IProtocolService
{
    private readonly List<BodyPart> _bodyParts = new()
    {
        new() { Code = "CHEST", DisplayName = "Chest", DisplayNameKorean = "흉부" },
        new() { Code = "ABDOMEN", DisplayName = "Abdomen", DisplayNameKorean = "복부" },
        new() { Code = "EXTREMITY", DisplayName = "Extremity", DisplayNameKorean = "사지" },
        new() { Code = "SPINE", DisplayName = "Spine", DisplayNameKorean = "척추" },
        new() { Code = "SKULL", DisplayName = "Skull", DisplayNameKorean = "두개" }
    };

    private readonly Dictionary<string, List<Projection>> _projections = new()
    {
        ["CHEST"] = new List<Projection>
        {
            new() { Code = "PA", DisplayName = "PA View", DisplayNameKorean = "후전방" },
            new() { Code = "AP", DisplayName = "AP View", DisplayNameKorean = "전후방" },
            new() { Code = "LATERAL", DisplayName = "Lateral", DisplayNameKorean = "측면" }
        },
        ["ABDOMEN"] = new List<Projection>
        {
            new() { Code = "AP", DisplayName = "AP View", DisplayNameKorean = "전후방" },
            new() { Code = "LATERAL", DisplayName = "Lateral", DisplayNameKorean = "측면" }
        },
        ["EXTREMITY"] = new List<Projection>
        {
            new() { Code = "AP", DisplayName = "AP View", DisplayNameKorean = "전후방" },
            new() { Code = "LATERAL", DisplayName = "Lateral", DisplayNameKorean = "측면" },
            new() { Code = "OBLIQUE", DisplayName = "Oblique", DisplayNameKorean = "사면" }
        },
        ["SPINE"] = new List<Projection>
        {
            new() { Code = "AP", DisplayName = "AP View", DisplayNameKorean = "전후방" },
            new() { Code = "LATERAL", DisplayName = "Lateral", DisplayNameKorean = "측면" }
        },
        ["SKULL"] = new List<Projection>
        {
            new() { Code = "AP", DisplayName = "AP View", DisplayNameKorean = "전후방" },
            new() { Code = "LATERAL", DisplayName = "Lateral", DisplayNameKorean = "측면" },
            new() { Code = "TOWNES", DisplayName = "Townes", DisplayNameKorean = "Townes" }
        }
    };

    /// <inheritdoc/>
    public Task<IReadOnlyList<BodyPart>> GetBodyPartsAsync(CancellationToken ct = default)
    {
        Debug.WriteLine($"[MockProtocolService] Getting {_bodyParts.Count} body parts");
        return Task.FromResult<IReadOnlyList<BodyPart>>(_bodyParts);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Projection>> GetProjectionsAsync(string bodyPartCode, CancellationToken ct = default)
    {
        if (_projections.TryGetValue(bodyPartCode, out var projections))
        {
            Debug.WriteLine($"[MockProtocolService] Getting {projections.Count} projections for {bodyPartCode}");
            return Task.FromResult<IReadOnlyList<Projection>>(projections);
        }

        Debug.WriteLine($"[MockProtocolService] No projections found for {bodyPartCode}");
        return Task.FromResult<IReadOnlyList<Projection>>(new List<Projection>());
    }

    /// <inheritdoc/>
    public Task<ProtocolPreset?> GetProtocolPresetAsync(string bodyPartCode, string projectionCode, CancellationToken ct = default)
    {
        var preset = new ProtocolPreset
        {
            ProtocolId = $"{bodyPartCode}_{projectionCode}",
            BodyPartCode = bodyPartCode,
            ProjectionCode = projectionCode,
            DefaultExposure = new ExposureParameters
            {
                KVp = 120,
                MA = 100,
                ExposureTimeMs = 100,
                SourceImageDistanceCm = 100,
                FocalSpotSize = FocalSpotSize.Large,
                IsAecMode = false
            }
        };

        Debug.WriteLine($"[MockProtocolService] Getting preset for {bodyPartCode}/{projectionCode}");
        return Task.FromResult<ProtocolPreset?>(preset);
    }

    /// <inheritdoc/>
    public Task<ProtocolSelectionResult> SelectProtocolAsync(ProtocolSelection selection, CancellationToken ct = default)
    {
        var preset = GetProtocolPresetAsync(selection.BodyPartCode, selection.ProjectionCode, ct).Result;
        if (preset == null)
        {
            throw new InvalidOperationException($"Protocol preset not found for {selection.BodyPartCode}/{selection.ProjectionCode}");
        }

        Debug.WriteLine($"[MockProtocolService] Protocol selected: {preset.ProtocolId}");
        return Task.FromResult(new ProtocolSelectionResult
        {
            Preset = preset,
            IsAecRecommended = false
        });
    }
}
