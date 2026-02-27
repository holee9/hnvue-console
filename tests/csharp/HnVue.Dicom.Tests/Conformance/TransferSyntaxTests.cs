using Dicom;
using Dicom.Imaging.Codec;
using FluentAssertions;
using HnVue.Dicom.Storage;
using Xunit;

namespace HnVue.Dicom.Tests.Conformance;

/// <summary>
/// Tests to verify Transfer Syntax negotiation and conformance.
/// SPEC-DICOM-001 FR-DICOM-02: Transfer Syntax Negotiation.
/// </summary>
public class TransferSyntaxTests
{
    // Transfer Syntax UIDs per SPEC-DICOM-001 Section 1.5
    private const string Jpeg2000LosslessUid = "1.2.840.10008.1.2.4.90";
    private const string JpegLosslessFopUid = "1.2.840.10008.1.2.4.70";
    private const string ExplicitVRLittleEndianUid = "1.2.840.10008.1.2.1";
    private const string ImplicitVRLittleEndianUid = "1.2.840.10008.1.2";
    private const string JpegBaselineUid = "1.2.840.10008.1.2.4.50";

    [Fact]
    public void StorageScu_ProposesCorrectTransferSyntaxes()
    {
        // Act - Use reflection to access private static field for testing
        var proposedSyntaxesField = typeof(StorageScu)
            .GetField("ProposedTransferSyntaxes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        proposedSyntaxesField.Should().NotBeNull();

        var proposedSyntaxes = proposedSyntaxesField!.GetValue(null) as DicomTransferSyntax[];
        proposedSyntaxes.Should().NotBeNull();

        // Assign to non-nullable variable to satisfy compiler
        var syntaxes = proposedSyntaxes!;

        // Assert - Priority order per FR-DICOM-02
        syntaxes.Should().HaveCount(4,
            "Storage SCU should propose exactly 4 transfer syntaxes");

        syntaxes[0].Should().Be(DicomTransferSyntax.JPEG2000Lossless,
            "First priority should be JPEG 2000 Lossless");
        syntaxes[1].Should().Be(DicomTransferSyntax.JPEGProcess14SV1,
            "Second priority should be JPEG Lossless (FOP)");
        syntaxes[2].Should().Be(DicomTransferSyntax.ExplicitVRLittleEndian,
            "Third priority should be Explicit VR Little Endian");
        syntaxes[3].Should().Be(DicomTransferSyntax.ImplicitVRLittleEndian,
            "Fourth priority (fallback) should be Implicit VR Little Endian");
    }

    [Fact]
    public void TransferSyntaxes_AreLosslessForDiagnosticImages()
    {
        // Act - Use static properties directly (fo-dicom 4.x/5.x API)
        var jpeg2000 = DicomTransferSyntax.JPEG2000Lossless;
        var jpegLossless = DicomTransferSyntax.JPEGProcess14SV1;
        var explicitVr = DicomTransferSyntax.ExplicitVRLittleEndian;
        var implicitVr = DicomTransferSyntax.ImplicitVRLittleEndian;

        // Assert - Lossy syntax should NOT be in proposed list
        jpeg2000.IsLossy.Should().BeFalse(
            "JPEG 2000 Lossless must be lossless");
        jpegLossless.IsLossy.Should().BeFalse(
            "JPEG Lossless (FOP) must be lossless");
        explicitVr.IsLossy.Should().BeFalse(
            "Explicit VR Little Endian must be lossless (uncompressed)");
        implicitVr.IsLossy.Should().BeFalse(
            "Implicit VR Little Endian must be lossless (uncompressed)");
    }

    [Fact]
    public void ImplicitVRLittleEndian_IsMandatoryFallback()
    {
        // Act
        var implicitVr = DicomTransferSyntax.ImplicitVRLittleEndian;

        // Assert - Implicit VR Little Endian must always be supported (DICOM mandatory)
        implicitVr.Should().NotBeNull(
            "Implicit VR Little Endian must be available as mandatory fallback");
        implicitVr!.Should().Be(DicomTransferSyntax.ImplicitVRLittleEndian);
    }

    [Fact]
    public void AllSupportedTransferSyntaxes_AreWellKnown()
    {
        // Arrange
        var supportedUids = new[]
        {
            Jpeg2000LosslessUid,
            JpegLosslessFopUid,
            ExplicitVRLittleEndianUid,
            ImplicitVRLittleEndianUid
        };

        // Act & Assert - Verify all supported transfer syntaxes are well-known
        // Use static properties for the four proposed syntaxes
        var syntaxes = new[]
        {
            DicomTransferSyntax.JPEG2000Lossless,
            DicomTransferSyntax.JPEGProcess14SV1,
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.ImplicitVRLittleEndian
        };

        foreach (var syntax in syntaxes)
        {
            syntax.Should().NotBeNull("Transfer syntax must be available");
            syntax!.IsRetired.Should().BeFalse("Proposed transfer syntaxes should not be retired");
        }
    }

    [Fact]
    public void Jpeg2000Lossless_HasCorrectProperties()
    {
        // Act
        var syntax = DicomTransferSyntax.JPEG2000Lossless;

        // Assert
        syntax.IsLossy.Should().BeFalse("JPEG 2000 Lossless must not be lossy");
        syntax.IsEncapsulated.Should().BeTrue("JPEG 2000 uses encapsulated encoding");
        syntax.UID.UID.Should().Be(Jpeg2000LosslessUid);
    }

    [Fact]
    public void JpegLosslessFop_HasCorrectProperties()
    {
        // Act
        var syntax = DicomTransferSyntax.JPEGProcess14SV1;

        // Assert
        syntax.IsLossy.Should().BeFalse("JPEG Lossless (FOP) must not be lossy");
        syntax.IsEncapsulated.Should().BeTrue("JPEG Lossless uses encapsulated encoding");
        syntax.UID.UID.Should().Be(JpegLosslessFopUid);
    }

    [Fact]
    public void ExplicitVRLittleEndian_HasCorrectProperties()
    {
        // Act
        var syntax = DicomTransferSyntax.ExplicitVRLittleEndian;

        // Assert
        syntax.IsLossy.Should().BeFalse("Explicit VR LE is uncompressed, therefore lossless");
        syntax.IsEncapsulated.Should().BeFalse("Explicit VR LE is not encapsulated");
        syntax.IsExplicitVR.Should().BeTrue("Explicit VR LE uses explicit VR encoding");
        syntax.UID.UID.Should().Be(ExplicitVRLittleEndianUid);
    }

    [Fact]
    public void ImplicitVRLittleEndian_HasCorrectProperties()
    {
        // Act
        var syntax = DicomTransferSyntax.ImplicitVRLittleEndian;

        // Assert
        syntax.IsLossy.Should().BeFalse("Implicit VR LE is uncompressed, therefore lossless");
        syntax.IsEncapsulated.Should().BeFalse("Implicit VR LE is not encapsulated");
        syntax.IsExplicitVR.Should().BeFalse("Implicit VR LE uses implicit VR encoding");
        syntax.UID.UID.Should().Be(ImplicitVRLittleEndianUid);
    }

    [Fact]
    public void JpegBaseline_IsLossy()
    {
        // Act - JPEGBaseline static property doesn't exist in fo-dicom
        // Use DicomUID.Parse with Lookup instead
        var jpegBaselineUidObj = DicomUID.Parse(JpegBaselineUid);
        var syntax = DicomTransferSyntax.Lookup(jpegBaselineUidObj);

        // Assert
        syntax.Should().NotBeNull("JPEG Baseline should be a known transfer syntax");
        syntax!.IsLossy.Should().BeTrue("JPEG Baseline (Process 1) is lossy");
        syntax.UID.UID.Should().Be(JpegBaselineUid);
    }

    [Fact]
    public void LossySyntax_IsNotProposedForDiagnosticImages()
    {
        // Arrange - JPEG Baseline is lossy and should NOT be proposed for diagnostic DX/CR
        var proposedSyntaxesField = typeof(StorageScu)
            .GetField("ProposedTransferSyntaxes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var proposedSyntaxes = proposedSyntaxesField?.GetValue(null) as DicomTransferSyntax[];

        // Assert
        proposedSyntaxes.Should().NotBeNull();
        proposedSyntaxes!.Select(s => s.UID.UID).Should().NotContain(JpegBaselineUid,
            "Lossy JPEG Baseline should NOT be proposed for diagnostic images per FR-DICOM-02");
    }

    [Fact]
    public void TranscodeInMemory_CanConvertToImplicitVr()
    {
        // Arrange - Create minimal dataset in Explicit VR
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.DigitalXRayImageStorageForPresentation },
            { DicomTag.SOPInstanceUID, "1.2.3.4.5.999" },
            { DicomTag.StudyInstanceUID, "1.2.3.4.5.10" },
            { DicomTag.SeriesInstanceUID, "1.2.3.4.5.11" },
            { DicomTag.Modality, "DX" },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)1 },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)16 },
            { DicomTag.HighBit, (ushort)15 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            new DicomOtherWord(DicomTag.PixelData, new ushort[] { 0 })
        };

        // Force to Explicit VR
        dataset.AddOrUpdate(DicomTag.TransferSyntaxUID, DicomTransferSyntax.ExplicitVRLittleEndian.UID);
        var file = new DicomFile(dataset);

        // Act - Transcode to Implicit VR
        var transcoded = StorageScu.TranscodeInMemory(file, DicomTransferSyntax.ImplicitVRLittleEndian);

        // Assert
        transcoded.Should().NotBeNull();
        transcoded.Dataset.InternalTransferSyntax.UID.UID.Should().Be(ImplicitVRLittleEndianUid,
            "Transcoded file must be in Implicit VR Little Endian");
    }

    [Fact]
    public void TranscodeInMemory_PreservesPixelData()
    {
        // Arrange - Create dataset with known pixel data
        var expectedPixelData = new ushort[] { 1234, 5678, 9012 };
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.DigitalXRayImageStorageForPresentation },
            { DicomTag.SOPInstanceUID, "1.2.3.4.5.888" },
            { DicomTag.StudyInstanceUID, "1.2.3.4.5.10" },
            { DicomTag.SeriesInstanceUID, "1.2.3.4.5.11" },
            { DicomTag.Modality, "DX" },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)3 },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)16 },
            { DicomTag.HighBit, (ushort)15 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            new DicomOtherWord(DicomTag.PixelData, expectedPixelData)
        };

        var file = new DicomFile(dataset);

        // Act - Transcode to different syntax
        var transcoded = StorageScu.TranscodeInMemory(file, DicomTransferSyntax.ImplicitVRLittleEndian);

        // Assert - Pixel data should be preserved
        var pixelData = transcoded.Dataset.GetValues<ushort>(DicomTag.PixelData);
        pixelData.Should().BeEquivalentTo(expectedPixelData,
            "Transcoding must preserve pixel data values");
    }

    [Fact]
    public void AllTransferSyntaxes_HaveValidUids()
    {
        // Arrange - DICOM UID format validation
        var uidPattern = new System.Text.RegularExpressions.Regex(@"^\d+(\.\d+)*$");

        var allUids = new[]
        {
            Jpeg2000LosslessUid,
            JpegLosslessFopUid,
            ExplicitVRLittleEndianUid,
            ImplicitVRLittleEndianUid,
            JpegBaselineUid
        };

        // Act & Assert
        foreach (var uid in allUids)
        {
            uidPattern.IsMatch(uid).Should().BeTrue(
                "Transfer Syntax UID '{0}' must follow DICOM format (dot-separated numeric components)", uid);
        }
    }
}
