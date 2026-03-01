using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Dicom.Mpps;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.Mpps;

/// <summary>
/// Tests for DicomMppsClient following TDD methodology (RED-GREEN-REFACTOR).
/// SPEC-WORKFLOW-001 TASK-407: MPPS N-CREATE/N-SET
/// </summary>
public class DicomMppsClientTests
{
    private readonly Mock<ILogger<DicomMppsClient>> _loggerMock;
    private readonly Mock<IMppsScu> _mppsScuMock;

    public DicomMppsClientTests()
    {
        _loggerMock = new Mock<ILogger<DicomMppsClient>>();
        _mppsScuMock = new Mock<IMppsScu>();
    }

    [Fact]
    public async Task CreateMppsAsync_ShouldReturnSopInstanceUid_WhenCreateSucceeds()
    {
        // Arrange
        var client = new DicomMppsClient(_mppsScuMock.Object, _loggerMock.Object);
        var expectedSopUid = "1.2.840.10008.1.1.1.1.9999.1";

        _mppsScuMock
            .Setup(x => x.CreateProcedureStepAsync(It.IsAny<MppsData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSopUid);

        // Act
        var result = await client.CreateMppsAsync(
            "study-uid-001",
            "series-uid-001",
            "step-id-001",
            "Chest PA",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.SopInstanceUid.Should().Be(expectedSopUid);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CreateMppsAsync_ShouldReturnFailedResult_WhenCreateThrowsException()
    {
        // Arrange
        var client = new DicomMppsClient(_mppsScuMock.Object, _loggerMock.Object);

        _mppsScuMock
            .Setup(x => x.CreateProcedureStepAsync(It.IsAny<MppsData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DicomMppsException(0xA700, "MPPS SCP unavailable"));

        // Act
        var result = await client.CreateMppsAsync(
            "study-uid-001",
            "series-uid-001",
            "step-id-001",
            "Chest PA",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.SopInstanceUid.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateExposureCompleteAsync_ShouldSucceed_WhenMppsScuSucceeds()
    {
        // Arrange
        var client = new DicomMppsClient(_mppsScuMock.Object, _loggerMock.Object);
        var sopUid = "1.2.840.10008.1.1.1.1.9999.1";

        _mppsScuMock
            .Setup(x => x.SetProcedureStepInProgressAsync(
                It.IsAny<string>(),
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await client.UpdateExposureCompleteAsync(
            sopUid,
            "image-uid-001",
            "series-uid-001",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task UpdateExposureCompleteAsync_ShouldReturnFailedResult_WhenMppsScuThrowsException()
    {
        // Arrange
        var client = new DicomMppsClient(_mppsScuMock.Object, _loggerMock.Object);
        var sopUid = "1.2.840.10008.1.1.1.1.9999.1";

        _mppsScuMock
            .Setup(x => x.SetProcedureStepInProgressAsync(
                It.IsAny<string>(),
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DicomMppsException(0xA700, "MPPS SCP unavailable"));

        // Act
        var result = await client.UpdateExposureCompleteAsync(
            sopUid,
            "image-uid-001",
            "series-uid-001",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteStudyAsync_ShouldSucceed_WhenMppsScuSucceeds()
    {
        // Arrange
        var client = new DicomMppsClient(_mppsScuMock.Object, _loggerMock.Object);
        var sopUid = "1.2.840.10008.1.1.1.1.9999.1";

        _mppsScuMock
            .Setup(x => x.CompleteProcedureStepAsync(
                It.IsAny<string>(),
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await client.CompleteStudyAsync(
            sopUid,
            new[] { "image-uid-001", "image-uid-002" },
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CompleteStudyAsync_ShouldReturnFailedResult_WhenMppsScuThrowsException()
    {
        // Arrange
        var client = new DicomMppsClient(_mppsScuMock.Object, _loggerMock.Object);
        var sopUid = "1.2.840.10008.1.1.1.1.9999.1";

        _mppsScuMock
            .Setup(x => x.CompleteProcedureStepAsync(
                It.IsAny<string>(),
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DicomMppsException(0xA700, "MPPS SCP unavailable"));

        // Act
        var result = await client.CompleteStudyAsync(
            sopUid,
            new[] { "image-uid-001" },
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateExposureCompleteAsync_ShouldContinueWorkflow_WhenMppsUnavailable()
    {
        // Arrange
        var client = new DicomMppsClient(_mppsScuMock.Object, _loggerMock.Object);
        var sopUid = "1.2.840.10008.1.1.1.1.9999.1";

        _mppsScuMock
            .Setup(x => x.SetProcedureStepInProgressAsync(
                It.IsAny<string>(),
                It.IsAny<MppsData>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MPPS SCP not configured"));

        // Act
        var result = await client.UpdateExposureCompleteAsync(
            sopUid,
            "image-uid-001",
            "series-uid-001",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        // Error handling should allow workflow to continue (log and return false)
        result.ErrorMessage.Should().NotBeNullOrEmpty();

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
