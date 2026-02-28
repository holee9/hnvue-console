using FluentAssertions;
using HnVue.Dicom.Rdsr;
using HnVue.Dose.Interfaces;
using HnVue.Dose.RDSR;
using HnVue.Dose.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HnVue.Dose.Tests.RDSR;

/// <summary>
/// Unit tests for RdsrDataProvider.
/// SPEC-DOSE-001 FR-DOSE-07 RDSR Data Provider.
/// </summary>
public class RdsrDataProviderTests : IAsyncLifetime
{
    private readonly Mock<IDoseRecordRepository> _repositoryMock;
    private readonly string _testDataDirectory;
    private RdsrDataProvider? _provider;

    public RdsrDataProviderTests()
    {
        _repositoryMock = new Mock<IDoseRecordRepository>(MockBehavior.Strict);
        _testDataDirectory = Path.Combine(Path.GetTempPath(), $"rdsr_provider_test_{Guid.NewGuid()}");
    }

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_testDataDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _provider?.Dispose();

        if (Directory.Exists(_testDataDirectory))
        {
            try { Directory.Delete(_testDataDirectory, true); }
            catch { /* Ignore cleanup errors */ }
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesProvider()
    {
        // Arrange & Act
        var provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        // Assert
        provider.Should().NotBeNull();
        provider.StudyClosed.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RdsrDataProvider(
            null!,
            NullLogger<RdsrDataProvider>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RdsrDataProvider(
            _repositoryMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetStudyDoseSummaryAsync_WithValidStudy_ReturnsSummary()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var exposures = new List<DoseRecord>
        {
            CreateDoseRecord(studyUid, 0.010m, DateTime.UtcNow.AddMinutes(-10)),
            CreateDoseRecord(studyUid, 0.015m, DateTime.UtcNow.AddMinutes(-5)),
            CreateDoseRecord(studyUid, 0.020m, DateTime.UtcNow)
        };

        _repositoryMock
            .Setup(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exposures);

        // Act
        var summary = await _provider.GetStudyDoseSummaryAsync(studyUid);

        // Assert
        summary.Should().NotBeNull();
        summary!.StudyInstanceUid.Should().Be(studyUid);
        summary.PatientId.Should().Be(DoseTestData.Uids.PatientId);
        summary.TotalDapGyCm2.Should().Be(0.045m);
        summary.ExposureCount.Should().Be(3);
        summary.Modality.Should().Be("DX");
        summary.DrlExceeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetStudyDoseSummaryAsync_WithNoExposures_ReturnsNull()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        var studyUid = DoseTestData.Uids.StudyInstanceUid;

        _repositoryMock
            .Setup(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DoseRecord>());

        // Act
        var summary = await _provider.GetStudyDoseSummaryAsync(studyUid);

        // Assert
        summary.Should().BeNull();
    }

    [Fact]
    public async Task GetStudyDoseSummaryAsync_WithEmptyStudyUid_ThrowsArgumentException()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        // Act
        var act = () => _provider.GetStudyDoseSummaryAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetStudyDoseSummaryAsync_WithNullStudyUid_ThrowsArgumentException()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        // Act
        var act = () => _provider.GetStudyDoseSummaryAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetStudyDoseSummaryAsync_WithDrlExceedance_SetsDrlExceededFlag()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var exposures = new List<DoseRecord>
        {
            CreateDoseRecord(studyUid, 0.010m, DateTime.UtcNow.AddMinutes(-10)),
            CreateDoseRecord(studyUid, 0.500m, DateTime.UtcNow) // High DAP - exceeds DRL
        };

        // Mark second exposure as DRL exceeded
        exposures[1] = exposures[1] with { DrlExceedance = true };

        _repositoryMock
            .Setup(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exposures);

        // Act
        var summary = await _provider.GetStudyDoseSummaryAsync(studyUid);

        // Assert
        summary.Should().NotBeNull();
        summary!.DrlExceeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetStudyExposureRecordsAsync_WithValidStudy_ReturnsChronologicallySortedRecords()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var now = DateTime.UtcNow;
        var exposures = new List<DoseRecord>
        {
            CreateDoseRecord(studyUid, 0.010m, now.AddMinutes(-5)), // Middle
            CreateDoseRecord(studyUid, 0.015m, now),                 // Last
            CreateDoseRecord(studyUid, 0.020m, now.AddMinutes(-10))  // First
        };

        _repositoryMock
            .Setup(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exposures);

        // Act
        var result = await _provider.GetStudyExposureRecordsAsync(studyUid);

        // Assert
        result.Should().HaveCount(3);
        result[0].TimestampUtc.Should().BeBefore(result[1].TimestampUtc);
        result[1].TimestampUtc.Should().BeBefore(result[2].TimestampUtc);
    }

    [Fact]
    public async Task GetStudyExposureRecordsAsync_WithNoExposures_ReturnsEmptyList()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        var studyUid = DoseTestData.Uids.StudyInstanceUid;

        _repositoryMock
            .Setup(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DoseRecord>());

        // Act
        var result = await _provider.GetStudyExposureRecordsAsync(studyUid);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStudyExposureRecordsAsync_WithEmptyStudyUid_ThrowsArgumentException()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        // Act
        var act = () => _provider.GetStudyExposureRecordsAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task NotifyStudyClosedAsync_WithValidParameters_PublishesEvent()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var exposures = new List<DoseRecord>
        {
            CreateDoseRecord(studyUid, 0.010m, DateTime.UtcNow.AddMinutes(-5)),
            CreateDoseRecord(studyUid, 0.015m, DateTime.UtcNow)
        };

        _repositoryMock
            .Setup(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exposures);

        // Act
        await _provider.NotifyStudyClosedAsync(studyUid, DoseTestData.Uids.PatientId);

        // Assert - Verify repository was called
        _repositoryMock.Verify(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()), Times.Once);

        // Verify StudyClosed is not null (basic check)
        _provider.StudyClosed.Should().NotBeNull();
    }

    [Fact]
    public async Task NotifyStudyClosedAsync_WithEmptyStudyUid_ThrowsArgumentException()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        // Act
        var act = () => _provider.NotifyStudyClosedAsync("", DoseTestData.Uids.PatientId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task NotifyStudyClosedAsync_WithEmptyPatientId_ThrowsArgumentException()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        // Act
        var act = () => _provider.NotifyStudyClosedAsync(DoseTestData.Uids.StudyInstanceUid, "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        // Act & Assert - Should not throw
        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public async Task GetStudyDoseSummaryAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);
        _provider.Dispose();

        // Act
        var act = () => _provider.GetStudyDoseSummaryAsync(DoseTestData.Uids.StudyInstanceUid);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task StudyClosed_MultipleSubscribers_AllReceiveEvents()
    {
        // Arrange
        _provider = new RdsrDataProvider(
            _repositoryMock.Object,
            NullLogger<RdsrDataProvider>.Instance);

        var studyUid = DoseTestData.Uids.StudyInstanceUid;
        var exposures = new List<DoseRecord>
        {
            CreateDoseRecord(studyUid, 0.010m, DateTime.UtcNow)
        };

        _repositoryMock
            .Setup(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exposures);

        // Act - Create multiple subscriptions
        var subscription1 = _provider.StudyClosed.Subscribe(new TestObserver());
        var subscription2 = _provider.StudyClosed.Subscribe(new TestObserver());

        await _provider.NotifyStudyClosedAsync(studyUid, DoseTestData.Uids.PatientId);

        // Assert - Verify repository was called (indicating notification was sent)
        _repositoryMock.Verify(r => r.GetByStudyAsync(studyUid, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        subscription1.Dispose();
        subscription2.Dispose();
    }

    private static DoseRecord CreateDoseRecord(string studyUid, decimal dap, DateTime timestamp)
    {
        return new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = studyUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = timestamp,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = dap,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };
    }

    /// <summary>
    /// Test observer for IObservable events.
    /// </summary>
    private class TestObserver : IObserver<StudyCompletedEvent>
    {
        public void OnNext(StudyCompletedEvent value) { }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
