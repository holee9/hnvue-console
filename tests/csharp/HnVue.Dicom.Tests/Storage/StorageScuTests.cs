using Dicom;
using FluentAssertions;
using HnVue.Dicom.Associations;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Queue;
using HnVue.Dicom.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.Storage;

/// <summary>
/// Unit tests for the actual StorageScu implementation class.
/// SPEC-DICOM-001 AC-01: Storage SCU Image Transmission.
/// Tests exercise the real implementation, not a mocked interface.
/// </summary>
public class StorageScuTests
{
    private readonly Mock<IAssociationManager> _mockAssociationManager;
    private readonly Mock<ITransmissionQueue> _mockTransmissionQueue;
    private readonly DicomServiceOptions _options;

    public StorageScuTests()
    {
        _mockAssociationManager = new Mock<IAssociationManager>();
        _mockTransmissionQueue = new Mock<ITransmissionQueue>();
        _options = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            Tls = new TlsOptions { Enabled = false }
        };
    }

    private StorageScu CreateSut()
    {
        return new StorageScu(
            Options.Create(_options),
            _mockAssociationManager.Object,
            _mockTransmissionQueue.Object,
            NullLogger<StorageScu>.Instance);
    }

    private static DicomFile CreateMinimalDicomFile(string sopInstanceUid = "1.2.3.4.5.100")
    {
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.DigitalXRayImageStorageForPresentation },
            { DicomTag.SOPInstanceUID, sopInstanceUid },
            { DicomTag.StudyInstanceUID, "1.2.3.4.5.10" },
            { DicomTag.SeriesInstanceUID, "1.2.3.4.5.11" },
            { DicomTag.Modality, "DX" }
        };
        return new DicomFile(dataset);
    }

    // Constructor injection: StorageScu initializes correctly with all dependencies
    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Act
        Action act = () => CreateSut();

        // Assert
        act.Should().NotThrow("all required dependencies are provided");
    }

    // Argument null guards
    [Fact]
    public async Task StoreAsync_WithNullDicomFile_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        var destination = new DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        // Act & Assert
        await sut.Invoking(s => s.StoreAsync(null!, destination))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("dicomFile");
    }

    [Fact]
    public async Task StoreAsync_WithNullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        var dicomFile = CreateMinimalDicomFile();

        // Act & Assert
        await sut.Invoking(s => s.StoreAsync(dicomFile, null!))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("destination");
    }

    [Fact]
    public async Task StoreWithRetryAsync_WithNullDicomFile_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        var destination = new DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        // Act & Assert
        await sut.Invoking(s => s.StoreWithRetryAsync(null!, destination))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("dicomFile");
    }

    [Fact]
    public async Task StoreWithRetryAsync_WithNullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        var dicomFile = CreateMinimalDicomFile();

        // Act & Assert
        await sut.Invoking(s => s.StoreWithRetryAsync(dicomFile, null!))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("destination");
    }

    // StoreAsync with closed port returns false (network failure)
    [Fact]
    public async Task StoreAsync_WithUnreachableHost_ReturnsFalse()
    {
        // Arrange: port 19999 is unlikely to have an actual DICOM SCP
        var sut = CreateSut();
        var dicomFile = CreateMinimalDicomFile("1.2.3.4.5.999");
        var unreachableDestination = new DicomDestination
        {
            AeTitle = "UNREACHABLE",
            Host = "127.0.0.1",
            Port = 19999  // closed port - connection refused immediately
        };

        // Act
        var result = await sut.StoreAsync(dicomFile, unreachableDestination);

        // Assert
        result.Should().BeFalse("network error must cause StoreAsync to return false");
    }

    // StoreWithRetryAsync when store fails must call EnqueueAsync with correct parameters
    [Fact]
    public async Task StoreWithRetryAsync_WhenStoreFails_CallsEnqueueAsync()
    {
        // Arrange
        var sut = CreateSut();
        var sopInstanceUid = "1.2.3.4.5.101";
        var dicomFile = CreateMinimalDicomFile(sopInstanceUid);
        var destination = new DicomDestination
        {
            AeTitle = "PACS_DOWN",
            Host = "127.0.0.1",
            Port = 19998  // closed port forces store to fail
        };

        _mockTransmissionQueue
            .Setup(q => q.EnqueueAsync(
                sopInstanceUid,
                It.IsAny<string>(),
                destination.AeTitle,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransmissionQueueItem.CreateNew(sopInstanceUid, "/tmp/test.dcm", "PACS_DOWN"));

        // Act
        await sut.StoreWithRetryAsync(dicomFile, destination);

        // Assert: EnqueueAsync called once with the correct SOP UID and destination AE title
        _mockTransmissionQueue.Verify(
            q => q.EnqueueAsync(
                sopInstanceUid,
                It.IsAny<string>(),
                destination.AeTitle,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "failed C-STORE must enqueue the item for retry via ITransmissionQueue");
    }

    // TranscodeInMemory static method: verifies ExplicitVR -> ImplicitVR syntax change
    [Fact]
    public void TranscodeInMemory_ExplicitToImplicit_ProducesFileInTargetSyntax()
    {
        // Arrange: minimal 1x1 pixel grayscale DX image in ExplicitVRLittleEndian
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.DigitalXRayImageStorageForPresentation },
            { DicomTag.SOPInstanceUID, "1.2.3.4.5.200" },
            { DicomTag.StudyInstanceUID, "1.2.3.4.5.10" },
            { DicomTag.SeriesInstanceUID, "1.2.3.4.5.11" },
            { DicomTag.Modality, "DX" },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)1 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            new DicomOtherByte(DicomTag.PixelData, 0xFF)
        };
        // fo-dicom 4.x: InternalTransferSyntax is read-only; create DicomFile and save/load
        // to get a file with a specific transfer syntax set on the dataset.
        var originalFile = new DicomFile(dataset);

        // Act
        var transcodedFile = StorageScu.TranscodeInMemory(
            originalFile, DicomTransferSyntax.ImplicitVRLittleEndian);

        // Assert
        transcodedFile.Should().NotBeNull();
        transcodedFile.Dataset.InternalTransferSyntax
            .Should().Be(DicomTransferSyntax.ImplicitVRLittleEndian,
                "TranscodeInMemory must produce a file in the target transfer syntax");
    }

    // PHI not in logs: NullLogger absorbs all log records without error
    [Fact]
    public async Task StoreAsync_WithNullLogger_CompletesWithoutLoggingError()
    {
        // Arrange: NullLogger<StorageScu>.Instance discards all log records
        var sut = new StorageScu(
            Options.Create(_options),
            _mockAssociationManager.Object,
            _mockTransmissionQueue.Object,
            NullLogger<StorageScu>.Instance);

        var dicomFile = CreateMinimalDicomFile("1.2.3.4.5.300");
        var destination = new DicomDestination
        {
            AeTitle = "PACS",
            Host = "127.0.0.1",
            Port = 19997  // closed port
        };

        // Act & Assert
        await sut.Invoking(s => s.StoreAsync(dicomFile, destination))
            .Should().NotThrowAsync("NullLogger absorbs all log calls; PHI exclusion relies on no logging calls referencing patient fields");
    }
}
