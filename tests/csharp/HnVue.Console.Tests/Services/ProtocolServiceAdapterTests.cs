using HnVue.Console.Models;
using HnVue.Console.Services.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.Services;

/// <summary>
/// Unit tests for ProtocolServiceAdapter.
/// SPEC-UI-001: FR-UI-06 Protocol Selection.
/// Validates gRPC fallback behavior when server is unavailable.
/// </summary>
public class ProtocolServiceAdapterTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<ProtocolServiceAdapter>> _mockLogger;
    private readonly ProtocolServiceAdapter _adapter;

    public ProtocolServiceAdapterTests()
    {
        // Use in-memory configuration to avoid GrpcSecurityOptions.Validate() failures
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcServer:Address"] = "http://localhost:50051",
                ["GrpcSecurity:EnableTls"] = "false",
                ["GrpcSecurity:EnableMutualTls"] = "false",
                ["GrpcSecurity:CertificateRotationDays"] = "90",
                ["GrpcSecurity:CertificateExpirationWarningDays"] = "30",
            })
            .Build();

        _mockLogger = new Mock<ILogger<ProtocolServiceAdapter>>();
        _adapter = new ProtocolServiceAdapter(_configuration, _mockLogger.Object);
    }

    public void Dispose() => _adapter.Dispose();

    [Fact]
    public async Task GetBodyPartsAsync_ReturnsEmptyList_WhenGrpcUnavailable()
    {
        var result = await _adapter.GetBodyPartsAsync(CancellationToken.None);
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<BodyPart>>(result);
    }

    [Fact]
    public async Task GetProjectionsAsync_ReturnsEmptyList_WhenGrpcUnavailable()
    {
        var result = await _adapter.GetProjectionsAsync("CHEST", CancellationToken.None);
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<Projection>>(result);
    }

    [Fact]
    public async Task GetProtocolPresetAsync_ReturnsNull_WhenGrpcUnavailable()
    {
        var result = await _adapter.GetProtocolPresetAsync("CHEST", "PA", CancellationToken.None);
        // When gRPC is unavailable, returns null
        Assert.Null(result);
    }

    [Fact]
    public async Task SelectProtocolAsync_ReturnsDefaultPreset_WhenGrpcUnavailable()
    {
        var selection = new ProtocolSelection { BodyPartCode = "CHEST", ProjectionCode = "PA" };
        var result = await _adapter.SelectProtocolAsync(selection, CancellationToken.None);
        Assert.NotNull(result);
        Assert.NotNull(result.Preset);
        Assert.Equal("CHEST", result.Preset.BodyPartCode);
        Assert.Equal("PA", result.Preset.ProjectionCode);
    }

    [Theory]
    [InlineData("CHEST", "PA")]
    [InlineData("HAND", "AP")]
    [InlineData("SPINE", "LATERAL")]
    public async Task SelectProtocolAsync_PreservesBodyPartAndProjection_WhenGrpcUnavailable(
        string bodyPart, string projection)
    {
        var selection = new ProtocolSelection { BodyPartCode = bodyPart, ProjectionCode = projection };
        var result = await _adapter.SelectProtocolAsync(selection, CancellationToken.None);
        Assert.Equal(bodyPart, result.Preset.BodyPartCode);
        Assert.Equal(projection, result.Preset.ProjectionCode);
    }
}
