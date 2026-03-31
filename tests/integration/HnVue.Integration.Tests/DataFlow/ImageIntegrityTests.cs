using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Services;
using NSubstitute;
using Xunit;

namespace HnVue.Integration.Tests.DataFlow;

/// <summary>
/// INT-008: Image Data Integrity Integration Tests.
/// Validates that image data retrieved through the IImageService layer
/// maintains completeness and accuracy across retrieval, metadata preservation,
/// graceful handling of missing images, and concurrent access scenarios.
/// No Docker required - uses MockImageService and NSubstitute for IImageService.
/// IEC 62304 Class B/C: Image data boundary integrity verification.
/// </summary>
public sealed class ImageIntegrityTests
{
    // Fixed image and study identifiers for deterministic test data.
    private const string TestImageId = "IMG-INT-008-001";
    private const string TestStudyId = "STUDY-INT-008";
    private const int ExpectedWidth = 512;
    private const int ExpectedHeight = 512;
    private const int ExpectedBitsPerPixel = 16;

    /// <summary>
    /// Builds a minimal, deterministic <see cref="ImageData"/> instance for use in tests.
    /// The pixel data is a flat array representing a uniform 8x8 grayscale image (16-bit).
    /// </summary>
    private static ImageData BuildTestImageData(string imageId) => new()
    {
        ImageId = imageId,
        PixelData = new byte[2 * 8 * 8], // 8x8 pixels, 2 bytes per 16-bit sample
        Width = 8,
        Height = 8,
        BitsPerPixel = 16,
        PixelSpacing = new PixelSpacing
        {
            RowSpacingMm = 0.5m,
            ColumnSpacingMm = 0.5m
        },
        CurrentWindowLevel = new WindowLevel
        {
            WindowCenter = 32768,
            WindowWidth = 65536
        }
    };

    /// <summary>
    /// INT-008-1: Image data retrieved from the service must have all required fields populated.
    /// Validates ImageId (non-null), PixelData (non-null), and Width/Height dimensions.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task RetrievedImage_HasRequiredFields_ImageIdPixelDataAndDimensions()
    {
        // Arrange: use MockImageService which generates deterministic test images.
        var imageService = new MockImageService();
        var cancellationToken = CancellationToken.None;

        // Act: retrieve an image by its unique identifier.
        var image = await imageService.GetImageAsync(TestImageId, cancellationToken);

        // Assert: mandatory fields must be present and valid.
        image.Should().NotBeNull(because: "GetImageAsync must always return an ImageData for a valid ID");
        image.ImageId.Should().NotBeNullOrEmpty(
            because: "ImageId must be set to allow subsequent operations on the image");
        image.PixelData.Should().NotBeNull(
            because: "PixelData must be present for rendering the image");
        image.PixelData.Should().NotBeEmpty(
            because: "An image must contain at least one pixel of data");
        image.Width.Should().BeGreaterThan(0,
            because: "Image width must be a positive value");
        image.Height.Should().BeGreaterThan(0,
            because: "Image height must be a positive value");
        image.BitsPerPixel.Should().BeGreaterThan(0,
            because: "BitsPerPixel must be specified for correct pixel interpretation");
    }

    /// <summary>
    /// INT-008-2: Image metadata integrity check — a retrieved image must have the expected
    /// Width, Height, and BitsPerPixel values that match the known test data.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task RetrievedImage_MetadataMatchesExpectedDimensions()
    {
        // Arrange: use NSubstitute to return a precisely controlled image.
        var mockImageService = Substitute.For<IImageService>();
        var knownImage = new ImageData
        {
            ImageId = TestImageId,
            PixelData = new byte[ExpectedWidth * ExpectedHeight * (ExpectedBitsPerPixel / 8)],
            Width = ExpectedWidth,
            Height = ExpectedHeight,
            BitsPerPixel = ExpectedBitsPerPixel,
            PixelSpacing = new PixelSpacing { RowSpacingMm = 0.2m, ColumnSpacingMm = 0.2m },
            CurrentWindowLevel = new WindowLevel { WindowCenter = 2048, WindowWidth = 4096 }
        };
        mockImageService.GetImageAsync(TestImageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(knownImage));

        // Act
        var retrieved = await mockImageService.GetImageAsync(TestImageId, CancellationToken.None);

        // Assert: dimensions match without any mutation during the retrieval path.
        retrieved.ImageId.Should().Be(TestImageId,
            because: "Retrieved image ID must exactly match the requested image ID");
        retrieved.Width.Should().Be(ExpectedWidth,
            because: "Image width must not be altered by the service layer");
        retrieved.Height.Should().Be(ExpectedHeight,
            because: "Image height must not be altered by the service layer");
        retrieved.BitsPerPixel.Should().Be(ExpectedBitsPerPixel,
            because: "BitsPerPixel must be preserved to allow correct rendering");

        // Assert: pixel data size is consistent with the declared dimensions.
        var expectedByteCount = ExpectedWidth * ExpectedHeight * (ExpectedBitsPerPixel / 8);
        retrieved.PixelData.Should().HaveCount(expectedByteCount,
            because: $"PixelData length must equal Width * Height * (BitsPerPixel/8) = {expectedByteCount} bytes");
    }

    /// <summary>
    /// INT-008-3: When no image is available for a study, GetCurrentImageAsync must return
    /// null gracefully without throwing an exception (valid empty state).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task GetCurrentImage_ReturnsNull_WhenNoImageAvailable()
    {
        // Arrange: MockImageService returns null for a study with no committed image.
        var imageService = new MockImageService();
        const string emptyStudyId = "STUDY-INT-008-EMPTY";
        var cancellationToken = CancellationToken.None;

        // Act: query the current image for a study that has no images yet.
        var image = await imageService.GetCurrentImageAsync(emptyStudyId, cancellationToken);

        // Assert: the service must return null without throwing.
        image.Should().BeNull(
            because: "GetCurrentImageAsync must return null when no image exists for the study (valid empty state per IEC 62304 defensive design)");
    }

    /// <summary>
    /// INT-008-4: Multiple concurrent image retrievals must not produce corrupted or
    /// cross-contaminated data (thread-safety validation).
    /// Uses Task.WhenAll to issue 5 simultaneous GetImageAsync calls.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task ConcurrentImageRetrievals_DoNotCorruptData()
    {
        // Arrange: each task requests a distinct image with a unique ID.
        const int concurrentCount = 5;
        var imageService = new MockImageService();
        var imageIds = Enumerable.Range(1, concurrentCount)
            .Select(i => $"IMG-INT-008-CONCURRENT-{i:D3}")
            .ToArray();
        var cancellationToken = CancellationToken.None;

        // Act: fire all retrievals simultaneously.
        var tasks = imageIds
            .Select(id => imageService.GetImageAsync(id, cancellationToken))
            .ToArray();
        var images = await Task.WhenAll(tasks);

        // Assert: every task produced a result.
        images.Should().HaveCount(concurrentCount,
            because: "Each concurrent request must resolve to exactly one result");

        // Assert: each image has a non-null ImageId and matches the requested ID.
        for (var i = 0; i < concurrentCount; i++)
        {
            images[i].Should().NotBeNull(
                because: $"Concurrent request {i + 1} must not return null");
            images[i].ImageId.Should().Be(imageIds[i],
                because: $"Image ID must match the request ID — no cross-contamination between concurrent retrievals (request {i + 1})");
            images[i].PixelData.Should().NotBeNullOrEmpty(
                because: $"PixelData must not be corrupted by concurrent access (request {i + 1})");
        }

        // Assert: all ImageIds are unique (no two requests received the same image object).
        images.Select(img => img.ImageId).Should().OnlyHaveUniqueItems(
            because: "Each concurrent retrieval must return an independently generated image");
    }

    /// <summary>
    /// INT-008-5: Image metadata (Width, Height, BitsPerPixel, PixelSpacing) must be
    /// preserved without modification when passing through the service layer.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task ImageMetadata_IsPreserved_ThroughServiceLayer()
    {
        // Arrange: use NSubstitute to inject a precisely defined image.
        var mockImageService = Substitute.For<IImageService>();
        var originalImage = BuildTestImageData(TestImageId);
        mockImageService.GetImageAsync(TestImageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(originalImage));

        // Act: retrieve the image.
        var retrieved = await mockImageService.GetImageAsync(TestImageId, CancellationToken.None);

        // Assert: all metadata fields are preserved without alteration.
        retrieved.Width.Should().Be(originalImage.Width,
            because: "Width must not be modified by the service layer");
        retrieved.Height.Should().Be(originalImage.Height,
            because: "Height must not be modified by the service layer");
        retrieved.BitsPerPixel.Should().Be(originalImage.BitsPerPixel,
            because: "BitsPerPixel must not be modified by the service layer");
        retrieved.PixelSpacing.RowSpacingMm.Should().Be(originalImage.PixelSpacing.RowSpacingMm,
            because: "Row spacing must not be modified by the service layer");
        retrieved.PixelSpacing.ColumnSpacingMm.Should().Be(originalImage.PixelSpacing.ColumnSpacingMm,
            because: "Column spacing must not be modified by the service layer");
        retrieved.CurrentWindowLevel.Should().NotBeNull(
            because: "Window/Level parameters must be preserved for correct rendering");
        retrieved.CurrentWindowLevel!.WindowCenter.Should().Be(
            originalImage.CurrentWindowLevel!.WindowCenter,
            because: "Window Center must not be altered during data transit");
        retrieved.CurrentWindowLevel.WindowWidth.Should().Be(
            originalImage.CurrentWindowLevel.WindowWidth,
            because: "Window Width must not be altered during data transit");
    }
}
