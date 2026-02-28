using FluentAssertions;
using HnVue.Dicom.Rdsr;
using HnVue.Dose.Recording;
using HnVue.Dose.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Dose.Tests.Recording;

/// <summary>
/// Unit tests for StudyDoseAccumulator.
/// SPEC-DOSE-001 FR-DOSE-03 Cumulative Dose Tracking Per Patient Per Study.
/// </summary>
public class StudyDoseAccumulatorTests
{
    private readonly StudyDoseAccumulator _accumulator;

    public StudyDoseAccumulatorTests()
    {
        _accumulator = new StudyDoseAccumulator(NullLogger<StudyDoseAccumulator>.Instance);
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesAccumulator()
    {
        // Act
        var accumulator = new StudyDoseAccumulator(NullLogger<StudyDoseAccumulator>.Instance);

        // Assert
        accumulator.Should().NotBeNull();
        accumulator.HasActiveStudy.Should().BeFalse();
        accumulator.ActiveStudyUid.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new StudyDoseAccumulator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OpenStudy_WithValidParameters_OpensStudySession()
    {
        // Act
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);

        // Assert
        _accumulator.HasActiveStudy.Should().BeTrue();
        _accumulator.ActiveStudyUid.Should().Be(DoseTestData.Uids.StudyInstanceUid);
    }

    [Fact]
    public void OpenStudy_WithNullStudyUid_ThrowsArgumentException()
    {
        // Act
        var act = () => _accumulator.OpenStudy(null!, DoseTestData.Uids.PatientId);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OpenStudy_WithEmptyStudyUid_ThrowsArgumentException()
    {
        // Act
        var act = () => _accumulator.OpenStudy("", DoseTestData.Uids.PatientId);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OpenStudy_WithNullPatientId_ThrowsArgumentException()
    {
        // Act
        var act = () => _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OpenStudy_WhenStudyAlreadyActive_ThrowsInvalidOperationException()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);

        // Act
        var act = () => _accumulator.OpenStudy("1.2.3.4.5.101", "PATIENT002");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already active*");
    }

    [Fact]
    public void CloseStudy_WhenNoActiveStudy_ThrowsInvalidOperationException()
    {
        // Act
        var act = () => _accumulator.CloseStudy();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No active study*");
    }

    [Fact]
    public void CloseStudy_WithActiveStudy_ClosesStudyAndReturnsSummary()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);

        // Act
        var summary = _accumulator.CloseStudy();

        // Assert
        summary.Should().NotBeNull();
        summary.StudyInstanceUid.Should().Be(DoseTestData.Uids.StudyInstanceUid);
        summary.PatientId.Should().Be(DoseTestData.Uids.PatientId);
        _accumulator.HasActiveStudy.Should().BeFalse();
        _accumulator.ActiveStudyUid.Should().BeNull();
    }

    [Fact]
    public void AddExposure_WithNullRecord_ThrowsArgumentNullException()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);

        // Act
        var act = () => _accumulator.AddExposure(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddExposure_WithNoActiveStudy_AddsToHoldingBuffer()
    {
        // Arrange
        var record = CreateDoseRecord();

        // Act
        var accumulation = _accumulator.AddExposure(record);

        // Assert
        accumulation.Should().NotBeNull();
        accumulation.ExposureCount.Should().Be(1);
        accumulation.CumulativeDapGyCm2.Should().Be(record.CalculatedDapGyCm2);
    }

    [Fact]
    public void AddExposure_WithActiveStudy_UpdatesCumulativeDose()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);
        var record = CreateDoseRecord(studyUid: DoseTestData.Uids.StudyInstanceUid);

        // Act
        var accumulation = _accumulator.AddExposure(record);

        // Assert
        accumulation.CumulativeDapGyCm2.Should().Be(record.CalculatedDapGyCm2);
        accumulation.ExposureCount.Should().Be(1);
        accumulation.StudyInstanceUid.Should().Be(DoseTestData.Uids.StudyInstanceUid);
    }

    [Fact]
    public void AddExposure_WithMultipleExposures_AccumulatesCorrectly()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);
        var record1 = CreateDoseRecord(studyUid: DoseTestData.Uids.StudyInstanceUid, dap: 0.010m);
        var record2 = CreateDoseRecord(studyUid: DoseTestData.Uids.StudyInstanceUid, dap: 0.015m);
        var record3 = CreateDoseRecord(studyUid: DoseTestData.Uids.StudyInstanceUid, dap: 0.020m);
        var expectedCumulative = 0.010m + 0.015m + 0.020m;

        // Act
        _accumulator.AddExposure(record1);
        _accumulator.AddExposure(record2);
        var accumulation = _accumulator.AddExposure(record3);

        // Assert
        accumulation.CumulativeDapGyCm2.Should().Be(expectedCumulative);
        accumulation.ExposureCount.Should().Be(3);
    }

    [Fact]
    public void AddExposure_WithMismatchedStudyUid_ThrowsInvalidOperationException()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);
        var record = CreateDoseRecord(studyUid: "1.2.3.4.5.999"); // Different study

        // Act
        var act = () => _accumulator.AddExposure(record);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not match active study*");
    }

    [Fact]
    public void GetCurrentAccumulation_WithNoActiveStudy_ReturnsNull()
    {
        // Act
        var accumulation = _accumulator.GetCurrentAccumulation();

        // Assert
        accumulation.Should().BeNull();
    }

    [Fact]
    public void GetCurrentAccumulation_WithActiveStudy_ReturnsCurrentState()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);
        var record = CreateDoseRecord(studyUid: DoseTestData.Uids.StudyInstanceUid, dap: 0.015m);
        _accumulator.AddExposure(record);

        // Act
        var accumulation = _accumulator.GetCurrentAccumulation();

        // Assert
        accumulation.Should().NotBeNull();
        accumulation!.CumulativeDapGyCm2.Should().Be(0.015m);
        accumulation.ExposureCount.Should().Be(1);
        accumulation.StudyInstanceUid.Should().Be(DoseTestData.Uids.StudyInstanceUid);
    }

    [Fact]
    public void AssociateHoldingBuffer_WithMatchingStudy_AssociatesRecords()
    {
        // Arrange
        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var record1 = CreateDoseRecord(studyUid: studyUid);
        var record2 = CreateDoseRecord(studyUid: studyUid);

        // Add to holding buffer (no active study)
        _accumulator.AddExposure(record1);
        _accumulator.AddExposure(record2);

        // Open study and associate
        _accumulator.OpenStudy(studyUid, DoseTestData.Uids.PatientId);

        // Act
        var associatedCount = _accumulator.AssociateHoldingBuffer(studyUid);

        // Assert
        associatedCount.Should().Be(2);
        var accumulation = _accumulator.GetCurrentAccumulation();
        accumulation!.ExposureCount.Should().Be(2);
    }

    [Fact]
    public void AssociateHoldingBuffer_WithNonMatchingStudy_AssociatesNothing()
    {
        // Arrange
        var studyUid1 = DoseTestData.Uids.StudyInstanceUid;
        var studyUid2 = "1.2.3.4.5.999";
        var record = CreateDoseRecord(studyUid: studyUid1);

        // Add to holding buffer
        _accumulator.AddExposure(record);

        // Open different study
        _accumulator.OpenStudy(studyUid2, "PATIENT002");

        // Act
        var associatedCount = _accumulator.AssociateHoldingBuffer(studyUid2);

        // Assert
        associatedCount.Should().Be(0);
        var accumulation = _accumulator.GetCurrentAccumulation();
        accumulation!.ExposureCount.Should().Be(0);
    }

    [Fact]
    public void CloseStudy_AfterAddingExposures_ReturnsCorrectSummary()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);
        var record1 = CreateDoseRecord(studyUid: DoseTestData.Uids.StudyInstanceUid, dap: 0.010m);
        var record2 = CreateDoseRecord(studyUid: DoseTestData.Uids.StudyInstanceUid, dap: 0.015m);
        _accumulator.AddExposure(record1);
        _accumulator.AddExposure(record2);
        var expectedCumulative = 0.010m + 0.015m;

        // Act
        var summary = _accumulator.CloseStudy();

        // Assert
        summary.CumulativeDapGyCm2.Should().Be(expectedCumulative);
        summary.ExposureCount.Should().Be(2);
        summary.PatientId.Should().Be(DoseTestData.Uids.PatientId);
    }

    [Fact]
    public async Task AddExposure_ThreadSafe_MultipleConcurrentCalls()
    {
        // Arrange
        _accumulator.OpenStudy(DoseTestData.Uids.StudyInstanceUid, DoseTestData.Uids.PatientId);
        var exposureCount = 10;

        // Act
        var tasks = Enumerable.Range(0, exposureCount).Select(i =>
        {
            return Task.Run(() =>
            {
                var record = CreateDoseRecord(
                    studyUid: DoseTestData.Uids.StudyInstanceUid,
                    dap: 0.001m,
                    eventId: Guid.NewGuid());
                _accumulator.AddExposure(record);
            });
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var accumulation = _accumulator.GetCurrentAccumulation();
        accumulation.Should().NotBeNull();
        accumulation!.ExposureCount.Should().Be(exposureCount);
        accumulation.CumulativeDapGyCm2.Should().Be(0.001m * exposureCount);
    }

    private static DoseRecord CreateDoseRecord(
        string? studyUid = null,
        decimal dap = 0.015m,
        Guid? eventId = null)
    {
        return new DoseRecord
        {
            ExposureEventId = eventId ?? Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = studyUid ?? DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = dap,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };
    }
}
