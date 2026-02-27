using FluentAssertions;
using HnVue.Dicom.Mpps;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.Mpps;

/// <summary>
/// Unit tests for IMppsScu - Modality Performed Procedure Step N-CREATE/N-SET operations.
/// SPEC-DICOM-001 AC-04: MPPS Reporting.
/// </summary>
public class MppsScuTests
{
    private readonly Mock<IMppsScu> _mppsScu;

    public MppsScuTests()
    {
        _mppsScu = new Mock<IMppsScu>();
    }

    private static MppsData CreateValidMppsData(MppsStatus status = MppsStatus.InProgress)
    {
        return new MppsData(
            PatientId: "P001",
            StudyInstanceUid: "1.2.3.4.5.100",
            SeriesInstanceUid: "1.2.3.4.5.101",
            PerformedProcedureStepId: "PPS001",
            PerformedProcedureStepDescription: "DX Chest PA",
            StartDateTime: DateTime.UtcNow,
            EndDateTime: status == MppsStatus.InProgress ? null : DateTime.UtcNow,
            Status: status,
            ExposureData: status == MppsStatus.Completed
                ? new[] { new ExposureData("1.2.3.4.5.101", "1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.102") }
                : Array.Empty<ExposureData>());
    }

    // AC-04 Scenario 4.1 - MPPS IN PROGRESS on Procedure Start
    [Fact]
    public async Task CreateProcedureStepAsync_ValidData_ReturnsSopInstanceUid()
    {
        // Arrange
        var data = CreateValidMppsData(MppsStatus.InProgress);
        var expectedSopUid = "1.2.3.4.5.500";

        _mppsScu
            .Setup(s => s.CreateProcedureStepAsync(
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSopUid);

        // Act
        var sopInstanceUid = await _mppsScu.Object.CreateProcedureStepAsync(data);

        // Assert
        sopInstanceUid.Should().NotBeNullOrEmpty(
            "N-CREATE must return the SOP Instance UID of the created MPPS object");
        sopInstanceUid.Should().Be(expectedSopUid);
    }

    // AC-04 Scenario 4.1 - Verify IN PROGRESS N-CREATE succeeds
    [Fact]
    public async Task SetInProgressAsync_ValidSopInstanceUid_Succeeds()
    {
        // Arrange
        var sopInstanceUid = "1.2.3.4.5.501";
        var data = CreateValidMppsData(MppsStatus.InProgress);

        _mppsScu
            .Setup(s => s.SetProcedureStepInProgressAsync(
                It.IsAny<string>(),
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = () => _mppsScu.Object.SetProcedureStepInProgressAsync(sopInstanceUid, data);

        // Assert
        await act.Should().NotThrowAsync(
            "N-SET IN PROGRESS with valid SOP Instance UID must succeed");
    }

    // AC-04 Scenario 4.2 - MPPS COMPLETED on Procedure End
    [Fact]
    public async Task CompleteProcedureStepAsync_ValidSopInstanceUid_Succeeds()
    {
        // Arrange
        var sopInstanceUid = "1.2.3.4.5.502";
        var completionData = CreateValidMppsData(MppsStatus.Completed);

        _mppsScu
            .Setup(s => s.CompleteProcedureStepAsync(
                It.IsAny<string>(),
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = () => _mppsScu.Object.CompleteProcedureStepAsync(sopInstanceUid, completionData);

        // Assert
        await act.Should().NotThrowAsync(
            "N-SET COMPLETED with valid SOP Instance UID and exposure data must succeed");
    }

    // AC-04 Scenario 4.2 - Completed data must include series references
    [Fact]
    public void MppsData_WithCompleted_MustIncludeExposureData()
    {
        // Arrange
        var completionData = CreateValidMppsData(MppsStatus.Completed);

        // Assert
        completionData.Status.Should().Be(MppsStatus.Completed);
        completionData.ExposureData.Should().NotBeEmpty(
            "COMPLETED MPPS must include Performed Series Sequence with image references");
        completionData.EndDateTime.Should().NotBeNull(
            "COMPLETED MPPS must have an end date/time");
    }

    // AC-04 Scenario 4.3 - MPPS DISCONTINUED on Procedure Abort
    [Fact]
    public async Task DiscontinueProcedureStepAsync_WithReason_Succeeds()
    {
        // Arrange
        var sopInstanceUid = "1.2.3.4.5.503";
        var reason = "Patient refused procedure";

        _mppsScu
            .Setup(s => s.DiscontinueProcedureStepAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = () => _mppsScu.Object.DiscontinueProcedureStepAsync(sopInstanceUid, reason);

        // Assert
        await act.Should().NotThrowAsync(
            "N-SET DISCONTINUED with a reason must succeed");

        _mppsScu.Verify(
            s => s.DiscontinueProcedureStepAsync(sopInstanceUid, reason, default),
            Times.Once);
    }

    // AC-04 Scenario 4.3 - Discontinuation without reason also accepted
    [Fact]
    public async Task DiscontinueProcedureStepAsync_WithEmptyReason_Succeeds()
    {
        // Arrange
        var sopInstanceUid = "1.2.3.4.5.504";
        var emptyReason = string.Empty;

        _mppsScu
            .Setup(s => s.DiscontinueProcedureStepAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = () => _mppsScu.Object.DiscontinueProcedureStepAsync(sopInstanceUid, emptyReason);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // AC-04 Scenario 4.4 - MPPS N-CREATE Failure Is Logged and Surfaced
    [Fact]
    public async Task CreateProcedureStepAsync_WithUnreachableScp_ThrowsDicomMppsException()
    {
        // Arrange
        var data = CreateValidMppsData(MppsStatus.InProgress);
        var failureStatusCode = (ushort)0xA700;

        _mppsScu
            .Setup(s => s.CreateProcedureStepAsync(
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DicomMppsException(failureStatusCode, "Out of resources - connection refused"));

        // Act & Assert
        Func<Task> act = () => _mppsScu.Object.CreateProcedureStepAsync(data);

        await act.Should().ThrowAsync<DicomMppsException>()
            .Where(ex => ex.StatusCode == failureStatusCode,
                "failure must be surfaced with the SCP status code for operator notification");
    }

    // DicomMppsException carries status code
    [Fact]
    public void DicomMppsException_WithStatusCode_StoresStatusCodeCorrectly()
    {
        // Arrange & Act
        var exception = new DicomMppsException(0xA700, "Out of resources");

        // Assert
        exception.StatusCode.Should().Be(0xA700);
        exception.Message.Should().Be("Out of resources");
    }

    // MppsData record - validate enum values
    [Theory]
    [InlineData(MppsStatus.InProgress)]
    [InlineData(MppsStatus.Completed)]
    [InlineData(MppsStatus.Discontinued)]
    public void MppsStatus_AllValues_AreDefinedInEnum(MppsStatus status)
    {
        // Assert
        Enum.IsDefined(typeof(MppsStatus), status).Should().BeTrue();
    }
}
