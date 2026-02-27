using System.Diagnostics;
using Dicom;
using FluentAssertions;
using HnVue.Dicom.Associations;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.IntegrationTests.TestData;
using HnVue.Dicom.Queue;
using HnVue.Dicom.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Dicom.IntegrationTests.Storage;

/// <summary>
/// Integration tests for Storage SCU using real Orthanc DICOM SCP.
/// Tests C-STORE image transmission with various transfer syntaxes.
/// SPEC-DICOM-001: FR-DICOM-01, FR-DICOM-02
/// </summary>
[Collection("Orthanc")]
public class StorageScuIntegrationTests : IDisposable
{
    private readonly OrthancFixture _orthanc;
    private readonly IStorageScu _storageScu;
    private readonly ITestOutputHelper _output;
    private readonly DicomServiceOptions _options;
    private readonly TransmissionQueue _transmissionQueue;

    public StorageScuIntegrationTests(OrthancFixture orthanc, ITestOutputHelper output)
    {
        _orthanc = orthanc;
        _output = output;

        _options = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            UidRoot = "2.25",
            DeviceSerial = "HNVUE_IT_001",
            AssociationPool = new AssociationPoolOptions
            {
                MaxSize = 2,
                AcquisitionTimeoutMs = 30000,
                IdleTimeoutMs = 30000
            },
            RetryQueue = new RetryQueueOptions
            {
                StoragePath = Path.Combine(Path.GetTempPath(), "HnVue_IT_Queue")
            },
            Timeouts = new TimeoutOptions
            {
                AssociationRequestMs = 5000,
                DimseOperationMs = 30000,
                SocketReceiveMs = 60000,
                SocketSendMs = 60000
            }
        };

        // Create StorageScu with real dependencies
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        var associationManager = new AssociationManager(
            Options.Create(_options),
            loggerFactory.CreateLogger<AssociationManager>());

        _transmissionQueue = new TransmissionQueue(
            Options.Create(_options),
            loggerFactory.CreateLogger<TransmissionQueue>());

        _storageScu = new StorageScu(
            Options.Create(_options),
            associationManager,
            _transmissionQueue,
            loggerFactory.CreateLogger<StorageScu>());
    }

    public void Dispose()
    {
        // Clean up any stored instances after each test
        try
        {
            _orthanc.DeleteAllInstancesAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore cleanup errors
        }

        // Clean up queue
        try
        {
            _transmissionQueue.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore cleanup errors
        }

        // Clean up temp queue directory
        try
        {
            var queueDir = _options.RetryQueue.StoragePath;
            if (Directory.Exists(queueDir))
            {
                Directory.Delete(queueDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Test: C-STORE a DX image to Orthanc and verify it was stored correctly.
    /// Transfer syntax: Explicit VR Little Endian (default).
    /// </summary>
    [Fact]
    public async Task StoreAsync_DxImage_StoresSuccessfully()
    {
        // Arrange
        var sopInstanceUid = DicomUID.Generate().UID;
        var dicomFile = TestDicomFiles.CreateDxImage(sopInstanceUid, "INTEGRATION^TEST", "IT001");
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort
        };

        _output.WriteLine($"Storing DICOM file with SOP Instance UID: {sopInstanceUid}");
        _output.WriteLine($"Destination: {destination.AeTitle} @ {destination.Host}:{destination.Port}");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _storageScu.StoreAsync(dicomFile, destination);
        stopwatch.Stop();

        _output.WriteLine($"C-STORE completed in {stopwatch.ElapsedMilliseconds}ms with result: {result}");

        // Assert
        result.Should().BeTrue("C-STORE should succeed for valid DX image");

        // Verify the instance was stored in Orthanc
        var instances = await _orthanc.GetInstancesAsync();
        instances.Should().NotBeEmpty("Orthanc should contain at least one instance");

        var instanceInfo = await _orthanc.GetInstanceInfoAsync(instances[0]);
        var tags = await _orthanc.GetInstanceTagsAsync(instances[0]);

        tags.RootElement.TryGetProperty("SOPInstanceUID", out var sopTag).Should().BeTrue();
        sopTag.GetString().Should().Be(sopInstanceUid, "stored SOP Instance UID should match");
    }

    /// <summary>
    /// Test: C-STORE a DX image with JPEG 2000 Lossless transfer syntax.
    /// Verifies transfer syntax negotiation and transcoding.
    /// </summary>
    [Fact]
    public async Task StoreAsync_Jpeg2000Lossless_StoresSuccessfully()
    {
        // Arrange
        var sopInstanceUid = DicomUID.Generate().UID;
        var dicomFile = TestDicomFiles.CreateJpeg2000Lossless(sopInstanceUid);
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort
        };

        _output.WriteLine($"Testing JPEG 2000 Lossless: {sopInstanceUid}");

        // Act
        var result = await _storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeTrue("JPEG 2000 Lossless C-STORE should succeed");

        var instances = await _orthanc.GetInstancesAsync();
        instances.Should().ContainSingle("exactly one instance should be stored");
    }

    /// <summary>
    /// Test: C-STORE a DX image with JPEG Lossless transfer syntax.
    /// Verifies transfer syntax negotiation and transcoding.
    /// </summary>
    [Fact]
    public async Task StoreAsync_JpegLossless_StoresSuccessfully()
    {
        // Arrange
        var sopInstanceUid = DicomUID.Generate().UID;
        var dicomFile = TestDicomFiles.CreateJpegLossless(sopInstanceUid);
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort
        };

        _output.WriteLine($"Testing JPEG Lossless: {sopInstanceUid}");

        // Act
        var result = await _storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeTrue("JPEG Lossless C-STORE should succeed");

        var instances = await _orthanc.GetInstancesAsync();
        instances.Should().ContainSingle("exactly one instance should be stored");
    }

    /// <summary>
    /// Test: C-STORE a DX image with Implicit VR Little Endian transfer syntax.
    /// Verifies basic DICOM association and storage.
    /// </summary>
    [Fact]
    public async Task StoreAsync_ImplicitVRLittleEndian_StoresSuccessfully()
    {
        // Arrange
        var sopInstanceUid = DicomUID.Generate().UID;
        var dicomFile = TestDicomFiles.CreateImplicitVRLittleEndian(sopInstanceUid);
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort
        };

        _output.WriteLine($"Testing Implicit VR Little Endian: {sopInstanceUid}");

        // Act
        var result = await _storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeTrue("Implicit VR Little Endian C-STORE should succeed");

        var instances = await _orthanc.GetInstancesAsync();
        instances.Should().ContainSingle("exactly one instance should be stored");
    }

    /// <summary>
    /// Test: C-STORE a CR image to Orthanc.
    /// Verifies support for different SOP classes.
    /// </summary>
    [Fact]
    public async Task StoreAsync_CrImage_StoresSuccessfully()
    {
        // Arrange
        var sopInstanceUid = DicomUID.Generate().UID;
        var dicomFile = TestDicomFiles.CreateCrImage(sopInstanceUid);
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort
        };

        _output.WriteLine($"Testing CR image: {sopInstanceUid}");

        // Act
        var result = await _storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeTrue("C-STORE should succeed for CR image");

        var instances = await _orthanc.GetInstancesAsync();
        instances.Should().ContainSingle("exactly one instance should be stored");
    }

    /// <summary>
    /// Test: C-STORE multiple images sequentially.
    /// Verifies association reuse and connection pooling.
    /// </summary>
    [Fact]
    public async Task StoreAsync_MultipleImages_AllStoredSuccessfully()
    {
        // Arrange
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort
        };

        var sopInstanceUids = new List<string>();
        const int imageCount = 5;

        // Act
        var results = new List<bool>();
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < imageCount; i++)
        {
            var sopInstanceUid = DicomUID.Generate().UID;
            sopInstanceUids.Add(sopInstanceUid);

            var dicomFile = TestDicomFiles.CreateDxImage(sopInstanceUid, $"PATIENT^{i:D3}", $"ID{i:D3}");
            var result = await _storageScu.StoreAsync(dicomFile, destination);
            results.Add(result);

            _output.WriteLine($"Stored image {i + 1}/{imageCount}: {sopInstanceUid} - Success: {result}");
        }

        stopwatch.Stop();
        _output.WriteLine($"Stored {imageCount} images in {stopwatch.ElapsedMilliseconds}ms");

        // Assert
        results.Should().OnlyContain(r => r, "all C-STORE operations should succeed");

        var instances = await _orthanc.GetInstancesAsync();
        instances.Should().HaveCount(imageCount, "all images should be stored in Orthanc");
    }

    /// <summary>
    /// Test: C-STORE to wrong AE title fails gracefully.
    /// Verifies error handling for invalid association parameters.
    /// </summary>
    [Fact]
    public async Task StoreAsync_WrongAeTitle_ReturnsFalse()
    {
        // Arrange
        var sopInstanceUid = DicomUID.Generate().UID;
        var dicomFile = TestDicomFiles.CreateDxImage(sopInstanceUid);
        var destination = new DicomDestination
        {
            AeTitle = "WRONG_AET",  // Orthanc uses "ORTHANC" by default
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort
        };

        _output.WriteLine($"Testing wrong AE title: {destination.AeTitle}");

        // Act
        var result = await _storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeFalse("C-STORE should fail for wrong AE title");

        var instances = await _orthanc.GetInstancesAsync();
        instances.Should().BeEmpty("no instances should be stored");
    }

    /// <summary>
    /// Test: C-STORE to wrong port fails gracefully.
    /// Verifies error handling for network errors.
    /// </summary>
    [Fact]
    public async Task StoreAsync_WrongPort_ReturnsFalse()
    {
        // Arrange
        var sopInstanceUid = DicomUID.Generate().UID;
        var dicomFile = TestDicomFiles.CreateDxImage(sopInstanceUid);
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort + 1000  // Wrong port
        };

        _output.WriteLine($"Testing wrong port: {destination.Port}");

        // Act
        var result = await _storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeFalse("C-STORE should fail for wrong port");
    }

    /// <summary>
    /// Test: Verify stored object matches transmitted object.
    /// Compares key DICOM tags between original and stored instances.
    /// </summary>
    [Fact]
    public async Task StoreAsync_VerifyStoredObject_MatchesTransmitted()
    {
        // Arrange
        var patientName = "VERIFY^PATIENT";
        var patientId = "VERIFY123";
        var sopInstanceUid = DicomUID.Generate().UID;

        var dicomFile = TestDicomFiles.CreateDxImage(sopInstanceUid, patientName, patientId);
        var destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = _orthanc.HostAddress,
            Port = _orthanc.HostDicomPort
        };

        // Act
        var result = await _storageScu.StoreAsync(dicomFile, destination);

        // Assert
        result.Should().BeTrue();

        var instances = await _orthanc.GetInstancesAsync();
        instances.Should().ContainSingle();

        var tags = await _orthanc.GetInstanceTagsAsync(instances[0]);

        tags.RootElement.TryGetProperty("PatientName", out var storedPatientName).Should().BeTrue();
        storedPatientName.GetString().Should().Be(patientName);

        tags.RootElement.TryGetProperty("PatientID", out var storedPatientId).Should().BeTrue();
        storedPatientId.GetString().Should().Be(patientId);

        tags.RootElement.TryGetProperty("SOPInstanceUID", out var storedSopInstanceUid).Should().BeTrue();
        storedSopInstanceUid.GetString().Should().Be(sopInstanceUid);

        tags.RootElement.TryGetProperty("Modality", out var modality).Should().BeTrue();
        modality.GetString().Should().Be("DX");
    }
}

/// <summary>
/// xUnit logger provider for test output.
/// </summary>
internal class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// xUnit logger that writes to test output.
/// </summary>
internal class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception}");
        }
    }
}
