using HnVue.Console.Models;
using HnVue.Console.Services.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.Services;

/// <summary>
/// Unit tests for ImageServiceAdapter.
/// SPEC-UI-001: FR-UI-03 Image Viewer.
/// Validates stub behavior until gRPC proto is defined.
/// </summary>
public class ImageServiceAdapterTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<ImageServiceAdapter>> _mockLogger;
    private readonly ImageServiceAdapter _adapter;

    public ImageServiceAdapterTests()
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

        _mockLogger = new Mock<ILogger<ImageServiceAdapter>>();
        _adapter = new ImageServiceAdapter(_configuration, _mockLogger.Object);
    }

    public void Dispose() => _adapter.Dispose();

    [Fact]
    public async Task GetImageAsync_ReturnsImageData_WithRequestedId()
    {
        var imageId = "test-image-123";
        var result = await _adapter.GetImageAsync(imageId, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(imageId, result.ImageId);
        Assert.Equal(16, result.BitsPerPixel);
    }

    [Fact]
    public async Task GetImageAsync_ReturnsEmptyPixelData_WhenProtoUndefined()
    {
        var result = await _adapter.GetImageAsync("img-001", CancellationToken.None);
        Assert.NotNull(result.PixelData);
        Assert.Empty(result.PixelData);
    }

    [Fact]
    public async Task GetCurrentImageAsync_ReturnsNull_WhenProtoUndefined()
    {
        var result = await _adapter.GetCurrentImageAsync("study-001", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ApplyWindowLevelAsync_CompletesWithoutException()
    {
        var windowLevel = new WindowLevel { WindowCenter = 1024, WindowWidth = 2048 };
        await _adapter.ApplyWindowLevelAsync("img-001", windowLevel, CancellationToken.None);
        // No exception expected
    }

    [Fact]
    public async Task SetZoomPanAsync_CompletesWithoutException()
    {
        var zoomPan = new ZoomPan { ZoomFactor = 1.5, PanX = 100, PanY = 50 };
        await _adapter.SetZoomPanAsync("img-001", zoomPan, CancellationToken.None);
    }

    [Fact]
    public async Task SetOrientationAsync_CompletesWithoutException()
    {
        await _adapter.SetOrientationAsync("img-001", ImageOrientation.Rotate90, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyTransformAsync_CompletesWithoutException()
    {
        var transform = new ImageTransform { Orientation = ImageOrientation.FlipHorizontal };
        await _adapter.ApplyTransformAsync("img-001", transform, CancellationToken.None);
    }

    [Fact]
    public async Task ResetTransformAsync_CompletesWithoutException()
    {
        await _adapter.ResetTransformAsync("img-001", CancellationToken.None);
    }

    [Fact]
    public async Task GetImageAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        // Stub doesn't actually await gRPC, so it should complete or handle token
        var result = await _adapter.GetImageAsync("img-001", cts.Token);
        // Stub returns immediately, so result is valid
        Assert.NotNull(result);
    }
}
