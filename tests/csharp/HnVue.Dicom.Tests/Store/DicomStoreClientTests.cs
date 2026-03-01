using System;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using FluentAssertions;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.Store;

/// <summary>
/// Tests for DicomStoreClient following TDD methodology (RED-GREEN-REFACTOR).
/// SPEC-WORKFLOW-001 TASK-408: C-STORE PACS Export
/// </summary>
public class DicomStoreClientTests
{
    private readonly Mock<ILogger<DicomStoreClient>> _loggerMock;
    private readonly Mock<IStorageScu> _storageScuMock;
    private readonly Mock<IPacsExportQueue> _exportQueueMock;

    public DicomStoreClientTests()
    {
        _loggerMock = new Mock<ILogger<DicomStoreClient>>();
        _storageScuMock = new Mock<IStorageScu>();
        _exportQueueMock = new Mock<IPacsExportQueue>();
    }

    [Fact]
    public async Task ExportToPacsAsync_ShouldReturnSuccess_WhenStoreSucceeds()
    {
        // Arrange
        var client = new DicomStoreClient(
            _storageScuMock.Object,
            _exportQueueMock.Object,
            _loggerMock.Object);

        var dicomFile = CreateTestDicomFile();
        var destination = new HnVue.Dicom.Configuration.DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        _storageScuMock
            .Setup(x => x.StoreAsync(It.IsAny<DicomFile>(), It.IsAny<DicomDestination>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await client.ExportToPacsAsync(dicomFile, destination, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();

        _exportQueueMock.Verify(
            x => x.EnqueueAsync(It.IsAny<PacsExportItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExportToPacsAsync_ShouldEnqueueForRetry_WhenStoreFails()
    {
        // Arrange
        var client = new DicomStoreClient(
            _storageScuMock.Object,
            _exportQueueMock.Object,
            _loggerMock.Object);

        var dicomFile = CreateTestDicomFile();
        var destination = new HnVue.Dicom.Configuration.DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        _storageScuMock
            .Setup(x => x.StoreAsync(It.IsAny<DicomFile>(), It.IsAny<DicomDestination>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await client.ExportToPacsAsync(dicomFile, destination, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();

        _exportQueueMock.Verify(
            x => x.EnqueueAsync(It.IsAny<PacsExportItem>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToPacsAsync_ShouldUseRetryQueue_WhenConfigured()
    {
        // Arrange
        var client = new DicomStoreClient(
            _storageScuMock.Object,
            _exportQueueMock.Object,
            _loggerMock.Object);

        var dicomFile = CreateTestDicomFile();
        var destination = new HnVue.Dicom.Configuration.DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        _storageScuMock
            .Setup(x => x.StoreAsync(It.IsAny<DicomFile>(), It.IsAny<DicomDestination>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await client.ExportToPacsAsync(dicomFile, destination, CancellationToken.None);

        // Assert
        _exportQueueMock.Verify(
            x => x.EnqueueAsync(It.IsAny<PacsExportItem>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToPacsAsync_ShouldTrackExportStatus()
    {
        // Arrange
        var client = new DicomStoreClient(
            _storageScuMock.Object,
            _exportQueueMock.Object,
            _loggerMock.Object);

        var dicomFile = CreateTestDicomFile();
        var destination = new HnVue.Dicom.Configuration.DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        _storageScuMock
            .Setup(x => x.StoreAsync(It.IsAny<DicomFile>(), It.IsAny<DicomDestination>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await client.ExportToPacsAsync(dicomFile, destination, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ExportStatus.Should().Be(PacsExportStatus.Succeeded);
    }

    [Fact]
    public async Task ExportToPacsAsync_ShouldNotifyOnFailure()
    {
        // Arrange
        var client = new DicomStoreClient(
            _storageScuMock.Object,
            _exportQueueMock.Object,
            _loggerMock.Object);

        var dicomFile = CreateTestDicomFile();
        var destination = new HnVue.Dicom.Configuration.DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        _storageScuMock
            .Setup(x => x.StoreAsync(It.IsAny<DicomFile>(), It.IsAny<DicomDestination>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await client.ExportToPacsAsync(dicomFile, destination, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ExportStatus.Should().Be(PacsExportStatus.Failed);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    private static DicomFile CreateTestDicomFile()
    {
        var dataset = new DicomDataset();
        dataset.AddOrUpdate(DicomTag.SOPClassUID, "1.2.840.10008.5.1.4.1.1.1");
        dataset.AddOrUpdate(DicomTag.SOPInstanceUID, "1.2.840.10008.1.1.1.1.9999.1");
        return new DicomFile(dataset);
    }
}
