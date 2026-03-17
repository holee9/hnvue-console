using HnVue.Console.Models;
using HnVue.Console.Services.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.Services;

/// <summary>
/// Unit tests for DoseServiceAdapter.
/// SPEC-UI-001: FR-UI-10 Dose Display.
/// Validates stub behavior until gRPC proto is defined.
/// </summary>
public class DoseServiceAdapterTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<DoseServiceAdapter>> _mockLogger;
    private readonly DoseServiceAdapter _adapter;

    public DoseServiceAdapterTests()
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

        _mockLogger = new Mock<ILogger<DoseServiceAdapter>>();
        _adapter = new DoseServiceAdapter(_configuration, _mockLogger.Object);
    }

    public void Dispose() => _adapter.Dispose();

    [Fact]
    public async Task GetCurrentDoseDisplayAsync_ReturnsValidDoseDisplay()
    {
        var result = await _adapter.GetCurrentDoseDisplayAsync(CancellationToken.None);
        Assert.NotNull(result);
        Assert.NotNull(result.CurrentDose);
        Assert.NotNull(result.CumulativeDose);
        Assert.Equal(DoseUnit.MilliGray, result.CurrentDose.Unit);
        Assert.Equal(DoseUnit.MilliGray, result.CumulativeDose.Unit);
        Assert.Equal(0, result.CurrentDose.Value);
    }

    [Fact]
    public async Task GetCurrentDoseDisplayAsync_ReturnsZeroExposureCount()
    {
        var result = await _adapter.GetCurrentDoseDisplayAsync(CancellationToken.None);
        Assert.Equal(0, result.ExposureCount);
    }

    [Fact]
    public async Task GetAlertThresholdAsync_ReturnsValidThreshold()
    {
        var result = await _adapter.GetAlertThresholdAsync(CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.WarningThreshold > 0);
        Assert.True(result.ErrorThreshold > result.WarningThreshold);
        Assert.Equal(DoseUnit.MilliGray, result.Unit);
    }

    [Fact]
    public async Task SetAlertThresholdAsync_CompletesWithoutException()
    {
        var threshold = new DoseAlertThreshold
        {
            WarningThreshold = 15m,
            ErrorThreshold = 25m,
            Unit = DoseUnit.MilliGray
        };
        await _adapter.SetAlertThresholdAsync(threshold, CancellationToken.None);
    }

    [Fact]
    public async Task SubscribeDoseUpdatesAsync_ReturnsEmptySequence_WhenProtoUndefined()
    {
        var updates = new List<DoseUpdate>();
        await foreach (var update in _adapter.SubscribeDoseUpdatesAsync(CancellationToken.None))
        {
            updates.Add(update);
        }
        Assert.Empty(updates);
    }

    [Fact]
    public async Task ResetCumulativeDoseAsync_CompletesWithoutException()
    {
        await _adapter.ResetCumulativeDoseAsync("study-001", CancellationToken.None);
    }

    [Theory]
    [InlineData("study-001")]
    [InlineData("study-abc-xyz")]
    [InlineData("")]
    public async Task ResetCumulativeDoseAsync_WithVariousStudyIds_CompletesSuccessfully(string studyId)
    {
        await _adapter.ResetCumulativeDoseAsync(studyId, CancellationToken.None);
        // All should complete without exception
    }
}
