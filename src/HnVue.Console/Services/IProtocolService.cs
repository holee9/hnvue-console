using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Protocol service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-06 Protocol Selection.
/// </summary>
public interface IProtocolService
{
    /// <summary>
    /// Gets available body parts.
    /// </summary>
    Task<IReadOnlyList<BodyPart>> GetBodyPartsAsync(CancellationToken ct);

    /// <summary>
    /// Gets available projections for a body part.
    /// </summary>
    Task<IReadOnlyList<Projection>> GetProjectionsAsync(string bodyPartCode, CancellationToken ct);

    /// <summary>
    /// Gets protocol preset for body part and projection.
    /// </summary>
    Task<ProtocolPreset?> GetProtocolPresetAsync(string bodyPartCode, string projectionCode, CancellationToken ct);

    /// <summary>
    /// Selects a protocol for the current study.
    /// </summary>
    Task<ProtocolSelectionResult> SelectProtocolAsync(ProtocolSelection selection, CancellationToken ct);
}
