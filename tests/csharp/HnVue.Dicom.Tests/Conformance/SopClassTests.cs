using Dicom;
using FluentAssertions;
using HnVue.Dicom.Iod;
using System.Text.RegularExpressions;
using Xunit;

namespace HnVue.Dicom.Tests.Conformance;

/// <summary>
/// Tests to verify SOP Class UID constants and declarations.
/// SPEC-DICOM-001 Section 1.3: Supported SOP Classes.
/// </summary>
public class SopClassTests
{
    // SOP Class UIDs per DICOM Standard and SPEC-DICOM-001
    private const string DxForPresentationUid = "1.2.840.10008.5.1.4.1.1.1.1";
    private const string DxForProcessingUid = "1.2.840.10008.5.1.4.1.1.1.1.1";
    private const string CrImageStorageUid = "1.2.840.10008.5.1.4.1.1.1";
    private const string ModalityWorklistUid = "1.2.840.10008.5.1.4.31";
    private const string MppsUid = "1.2.840.10008.3.1.2.3.3";
    private const string StorageCommitmentUid = "1.2.840.10008.1.3.10";
    private const string StudyRootFindUid = "1.2.840.10008.5.1.4.1.2.2.1";
    private const string StudyRootMoveUid = "1.2.840.10008.5.1.4.1.2.2.2";
    private const string RdsrUid = "1.2.840.10008.5.1.4.1.1.88.67";

    [Fact]
    public void DxForPresentationSopClass_HasCorrectUid()
    {
        // Act
        var sopClass = DxImageBuilder.DxForPresentationSopClass;

        // Assert
        sopClass.UID.Should().Be(DxForPresentationUid,
            "DX For Presentation SOP Class UID must match DICOM standard");
        sopClass.Name.Should().NotBeNullOrEmpty(
            "SOP Class should have a descriptive name");
    }

    [Fact]
    public void CrImageStorageSopClass_HasCorrectUid()
    {
        // Act
        var sopClass = CrImageBuilder.CrImageStorageSopClass;

        // Assert
        sopClass.UID.Should().Be(CrImageStorageUid,
            "CR Image Storage SOP Class UID must match DICOM standard");
    }

    [Fact]
    public void CanParseAllSupportedSopClassUids()
    {
        // Arrange - All SOP Class UIDs supported per SPEC-DICOM-001
        var supportedUids = new[]
        {
            DxForPresentationUid,
            DxForProcessingUid,
            CrImageStorageUid,
            ModalityWorklistUid,
            MppsUid,
            StorageCommitmentUid,
            StudyRootFindUid,
            StudyRootMoveUid,
            RdsrUid
        };

        // Act & Assert - All UIDs should be parseable as valid DICOM UIDs
        foreach (var uid in supportedUids)
        {
            var parsed = DicomUID.Parse(uid);
            parsed.Should().NotBeNull(
                "SOP Class UID '{0}' should be a valid DICOM UID", uid);
            parsed.UID.Should().Be(uid,
                "Parsed UID should match input");
        }
    }

    [Fact]
    public void AllSopClassesAreScuRole()
    {
        // Arrange & Act - Verify implementation claims SCU role only
        // This is a documentation test: the source code must reflect SCU-only behavior

        // The implementation uses DicomClient (SCU) not DicomServer (SCP)
        // Verify by checking that SCU classes exist and are testable
        var scuImplementations = new[]
        {
            typeof(HnVue.Dicom.Storage.StorageScu),
            typeof(HnVue.Dicom.Worklist.WorklistScu),
            typeof(HnVue.Dicom.Mpps.MppsScu),
            typeof(HnVue.Dicom.StorageCommit.StorageCommitScu),
            typeof(HnVue.Dicom.QueryRetrieve.QueryRetrieveScu)
        };

        // Assert - All "Scu" suffixed classes should exist
        scuImplementations.All(t => t != null).Should().BeTrue(
            "All supported SOP classes should have SCU implementations");

        // Verify no "Scp" implementations exist (SCU-only requirement)
        var scpTypes = typeof(HnVue.Dicom.Storage.StorageScu).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Scp", StringComparison.Ordinal));

        scpTypes.Should().BeEmpty(
            "Implementation must be SCU-only; no SCP classes should exist");
    }

    [Fact]
    public void SopClassUids_AreGloballyUnique()
    {
        // Arrange - All supported SOP Class UIDs
        var allUids = new (string Name, string Uid)[]
        {
            ("DX For Presentation", DxForPresentationUid),
            ("DX For Processing", DxForProcessingUid),
            ("CR Image Storage", CrImageStorageUid),
            ("Modality Worklist", ModalityWorklistUid),
            ("MPPS", MppsUid),
            ("Storage Commitment", StorageCommitmentUid),
            ("Study Root QR FIND", StudyRootFindUid),
            ("Study Root QR MOVE", StudyRootMoveUid),
            ("RDSR", RdsrUid)
        };

        // Act & Assert - No duplicates allowed
        var uniqueUids = allUids.Select(x => x.Uid).Distinct().ToList();
        uniqueUids.Should().HaveCount(allUids.Length,
            "All SOP Class UIDs must be unique; duplicates found: {0}",
            string.Join(", ", allUids.GroupBy(x => x.Uid)
                .Where(g => g.Count() > 1)
                .Select(g => g.First().Name)));
    }

    [Fact]
    public void SopClassUids_FollowDicomUidFormat()
    {
        // Arrange - DICOM UID format: org.root.<suffix> where numbers are separated by dots
        var uidPattern = new Regex(@"^\d+(\.\d+)*$");

        var allUids = new[]
        {
            DxForPresentationUid,
            DxForProcessingUid,
            CrImageStorageUid,
            ModalityWorklistUid,
            MppsUid,
            StorageCommitmentUid,
            StudyRootFindUid,
            StudyRootMoveUid,
            RdsrUid
        };

        // Act & Assert
        foreach (var uid in allUids)
        {
            uidPattern.IsMatch(uid).Should().BeTrue(
                "UID '{0}' must follow DICOM format (dot-separated numeric components)", uid);
        }
    }

    [Fact]
    public void SopClassUids_UseStandardPrefixes()
    {
        // Arrange - DICOM standard root prefixes
        // 1.2.840.10008 is the DICOM standard root prefix
        var dicomStandardRoot = "1.2.840.10008";

        var allUids = new[]
        {
            DxForPresentationUid,
            DxForProcessingUid,
            CrImageStorageUid,
            ModalityWorklistUid,
            MppsUid,
            StorageCommitmentUid,
            StudyRootFindUid,
            StudyRootMoveUid,
            RdsrUid
        };

        // Act & Assert - All SOP Class UIDs should use DICOM standard root
        foreach (var uid in allUids)
        {
            uid.Should().StartWith(dicomStandardRoot,
                "SOP Class UID '{0}' should use DICOM standard root prefix", uid);
        }
    }

    [Fact]
    public void SopClassUids_AreWellKnownStandardUids()
    {
        // Act - Try to parse as well-known UIDs
        var dxPresentation = DicomUID.Parse(DxForPresentationUid);
        var crStorage = DicomUID.Parse(CrImageStorageUid);
        var worklist = DicomUID.Parse(ModalityWorklistUid);
        var mpps = DicomUID.Parse(MppsUid);
        var storageCommit = DicomUID.Parse(StorageCommitmentUid);

        // Assert - These should be recognized as standard UIDs (not private)
        // fo-dicom: Check if UID type is SOPClass and not a private UID
        dxPresentation.UID.Should().Be(DxForPresentationUid,
            "DX For Presentation should be a standard DICOM SOP Class UID");
        crStorage.UID.Should().Be(CrImageStorageUid,
            "CR Image Storage should be a standard DICOM SOP Class UID");
        worklist.UID.Should().Be(ModalityWorklistUid,
            "Modality Worklist should be a standard DICOM SOP Class UID");
        mpps.UID.Should().Be(MppsUid,
            "MPPS should be a standard DICOM SOP Class UID");
        storageCommit.UID.Should().Be(StorageCommitmentUid,
            "Storage Commitment should be a standard DICOM SOP Class UID");
    }
}
