using FluentAssertions;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Mpps;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HnVue.Dicom.Tests.Mpps;

/// <summary>
/// Unit tests for the actual MppsScu implementation class.
/// SPEC-DICOM-001 AC-04: MPPS Reporting.
/// Network tests use a closed port (fast failure); dataset tests use in-memory data.
/// </summary>
public class MppsScuTests
{
    private readonly DicomServiceOptions _optionsWithScp;
    private readonly DicomServiceOptions _optionsWithoutScp;

    public MppsScuTests()
    {
        _optionsWithScp = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            MppsScp = new DicomDestination
            {
                AeTitle = "MPPS_SCP",
                Host = "127.0.0.1",
                Port = 19994  // closed port - fast connection refused
            }
        };

        _optionsWithoutScp = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            MppsScp = null
        };
    }

    private MppsScu CreateSut(DicomServiceOptions? options = null)
    {
        return new MppsScu(
            Options.Create(options ?? _optionsWithScp),
            NullLogger<MppsScu>.Instance);
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

    // Constructor injection: MppsScu initializes correctly
    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        // Act
        Action act = () => CreateSut();

        // Assert
        act.Should().NotThrow("all required dependencies are provided");
    }

    // CreateProcedureStepAsync throws when MppsScp is null
    [Fact]
    public async Task CreateProcedureStepAsync_WithNullMppsScp_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithoutScp);
        var data = CreateValidMppsData(MppsStatus.InProgress);

        // Act & Assert
        await sut.Invoking(s => s.CreateProcedureStepAsync(data))
            .Should().ThrowAsync<InvalidOperationException>(
                "MppsScp must be configured before sending MPPS N-CREATE requests");
    }

    // SetProcedureStepInProgressAsync throws when MppsScp is null
    [Fact]
    public async Task SetProcedureStepInProgressAsync_WithNullMppsScp_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithoutScp);
        var data = CreateValidMppsData(MppsStatus.InProgress);

        // Act & Assert
        await sut.Invoking(s => s.SetProcedureStepInProgressAsync("1.2.3.4.5.500", data))
            .Should().ThrowAsync<InvalidOperationException>(
                "MppsScp must be configured before sending MPPS N-SET requests");
    }

    // CompleteProcedureStepAsync throws when MppsScp is null
    [Fact]
    public async Task CompleteProcedureStepAsync_WithNullMppsScp_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithoutScp);
        var data = CreateValidMppsData(MppsStatus.Completed);

        // Act & Assert
        await sut.Invoking(s => s.CompleteProcedureStepAsync("1.2.3.4.5.500", data))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // DiscontinueProcedureStepAsync throws when MppsScp is null
    [Fact]
    public async Task DiscontinueProcedureStepAsync_WithNullMppsScp_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithoutScp);

        // Act & Assert
        await sut.Invoking(s => s.DiscontinueProcedureStepAsync("1.2.3.4.5.500", "patient refused"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // CreateProcedureStepAsync with unreachable SCP propagates network exception
    [Fact]
    public async Task CreateProcedureStepAsync_WithUnreachableScp_ThrowsNetworkException()
    {
        // Arrange: closed port forces immediate connection refused
        var sut = CreateSut(_optionsWithScp);
        var data = CreateValidMppsData(MppsStatus.InProgress);

        // Act & Assert
        await sut.Invoking(s => s.CreateProcedureStepAsync(data))
            .Should().ThrowAsync<Exception>(
                "unreachable MPPS SCP causes a network exception to propagate to the caller");
    }

    // MppsData with Completed status must include ExposureData and EndDateTime
    [Fact]
    public void MppsData_WithCompleted_MustIncludeExposureDataAndEndTime()
    {
        // Arrange & Act
        var completionData = CreateValidMppsData(MppsStatus.Completed);

        // Assert
        completionData.Status.Should().Be(MppsStatus.Completed);
        completionData.ExposureData.Should().NotBeEmpty(
            "COMPLETED MPPS must include Performed Series Sequence with image references per FR-DICOM-04");
        completionData.EndDateTime.Should().NotBeNull(
            "COMPLETED MPPS must have an end date/time");
    }

    // MppsData with InProgress status has no EndDateTime and no ExposureData
    [Fact]
    public void MppsData_WithInProgress_HasNoEndTimeAndNoExposureData()
    {
        // Arrange & Act
        var inProgressData = CreateValidMppsData(MppsStatus.InProgress);

        // Assert
        inProgressData.Status.Should().Be(MppsStatus.InProgress);
        inProgressData.EndDateTime.Should().BeNull("IN PROGRESS MPPS has not completed yet");
        inProgressData.ExposureData.Should().BeEmpty("IN PROGRESS MPPS has no exposure data yet");
    }

    // MppsStatus enum defines all three values required by DICOM PS3.3
    [Theory]
    [InlineData(MppsStatus.InProgress)]
    [InlineData(MppsStatus.Completed)]
    [InlineData(MppsStatus.Discontinued)]
    public void MppsStatus_AllValues_AreDefinedInEnum(MppsStatus status)
    {
        // Assert
        Enum.IsDefined(typeof(MppsStatus), status).Should().BeTrue(
            $"MppsStatus.{status} must be defined per DICOM PS3.3 MPPS state machine");
    }

    // DicomMppsException carries the DICOM status code
    [Fact]
    public void DicomMppsException_WithStatusCode_StoresStatusCodeCorrectly()
    {
        // Arrange & Act
        var exception = new DicomMppsException(0xA700, "Out of resources");

        // Assert
        exception.StatusCode.Should().Be(0xA700);
        exception.Message.Should().Be("Out of resources");
    }

    // ExposureData record fields are accessible
    [Fact]
    public void ExposureData_WithValidValues_FieldsAreAccessible()
    {
        // Arrange & Act
        var exposure = new ExposureData(
            SeriesInstanceUid: "1.2.3.4.5.101",
            SopClassUid: "1.2.840.10008.5.1.4.1.1.1.1",
            SopInstanceUid: "1.2.3.4.5.102");

        // Assert
        exposure.SeriesInstanceUid.Should().Be("1.2.3.4.5.101");
        exposure.SopClassUid.Should().Be("1.2.840.10008.5.1.4.1.1.1.1");
        exposure.SopInstanceUid.Should().Be("1.2.3.4.5.102");
    }
}
