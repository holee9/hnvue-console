using FluentAssertions;
using HnVue.Dicom.Rdsr;
using HnVue.Dose.Exceptions;
using HnVue.Dose.Interfaces;
using HnVue.Dose.Recording;
using HnVue.Dose.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Dose.Tests.Recording;

/// <summary>
/// Unit tests for DoseRecordRepository.
/// SPEC-DOSE-001 FR-DOSE-01, NFR-DOSE-02 Atomic persistence, crash recovery.
/// </summary>
public class DoseRecordRepositoryTests : IAsyncLifetime
{
    private readonly string _testDataDirectory;
    private DoseRecordRepository _repository = null!;

    public DoseRecordRepositoryTests()
    {
        _testDataDirectory = Path.Combine(Path.GetTempPath(), $"dose_tests_{Guid.NewGuid()}");
    }

    public Task InitializeAsync()
    {
        _repository = new DoseRecordRepository(
            NullLogger<DoseRecordRepository>.Instance,
            _testDataDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDataDirectory))
        {
            try
            {
                Directory.Delete(_testDataDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PersistAsync_WithValidRecord_PersistsRecord()
    {
        // Arrange
        var record = CreateDoseRecord();

        // Act
        await _repository.PersistAsync(record);

        // Assert
        var retrieved = await _repository.GetByStudyAsync(record.StudyInstanceUid);
        retrieved.Should().HaveCount(1);
        retrieved[0].ExposureEventId.Should().Be(record.ExposureEventId);
    }

    [Fact]
    public async Task PersistAsync_WithNullRecord_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _repository.PersistAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PersistAsync_WithMissingStudyUid_ThrowsArgumentException()
    {
        // Arrange
        var record = CreateDoseRecord();
        record = record with { StudyInstanceUid = "" };

        // Act
        var act = async () => await _repository.PersistAsync(record);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PersistAsync_WithMultipleRecords_PersistsAllRecords()
    {
        // Arrange
        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var record1 = CreateDoseRecord(studyUid, dap: 0.010m);
        var record2 = CreateDoseRecord(studyUid, dap: 0.015m);
        var record3 = CreateDoseRecord(studyUid, dap: 0.020m);

        // Act
        await _repository.PersistAsync(record1);
        await _repository.PersistAsync(record2);
        await _repository.PersistAsync(record3);

        // Assert
        var retrieved = await _repository.GetByStudyAsync(studyUid);
        retrieved.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByStudyAsync_WithValidStudyUid_ReturnsRecords()
    {
        // Arrange
        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var record1 = CreateDoseRecord(studyUid);
        var record2 = CreateDoseRecord(studyUid);
        await _repository.PersistAsync(record1);
        await _repository.PersistAsync(record2);

        // Act
        var retrieved = await _repository.GetByStudyAsync(studyUid);

        // Assert
        retrieved.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByStudyAsync_WithNonExistentStudy_ReturnsEmptyList()
    {
        // Act
        var retrieved = await _repository.GetByStudyAsync("1.2.3.4.5.999");

        // Assert
        retrieved.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByStudyAsync_WithNullStudyUid_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _repository.GetByStudyAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByStudyAsync_WithEmptyStudyUid_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _repository.GetByStudyAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByStudyAsync_ReturnsRecordsOrderedByTimestamp()
    {
        // Arrange
        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var timestamp1 = DateTime.UtcNow.AddMinutes(-2);
        var timestamp2 = DateTime.UtcNow.AddMinutes(-1);
        var timestamp3 = DateTime.UtcNow;

        var record1 = CreateDoseRecord(studyUid, timestamp: timestamp1);
        var record2 = CreateDoseRecord(studyUid, timestamp: timestamp2);
        var record3 = CreateDoseRecord(studyUid, timestamp: timestamp3);

        // Persist out of order
        await _repository.PersistAsync(record3);
        await _repository.PersistAsync(record1);
        await _repository.PersistAsync(record2);

        // Act
        var retrieved = await _repository.GetByStudyAsync(studyUid);

        // Assert
        retrieved.Should().HaveCount(3);
        retrieved[0].TimestampUtc.Should().Be(timestamp1);
        retrieved[1].TimestampUtc.Should().Be(timestamp2);
        retrieved[2].TimestampUtc.Should().Be(timestamp3);
    }

    [Fact]
    public async Task PersistAsync_CompletesWithin1Second()
    {
        // Arrange
        var record = CreateDoseRecord();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _repository.PersistAsync(record);
        stopwatch.Stop();

        // Assert: Must complete within 1 second per NFR-DOSE-02
        stopwatch.ElapsedMilliseconds.Should().BeLessOrEqualTo(1000);
    }

    [Fact]
    public async Task PersistAsync_ThreadSafe_MultipleConcurrentWrites()
    {
        // Arrange
        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var recordCount = 10;

        // Act
        var tasks = Enumerable.Range(0, recordCount).Select(i =>
        {
            return Task.Run(async () =>
            {
                var record = CreateDoseRecord(studyUid, eventId: Guid.NewGuid());
                await _repository.PersistAsync(record);
            });
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var retrieved = await _repository.GetByStudyAsync(studyUid);
        retrieved.Should().HaveCount(recordCount);
    }

    [Fact]
    public async Task PersistAsync_WithDifferentStudies_SeparatesRecords()
    {
        // Arrange
        var studyUid1 = DoseTestData.Uids.StudyInstanceUid;
        var studyUid2 = "1.2.3.4.5.999";
        var record1 = CreateDoseRecord(studyUid1);
        var record2 = CreateDoseRecord(studyUid2);

        // Act
        await _repository.PersistAsync(record1);
        await _repository.PersistAsync(record2);

        // Assert
        var retrieved1 = await _repository.GetByStudyAsync(studyUid1);
        var retrieved2 = await _repository.GetByStudyAsync(studyUid2);

        retrieved1.Should().HaveCount(1);
        retrieved2.Should().HaveCount(1);
        retrieved1[0].StudyInstanceUid.Should().Be(studyUid1);
        retrieved2[0].StudyInstanceUid.Should().Be(studyUid2);
    }

    [Fact]
    public async Task PersistAsync_AfterCloseAndReopen_RetainsRecords()
    {
        // Arrange
        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var record = CreateDoseRecord(studyUid);
        await _repository.PersistAsync(record);

        // Simulate repository reinitialization
        var newRepository = new DoseRecordRepository(
            NullLogger<DoseRecordRepository>.Instance,
            _testDataDirectory);

        // Act
        var retrieved = await newRepository.GetByStudyAsync(studyUid);

        // Assert
        retrieved.Should().HaveCount(1);
        retrieved[0].ExposureEventId.Should().Be(record.ExposureEventId);
    }

    private static DoseRecord CreateDoseRecord(
        string? studyUid = null,
        decimal dap = 0.015m,
        DateTime? timestamp = null,
        Guid? eventId = null)
    {
        return new DoseRecord
        {
            ExposureEventId = eventId ?? Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = studyUid ?? DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = timestamp ?? DateTime.UtcNow,
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
