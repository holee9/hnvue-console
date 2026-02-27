using Dicom;
using FluentAssertions;
using HnVue.Dicom.Rdsr;
using HnVue.Dicom.Uid;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Dicom.Tests.Rdsr;

/// <summary>
/// Unit tests for RdsrBuilder - X-Ray Radiation Dose Structured Report (RDSR) construction.
/// SPEC-DICOM-001 AC-07 (task assignment): RDSR IOD conformance.
/// Maps to acceptance.md AC-11 Scenario 11.2 (IHE REM compliance at integration level).
/// </summary>
public class RdsrBuilderTests
{
    private readonly RdsrBuilder _builder;

    // SOP Class UID for X-Ray Radiation Dose SR Storage
    private const string RdsrSopClassUid = "1.2.840.10008.5.1.4.1.1.88.67";

    public RdsrBuilderTests()
    {
        var uidGenerator = new UidGenerator("1.2.3.4.5", "HNVUE001");
        _builder = new RdsrBuilder(uidGenerator, NullLogger<RdsrBuilder>.Instance);
    }

    private static StudyDoseSummary CreateValidStudySummary()
    {
        return new StudyDoseSummary
        {
            StudyInstanceUid = "1.2.3.4.5.100",
            PatientId = "P001",
            PatientName = "Test^Patient",
            Modality = "DX",
            TotalDapGyCm2 = 27.5m,
            ExposureCount = 2,
            StudyStartTimeUtc = DateTime.UtcNow.AddHours(-1),
            StudyEndTimeUtc = DateTime.UtcNow,
            AccessionNumber = "ACC001",
            PerformedStationAeTitle = "HNVUE_CONSOLE"
        };
    }

    private static DoseRecord CreateDoseRecord(string irradiationEventUid, string studyInstanceUid = "1.2.3.4.5.100")
    {
        return new DoseRecord
        {
            IrradiationEventUid = irradiationEventUid,
            StudyInstanceUid = studyInstanceUid,
            PatientId = "P001",
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            CalculatedDapGyCm2 = 15m,
            DoseSource = DoseSource.Calculated,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m
        };
    }

    // BuildAsync_WithValidProvider_ReturnsSrDocument
    [Fact]
    public void Build_WithValidProvider_ReturnsSrDocument()
    {
        // Arrange
        var summary = CreateValidStudySummary();
        var exposures = new[]
        {
            CreateDoseRecord("1.2.3.4.5.700"),
            CreateDoseRecord("1.2.3.4.5.701")
        };

        // Act
        var dicomFile = _builder.Build(summary, exposures);

        // Assert
        dicomFile.Should().NotBeNull();
        dicomFile.Dataset.Should().NotBeNull();
    }

    // BuildAsync_VerifiesCorrectSopClassUid
    [Fact]
    public void Build_VerifiesCorrectSopClassUid()
    {
        // Arrange
        var summary = CreateValidStudySummary();
        var exposures = new[] { CreateDoseRecord("1.2.3.4.5.700") };

        // Act
        var dicomFile = _builder.Build(summary, exposures);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.SOPClassUID)
            .Should().Be("1.2.840.10008.5.1.4.1.1.88.67",
                "X-Ray Radiation Dose SR SOP Class UID must be 1.2.840.10008.5.1.4.1.1.88.67");
    }

    // BuildAsync_MapsAllDoseRecordsToIrradiationEvents
    [Fact]
    public void Build_MapsAllDoseRecordsToIrradiationEvents()
    {
        // Arrange
        var summary = CreateValidStudySummary();
        var exposures = new[]
        {
            CreateDoseRecord("1.2.3.4.5.700"),
            CreateDoseRecord("1.2.3.4.5.701")
        };
        var expectedEventCount = exposures.Length;

        // Act
        var dicomFile = _builder.Build(summary, exposures);

        // Assert: Content Sequence must contain the irradiation event items
        // The content sequence contains: language item + DAP total + N irradiation events
        var contentSequence = dicomFile.Dataset.GetSequence(DicomTag.ContentSequence);
        Assert.NotNull(contentSequence);

        // Count CONTAINER items (these are the irradiation event containers)
        var irradiationEventContainers = contentSequence.Items
            .Where(ds => ds.GetSingleValue<string>(DicomTag.ValueType) == "CONTAINER")
            .ToList();

        irradiationEventContainers.Should().HaveCount(expectedEventCount,
            "every dose record must be mapped to an irradiation event container");
    }

    [Fact]
    public void Build_WithNoDoseRecords_ReturnsValidSrWithNoIrradiationEvents()
    {
        // Arrange
        var summary = CreateValidStudySummary() with { ExposureCount = 0, TotalDapGyCm2 = 0m };
        var exposures = Array.Empty<DoseRecord>();

        // Act
        var dicomFile = _builder.Build(summary, exposures);

        // Assert
        dicomFile.Should().NotBeNull();
        dicomFile.Dataset.GetString(DicomTag.SOPClassUID).Should().Be(RdsrSopClassUid);

        var contentSequence = dicomFile.Dataset.GetSequence(DicomTag.ContentSequence);
        var irradiationEvents = contentSequence.Items
            .Where(ds => ds.GetSingleValue<string>(DicomTag.ValueType) == "CONTAINER")
            .ToList();
        irradiationEvents.Should().BeEmpty("no dose records means no irradiation events");
    }

    [Fact]
    public void Build_WithNullStudySummary_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _builder.Build(null!, Array.Empty<DoseRecord>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_WithNullExposures_ThrowsArgumentNullException()
    {
        // Arrange
        var summary = CreateValidStudySummary();

        // Act & Assert
        var act = () => _builder.Build(summary, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_ModalityIsSetToSr()
    {
        // Arrange
        var summary = CreateValidStudySummary();
        var exposures = new[] { CreateDoseRecord("1.2.3.4.5.700") };

        // Act
        var dicomFile = _builder.Build(summary, exposures);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.Modality).Should().Be("SR",
            "RDSR is an SR (Structured Report) document, modality must be SR");
    }

    [Fact]
    public void Build_CompletionFlagIsComplete()
    {
        // Arrange
        var summary = CreateValidStudySummary();
        var exposures = new[] { CreateDoseRecord("1.2.3.4.5.700") };

        // Act
        var dicomFile = _builder.Build(summary, exposures);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.CompletionFlag).Should().Be("COMPLETE",
            "RDSR for a closed study must have COMPLETE completion flag per IHE REM");
    }

    [Fact]
    public void Build_PatientIdIsPreservedInDataset()
    {
        // Arrange
        var summary = CreateValidStudySummary();
        var exposures = new[] { CreateDoseRecord("1.2.3.4.5.700") };

        // Act
        var dicomFile = _builder.Build(summary, exposures);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.PatientID).Should().Be(summary.PatientId);
    }

    [Fact]
    public void Build_StudyInstanceUidIsPreservedInDataset()
    {
        // Arrange
        var summary = CreateValidStudySummary();
        var exposures = new[] { CreateDoseRecord("1.2.3.4.5.700") };

        // Act
        var dicomFile = _builder.Build(summary, exposures);

        // Assert
        dicomFile.Dataset.GetString(DicomTag.StudyInstanceUID).Should().Be(summary.StudyInstanceUid);
    }

    [Fact]
    public void Build_WithMeasuredDap_UsesEffectiveDapValue()
    {
        // Arrange
        var summary = CreateValidStudySummary();
        var doseRecord = CreateDoseRecord("1.2.3.4.5.700") with
        {
            CalculatedDapGyCm2 = 15m,
            MeasuredDapGyCm2 = 14.5m, // measured value takes precedence
            DoseSource = DoseSource.Measured
        };

        // Assert measured effective DAP calculation
        doseRecord.EffectiveDapGyCm2.Should().Be(14.5m,
            "measured DAP takes precedence over calculated DAP per DoseSource.Measured");
    }

    [Fact]
    public void DoseRecord_EffectiveDap_UsesCalculatedWhenNotMeasured()
    {
        // Arrange
        var record = CreateDoseRecord("1.2.3.4.5.700") with
        {
            CalculatedDapGyCm2 = 15m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated
        };

        // Assert
        record.EffectiveDapGyCm2.Should().Be(15m,
            "calculated DAP is used when measurement is unavailable");
    }
}
