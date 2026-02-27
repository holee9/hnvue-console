using Dicom;
using FluentAssertions;
using HnVue.Dicom.Iod;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Dicom.Tests.Iod;

/// <summary>
/// Unit tests for DxImageBuilder - Digital X-Ray IOD construction.
/// SPEC-DICOM-001 AC-06 (task assignment): DX IOD conformance.
/// Maps to acceptance.md AC-11 Scenario 11.1 (DVTK validation at integration level).
/// </summary>
public class DxImageBuilderTests
{
    private readonly DxImageBuilder _builder;

    // SOP Class UID for Digital X-Ray Image Storage - For Presentation
    private const string DxSopClassUid = "1.2.840.10008.5.1.4.1.1.1.1";

    public DxImageBuilderTests()
    {
        _builder = new DxImageBuilder(NullLogger<DxImageBuilder>.Instance);
    }

    private static DicomImageData CreateValidDxData(string? sopInstanceUid = null)
    {
        return new DicomImageData
        {
            PatientId = "P001",
            PatientName = "Test^Patient",
            StudyInstanceUid = "1.2.3.4.5.100",
            SeriesInstanceUid = "1.2.3.4.5.101",
            SopInstanceUid = sopInstanceUid ?? "1.2.3.4.5.102",
            Modality = "DX",
            Rows = 512,
            Columns = 512,
            BitsAllocated = 16,
            BitsStored = 12,
            HighBit = 11,
            PixelRepresentation = 0,
            PhotometricInterpretation = "MONOCHROME2",
            PixelData = new byte[512 * 512 * 2],
            PresentationIntentType = "FOR PRESENTATION"
        };
    }

    // AC-06: Build_WithValidData_ReturnsConformantDicomFile
    [Fact]
    public void Build_WithValidData_ReturnsConformantDicomFile()
    {
        // Arrange
        var imageData = CreateValidDxData();

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Should().NotBeNull();
        dicomFile.Dataset.Should().NotBeNull();
        dicomFile.Dataset.Contains(DicomTag.SOPClassUID).Should().BeTrue();
        dicomFile.Dataset.GetString(DicomTag.SOPClassUID).Should().Be(DxSopClassUid);
    }

    // AC-06: Build_VerifiesMandatoryType1Attributes
    [Fact]
    public void Build_VerifiesMandatoryType1Attributes()
    {
        // Arrange
        var imageData = CreateValidDxData();

        // Act
        var dicomFile = _builder.Build(imageData);
        var dataset = dicomFile.Dataset;

        // Assert: Verify mandatory Type 1 attributes are present and non-empty
        dataset.Contains(DicomTag.SOPClassUID).Should().BeTrue("SOP Class UID is Type 1 mandatory");
        dataset.GetString(DicomTag.SOPClassUID).Should().NotBeNullOrEmpty();

        dataset.Contains(DicomTag.SOPInstanceUID).Should().BeTrue("SOP Instance UID is Type 1 mandatory");
        dataset.GetString(DicomTag.SOPInstanceUID).Should().NotBeNullOrEmpty();

        dataset.Contains(DicomTag.Modality).Should().BeTrue("Modality is Type 1 mandatory");
        dataset.GetString(DicomTag.Modality).Should().Be("DX");

        dataset.Contains(DicomTag.StudyInstanceUID).Should().BeTrue("Study Instance UID is Type 1 mandatory");
        dataset.GetString(DicomTag.StudyInstanceUID).Should().NotBeNullOrEmpty();

        dataset.Contains(DicomTag.SeriesInstanceUID).Should().BeTrue("Series Instance UID is Type 1 mandatory");
        dataset.GetString(DicomTag.SeriesInstanceUID).Should().NotBeNullOrEmpty();

        dataset.Contains(DicomTag.Rows).Should().BeTrue("Rows is Type 1 mandatory");
        dataset.GetValue<ushort>(DicomTag.Rows, 0).Should().Be(512);

        dataset.Contains(DicomTag.Columns).Should().BeTrue("Columns is Type 1 mandatory");
        dataset.GetValue<ushort>(DicomTag.Columns, 0).Should().Be(512);

        dataset.Contains(DicomTag.BitsAllocated).Should().BeTrue("Bits Allocated is Type 1 mandatory");
        dataset.Contains(DicomTag.BitsStored).Should().BeTrue("Bits Stored is Type 1 mandatory");
        dataset.Contains(DicomTag.HighBit).Should().BeTrue("High Bit is Type 1 mandatory");
        dataset.Contains(DicomTag.PixelRepresentation).Should().BeTrue("Pixel Representation is Type 1 mandatory");
        dataset.Contains(DicomTag.PixelData).Should().BeTrue("Pixel Data is Type 1 mandatory");
    }

    // AC-06: Build_VerifiesCorrectSopClassUid
    [Fact]
    public void Build_VerifiesCorrectSopClassUid()
    {
        // Arrange
        var imageData = CreateValidDxData();

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.SOPClassUID)
            .Should().Be("1.2.840.10008.5.1.4.1.1.1.1",
                "DX Image Storage SOP Class UID must be 1.2.840.10008.5.1.4.1.1.1.1");
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
        var imageData = CreateValidDxData() with { PixelData = Array.Empty<byte>() };

        // Act & Assert
        var act = () => _builder.Build(imageData);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pixel data*");
    }

    [Fact]
    public void Build_SopInstanceUid_MatchesInputData()
    {
        // Arrange
        var sopUid = "1.2.3.4.5.999";
        var imageData = CreateValidDxData(sopUid);

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.SOPInstanceUID).Should().Be(sopUid);
    }

    [Fact]
    public void Build_WithPresentationIntentType_SetsCorrectly()
    {
        // Arrange
        var imageData = CreateValidDxData() with { PresentationIntentType = "FOR PRESENTATION" };

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.PresentationIntentType).Should().Be("FOR PRESENTATION");
    }

    [Fact]
    public void Build_ImageType_ContainsOriginalAndPrimary()
    {
        // Arrange
        var imageData = CreateValidDxData();

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        var imageType = dicomFile.Dataset.GetValues<string>(DicomTag.ImageType);
        imageType.Should().Contain("ORIGINAL");
        imageType.Should().Contain("PRIMARY");
    }

    [Fact]
    public void Build_BurnedInAnnotation_IsSetToNo()
    {
        // Arrange
        var imageData = CreateValidDxData();

        // Act
        var dicomFile = _builder.Build(imageData);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.BurnedInAnnotation).Should().Be("NO",
            "DX For Presentation must not have burned-in annotations per DICOM standard");
    }

    // Static SOP class UID constant verification
    [Fact]
    public void DxForPresentationSopClass_HasCorrectUid()
    {
        // Assert
        DxImageBuilder.DxForPresentationSopClass.UID.Should().Be("1.2.840.10008.5.1.4.1.1.1.1");
    }
}
