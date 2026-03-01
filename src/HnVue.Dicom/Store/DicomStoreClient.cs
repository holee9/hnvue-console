using System;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using HnVue.Dicom.Configuration;
using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Storage;

/// <summary>
/// High-level client for DICOM C-STORE operations (PACS export).
/// Provides retry queue and error handling for image export.
/// </summary>
/// <remarks>
/// @MX:WARN Retry logic - Client wraps storage SCU with retry queue
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-408
///
/// Features:
/// - C-STORE for DICOM images
/// - Retry queue (3 retries, exponential backoff)
/// - Export status tracking
/// - Error notification
/// </remarks>
public sealed class DicomStoreClient
{
    private readonly IStorageScu _storageScu;
    private readonly IPacsExportQueue _exportQueue;
    private readonly ILogger<DicomStoreClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomStoreClient"/> class.
    /// </summary>
    /// <param name="storageScu">The underlying storage SCU implementation.</param>
    /// <param name="exportQueue">The retry queue for failed exports.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DicomStoreClient(
        IStorageScu storageScu,
        IPacsExportQueue exportQueue,
        ILogger<DicomStoreClient> logger)
    {
        _storageScu = storageScu ?? throw new ArgumentNullException(nameof(storageScu));
        _exportQueue = exportQueue ?? throw new ArgumentNullException(nameof(exportQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Exports a DICOM file to the specified PACS destination.
    /// </summary>
    /// <param name="dicomFile">The DICOM file to export.</param>
    /// <param name="destination">The target PACS destination.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="PacsExportResult"/> indicating the export status.
    /// </returns>
    /// <remarks>
    /// @MX:WARN Retry logic - Failed exports are enqueued for retry
    ///
    /// On failure, the export is enqueued for retry with exponential backoff.
    /// </remarks>
    public async Task<PacsExportResult> ExportToPacsAsync(
        DicomFile dicomFile,
        DicomDestination destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dicomFile);
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            var sopInstanceUid = dicomFile.Dataset.GetString(DicomTag.SOPInstanceUID);

            _logger.LogInformation(
                "Exporting to PACS (SopInstanceUid: {SopInstanceUid}, Destination: {AeTitle}@{Host}:{Port})",
                sopInstanceUid,
                destination.AeTitle,
                destination.Host,
                destination.Port);

            var success = await _storageScu.StoreAsync(dicomFile, destination, cancellationToken);

            if (success)
            {
                _logger.LogInformation(
                    "PACS export succeeded (SopInstanceUid: {SopInstanceUid})",
                    sopInstanceUid);

                return PacsExportResult.Success();
            }

            // Export failed - enqueue for retry
            _logger.LogWarning(
                "PACS export failed, enqueuing for retry (SopInstanceUid: {SopInstanceUid})",
                sopInstanceUid);

            await EnqueueForRetryAsync(dicomFile, destination, cancellationToken);

            return PacsExportResult.Failure(
                $"PACS export failed for {sopInstanceUid}, enqueued for retry");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "PACS export failed unexpectedly");

            await EnqueueForRetryAsync(dicomFile, destination, cancellationToken);

            return PacsExportResult.Failure(
                $"PACS export failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Enqueues a failed export for retry.
    /// </summary>
    /// <remarks>
    /// @MX:WARN Retry logic - Enqueue with exponential backoff
    /// </remarks>
    private async Task EnqueueForRetryAsync(
        DicomFile dicomFile,
        DicomDestination destination,
        CancellationToken cancellationToken)
    {
        try
        {
            // Save DICOM file to temporary location if needed
            var filePath = GetOrSaveTempFile(dicomFile);

            var exportItem = PacsExportItem.Create(
                filePath,
                destination.AeTitle,
                destination.Host,
                destination.Port);

            await _exportQueue.EnqueueAsync(exportItem, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to enqueue export for retry");
        }
    }

    /// <summary>
    /// Gets or saves the DICOM file to a temporary location.
    /// </summary>
    private string GetOrSaveTempFile(DicomFile dicomFile)
    {
        // If the file has a path, use it
        if (!string.IsNullOrWhiteSpace(dicomFile.File?.Name))
        {
            return dicomFile.File.Name;
        }

        // Otherwise save to temp directory
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HnVue",
            "DicomTemp",
            "Export");

        Directory.CreateDirectory(tempDir);

        var sopInstanceUid = dicomFile.Dataset.GetString(DicomTag.SOPInstanceUID);
        var tempPath = Path.Combine(tempDir, $"{sopInstanceUid}.dcm");
        dicomFile.Save(tempPath);

        return tempPath;
    }
}
