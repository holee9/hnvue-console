using Dicom;
using FluentAssertions;
using HnVue.Dicom.Iod;
using Xunit;

namespace HnVue.Dicom.Tests.Conformance;

/// <summary>
/// Tests to verify Character Set support per DICOM Conformance Statement.
/// SPEC-DICOM-001 Section 6: Support of Character Sets.
/// </summary>
public class CharacterSetTests
{
    // DICOM Character Set values per SPEC-DICOM-001 Section 6.1
    private const string Iso8859_1 = "ISO_IR 100";  // Latin-1
    private const string IsoIr6 = "ISO_IR 6";        // ASCII (default)
    private const string IsoIr192 = "ISO_IR 192";    // UTF-8 (extended)

    [Fact]
    public void DefaultCharacterSet_IsIsoIr6()
    {
        // Arrange - ISO_IR 6 (ASCII) is the default character repertoire
        // This is the minimum required character set per DICOM standard

        // Act - Create a DICOM dataset
        var dataset = new DicomDataset();

        // Assert - By default, fo-dicom uses ISO_IR 6
        dataset.TryGetSingleValue<string>(DicomTag.SpecificCharacterSet, out var charset).Should().BeFalse(
            "Default character set should be empty (implies ISO_IR 6 per DICOM standard)");
    }

    [Fact]
    public void DxImageBuilder_UsesIsoIr6AsDefault()
    {
        // Arrange
        var imageData = new DicomImageData
        {
            SopInstanceUid = "1.2.3.4.5.100",
            StudyInstanceUid = "1.2.3.4.5.10",
            SeriesInstanceUid = "1.2.3.4.5.11",
            PatientName = "Test^Patient",
            PatientId = "PAT123",
            PatientBirthDate = new DateOnly(1990, 1, 1),
            Modality = "DX",
            Rows = 100,
            Columns = 100,
            PixelData = new byte[200]  // 100x100x2 bytes
        };

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DxImageBuilder>.Instance;
        var builder = new DxImageBuilder(logger);

        // Act
        var result = builder.Build(imageData);

        // Assert - DX IOD should use ISO_IR 6 as default character set
        result.Dataset.Contains(DicomTag.SpecificCharacterSet).Should().BeTrue(
            "Specific Character Set (0008,0005) must be present");
        result.Dataset.GetSingleValue<string>(DicomTag.SpecificCharacterSet).Should().Be(IsoIr6,
            "DX IOD should use ISO_IR 6 (ASCII) as default character set");
    }

    [Fact]
    public void CrImageBuilder_UsesIsoIr6AsDefault()
    {
        // Arrange
        var imageData = new DicomImageData
        {
            SopInstanceUid = "1.2.3.4.5.101",
            StudyInstanceUid = "1.2.3.4.5.10",
            SeriesInstanceUid = "1.2.3.4.5.11",
            PatientName = "Test^Patient",
            PatientId = "PAT124",
            PatientBirthDate = new DateOnly(1990, 1, 1),
            Modality = "CR",
            Rows = 100,
            Columns = 100,
            PixelData = new byte[200]
        };

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<CrImageBuilder>.Instance;
        var builder = new CrImageBuilder(logger);

        // Act
        var result = builder.Build(imageData);

        // Assert - CR IOD should use ISO_IR 6 as default character set
        result.Dataset.Contains(DicomTag.SpecificCharacterSet).Should().BeTrue(
            "Specific Character Set (0008,0005) must be present");
        result.Dataset.GetSingleValue<string>(DicomTag.SpecificCharacterSet).Should().Be(IsoIr6,
            "CR IOD should use ISO_IR 6 (ASCII) as default character set");
    }

    [Fact]
    public void CanSetUtf8CharacterSet()
    {
        // Arrange
        var dataset = new DicomDataset();

        // Act - Set UTF-8 character set
        dataset.AddOrUpdate(DicomTag.SpecificCharacterSet, IsoIr192);

        // Assert
        dataset.GetSingleValue<string>(DicomTag.SpecificCharacterSet).Should().Be(IsoIr192,
            "Should be able to set ISO_IR 192 (UTF-8) for extended character support");
    }

    [Fact]
    public void CanSetIso8859_1CharacterSet()
    {
        // Arrange
        var dataset = new DicomDataset();

        // Act - Set Latin-1 character set
        dataset.AddOrUpdate(DicomTag.SpecificCharacterSet, Iso8859_1);

        // Assert
        dataset.GetSingleValue<string>(DicomTag.SpecificCharacterSet).Should().Be(Iso8859_1,
            "Should be able to set ISO_IR 100 (Latin-1) for extended Latin character support");
    }

    [Fact]
    public void PatientName_SupportsAsciiCharacters()
    {
        // Arrange - DICOM Person Name format: Family^Given
        var asciiName = "Doe^John";
        var dataset = new DicomDataset();

        // Act
        dataset.AddOrUpdate(DicomTag.PatientName, asciiName);

        // Assert
        dataset.GetSingleValue<string>(DicomTag.PatientName).Should().Be(asciiName,
            "Patient Name should support ASCII characters with default character set");
    }

    [Fact]
    public void SpecificCharacterSet_TagExists()
    {
        // Arrange
        var tag = DicomTag.SpecificCharacterSet;

        // Assert - Verify tag is correctly defined
        tag.Group.Should().Be((ushort)0x0008);
        tag.Element.Should().Be((ushort)0x0005);
    }

    [Fact]
    public void CharacterSet_IsType1c()
    {
        // Arrange - Specific Character Set (0008,0005) is Type 1C
        // Type 1C: Required if extended characters are used, otherwise not required

        // DICOM datasets without extended characters don't require the tag
        var dataset = new DicomDataset();

        // Assert - Tag can be absent (Type 1C condition not met)
        dataset.Contains(DicomTag.SpecificCharacterSet).Should().BeFalse(
            "Specific Character Set is Type 1C - optional when no extended characters present");
    }

    [Fact]
    public void ConformanceStatement_DocumentsIso8859_1()
    {
        // Arrange
        var content = File.ReadAllText(
            "../../../../../../../src/HnVue.Dicom/Conformance/DicomConformanceStatement.md");

        // Assert - Per SPEC-DICOM-001 Section 6.1
        content.Should().Contain("ISO 8859-1",
            "Conformance Statement must document ISO 8859-1 (Latin-1) character repertoire");
    }

    [Fact]
    public void ConformanceStatement_DocumentsUtf8Support()
    {
        // Arrange
        var content = File.ReadAllText(
            "../../../../../../../src/HnVue.Dicom/Conformance/DicomConformanceStatement.md");

        // Assert - Per SPEC-DICOM-001 Section 6.2
        content.Should().Contain("UTF-8",
            "Conformance Statement must document UTF-8 (ISO_IR 192) extended character support");
        content.Should().Contain("ISO_IR 192",
            "Conformance Statement must reference ISO_IR 192 for UTF-8 encoding");
    }

    [Theory]
    [InlineData("", "ASCII")]
    [InlineData("ISO_IR 6", "ASCII")]
    [InlineData("ISO_IR 100", "Latin-1")]
    [InlineData("ISO_IR 192", "UTF-8")]
    public void RecognizedCharacterSets_AreValid(string value, string description)
    {
        // Arrange & Act
        var dataset = new DicomDataset();
        if (!string.IsNullOrEmpty(value))
        {
            dataset.AddOrUpdate(DicomTag.SpecificCharacterSet, value);
        }

        // Assert - These character sets should be valid DICOM values
        if (!string.IsNullOrEmpty(value))
        {
            dataset.GetSingleValue<string>(DicomTag.SpecificCharacterSet).Should().Be(value,
                $"Should support {description} character set");
        }
    }

    [Fact]
    public void CanUseMultipleCharacterSets()
    {
        // Arrange - DICOM allows multiple character sets to be specified
        // Format: First value is default, additional values are for fallback
        var expectedSets = new[] { "ISO_IR 192", "ISO_IR 100" };
        var dataset = new DicomDataset();

        // Act - Add as backslash-separated string (DICOM standard format)
        // Using DicomCodeString with a single backslash-separated value
        dataset.AddOrUpdate(DicomTag.SpecificCharacterSet, "ISO_IR 192\\ISO_IR 100");

        // Assert - Use string[] method to get all values
        var values = dataset.GetValues<string>(DicomTag.SpecificCharacterSet);
        values.Should().BeEquivalentTo(expectedSets,
            "Should support multiple character sets for fallback scenarios");
    }
}
