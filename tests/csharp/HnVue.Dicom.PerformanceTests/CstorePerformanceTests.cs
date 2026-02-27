using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Dicom;
using DicomNetwork = Dicom.Network;
using DicomClient = Dicom.Network.Client.DicomClient;
using HnVue.Dicom.Associations;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Queue;
using HnVue.Dicom.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace HnVue.Dicom.PerformanceTests;

/// <summary>
/// Performance benchmarks for C-STORE operations.
/// SPEC-DICOM-001 NFR-PERF-01: C-STORE 50MB DX image within 10 seconds (100 Mbit/s).
/// Tests different Transfer Syntaxes to measure encoding and transmission performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[StopOnFirstError]
public class CstorePerformanceTests
{
    private StorageScu? _storageScu;
    private DicomFile? _largeDicomFile;
    private DicomDestination _destination = null!;
    private const int ImageSizeBytes = 50 * 1024 * 1024; // 50 MB
    private const int TargetSeconds = 10;

    [GlobalSetup]
    public void Setup()
    {
        // Configure storage SCU with a mock destination
        // Note: For actual benchmarking, ensure a DICOM SCP is running on localhost:11112
        var options = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_PERF_TEST",
            Tls = new TlsOptions { Enabled = false }
        };

        _storageScu = new StorageScu(
            Options.Create(options),
            new NullAssociationManager(),
            new NullTransmissionQueue(),
            NullLogger<StorageScu>.Instance);

        _destination = new DicomDestination
        {
            AeTitle = "ORTHANC",
            Host = "localhost",
            Port = 11112
        };

        // Create a 50MB DX image (simulated)
        _largeDicomFile = CreateLargeDicomFile(ImageSizeBytes);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // No cleanup needed for in-memory mocks
    }

    /// <summary>
    /// Benchmark: In-memory transcoding performance (measures codec overhead).
    /// This benchmark runs without requiring a running DICOM SCP.
    /// </summary>
    [Benchmark]
    public DicomFile TranscodeInMemory_ImplicitToJpeg2000Lossless()
    {
        if (_largeDicomFile == null)
            throw new InvalidOperationException("Setup not completed");

        return StorageScu.TranscodeInMemory(_largeDicomFile, DicomTransferSyntax.JPEG2000Lossless);
    }

    /// <summary>
    /// Benchmark: In-memory transcoding to Explicit VR Little Endian.
    /// </summary>
    [Benchmark]
    public DicomFile TranscodeInMemory_ImplicitToExplicitVR()
    {
        if (_largeDicomFile == null)
            throw new InvalidOperationException("Setup not completed");

        return StorageScu.TranscodeInMemory(_largeDicomFile, DicomTransferSyntax.ExplicitVRLittleEndian);
    }

    /// <summary>
    /// Benchmark: 50MB DICOM file creation (pixel data generation).
    /// </summary>
    [Benchmark]
    public DicomFile CreateLargeDicomFile_50MB()
    {
        return CreateLargeDicomFile(ImageSizeBytes);
    }

    /// <summary>
    /// Benchmark: C-STORE with Implicit VR Little Endian (baseline).
    /// Target: Complete within 10 seconds for 50MB image.
    /// Note: Requires a running DICOM SCP on localhost:11112.
    /// </summary>
    [Benchmark]
    public async Task<bool> StoreAsync_ImplicitVRLittleEndian()
    {
        if (_storageScu == null || _largeDicomFile == null)
            throw new InvalidOperationException("Setup not completed");

        var sw = Stopwatch.StartNew();
        var result = await _storageScu.StoreAsync(_largeDicomFile, _destination);
        sw.Stop();

        if (sw.Elapsed.TotalSeconds > TargetSeconds)
        {
            throw new InvalidOperationException(
                $"NFR-PERF-01 violation: C-STORE took {sw.Elapsed.TotalSeconds:F2}s " +
                $"(target: {TargetSeconds}s)");
        }

        return result;
    }

    /// <summary>
    /// Benchmark: C-STORE with Explicit VR Little Endian.
    /// Note: Requires a running DICOM SCP on localhost:11112.
    /// </summary>
    [Benchmark]
    public async Task<bool> StoreAsync_ExplicitVRLittleEndian()
    {
        if (_storageScu == null || _largeDicomFile == null)
            throw new InvalidOperationException("Setup not completed");

        var transcodedFile = StorageScu.TranscodeInMemory(_largeDicomFile, DicomTransferSyntax.ExplicitVRLittleEndian);

        var sw = Stopwatch.StartNew();
        var result = await _storageScu.StoreAsync(transcodedFile, _destination);
        sw.Stop();

        if (sw.Elapsed.TotalSeconds > TargetSeconds)
        {
            throw new InvalidOperationException(
                $"NFR-PERF-01 violation: C-STORE took {sw.Elapsed.TotalSeconds:F2}s " +
                $"(target: {TargetSeconds}s)");
        }

        return result;
    }

    private DicomFile CreateLargeDicomFile(int sizeBytes)
    {
        // Calculate pixel data size for a 16-bit grayscale image
        var pixelDataSize = sizeBytes - 2048;
        var rows = 5000;
        var columns = 4000;

        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.DigitalXRayImageStorageForPresentation },
            { DicomTag.SOPInstanceUID, DicomUID.Generate().UID },
            { DicomTag.StudyInstanceUID, DicomUID.Generate().UID },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate().UID },
            { DicomTag.Modality, "DX" },
            { DicomTag.PatientID, "PERF_TEST_PATIENT" },
            { DicomTag.PatientName, "Performance^Test" },
            { DicomTag.PatientBirthDate, "19900101" },
            { DicomTag.PatientSex, "O" },
            { DicomTag.AccessionNumber, "PERF_ACC_001" },
            { DicomTag.StudyDate, DateTime.Now.ToString("yyyyMMdd") },
            { DicomTag.StudyTime, DateTime.Now.ToString("HHmmss") },
            { DicomTag.Rows, (ushort)rows },
            { DicomTag.Columns, (ushort)columns },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)16 },
            { DicomTag.HighBit, (ushort)15 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            { DicomTag.PixelSpacing, new[] { "0.1", "0.1" } }
        };

        // Generate pixel data (fill with pattern)
        var pixelData = new byte[pixelDataSize];
        for (int i = 0; i < pixelDataSize; i++)
        {
            pixelData[i] = (byte)(i % 256);
        }

        dataset.AddOrUpdate(DicomTag.PixelData, pixelData);

        return new DicomFile(dataset);
    }

    // No-op implementations for benchmark isolation
    private sealed class NullAssociationManager : IAssociationManager
    {
        public Task<DicomClient> CreateAssociationAsync(
            DicomDestination destination,
            List<DicomNetwork.DicomPresentationContext> presentationContexts,
            CancellationToken cancellationToken = default)
        {
            var tlsEnabled = destination.TlsEnabled ?? false;
            var client = new DicomClient(
                destination.Host,
                destination.Port,
                tlsEnabled,
                "HNVUE_PERF_TEST",
                destination.AeTitle);
            return Task.FromResult(client);
        }

        public Task CloseAssociationAsync(DicomClient client, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public (bool IsValid, List<string> Errors) ValidateDestination(DicomDestination destination)
        {
            if (string.IsNullOrWhiteSpace(destination.AeTitle))
                return (false, new List<string> { "AE Title is required" });
            if (string.IsNullOrWhiteSpace(destination.Host))
                return (false, new List<string> { "Host is required" });
            if (destination.Port < 1 || destination.Port > 65535)
                return (false, new List<string> { "Port must be between 1 and 65535" });
            return (true, new List<string>());
        }
    }

    private sealed class NullTransmissionQueue : ITransmissionQueue
    {
        public Task<TransmissionQueueItem> EnqueueAsync(
            string sopInstanceUid,
            string filePath,
            string destinationAeTitle,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TransmissionQueueItem.CreateNew(sopInstanceUid, filePath, destinationAeTitle));
        }

        public Task<TransmissionQueueItem?> DequeueNextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<TransmissionQueueItem?>(null);
        }

        public Task UpdateStatusAsync(
            Guid id,
            QueueItemStatus newStatus,
            int attemptCount,
            DateTimeOffset? nextRetryAt,
            string? lastError,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }
}
