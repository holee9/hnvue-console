using Dicom;
using FluentAssertions;
using HnVue.Dicom.Iod;
using Xunit;

namespace HnVue.Dicom.Tests.Conformance;

/// <summary>
/// Tests to verify DICOM Conformance Statement compliance.
/// SPEC-DICOM-001 FR-DICOM-12: DICOM Conformance Statement requirements.
/// Validates that implemented SOP classes, transfer syntaxes, and IHE profiles
/// match the documented Conformance Statement.
/// </summary>
public class ConformanceStatementTests
{
    // Conformance Statement path - from test output directory (artifacts/bin/.../net8.0)
    // Path: ../../../../../ (to project root where src/ is)
    private const string ConformanceStatementPath = "../../../../../../../src/HnVue.Dicom/Conformance/DicomConformanceStatement.md";

    // Expected SOP Class UIDs per SPEC-DICOM-001 Appendix A
    private static readonly Dictionary<string, string> ExpectedSopClasses = new(StringComparer.Ordinal)
    {
        // DX Image Storage - For Presentation (IHE SWF)
        ["1.2.840.10008.5.1.4.1.1.1.1"] = "Digital X-Ray Image - For Presentation",
        // DX Image Storage - For Processing (IHE SWF)
        ["1.2.840.10008.5.1.4.1.1.1.1.1"] = "Digital X-Ray Image - For Processing",
        // CR Image Storage (IHE SWF)
        ["1.2.840.10008.5.1.4.1.1.1"] = "Computed Radiography Image",
        // Modality Worklist (IHE SWF)
        ["1.2.840.10008.5.1.4.31"] = "Modality Worklist Information - FIND",
        // MPPS (IHE SWF)
        ["1.2.840.10008.3.1.2.3.3"] = "Modality Performed Procedure Step",
        // Storage Commitment (IHE SWF)
        ["1.2.840.10008.1.3.10"] = "Storage Commitment Push Model",
        // Study Root Query/Retrieve - FIND (IHE PIR)
        ["1.2.840.10008.5.1.4.1.2.2.1"] = "Study Root Query/Retrieve - FIND",
        // Study Root Query/Retrieve - MOVE (IHE PIR)
        ["1.2.840.10008.5.1.4.1.2.2.2"] = "Study Root Query/Retrieve - MOVE",
        // RDSR (IHE REM)
        ["1.2.840.10008.5.1.4.1.1.88.67"] = "X-Ray Radiation Dose SR"
    };

    // Expected IHE Integration Profiles per SPEC-DICOM-001 Section 4.1
    private static readonly string[] ExpectedIheProfiles = { "SWF", "PIR", "REM" };

    // Expected Transfer Syntax priority order per SPEC-DICOM-001 FR-DICOM-02
    private static readonly (string Name, string Uid)[] ExpectedTransferSyntaxes =
    [
        ("JPEG 2000 Lossless Only", "1.2.840.10008.1.2.4.90"),
        ("JPEG Lossless, Non-Hierarchical", "1.2.840.10008.1.2.4.70"),
        ("Explicit VR Little Endian", "1.2.840.10008.1.2.1"),
        ("Implicit VR Little Endian", "1.2.840.10008.1.2")
    ];

    [Fact]
    public void ConformanceStatement_FileExists()
    {
        // Act & Assert
        File.Exists(ConformanceStatementPath).Should().BeTrue(
            "Conformance Statement must exist at the documented path");
    }

    [Fact]
    public void ConformanceStatement_ContainsRequiredSections()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert
        content.Should().Contain("## Section 1 - Implementation Model",
            "Conformance Statement must document the implementation model");
        content.Should().Contain("## Section 2 - AE Specifications",
            "Conformance Statement must document AE specifications");
        content.Should().Contain("## Section 3 - Network Communication Support",
            "Conformance Statement must document network communication support");
        content.Should().Contain("## Section 4 - Extensions / Specializations / Privatizations",
            "Conformance Statement must document extensions");
        content.Should().Contain("## Section 5 - Configuration",
            "Conformance Statement must document configuration");
        content.Should().Contain("## Section 6 - Support of Character Sets",
            "Conformance Statement must document character set support");
    }

    [Fact]
    public void ConformanceStatement_DocumentsAllSopClasses()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert
        foreach (var (uid, name) in ExpectedSopClasses)
        {
            content.Should().Contain(uid,
                "Conformance Statement must document SOP Class UID: {0}", uid);
        }
    }

    [Fact]
    public void ConformanceStatement_DocumentsIheProfiles()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert
        content.Should().Contain("## Appendix B: IHE Integration Profile Claims",
            "Conformance Statement must document IHE profile claims");

        foreach (var profile in ExpectedIheProfiles)
        {
            content.Should().Contain(profile,
                "Conformance Statement must claim IHE profile: {0}", profile);
        }
    }

    [Fact]
    public void ConformanceStatement_DocumentsSwfTransactions()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert - IHE SWF required transactions
        content.Should().Contain("RAD-5", "SWF must include Query Modality Worklist (RAD-5)");
        content.Should().Contain("RAD-6", "SWF must include MPPS In Progress (RAD-6)");
        content.Should().Contain("RAD-7", "SWF must include MPPS Completed (RAD-7)");
        content.Should().Contain("RAD-8", "SWF must include Modality Images Stored (RAD-8)");
        content.Should().Contain("RAD-10", "SWF must include Storage Commitment (RAD-10)");
    }

    [Fact]
    public void ConformanceStatement_DocumentsTransferSyntaxPriorityOrder()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert - Verify priority order by checking sequence
        var jpeg2000Index = content.IndexOf("JPEG 2000 Lossless Only", StringComparison.Ordinal);
        var jpegLosslessIndex = content.IndexOf("JPEG Lossless, Non-Hierarchical", StringComparison.Ordinal);
        var explicitVrIndex = content.IndexOf("Explicit VR Little Endian", StringComparison.Ordinal);
        var implicitVrIndex = content.IndexOf("Implicit VR Little Endian", StringComparison.Ordinal);

        // All should be present
        jpeg2000Index.Should().BeGreaterThan(-1, "JPEG 2000 Lossless must be documented");
        jpegLosslessIndex.Should().BeGreaterThan(-1, "JPEG Lossless must be documented");
        explicitVrIndex.Should().BeGreaterThan(-1, "Explicit VR LE must be documented");
        implicitVrIndex.Should().BeGreaterThan(-1, "Implicit VR LE must be documented");

        // Priority order must be preserved in documentation
        jpeg2000Index.Should().BeLessThan(jpegLosslessIndex,
            "JPEG 2000 Lossless should be preferred over JPEG Lossless");
        jpegLosslessIndex.Should().BeLessThan(explicitVrIndex,
            "JPEG Lossless should be preferred over Explicit VR LE");
        explicitVrIndex.Should().BeLessThan(implicitVrIndex,
            "Explicit VR LE should be preferred over Implicit VR LE");
    }

    [Fact]
    public void ConformanceStatement_DocumentsCharacterSets()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert
        content.Should().Contain("ISO 8859-1",
            "Conformance Statement must document ISO 8859-1 default character set");
        content.Should().Contain("ISO_IR 192",
            "Conformance Statement must document ISO_IR 192 (UTF-8) extended character support");
    }

    [Fact]
    public void ConformanceStatement_DocumentsTlsSupport()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert
        content.Should().Contain("TLS 1.2",
            "Conformance Statement must document TLS 1.2 minimum version");
        content.Should().Contain("TLS 1.3",
            "Conformance Statement should document TLS 1.3 preferred version");
        content.Should().Contain("Basic TLS Secure Transport",
            "Conformance Statement must claim DICOM Basic TLS Secure Transport Connection Profile");
    }

    [Fact]
    public void ConformanceStatement_DocumentsScuOnlyRole()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert
        content.Should().Contain("SCU", "Conformance Statement must document SCU role");
        content.Should().Contain("acting exclusively in the **SCU (Service Class User) role**",
            "Conformance Statement must state SCU-only role (no SCP capability)");
    }

    [Fact]
    public void ConformanceStatement_DocumentsStorageScuStatusHandling()
    {
        // Arrange
        var content = File.ReadAllText(ConformanceStatementPath);

        // Act & Assert - Verify status code documentation per SPEC-DICOM-001 Section 4.5.1
        content.Should().Contain("0x0000",
            "Conformance Statement must document Success status");
        content.Should().Contain("0xB000",
            "Conformance Statement must document Warning status range");
        content.Should().Contain("0xA700",
            "Conformance Statement must document Failure status ranges");
        content.Should().Contain("0xC000",
            "Conformance Statement must document Cannot Understand status range");
    }
}
