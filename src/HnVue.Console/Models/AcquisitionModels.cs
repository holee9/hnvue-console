namespace HnVue.Console.Models;

/// <summary>
/// Exposure parameters.
/// SPEC-UI-001: FR-UI-07 Exposure Parameter Display.
/// </summary>
public record ExposureParameters
{
    public required int KVp { get; init; }
    public required int MA { get; init; }
    public required int ExposureTimeMs { get; init; }
    public required int SourceImageDistanceCm { get; init; } // SID
    public required FocalSpotSize FocalSpotSize { get; init; }
    public bool IsAecMode { get; init; }
}

/// <summary>
/// Focal spot size enumeration.
/// </summary>
public enum FocalSpotSize
{
    Small,
    Large,
    Fine,
    Coarse
}

/// <summary>
/// Exposure parameter range validation.
/// </summary>
public record ExposureParameterRange
{
    public required IntRange KvpRange { get; init; }
    public required IntRange MaRange { get; init; }
    public required IntRange TimeRangeMs { get; init; }
    public required IntRange SidRangeCm { get; init; }
}

/// <summary>
/// Integer range.
/// </summary>
public record IntRange
{
    public required int Min { get; init; }
    public required int Max { get; init; }
}

/// <summary>
/// Preview frame data.
/// </summary>
public record PreviewFrame
{
    public required byte[] PixelData { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int BitsPerPixel { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Exposure trigger request.
/// </summary>
public record ExposureTriggerRequest
{
    public required string StudyId { get; init; }
    public required string ProtocolId { get; init; }
    public required ExposureParameters Parameters { get; init; }
}

/// <summary>
/// Exposure trigger result.
/// </summary>
public record ExposureTriggerResult
{
    public required bool Success { get; init; }
    public required string? ImageId { get; init; }
    public required string? ErrorMessage { get; init; }
}
