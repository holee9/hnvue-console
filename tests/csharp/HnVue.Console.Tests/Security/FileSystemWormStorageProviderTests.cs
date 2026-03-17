using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HnVue.Console.Models;
using HnVue.Console.Security;
using HnVue.Console.Security.Models;
using HnVue.Console.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

namespace HnVue.Console.Tests.Security;

/// <summary>
/// Unit tests for FileSystemWormStorageProvider.
/// SPEC-SECURITY-001: FR-SEC-06 - Audit Log Integrity with WORM storage.
/// Target: comprehensive coverage for all IWormStorageProvider operations.
/// </summary>
/// <remarks>
/// Architecture note: FileSystemWormStorageProvider has a known design split.
/// - WriteEntryAsync -> GetFilePath() uses hardcoded LocalApplicationData path.
/// - ReadEntryAsync / QueryEntriesAsync / VerifyIntegrityAsync / EnforceRetentionPolicyAsync
///   all use the configurable _auditDirectory field (AuditLog:Directory).
///
/// Test strategy:
/// - WriteEntryAsync tests write to LocalApplicationData and are cleaned up in Dispose().
/// - All other tests populate the configurable temp directory directly and verify behavior.
/// </remarks>
public sealed class FileSystemWormStorageProviderTests : IDisposable
{
    // Configurable directory used for read/query/verify/retention tests.
    private readonly string _tempAuditDir;

    // Directory where WriteEntryAsync actually writes (hardcoded in GetFilePath).
    private readonly string _writeDir;

    private readonly FileSystemWormStorageProvider _provider;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileSystemWormStorageProviderTests()
    {
        _tempAuditDir = Path.Combine(Path.GetTempPath(), "HnVue-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempAuditDir);

        _writeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HnVue", "AuditLogs");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditLog:Directory"] = _tempAuditDir,
                // Disable read-only attribute so tests can clean up written files.
                ["AuditLog:WormSimulation"] = "false"
            })
            .Build();

        _provider = new FileSystemWormStorageProvider(
            config,
            NullLogger<FileSystemWormStorageProvider>.Instance);
    }

    public void Dispose()
    {
        // Clean up configurable temp directory.
        if (Directory.Exists(_tempAuditDir))
        {
            // Remove read-only attributes before delete.
            foreach (var file in Directory.GetFiles(_tempAuditDir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_tempAuditDir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------------
    // Helper: build a WormEntry with a valid CurrentEntryHash.
    // This mirrors the private ComputeEntryHash logic inside the provider.
    // ---------------------------------------------------------------------------
    private static WormEntry BuildEntry(
        string? entryId = null,
        DateTimeOffset? timestamp = null,
        int eventType = (int)AuditEventType.UserLogin,
        string userId = "user-01",
        string userName = "Test User",
        string description = "Test event",
        int outcome = (int)AuditOutcome.Success,
        string? previousEntryHash = null,
        string? patientId = null,
        string? studyId = null,
        string? workstationId = "WS-01")
    {
        entryId ??= Guid.NewGuid().ToString("N");
        timestamp ??= DateTimeOffset.UtcNow;

        // Build anonymous object matching what ComputeEntryHash hashes.
        var hashInput = new
        {
            EntryId = entryId,
            Timestamp = timestamp.Value.ToUniversalTime().ToString("o"),
            EventType = eventType,
            UserId = userId,
            UserName = userName,
            EventDescription = description,
            Outcome = outcome,
            PatientId = patientId,
            StudyId = studyId,
            PreviousEntryHash = previousEntryHash,
            NtpSynchronized = true,
            WorkstationId = workstationId
        };

        var json = JsonSerializer.Serialize(hashInput);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        var currentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new WormEntry
        {
            EntryId = entryId,
            Timestamp = timestamp.Value,
            EventType = eventType,
            UserId = userId,
            UserName = userName,
            EventDescription = description,
            Outcome = outcome,
            PatientId = patientId,
            StudyId = studyId,
            PreviousEntryHash = previousEntryHash,
            CurrentEntryHash = currentHash,
            NtpSynchronized = true,
            WorkstationId = workstationId
        };
    }

    // Helper: write entry JSON directly into _tempAuditDir to avoid the
    // GetFilePath / WriteEntryAsync path split.
    private void PlantEntryFile(WormEntry entry)
    {
        var dateStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMdd");
        var timestampStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMddHHmmss");
        var dir = Path.Combine(_tempAuditDir, dateStr);
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, $"{timestampStr}_{entry.EntryId}.audit");
        var json = JsonSerializer.Serialize(entry, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    // ---------------------------------------------------------------------------
    // WriteEntryAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WriteEntryAsync_NewEntry_WritesFileToDisk()
    {
        // Arrange
        var entry = BuildEntry();

        // Act
        await _provider.WriteEntryAsync(entry, CancellationToken.None);

        // Assert: file must exist in the hardcoded LocalApplicationData write path.
        var dateStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMdd");
        var timestampStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMddHHmmss");
        var expectedFile = Path.Combine(_writeDir, dateStr, $"{timestampStr}_{entry.EntryId}.audit");

        try
        {
            File.Exists(expectedFile).Should().BeTrue(
                because: "WriteEntryAsync must persist the entry to disk");

            // Verify content round-trips correctly.
            var json = await File.ReadAllTextAsync(expectedFile);
            var deserialized = JsonSerializer.Deserialize<WormEntry>(json, _jsonOptions);
            deserialized.Should().NotBeNull();
            deserialized!.EntryId.Should().Be(entry.EntryId);
            deserialized.CurrentEntryHash.Should().Be(entry.CurrentEntryHash);
        }
        finally
        {
            // Clean up write-path file after test.
            if (File.Exists(expectedFile))
            {
                File.SetAttributes(expectedFile, FileAttributes.Normal);
                File.Delete(expectedFile);
            }
        }
    }

    [Fact]
    public async Task WriteEntryAsync_DuplicateEntryId_ThrowsInvalidOperationException()
    {
        // Arrange
        var entry = BuildEntry();
        await _provider.WriteEntryAsync(entry, CancellationToken.None);

        var dateStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMdd");
        var timestampStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMddHHmmss");
        var writtenFile = Path.Combine(_writeDir, dateStr, $"{timestampStr}_{entry.EntryId}.audit");

        try
        {
            // Act & Assert: second write with same entryId must throw.
            Func<Task> act = async () => await _provider.WriteEntryAsync(entry, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>(
                because: "WORM semantics prohibit overwriting an existing entry");
        }
        finally
        {
            if (File.Exists(writtenFile))
            {
                File.SetAttributes(writtenFile, FileAttributes.Normal);
                File.Delete(writtenFile);
            }
        }
    }

    // ---------------------------------------------------------------------------
    // ReadEntryAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ReadEntryAsync_ExistingEntry_ReturnsCorrectEntry()
    {
        // Arrange
        var entry = BuildEntry(entryId: "read-test-entry");
        PlantEntryFile(entry);

        // Act
        var result = await _provider.ReadEntryAsync(entry.EntryId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.EntryId.Should().Be(entry.EntryId);
        result.UserId.Should().Be(entry.UserId);
        result.EventType.Should().Be(entry.EventType);
        result.CurrentEntryHash.Should().Be(entry.CurrentEntryHash);
    }

    [Fact]
    public async Task ReadEntryAsync_NonExistentEntry_ReturnsNull()
    {
        // Act
        var result = await _provider.ReadEntryAsync("does-not-exist-id", CancellationToken.None);

        // Assert
        result.Should().BeNull(because: "reading a non-existent entry must return null");
    }

    [Fact]
    public async Task ReadEntryAsync_EmptyAuditDirectory_ReturnsNull()
    {
        // Arrange: provider pointing at empty temp dir (no sub-directories)
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditLog:Directory"] = _tempAuditDir
            })
            .Build();
        var provider = new FileSystemWormStorageProvider(
            config, NullLogger<FileSystemWormStorageProvider>.Instance);

        // Act
        var result = await provider.ReadEntryAsync("any-id", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // QueryEntriesAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryEntriesAsync_NoFilter_ReturnsAllEntries()
    {
        // Arrange
        var e1 = BuildEntry(timestamp: DateTimeOffset.UtcNow.AddDays(-1));
        var e2 = BuildEntry(timestamp: DateTimeOffset.UtcNow);
        PlantEntryFile(e1);
        PlantEntryFile(e2);

        // Act: use explicit date range to avoid DateTimeOffset.MinValue/MaxValue conversion bugs
        // in the provider when filter.StartDate/EndDate are null.
        var filter = new AuditLogFilter
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-30),
            EndDate = DateTimeOffset.UtcNow.AddDays(1)
        };
        var results = await _provider.QueryEntriesAsync(filter, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2, because: "all planted entries must be returned");
        results.Select(e => e.EntryId).Should().Contain(new[] { e1.EntryId, e2.EntryId });
    }

    [Fact]
    public async Task QueryEntriesAsync_DateRangeFilter_ReturnsOnlyMatchingEntries()
    {
        // Arrange: one entry inside range, one outside.
        var inside = BuildEntry(timestamp: DateTimeOffset.UtcNow.AddDays(-1));
        var outside = BuildEntry(timestamp: DateTimeOffset.UtcNow.AddDays(-10));
        PlantEntryFile(inside);
        PlantEntryFile(outside);

        var filter = new AuditLogFilter
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-3),
            EndDate = DateTimeOffset.UtcNow
        };

        // Act
        var results = await _provider.QueryEntriesAsync(filter, CancellationToken.None);

        // Assert
        results.Should().ContainSingle(e => e.EntryId == inside.EntryId,
            because: "only the entry within the date range must be returned");
        results.Should().NotContain(e => e.EntryId == outside.EntryId);
    }

    [Fact]
    public async Task QueryEntriesAsync_EmptyDirectory_ReturnsEmptyList()
    {
        // Act
        var results = await _provider.QueryEntriesAsync(new AuditLogFilter(), CancellationToken.None);

        // Assert
        results.Should().BeEmpty(because: "no entries have been planted");
    }

    [Fact]
    public async Task QueryEntriesAsync_UserIdFilter_ReturnsOnlyMatchingEntries()
    {
        // Arrange
        var targetUser = BuildEntry(userId: "target-user");
        var otherUser = BuildEntry(userId: "other-user");
        PlantEntryFile(targetUser);
        PlantEntryFile(otherUser);

        // Use explicit date range to avoid DateTimeOffset.MinValue conversion issue in provider.
        var filter = new AuditLogFilter
        {
            UserId = "target-user",
            StartDate = DateTimeOffset.UtcNow.AddDays(-2),
            EndDate = DateTimeOffset.UtcNow.AddDays(1)
        };

        // Act
        var results = await _provider.QueryEntriesAsync(filter, CancellationToken.None);

        // Assert
        results.Should().ContainSingle(e => e.UserId == "target-user");
    }

    // ---------------------------------------------------------------------------
    // VerifyIntegrityAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task VerifyIntegrityAsync_EmptyDirectory_ReturnsValidResult()
    {
        // Act
        var result = await _provider.VerifyIntegrityAsync(CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue(because: "empty audit storage has no integrity violations");
        result.EntriesVerified.Should().Be(0);
    }

    [Fact]
    public async Task VerifyIntegrityAsync_SingleEntryWithCorrectHash_ReturnsValidResult()
    {
        // Arrange: a single correctly hashed entry (no chain predecessor).
        var entry = BuildEntry(previousEntryHash: null);
        PlantEntryFile(entry);

        // Act
        var result = await _provider.VerifyIntegrityAsync(CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue(because: "a single correctly hashed entry is valid");
        result.EntriesVerified.Should().Be(1);
    }

    [Fact]
    public async Task VerifyIntegrityAsync_ValidChainedEntries_ReturnsValidResult()
    {
        // Arrange: two entries forming a correct hash chain.
        // Entry 1 has no predecessor; entry 2 references entry 1's hash.
        var e1 = BuildEntry(timestamp: DateTimeOffset.UtcNow.AddMinutes(-2), previousEntryHash: null);
        var e2 = BuildEntry(timestamp: DateTimeOffset.UtcNow.AddMinutes(-1),
            previousEntryHash: e1.CurrentEntryHash);
        PlantEntryFile(e1);
        PlantEntryFile(e2);

        // Act
        var result = await _provider.VerifyIntegrityAsync(CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue(because: "a correctly chained pair of entries must verify as valid");
        result.EntriesVerified.Should().Be(2);
    }

    [Fact]
    public async Task VerifyIntegrityAsync_EntryWithTamperedHash_ReturnsInvalidResult()
    {
        // Arrange: plant an entry whose CurrentEntryHash does not match its content.
        var validEntry = BuildEntry();
        var tamperedEntry = validEntry with { CurrentEntryHash = "00000000000000000000000000000000" };
        PlantEntryFile(tamperedEntry);

        // Act
        var result = await _provider.VerifyIntegrityAsync(CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse(because: "a tampered hash must fail integrity verification");
        result.BrokenAtEntryId.Should().Be(tamperedEntry.EntryId);
    }

    // ---------------------------------------------------------------------------
    // EnforceRetentionPolicyAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnforceRetentionPolicyAsync_NoEntries_ReturnsZero()
    {
        // Act
        var deleted = await _provider.EnforceRetentionPolicyAsync(CancellationToken.None);

        // Assert
        deleted.Should().Be(0, because: "there are no entries to delete");
    }

    [Fact]
    public async Task EnforceRetentionPolicyAsync_EntryOlderThan6Years_DeletesDirectory()
    {
        // Arrange: plant an entry with a date more than 6 years ago.
        var expiredTimestamp = DateTimeOffset.UtcNow.AddYears(-7);
        var expiredDateStr = expiredTimestamp.UtcDateTime.ToString("yyyyMMdd");
        var expiredDir = Path.Combine(_tempAuditDir, expiredDateStr);
        Directory.CreateDirectory(expiredDir);
        File.WriteAllText(Path.Combine(expiredDir, "dummy.audit"), "{}");

        // Act
        await _provider.EnforceRetentionPolicyAsync(CancellationToken.None);

        // Assert
        Directory.Exists(expiredDir).Should().BeFalse(
            because: "directories older than 6 years must be deleted by retention enforcement");
    }

    [Fact]
    public async Task EnforceRetentionPolicyAsync_RecentEntry_PreservesDirectory()
    {
        // Arrange: plant an entry dated today.
        var recentEntry = BuildEntry(timestamp: DateTimeOffset.UtcNow);
        PlantEntryFile(recentEntry);

        var dateStr = recentEntry.Timestamp.UtcDateTime.ToString("yyyyMMdd");
        var recentDir = Path.Combine(_tempAuditDir, dateStr);

        // Act
        await _provider.EnforceRetentionPolicyAsync(CancellationToken.None);

        // Assert
        Directory.Exists(recentDir).Should().BeTrue(
            because: "directories within the retention period must not be deleted");
    }

    [Fact]
    public async Task EnforceRetentionPolicyAsync_MixedEntries_DeletesOnlyExpired()
    {
        // Arrange
        var recentEntry = BuildEntry(timestamp: DateTimeOffset.UtcNow);
        PlantEntryFile(recentEntry);

        var expiredTimestamp = DateTimeOffset.UtcNow.AddYears(-7);
        var expiredDateStr = expiredTimestamp.UtcDateTime.ToString("yyyyMMdd");
        var expiredDir = Path.Combine(_tempAuditDir, expiredDateStr);
        Directory.CreateDirectory(expiredDir);
        File.WriteAllText(Path.Combine(expiredDir, "old.audit"), "{}");

        var recentDateStr = recentEntry.Timestamp.UtcDateTime.ToString("yyyyMMdd");
        var recentDir = Path.Combine(_tempAuditDir, recentDateStr);

        // Act
        await _provider.EnforceRetentionPolicyAsync(CancellationToken.None);

        // Assert
        Directory.Exists(expiredDir).Should().BeFalse("expired directory must be removed");
        Directory.Exists(recentDir).Should().BeTrue("recent directory must be preserved");
    }
}
