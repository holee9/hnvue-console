using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using HnVue.Console.Security.Models;
using HnVue.Console.Models;
using HnVue.Console.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HnVue.Console.Security;

/// <summary>
/// Azure Blob Storage-based WORM (Write Once, Read Many) storage provider for audit logs.
/// SPEC-SECURITY-001: FR-SEC-06 - Audit Log Integrity with true WORM storage.
///
/// Implementation uses Azure Immutable Blob Storage with legal hold support.
/// This provides regulatory-compliant WORM semantics that cannot be bypassed even by administrators.
/// Files cannot be modified or deleted until the immutability policy expires.
///
/// Recommended for production environments requiring:
/// - SEC 17a-4 compliance (broker-dealer record keeping)
/// - FINRA compliance (financial industry regulatory)
/// - CFTC compliance (commodity futures trading)
/// - FDA 21 CFR Part 11 compliance (electronic records)
/// - MFDS 6-year retention (Korean medical device regulations)
/// </summary>
/// <param name="configuration">Application configuration.</param>
/// <param name="logger">Logger instance.</param>
public sealed class AzureBlobWormStorageProvider(
    IConfiguration configuration,
    ILogger<AzureBlobWormStorageProvider> logger) : IWormStorageProvider
{
    private const int DefaultRetentionYears = 6;

    private readonly BlobContainerClient _containerClient = CreateContainerClient(configuration);
    private readonly int _retentionYears = configuration.GetValue<int>(
        "AuditLog:AzureBlobStorage:RetentionYears",
        DefaultRetentionYears);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Writes an audit log entry to immutable Azure Blob Storage.
    /// Once written, the entry cannot be modified or deleted until retention expires (true WORM).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the entry already exists (overwrite protection).
    /// </exception>
    /// <exception cref="RequestFailedException">
    /// Thrown if Azure storage operation fails.
    /// </exception>
    public async Task WriteEntryAsync(WormEntry entry, CancellationToken ct)
    {
        var blobName = GetBlobName(entry.EntryId, entry.Timestamp);
        var blobClient = _containerClient.GetBlobClient(blobName);

        // @MX:ANCHOR - WORM write protection: check if blob exists before writing
        // This ensures true write-once semantics enforced by Azure Blob Storage
        if (await blobClient.ExistsAsync(cancellationToken: ct))
        {
            throw new InvalidOperationException(
                $"Audit entry {entry.EntryId} already exists at {blobName}. WORM storage prohibits overwrites.");
        }

        // Serialize entry to JSON
        var json = JsonSerializer.Serialize(entry, _jsonOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Upload blob without overwriting (true WORM)
        // @MX:NOTE - overwrite: false ensures write-once semantics
        await blobClient.UploadAsync(stream, overwrite: false, cancellationToken: ct);

        // Set immutability policy for regulatory compliance
        // @MX:ANCHOR - Immutability policy: core WORM enforcement mechanism
        // Once set, this blob cannot be modified or deleted until the policy expires
        var retentionDate = DateTimeOffset.UtcNow.AddYears(_retentionYears);

        try
        {
            await blobClient.SetImmutabilityPolicyAsync(
                immutabilityPolicy: new BlobImmutabilityPolicy { ExpiresOn = retentionDate },
                cancellationToken: ct);

            logger.LogInformation(
                "Set immutability policy for WORM entry {EntryId} until {RetentionDate}",
                entry.EntryId, retentionDate);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "BlobImmutableStorageNotEnabled")
        {
            logger.LogError(
                ex,
                "Immutable storage is not enabled on the storage account. " +
                "Please enable immutable storage with versioning to use WORM storage. " +
                "Storage Account: {StorageAccountName}",
                _containerClient.AccountName);

            throw new InvalidOperationException(
                "Azure Immutable Blob Storage is not enabled. " +
                "Please enable 'Immutable storage with versioning' on the storage account to use WORM storage.",
                ex);
        }

        logger.LogInformation("WORM entry written: {EntryId} to blob {BlobName}",
            entry.EntryId, blobName);
    }

    /// <summary>
    /// Reads an audit log entry from WORM storage by entry ID.
    /// </summary>
    /// <exception cref="RequestFailedException">
    /// Thrown if Azure storage operation fails.
    /// </exception>
    public async Task<WormEntry?> ReadEntryAsync(string entryId, CancellationToken ct)
    {
        // Search for blob by listing and filtering (most recent first)
        await foreach (var blob in _containerClient.GetBlobsAsync(
            prefix: "audit/",
            traits: BlobTraits.Metadata,
            cancellationToken: ct))
        {
            // Extract entry ID from blob name (format: audit/YYYY/MM/DD/{timestamp}_{entryId}.audit)
            var blobEntryId = ExtractEntryIdFromBlobName(blob.Name);
            if (string.Equals(blobEntryId, entryId, StringComparison.OrdinalIgnoreCase))
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken: ct);
                var json = response.Value.Content.ToString();

                var entry = JsonSerializer.Deserialize<WormEntry>(json, _jsonOptions);
                return entry;
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

        // Determine date range prefix for efficient querying
        // Azure Blob Storage supports prefix-based filtering for better performance
        var startDate = filter.StartDate ?? DateTimeOffset.MinValue;
        var endDate = filter.EndDate ?? DateTimeOffset.MaxValue;

        // Build prefix for date range filtering
        // Format: audit/YYYY/MM/
        var startPrefix = $"audit/{startDate.UtcDateTime:yyyy/MM}/";
        var endPrefix = $"audit/{endDate.UtcDateTime:yyyy/MM}/";

        await foreach (var blob in _containerClient.GetBlobsAsync(
            prefix: "audit/",
            traits: BlobTraits.Metadata,
            cancellationToken: ct))
        {
            ct.ThrowIfCancellationRequested();

            // Skip blobs outside date range
            if (!IsBlobInDateRange(blob.Name, startDate, endDate))
            {
                continue;
            }

            try
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken: ct);
                var json = response.Value.Content.ToString();

                var entry = JsonSerializer.Deserialize<WormEntry>(json, _jsonOptions);
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
                logger.LogWarning(ex, "Failed to parse audit blob: {BlobName}", blob.Name);
            }
            catch (RequestFailedException ex)
            {
                logger.LogWarning(ex, "Failed to download audit blob: {BlobName}", blob.Name);
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
        var entries = new List<WormEntry>();

        // Load all entries in chronological order
        await foreach (var blob in _containerClient.GetBlobsAsync(
            prefix: "audit/",
            traits: BlobTraits.Metadata,
            cancellationToken: ct))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken: ct);
                var json = response.Value.Content.ToString();

                var entry = JsonSerializer.Deserialize<WormEntry>(json, _jsonOptions);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to parse audit blob during verification: {BlobName}", blob.Name);
                return new AuditVerificationResult
                {
                    IsValid = false,
                    BrokenAtEntryId = ExtractEntryIdFromBlobName(blob.Name),
                    Message = $"Corrupted audit blob: {blob.Name}",
                    EntriesVerified = entries.Count
                };
            }
            catch (RequestFailedException ex)
            {
                logger.LogError(ex, "Failed to download audit blob during verification: {BlobName}", blob.Name);
                return new AuditVerificationResult
                {
                    IsValid = false,
                    BrokenAtEntryId = ExtractEntryIdFromBlobName(blob.Name),
                    Message = $"Failed to download blob: {blob.Name}",
                    EntriesVerified = entries.Count
                };
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

        // Sort entries by timestamp for hash chain verification
        entries = entries.OrderBy(e => e.Timestamp).ToList();

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
    /// Enforces retention policy by removing entries whose immutability policy has expired.
    /// This is the ONLY allowed deletion operation in WORM storage.
    /// SPEC-SECURITY-001: 6-year retention policy for medical device compliance.
    /// </summary>
    /// <remarks>
    /// With Azure Immutable Blob Storage, blobs cannot be deleted until the immutability policy expires.
    /// This method will only successfully delete blobs whose policy has already expired.
    /// </remarks>
    public async Task<int> EnforceRetentionPolicyAsync(CancellationToken ct)
    {
        var deletedCount = 0;
        var cutoffDate = DateTimeOffset.UtcNow.AddYears(-_retentionYears);

        // Scan all audit blobs
        await foreach (var blob in _containerClient.GetBlobsAsync(
            prefix: "audit/",
            traits: BlobTraits.Metadata | BlobTraits.ImmutabilityPolicy,
            cancellationToken: ct))
        {
            ct.ThrowIfCancellationRequested();

            // Check if blob is older than retention period
            if (blob.Properties.CreatedOn.HasValue &&
                blob.Properties.CreatedOn.Value < cutoffDate)
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);

                try
                {
                    // Check if immutability policy has expired
                    var blobProperties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
                    if (blobProperties.Value.ImmutabilityPolicy.ExpiresOn.HasValue &&
                        blobProperties.Value.ImmutabilityPolicy.ExpiresOn.Value <= DateTimeOffset.UtcNow)
                    {
                        logger.LogInformation(
                            "Deleting expired audit blob: {BlobName} (expired: {ExpirationDate})",
                            blob.Name,
                            blobProperties.Value.ImmutabilityPolicy.ExpiresOn.Value);

                        await blobClient.DeleteAsync(cancellationToken: ct);
                        deletedCount++;
                    }
                    else if (blobProperties.Value.ImmutabilityPolicy is not null)
                    {
                        logger.LogDebug(
                            "Skipping blob with active immutability policy: {BlobName} (expires: {ExpirationDate})",
                            blob.Name,
                            blobProperties.Value.ImmutabilityPolicy.ExpiresOn);
                    }
                    else
                    {
                        // No immutability policy (should not happen in WORM mode)
                        logger.LogWarning(
                            "Blob without immutability policy found: {BlobName}. Deleting.",
                            blob.Name);

                        await blobClient.DeleteAsync(cancellationToken: ct);
                        deletedCount++;
                    }
                }
                catch (RequestFailedException ex) when (ex.ErrorCode == "BlobImmutablePolicyCannotBeDeleted")
                {
                    logger.LogDebug(
                        "Cannot delete blob with active immutability policy: {BlobName}",
                        blob.Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete expired audit blob: {BlobName}", blob.Name);
                }
            }
        }

        if (deletedCount > 0)
        {
            logger.LogInformation(
                "Enforced retention policy: deleted {DeletedCount} entries older than {CutoffDate}",
                deletedCount, cutoffDate);
        }

        return deletedCount;
    }

    /// <summary>
    /// Creates the blob container client from configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if connection string is missing or invalid.
    /// </exception>
    private static BlobContainerClient CreateContainerClient(IConfiguration configuration)
    {
        var connectionString = configuration["AuditLog:AzureBlobStorage:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "AuditLog:AzureBlobStorage:ConnectionString is not configured. " +
                "Please set the connection string in appsettings.json or environment variables.");
        }

        var containerName = configuration["AuditLog:AzureBlobStorage:ContainerName"] ?? "hnvue-audit-logs";

        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        return containerClient;
    }

    /// <summary>
    /// Gets the blob name for an audit entry.
    /// Format: audit/YYYY/MM/DD/{timestamp}_{entryId}.audit
    /// </summary>
    private static string GetBlobName(string entryId, DateTimeOffset timestamp)
    {
        var year = timestamp.UtcDateTime.Year;
        var month = timestamp.UtcDateTime.ToString("MM");
        var day = timestamp.UtcDateTime.ToString("dd");
        var timestampStr = timestamp.UtcDateTime.ToString("yyyyMMddHHmmss");

        return $"audit/{year}/{month}/{day}/{timestampStr}_{entryId}.audit";
    }

    /// <summary>
    /// Extracts the entry ID from a blob name.
    /// </summary>
    private static string? ExtractEntryIdFromBlobName(string blobName)
    {
        // Format: audit/YYYY/MM/DD/{timestamp}_{entryId}.audit
        var lastSlash = blobName.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return null;
        }

        var fileName = blobName[(lastSlash + 1)..]; // Remove .audit extension
        var extensionIndex = fileName.IndexOf(".audit", StringComparison.OrdinalIgnoreCase);
        if (extensionIndex >= 0)
        {
            fileName = fileName[..extensionIndex];
        }

        // Extract entry ID after timestamp
        var underscoreIndex = fileName.LastIndexOf('_');
        if (underscoreIndex >= 0 && underscoreIndex < fileName.Length - 1)
        {
            return fileName[(underscoreIndex + 1)..];
        }

        return fileName;
    }

    /// <summary>
    /// Checks if a blob is within the specified date range.
    /// </summary>
    private static bool IsBlobInDateRange(string blobName, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        // Extract date from blob path: audit/YYYY/MM/DD/...
        var match = System.Text.RegularExpressions.Regex.Match(blobName, @"audit/(\d{4})/(\d{2})/(\d{2})/");
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out var year) ||
            !int.TryParse(match.Groups[2].Value, out var month) ||
            !int.TryParse(match.Groups[3].Value, out var day))
        {
            return false;
        }

        var blobDate = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        return blobDate >= startDate.Date && blobDate <= endDate.Date.AddDays(1);
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
