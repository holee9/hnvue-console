using FluentAssertions;
using HnVue.Dicom.Facade;
using HnVue.Dicom.Iod;
using HnVue.Dicom.Mpps;
using HnVue.Dicom.Rdsr;
using HnVue.Dicom.Worklist;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.Facade;

/// <summary>
/// Unit tests for IDicomServiceFacade - orchestration of DICOM builder and SCU operations.
/// SPEC-DICOM-001: Verifies that the facade correctly coordinates component interactions.
/// </summary>
public class DicomServiceFacadeTests
{
    private readonly Mock<IDicomServiceFacade> _facade;
    private readonly Mock<IRdsrDataProvider> _rdsrDataProvider;

    public DicomServiceFacadeTests()
    {
        _facade = new Mock<IDicomServiceFacade>();
        _rdsrDataProvider = new Mock<IRdsrDataProvider>();
    }

    private static DicomImageData CreateValidDxImageData()
    {
        return new DicomImageData
        {
            PatientId = "P001",
            PatientName = "Test^Patient",
            StudyInstanceUid = "1.2.3.4.5.100",
            SeriesInstanceUid = "1.2.3.4.5.101",
            SopInstanceUid = "1.2.3.4.5.102",
            Modality = "DX",
            Rows = 512,
            Columns = 512,
            PixelData = new byte[512 * 512 * 2]
        };
    }

    private static MppsData CreateValidMppsData()
    {
        return new MppsData(
            PatientId: "P001",
            StudyInstanceUid: "1.2.3.4.5.100",
            SeriesInstanceUid: "1.2.3.4.5.101",
            PerformedProcedureStepId: "PPS001",
            PerformedProcedureStepDescription: "DX Chest PA",
            StartDateTime: DateTime.UtcNow,
            EndDateTime: null,
            Status: MppsStatus.InProgress,
            ExposureData: Array.Empty<ExposureData>());
    }

    // StoreImageAsync_Orchestrates_BuilderAndStorageScu
    [Fact]
    public async Task StoreImageAsync_Orchestrates_BuilderAndStorageScu()
    {
        // Arrange
        var imageData = CreateValidDxImageData();
        var expectedSopUid = "1.2.3.4.5.102";

        _facade
            .Setup(f => f.StoreImageAsync(
                It.IsAny<DicomImageData>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSopUid);

        // Act
        var sopInstanceUid = await _facade.Object.StoreImageAsync(imageData);

        // Assert
        sopInstanceUid.Should().NotBeNullOrEmpty(
            "facade must return the SOP Instance UID after building and storing the image");
        sopInstanceUid.Should().Be(expectedSopUid);

        _facade.Verify(
            f => f.StoreImageAsync(imageData, default),
            Times.Once);
    }

    // StoreImageAsync_WithUnsupportedModality_ThrowsNotSupportedException
    [Fact]
    public async Task StoreImageAsync_WithUnsupportedModality_ThrowsNotSupportedException()
    {
        // Arrange
        var imageData = CreateValidDxImageData() with { Modality = "MR" }; // unsupported

        _facade
            .Setup(f => f.StoreImageAsync(
                It.Is<DicomImageData>(d => d.Modality == "MR"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("Modality MR is not supported"));

        // Act & Assert
        Func<Task> act = () => _facade.Object.StoreImageAsync(imageData);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*MR*");
    }

    // ExportStudyDoseAsync_Orchestrates_RdsrBuilderAndStorageScu
    [Fact]
    public async Task ExportStudyDoseAsync_Orchestrates_RdsrBuilderAndStorageScu()
    {
        // Arrange
        var studyInstanceUid = "1.2.3.4.5.100";

        var summary = new StudyDoseSummary
        {
            StudyInstanceUid = studyInstanceUid,
            PatientId = "P001",
            Modality = "DX",
            TotalDapGyCm2 = 15m,
            ExposureCount = 1,
            StudyStartTimeUtc = DateTime.UtcNow.AddHours(-1),
            StudyEndTimeUtc = DateTime.UtcNow
        };

        _rdsrDataProvider
            .Setup(p => p.GetStudyDoseSummaryAsync(studyInstanceUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        _rdsrDataProvider
            .Setup(p => p.GetStudyExposureRecordsAsync(studyInstanceUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DoseRecord>
            {
                new DoseRecord
                {
                    IrradiationEventUid = "1.2.3.4.5.700",
                    StudyInstanceUid = studyInstanceUid,
                    PatientId = "P001",
                    KvpValue = 80m,
                    MasValue = 10m,
                    CalculatedDapGyCm2 = 15m
                }
            }.AsReadOnly());

        var expectedResult = new RdsrExportResult
        {
            Success = true,
            RdsrSopInstanceUid = "1.2.3.4.5.800"
        };

        _facade
            .Setup(f => f.ExportStudyDoseAsync(
                It.IsAny<string>(),
                It.IsAny<IRdsrDataProvider>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _facade.Object.ExportStudyDoseAsync(studyInstanceUid, _rdsrDataProvider.Object);

        // Assert
        result.Success.Should().BeTrue();
        result.RdsrSopInstanceUid.Should().NotBeNullOrEmpty(
            "RDSR SOP Instance UID must be returned after successful export");
    }

    // FetchWorklistAsync - verify facade delegates to worklist SCU
    [Fact]
    public async Task FetchWorklistAsync_WithQuery_ReturnsList()
    {
        // Arrange
        var query = new WorklistQuery(ScheduledDate: DateRange.Today(), Modality: "DX");
        var expectedItems = new List<WorklistItem>
        {
            new WorklistItem(
                PatientId: "P001",
                PatientName: "Doe^John",
                BirthDate: null,
                PatientSex: "M",
                StudyInstanceUid: null,
                AccessionNumber: "ACC001",
                RequestedProcedureId: "REQ001",
                ScheduledProcedureStep: new ScheduledProcedureStep(
                    "SPS001", "DX Chest", DateTime.Today, null, "DX"))
        };

        _facade
            .Setup(f => f.FetchWorklistAsync(It.IsAny<WorklistQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItems);

        // Act
        var result = await _facade.Object.FetchWorklistAsync(query);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveCount(1);
    }

    // StartProcedureStepAsync - verify MPPS delegation
    [Fact]
    public async Task StartProcedureStepAsync_ValidData_ReturnsMppsUid()
    {
        // Arrange
        var mppsData = CreateValidMppsData();
        var expectedMppsUid = "1.2.3.4.5.500";

        _facade
            .Setup(f => f.StartProcedureStepAsync(
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMppsUid);

        // Act
        var mppsUid = await _facade.Object.StartProcedureStepAsync(mppsData);

        // Assert
        mppsUid.Should().NotBeNullOrEmpty();
        mppsUid.Should().Be(expectedMppsUid);
    }

    // CompleteProcedureStepAsync - verify MPPS N-SET delegation
    [Fact]
    public async Task CompleteProcedureStepAsync_ValidMppsUid_Succeeds()
    {
        // Arrange
        var mppsUid = "1.2.3.4.5.500";
        var completionData = new MppsData(
            PatientId: "P001",
            StudyInstanceUid: "1.2.3.4.5.100",
            SeriesInstanceUid: "1.2.3.4.5.101",
            PerformedProcedureStepId: "PPS001",
            PerformedProcedureStepDescription: "DX Chest PA",
            StartDateTime: DateTime.UtcNow.AddMinutes(-10),
            EndDateTime: DateTime.UtcNow,
            Status: MppsStatus.Completed,
            ExposureData: new[]
            {
                new ExposureData("1.2.3.4.5.101", "1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.102")
            });

        _facade
            .Setup(f => f.CompleteProcedureStepAsync(
                It.IsAny<string>(),
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = () => _facade.Object.CompleteProcedureStepAsync(mppsUid, completionData);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // RequestStorageCommitAsync - verify N-ACTION delegation
    [Fact]
    public async Task RequestStorageCommitAsync_ValidSopInstanceUids_ReturnsTransactionUid()
    {
        // Arrange
        var sopInstanceUids = new[] { "1.2.3.4.5.100", "1.2.3.4.5.101" };
        var expectedTransactionUid = "1.2.3.4.5.9001";

        _facade
            .Setup(f => f.RequestStorageCommitAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTransactionUid);

        // Act
        var transactionUid = await _facade.Object.RequestStorageCommitAsync(sopInstanceUids);

        // Assert
        transactionUid.Should().NotBeNullOrEmpty();
        transactionUid.Should().Be(expectedTransactionUid);
    }

    // ExportStudyDoseAsync with cancellation
    [Fact]
    public async Task ExportStudyDoseAsync_WithCancellation_PropagatesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _facade
            .Setup(f => f.ExportStudyDoseAsync(
                It.IsAny<string>(),
                It.IsAny<IRdsrDataProvider>(),
                It.Is<CancellationToken>(t => t.IsCancellationRequested)))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        Func<Task> act = () => _facade.Object.ExportStudyDoseAsync(
            "1.2.3.4.5.100",
            _rdsrDataProvider.Object,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // RdsrExportResult record - verify Success false case
    [Fact]
    public async Task ExportStudyDoseAsync_WhenRdsrBuildFails_ReturnsFailureResult()
    {
        // Arrange
        _facade
            .Setup(f => f.ExportStudyDoseAsync(
                It.IsAny<string>(),
                It.IsAny<IRdsrDataProvider>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RdsrExportResult
            {
                Success = false,
                ErrorMessage = "Failed to build RDSR: missing mandatory attributes"
            });

        // Act
        var result = await _facade.Object.ExportStudyDoseAsync(
            "1.2.3.4.5.999",
            _rdsrDataProvider.Object);

        // Assert
        result.Success.Should().BeFalse();
        result.RdsrSopInstanceUid.Should().BeNull("no SOP UID is returned on failure");
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
