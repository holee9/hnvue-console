using Dicom;
using FluentAssertions;
using HnVue.Dicom.Iod;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Dicom.Tests.Iod;

/// <summary>
/// Unit tests for CrImageBuilder - Computed Radiography IOD construction.
/// SPEC-DICOM-001 AC-06 (task assignment): CR IOD conformance.
/// </summary>
public class CrImageBuilderTests
{
    private readonly CrImageBuilder _builder;

    // SOP Class UID for Computed Radiography Image Storage
    private const string CrSopClassUid = "1.2.840.10008.5.1.4.1.1.1";

    public CrImageBuilderTests()
    {
        _builder = new CrImageBuilder(NullLogger<CrImageBuilder>.Instance);
    }

    private static DicomImageData CreateValidCrData(string? sopInstanceUid = null)
    {
        return new DicomImageData
        {
            PatientId = "P002",
            PatientName = "CR^Test^Patient",
            StudyInstanceUid = "1.2.3.4.5.300",
            SeriesInstanceUid = "1.2.3.4.5.301",
            SopInstanceUid = sopInstanceUid ?? "1.2.3.4.5.302",
            Modality = "CR",
            Rows = 2048,
            Columns = 2048,
            BitsAllocated = 16,
            BitsStored = 12,
            HighBit = 11,
            PixelRepresentation = 0,
            PhotometricInterpretation = "MONOCHROME2",
            PixelData = new byte[2048 * 2048 * 2]
        };
    }

    // Build_WithValidData_ReturnsConformantDicomFile
    [Fact]
    public void Build_WithValidData_ReturnsConformantDicomFile()
    {
        // Arrange
        var imageData = CreateValidCrData();

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Should().NotBeNull();
        dicomFile.Dataset.Should().NotBeNull();
        dicomFile.Dataset.Contains(DicomTag.SOPClassUID).Should().BeTrue();
        dicomFile.Dataset.GetString(DicomTag.SOPClassUID).Should().Be(CrSopClassUid);
    }

    // Build_VerifiesCorrectSopClassUid
    [Fact]
    public void Build_VerifiesCorrectSopClassUid()
    {
        // Arrange
        var imageData = CreateValidCrData();

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.SOPClassUID)
            .Should().Be("1.2.840.10008.5.1.4.1.1.1",
                "CR Image Storage SOP Class UID must be 1.2.840.10008.5.1.4.1.1.1");
    }

    // CR and DX must have different SOP class UIDs
    [Fact]
    public void CrSopClassUid_DifferentFrom_DxSopClassUid()
    {
        // Assert
        CrImageBuilder.CrImageStorageSopClass.UID.Should().NotBe("1.2.840.10008.5.1.4.1.1.1.1",
            "CR and DX images must have distinct SOP class UIDs");
    }

    [Fact]
    public void Build_MandatoryType1Attributes_AllPresent()
    {
        // Arrange
        var imageData = CreateValidCrData();

        // Act
        var dicomFile = _builder.Build(imageData);
        var dataset = dicomFile.Dataset;

        // Assert mandatory attributes
        dataset.Contains(DicomTag.SOPClassUID).Should().BeTrue("SOP Class UID is Type 1");
        dataset.Contains(DicomTag.SOPInstanceUID).Should().BeTrue("SOP Instance UID is Type 1");
        dataset.Contains(DicomTag.Modality).Should().BeTrue("Modality is Type 1");
        dataset.GetString(DicomTag.Modality).Should().Be("CR");
        dataset.Contains(DicomTag.StudyInstanceUID).Should().BeTrue("Study Instance UID is Type 1");
        dataset.Contains(DicomTag.SeriesInstanceUID).Should().BeTrue("Series Instance UID is Type 1");
        dataset.Contains(DicomTag.Rows).Should().BeTrue("Rows is Type 1");
        dataset.Contains(DicomTag.Columns).Should().BeTrue("Columns is Type 1");
        dataset.Contains(DicomTag.BitsAllocated).Should().BeTrue("Bits Allocated is Type 1");
        dataset.Contains(DicomTag.BitsStored).Should().BeTrue("Bits Stored is Type 1");
        dataset.Contains(DicomTag.HighBit).Should().BeTrue("High Bit is Type 1");
        dataset.Contains(DicomTag.PixelRepresentation).Should().BeTrue("Pixel Representation is Type 1");
        dataset.Contains(DicomTag.PixelData).Should().BeTrue("Pixel Data is Type 1");
    }

    [Fact]
    public void Build_WithLargePixelData_HandlesHighResolutionImages()
    {
        // Arrange: 2048x2048 CR image (typical for CR plates)
        var imageData = CreateValidCrData();

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.GetValue<ushort>(DicomTag.Rows, 0).Should().Be(2048,
            "CR typically uses large detector sizes like 2048x2048");
        dicomFile.Dataset.GetValue<ushort>(DicomTag.Columns, 0).Should().Be(2048);
    }

    [Fact]
    public void Build_WithNullImageData_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _builder.Build(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_WithEmptyPixelData_ThrowsInvalidOperationException()
    {
        // Arrange
        var imageData = CreateValidCrData() with { PixelData = Array.Empty<byte>() };

        // Act & Assert
        var act = () => _builder.Build(imageData);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pixel data*");
    }

    [Fact]
    public void Build_WithPlateType_SetsPlateType()
    {
        // Arrange
        var imageData = CreateValidCrData() with { PlateType = "FLEXIBLE" };

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.Contains(DicomTag.PlateType).Should().BeTrue();
        dicomFile.Dataset.GetString(DicomTag.PlateType).Should().Be("FLEXIBLE");
    }

    [Fact]
    public void Build_WithImageLaterality_SetsLaterality()
    {
        // Arrange
        var imageData = CreateValidCrData() with { ImageLaterality = "L" };

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.Contains(DicomTag.ImageLaterality).Should().BeTrue();
        dicomFile.Dataset.GetString(DicomTag.ImageLaterality).Should().Be("L");
    }

    [Fact]
    public void Build_BurnedInAnnotation_IsSetToNo()
    {
        // Arrange
        var imageData = CreateValidCrData();

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.BurnedInAnnotation).Should().Be("NO");
    }

    // Static SOP class UID constant verification
    [Fact]
    public void CrImageStorageSopClass_HasCorrectUid()
    {
        // Assert
        CrImageBuilder.CrImageStorageSopClass.UID.Should().Be("1.2.840.10008.5.1.4.1.1.1");
    }
}
