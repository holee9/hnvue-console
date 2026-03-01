namespace HnVue.Console.Models;

/// <summary>
/// Body part for protocol selection.
/// SPEC-UI-001: FR-UI-06 Protocol Selection.
/// </summary>
public record BodyPart
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public required string DisplayNameKorean { get; init; }
}

/// <summary>
/// Projection (view) for protocol selection.
/// </summary>
public record Projection
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public required string DisplayNameKorean { get; init; }
}

/// <summary>
/// Imaging protocol preset.
/// </summary>
public record ProtocolPreset
{
    public required string ProtocolId { get; init; }
    public required string BodyPartCode { get; init; }
    public required string ProjectionCode { get; init; }
    public required ExposureParameters DefaultExposure { get; init; }
}

/// <summary>
/// Protocol selection request.
/// </summary>
public record ProtocolSelection
{
    public required string BodyPartCode { get; init; }
    public required string ProjectionCode { get; init; }
}

/// <summary>
/// Protocol selection result.
/// </summary>
public record ProtocolSelectionResult
{
    public required ProtocolPreset Preset { get; init; }
    public required bool IsAecRecommended { get; init; }
}
