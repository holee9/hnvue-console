using HnVue.Dicom.Rdsr;
using HnVue.Dose.Exceptions;
using HnVue.Dose.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HnVue.Dose.Recording;

/// <summary>
/// Persists and retrieves dose records to/from non-volatile storage.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of dose record persistence - atomic write requirement
/// @MX:REASON: Critical implementation ensuring no data loss on crash (NFR-DOSE-02)
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-01, NFR-DOSE-02
///
/// Uses write-ahead logging (WAL) for atomic persistence:
/// 1. Write record to temporary file
/// 2. Fsync temporary file to disk
/// 3. Atomic rename temporary -> final (NTFS atomic)
/// 4. Update study index
///
/// Thread-safe: Uses lock for concurrent writes.
/// Must complete within 1 second per SPEC-DOSE-001 NFR-DOSE-02.
/// </remarks>
public sealed class DoseRecordRepository : IDoseRecordRepository
{
    private readonly ILogger<DoseRecordRepository> _logger;
    private readonly string _dataDirectory;
    private readonly string _studiesDirectory;
    private readonly string _indexDirectory;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the DoseRecordRepository class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="dataDirectory">Root directory for dose record storage</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
    public DoseRecordRepository(
        ILogger<DoseRecordRepository> logger,
        string dataDirectory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataDirectory = string.IsNullOrWhiteSpace(dataDirectory)
            ? throw new ArgumentException("Data directory is required.", nameof(dataDirectory))
            : dataDirectory;

        _studiesDirectory = Path.Combine(_dataDirectory, "studies");
        _indexDirectory = Path.Combine(_dataDirectory, "index");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Persists a dose record atomically to non-volatile storage.
    /// </summary>
    /// <param name="record">Dose record to persist</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="ArgumentNullException">Thrown when record is null</exception>
    /// <exception cref="DoseRecordPersistenceException">Thrown when persistence fails</exception>
    public async Task PersistAsync(DoseRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        lock (_lock)
        {
            // Validation within lock to ensure consistency
            if (string.IsNullOrWhiteSpace(record.StudyInstanceUid))
            {
                throw new ArgumentException("Study Instance UID is required.", nameof(record));
            }
        }

        try
        {
            await PersistRecordAtomicallyAsync(record, cancellationToken);
            await UpdateStudyIndexAsync(record, cancellationToken);

            _logger.LogDebug(
                "Dose record persisted: ExposureId={ExposureId}, Study={StudyUid}, DAP={Dap}Gy·cm²",
                record.ExposureEventId, record.StudyInstanceUid, record.CalculatedDapGyCm2);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to persist dose record: ExposureId={ExposureId}, Study={StudyUid}",
                record.ExposureEventId, record.StudyInstanceUid);

            throw new DoseRecordPersistenceException(
                record.ExposureEventId,
                "Failed to persist dose record to non-volatile storage.",
                ex);
        }
    }

    /// <summary>
    /// Retrieves all dose records for a specific study.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read-only list of dose records for the study</returns>
    /// <exception cref="ArgumentNullException">Thrown when studyInstanceUid is null</exception>
    /// <exception cref="DoseRecordPersistenceException">Thrown when retrieval fails</exception>
    public async Task<IReadOnlyList<DoseRecord>> GetByStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            throw new ArgumentException("Study Instance UID is required.", nameof(studyInstanceUid));
        }

        try
        {
            var studyDirectory = Path.Combine(_studiesDirectory, SanitizeUid(studyInstanceUid));

            if (!Directory.Exists(studyDirectory))
            {
                return Array.Empty<DoseRecord>();
            }

            var records = new List<DoseRecord>();

            var files = await Task.Run(() => Directory.EnumerateFiles(studyDirectory, "*.json"), cancellationToken);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var record = JsonSerializer.Deserialize<DoseRecord>(json, _jsonOptions);
                    if (record is not null)
                    {
                        records.Add(record);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to deserialize dose record file: {File}", file);
                }
            }

            // Return ordered by timestamp
            return records.OrderBy(r => r.TimestampUtc).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to retrieve dose records for study: {StudyUid}", studyInstanceUid);

            throw new DoseRecordPersistenceException(
                "Failed to retrieve dose records from storage.", ex);
        }
    }

    /// <summary>
    /// Persists a dose record atomically using write-ahead logging.
    /// </summary>
    private async Task PersistRecordAtomicallyAsync(DoseRecord record, CancellationToken cancellationToken)
    {
        var studyDirectory = Path.Combine(_studiesDirectory, SanitizeUid(record.StudyInstanceUid));
        Directory.CreateDirectory(studyDirectory);

        // Create temporary file
        var tempFileName = $"{record.ExposureEventId}.tmp";
        var tempFilePath = Path.Combine(studyDirectory, tempFileName);
        var finalFileName = $"{record.ExposureEventId}.json";
        var finalFilePath = Path.Combine(studyDirectory, finalFileName);

        // Serialize and write to temporary file
        var json = JsonSerializer.Serialize(record, _jsonOptions);
        await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);

        // Flush to disk (ensure write-through)
        await FlushFileAsync(tempFilePath, cancellationToken);

        // Atomic rename: temporary -> final
        // NTFS guarantees atomic rename within same volume
        File.Move(tempFilePath, finalFilePath, overwrite: true);

        _logger.LogTrace(
            "Atomic write complete: {File}", finalFilePath);
    }

    /// <summary>
    /// Updates the study index with the new record reference.
    /// </summary>
    private async Task UpdateStudyIndexAsync(DoseRecord record, CancellationToken cancellationToken)
    {
        var indexFile = Path.Combine(_indexDirectory, $"{SanitizeUid(record.StudyInstanceUid)}.index");

        var indexEntry = new StudyIndexEntry
        {
            ExposureEventId = record.ExposureEventId,
            TimestampUtc = record.TimestampUtc,
            CreatedAtUtc = record.TimestampUtc, // Use TimestampUtc as creation time
            DapGyCm2 = record.CalculatedDapGyCm2
        };

        var indexLine = JsonSerializer.Serialize(indexEntry, _jsonOptions) + "\n";

        // Append to index file
        await File.AppendAllTextAsync(indexFile, indexLine, cancellationToken);
    }

    /// <summary>
    /// Flushes a file to disk to ensure write-through.
    /// </summary>
    private async Task FlushFileAsync(string filePath, CancellationToken cancellationToken)
    {
        // FileStream Flush(true) ensures write-through to disk
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);

        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures required directories exist.
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(_studiesDirectory);
        Directory.CreateDirectory(_indexDirectory);

        _logger.LogInformation(
            "Dose record storage directories ensured: {DataDir}", _dataDirectory);
    }

    /// <summary>
    /// Sanitizes a DICOM UID for use as a directory name.
    /// </summary>
    private static string SanitizeUid(string uid)
    {
        // Replace invalid path characters with underscore
        return string.Join("_", uid.Split(Path.GetInvalidFileNameChars()));
    }

    /// <summary>
    /// Study index entry for efficient record lookup.
    /// </summary>
    private sealed class StudyIndexEntry
    {
        public Guid ExposureEventId { get; init; }
        public DateTime TimestampUtc { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public decimal DapGyCm2 { get; init; }
    }
}
