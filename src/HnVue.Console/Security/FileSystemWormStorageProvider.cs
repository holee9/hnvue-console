using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HnVue.Console.Security.Models;
using HnVue.Console.Models;
using HnVue.Console.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HnVue.Console.Security;

/// <summary>
/// File system-based WORM (Write Once, Read Many) storage provider for audit logs.
/// SPEC-SECURITY-001: FR-SEC-06 - Audit Log Integrity with WORM storage.
///
/// Implementation uses Windows file attributes (ReadOnly/Archive) to simulate WORM semantics.
/// Files are written atomically using temporary files and then marked as read-only.
/// This provides OS-level protection against casual modification, though administrators
/// can still remove the read-only attribute (true WORM requires Azure Immutable Blob Storage).
/// </summary>
/// <param name="configuration">Application configuration.</param>
/// <param name="logger">Logger instance.</param>
public sealed partial class FileSystemWormStorageProvider(
    IConfiguration configuration,
    ILogger<FileSystemWormStorageProvider> logger) : IWormStorageProvider
{
    private const int RetentionYears = 6;
    private const string HashAlgorithmName = "SHA-256";

    private readonly string _auditDirectory = configuration["AuditLog:Directory"]
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HnVue",
            "AuditLogs");

    private readonly bool _simulateWorm = configuration.GetValue<bool>("AuditLog:WormSimulation", defaultValue: true);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writeLocks = new();

    /// <summary>
    /// Writes an audit log entry to WORM storage.
    /// Once written, the entry cannot be modified (WORM semantics enforced via read-only attributes).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the entry already exists (overwrite protection).
    /// </exception>
    public async Task WriteEntryAsync(WormEntry entry, CancellationToken ct)
    {
        var filePath = GetFilePath(entry.EntryId, entry.Timestamp);

        // @MX:ANCHOR - WORM write protection: prevent overwrites
        // This check ensures atomic write-once semantics by verifying file doesn't exist before writing
        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"Audit entry {entry.EntryId} already exists at {filePath}. WORM storage prohibits overwrites.");
        }

        // Acquire per-entry lock to prevent concurrent writes
        using var _ = await SemaphoreSlimExtensions.LockAsync(_writeLocks, entry.EntryId, ct);

        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        // Write atomically to temporary file
        var tempPath = filePath + ".tmp";
        var json = JsonSerializer.Serialize(entry, _jsonOptions);

        // @MX:NOTE - Using WriteThrough for immediate disk write
        // This ensures data is written to disk before returning, preventing data loss on crash
        await using (var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough | FileOptions.Asynchronous))
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(json), ct);
        }

        // Atomic rename for crash safety
        File.Move(tempPath, filePath, overwrite: false);

        // Set read-only attribute (WORM simulation)
        // @MX:WARN - This is OS-level protection, administrators can bypass
        // For production compliance, use AzureBlobWormStorageProvider instead
        if (_simulateWorm)
        {
            var fileInfo = new FileInfo(filePath);
            fileInfo.Attributes |= FileAttributes.ReadOnly | FileAttributes.Archive;
            logger.LogDebug("Set read-only attribute for WORM entry: {EntryId} at {FilePath}",
                entry.EntryId, filePath);
        }

        logger.LogInformation("WORM entry written: {EntryId} at {FilePath}", entry.EntryId, filePath);
    }

    /// <summary>
    /// Reads an audit log entry from WORM storage by entry ID.
    /// </summary>
    public async Task<WormEntry?> ReadEntryAsync(string entryId, CancellationToken ct)
    {
        // Search for entry file by scanning directories (most recent first)
        var auditBaseDir = new DirectoryInfo(_auditDirectory);
        if (!auditBaseDir.Exists)
        {
            return null;
        }

        // Pattern: YYYYMMDD/{timestamp}_{entryId}.audit or YYYYMMDD/{entryId}.audit
        // Search reverse chronological order (newest first)
        foreach (var dateDir in auditBaseDir.EnumerateDirectories()
            .OrderByDescending(d => d.Name))
        {
            var entryFiles = dateDir.EnumerateFiles($"*{entryId}.audit");
            foreach (var file in entryFiles)
            {
                return await ReadEntryFromFileAsync(file.FullName, ct);
            }
        }

        return null;
    }

    /// <summary>
    /// Queries audit log entries with optional filtering.
    /// </summary>
    public async Task<IReadOnlyList<WormEntry>> QueryEntriesAsync(
        AuditLogFilter filter,
        CancellationToken ct)
    {
        var results = new List<WormEntry>();
        var auditBaseDir = new DirectoryInfo(_auditDirectory);

        if (!auditBaseDir.Exists)
        {
            return results;
        }

        // Determine date range to scan
        var startDate = filter.StartDate ?? DateTimeOffset.MinValue;
        var endDate = filter.EndDate ?? DateTimeOffset.MaxValue;

        // Scan date directories within range
        foreach (var dateDir in auditBaseDir.EnumerateDirectories())
        {
            if (!DateTimeOffset.TryParseExact(dateDir.Name, "yyyyMMdd", null,
                DateTimeStyles.AssumeUniversal, out var dateDirDate))
            {
                continue;
            }

            // Skip directories outside filter range
            if (dateDirDate < startDate.Date || dateDirDate > endDate.Date.AddDays(1))
            {
                continue;
            }

            // Read all entry files in this directory
            foreach (var file in dateDir.EnumerateFiles("*.audit"))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var entry = await ReadEntryFromFileAsync(file.FullName, ct);
                    if (entry == null)
                    {
                        continue;
                    }

                    // Apply filter criteria
                    if (!MatchesFilter(entry, filter))
                    {
                        continue;
                    }

                    results.Add(entry);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to parse audit entry file: {FilePath}", file.FullName);
                }
            }
        }

        // Sort by timestamp descending (newest first)
        return results.OrderByDescending(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// Verifies the integrity of the audit log hash chain.
    /// </summary>
    public async Task<AuditVerificationResult> VerifyIntegrityAsync(CancellationToken ct)
    {
        var auditBaseDir = new DirectoryInfo(_auditDirectory);
        if (!auditBaseDir.Exists)
        {
            return new AuditVerificationResult
            {
                IsValid = true,
                Message = "No audit logs found to verify",
                EntriesVerified = 0
            };
        }

        var entries = new List<WormEntry>();
        var allDateDirs = auditBaseDir.EnumerateDirectories()
            .OrderBy(d => d.Name);

        // Load all entries in chronological order
        foreach (var dateDir in allDateDirs)
        {
            foreach (var file in dateDir.EnumerateFiles("*.audit").OrderBy(f => f.Name))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var entry = await ReadEntryFromFileAsync(file.FullName, ct);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Failed to parse audit entry during verification: {FilePath}", file.FullName);
                    return new AuditVerificationResult
                    {
                        IsValid = false,
                        BrokenAtEntryId = Path.GetFileNameWithoutExtension(file.Name),
                        Message = $"Corrupted audit entry: {file.FullName}",
                        EntriesVerified = entries.Count
                    };
                }
            }
        }

        if (entries.Count == 0)
        {
            return new AuditVerificationResult
            {
                IsValid = true,
                Message = "No audit entries to verify",
                EntriesVerified = 0
            };
        }

        // Verify hash chain
        string? previousHash = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            // Verify hash matches computed content hash
            var computedHash = ComputeEntryHash(entry);
            if (!string.Equals(computedHash, entry.CurrentEntryHash, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError(
                    "Hash mismatch for entry {EntryId}: expected {ComputedHash}, found {StoredHash}",
                    entry.EntryId, computedHash, entry.CurrentEntryHash);

                return new AuditVerificationResult
                {
                    IsValid = false,
                    BrokenAtEntryId = entry.EntryId,
                    Message = $"Hash mismatch for entry {entry.EntryId}",
                    EntriesVerified = entries.Count
                };
            }

            // Verify chain linkage
            if (i > 0 && !string.Equals(entry.PreviousEntryHash, previousHash, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError(
                    "Chain break at entry {EntryId}: expected previous hash {ExpectedHash}, found {ActualHash}",
                    entry.EntryId, previousHash, entry.PreviousEntryHash);

                return new AuditVerificationResult
                {
                    IsValid = false,
                    BrokenAtEntryId = entry.EntryId,
                    Message = $"Hash chain broken at entry {entry.EntryId}",
                    EntriesVerified = entries.Count
                };
            }

            previousHash = entry.CurrentEntryHash;
        }

        logger.LogInformation("Audit trail integrity verified: {EntryCount} entries", entries.Count);

        return new AuditVerificationResult
        {
            IsValid = true,
            Message = $"Audit trail integrity verified: {entries.Count} entries",
            EntriesVerified = entries.Count
        };
    }

    /// <summary>
    /// Enforces retention policy by removing entries older than the retention period.
    /// This is the ONLY allowed deletion operation in WORM storage.
    /// SPEC-SECURITY-001: 6-year retention policy for medical device compliance.
    /// </summary>
    public async Task<int> EnforceRetentionPolicyAsync(CancellationToken ct)
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddYears(-RetentionYears);
        var deletedCount = 0;
        var auditBaseDir = new DirectoryInfo(_auditDirectory);

        if (!auditBaseDir.Exists)
        {
            return 0;
        }

        // Scan date directories older than retention period
        foreach (var dateDir in auditBaseDir.EnumerateDirectories())
        {
            if (!DateTimeOffset.TryParseExact(dateDir.Name, "yyyyMMdd", null,
                DateTimeStyles.AssumeUniversal, out var dateDirDate))
            {
                continue;
            }

            // Delete entire directory if older than cutoff
            if (dateDirDate < cutoffDate.Date)
            {
                try
                {
                    logger.LogInformation("Deleting expired audit directory: {DirPath} (date: {Date})",
                        dateDir.FullName, dateDirDate);

                    dateDir.Delete(recursive: true);
                    deletedCount += (int)dateDir.EnumerateFiles("*.audit").Count();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete expired audit directory: {DirPath}", dateDir.FullName);
                }
            }
        }

        if (deletedCount > 0)
        {
            logger.LogInformation("Enforced retention policy: deleted {DeletedCount} entries older than {CutoffDate}",
                deletedCount, cutoffDate);
        }

        return await Task.FromResult(deletedCount);
    }

    /// <summary>
    /// Gets the file path for an audit entry.
    /// Format: {auditDirectory}\YYYYMMDD\{timestamp}_{entryId}.audit
    /// </summary>
    private static string GetFilePath(string entryId, DateTimeOffset timestamp)
    {
        var dateStr = timestamp.UtcDateTime.ToString("yyyyMMdd");
        var timestampStr = timestamp.UtcDateTime.ToString("yyyyMMddHHmmss");
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HnVue",
            "AuditLogs",
            dateStr,
            $"{timestampStr}_{entryId}.audit");
    }

    /// <summary>
    /// Reads an audit entry from a file.
    /// </summary>
    private async Task<WormEntry?> ReadEntryFromFileAsync(string filePath, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(filePath, ct);
        var entry = JsonSerializer.Deserialize<WormEntry>(json, _jsonOptions);

        if (entry == null)
        {
            logger.LogWarning("Failed to deserialize audit entry from: {FilePath}", filePath);
        }

        return entry;
    }

    /// <summary>
    /// Computes SHA-256 hash for an audit entry.
    /// </summary>
    /// <remarks>
    /// Hash computation excludes the CurrentEntryHash field itself.
    /// This ensures the hash represents the content, not the hash.
    /// </remarks>
    private static string ComputeEntryHash(WormEntry entry)
    {
        // Create a copy without the hash field for computation
        var hashInput = new
        {
            entry.EntryId,
            Timestamp = entry.Timestamp.ToUniversalTime().ToString("o"),
            entry.EventType,
            entry.UserId,
            entry.UserName,
            entry.EventDescription,
            entry.Outcome,
            entry.PatientId,
            entry.StudyId,
            entry.PreviousEntryHash,
            entry.NtpSynchronized,
            entry.WorkstationId
        };

        var json = JsonSerializer.Serialize(hashInput);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Checks if an entry matches the filter criteria.
    /// </summary>
    private static bool MatchesFilter(WormEntry entry, AuditLogFilter filter)
    {
        // Date range filter
        if (filter.StartDate.HasValue && entry.Timestamp < filter.StartDate.Value)
        {
            return false;
        }

        if (filter.EndDate.HasValue && entry.Timestamp > filter.EndDate.Value)
        {
            return false;
        }

        // Event type filter
        if (filter.EventType.HasValue && entry.EventType != (int)filter.EventType.Value)
        {
            return false;
        }

        // User filter
        if (!string.IsNullOrEmpty(filter.UserId) &&
            !string.Equals(entry.UserId, filter.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Patient filter
        if (!string.IsNullOrEmpty(filter.PatientId) &&
            !string.Equals(entry.PatientId, filter.PatientId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Outcome filter
        if (filter.Outcome.HasValue && entry.Outcome != (int)filter.Outcome.Value)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Extension methods for semaphore-based locking.
/// </summary>
internal static class SemaphoreSlimExtensions
{
    /// <summary>
    /// Acquires a semaphore lock using a key-based dictionary.
    /// </summary>
    public static async Task<IDisposable> LockAsync(
        ConcurrentDictionary<string, SemaphoreSlim> locks,
        string key,
        CancellationToken ct)
    {
        var semaphore = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(ct);
        return new LockReleaser(semaphore, key, locks);
    }

    /// <summary>
    /// Releases semaphore lock and removes from dictionary.
    /// </summary>
    private sealed class LockReleaser(
        SemaphoreSlim semaphore,
        string key,
        ConcurrentDictionary<string, SemaphoreSlim> locks) : IDisposable
    {
        public void Dispose()
        {
            semaphore.Release();
            // Clean up if no waiters
            if (semaphore.CurrentCount == 1)
            {
                locks.TryRemove(key, out _);
            }
        }
    }
}
