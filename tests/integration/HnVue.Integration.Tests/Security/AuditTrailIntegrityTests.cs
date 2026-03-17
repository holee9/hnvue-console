using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Security;
using HnVue.Console.Security.Models;
using HnVue.Console.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Integration.Tests.Security;

/// <summary>INT-004: Audit Trail Integrity Integration Tests. No Docker required.</summary>
public sealed class AuditTrailIntegrityTests : IDisposable
{
    private readonly string _auditDir;
    private readonly FileSystemWormStorageProvider _provider;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AuditTrailIntegrityTests()
    {
        _auditDir = Path.Combine(Path.GetTempPath(), "HnVue-Integration-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_auditDir);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AuditLog:Directory"] = _auditDir, ["AuditLog:WormSimulation"] = "false" })
            .Build();
        _provider = new FileSystemWormStorageProvider(config, NullLogger<FileSystemWormStorageProvider>.Instance);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_auditDir)) return;
        foreach (var file in Directory.GetFiles(_auditDir, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_auditDir, recursive: true);
    }

    private static WormEntry BuildEntry(
        string? entryId = null, DateTimeOffset? timestamp = null,
        int eventType = (int)AuditEventType.UserLogin, string userId = "user-01", string userName = "Test User",
        string description = "Integration test event", int outcome = (int)AuditOutcome.Success,
        string? previousEntryHash = null, string? patientId = null, string? studyId = null, string? workstationId = "INT-WS-01")
    {
        entryId ??= Guid.NewGuid().ToString("N");
        timestamp ??= DateTimeOffset.UtcNow;
        var hashInput = new { EntryId = entryId, Timestamp = timestamp.Value.ToUniversalTime().ToString("o"),
            EventType = eventType, UserId = userId, UserName = userName, EventDescription = description,
            Outcome = outcome, PatientId = patientId, StudyId = studyId,
            PreviousEntryHash = previousEntryHash, NtpSynchronized = true, WorkstationId = workstationId };
        var json = JsonSerializer.Serialize(hashInput);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        var currentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return new WormEntry { EntryId = entryId, Timestamp = timestamp.Value, EventType = eventType,
            UserId = userId, UserName = userName, EventDescription = description, Outcome = outcome,
            PatientId = patientId, StudyId = studyId, PreviousEntryHash = previousEntryHash,
            CurrentEntryHash = currentHash, NtpSynchronized = true, WorkstationId = workstationId };
    }

    private void PlantEntry(WormEntry entry)
    {
        var dateStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMdd");
        var tsStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMddHHmmss");
        var dir = Path.Combine(_auditDir, dateStr);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{tsStr}_{entry.EntryId}.audit"), JsonSerializer.Serialize(entry, _jsonOptions));
    }

    // INT-004-1: Hash chain integrity for 10 entries
    [Fact]
    public async Task AuditTrail_HashChain_IsValidForMultipleEntries()
    {
        string? prevHash = null;
        for (int i = 0; i < 10; i++)
        {
            var entry = BuildEntry(timestamp: DateTimeOffset.UtcNow.AddSeconds(i), description: $"Event {i}", previousEntryHash: prevHash);
            PlantEntry(entry);
            prevHash = entry.CurrentEntryHash;
        }
        var result = await _provider.VerifyIntegrityAsync(CancellationToken.None);
        result.IsValid.Should().BeTrue(because: "10 correctly chained entries must pass integrity verification");
        result.EntriesVerified.Should().Be(10);
        result.BrokenAtEntryId.Should().BeNull();
    }

    // INT-004-2: Tamper detection
    [Fact]
    public async Task AuditTrail_TamperedEntry_DetectedByVerification()
    {
        string? prevHash = null;
        string? targetFilePath = null;
        for (int i = 0; i < 5; i++)
        {
            var entry = BuildEntry(timestamp: DateTimeOffset.UtcNow.AddSeconds(i), description: $"Event {i}", previousEntryHash: prevHash);
            PlantEntry(entry);
            prevHash = entry.CurrentEntryHash;
            if (i == 2)
            {
                var dateStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMdd");
                var tsStr = entry.Timestamp.UtcDateTime.ToString("yyyyMMddHHmmss");
                targetFilePath = Path.Combine(_auditDir, dateStr, $"{tsStr}_{entry.EntryId}.audit");
            }
        }
        targetFilePath.Should().NotBeNull();
        var original = await File.ReadAllTextAsync(targetFilePath!);
        await File.WriteAllTextAsync(targetFilePath!, original.Replace("Event 2", "TAMPERED_EVENT"));
        var result = await _provider.VerifyIntegrityAsync(CancellationToken.None);
        result.IsValid.Should().BeFalse(because: "altering entry content must break the hash chain");
    }

    // INT-004-3: All AuditEventType values are storable
    [Fact]
    public async Task AuditTrail_AllEventTypes_AreLogged()
    {
        var eventTypes = Enum.GetValues<AuditEventType>();
        var baseTime = DateTimeOffset.UtcNow;
        foreach (var (eventType, idx) in eventTypes.Select((et, i) => (et, i)))
            PlantEntry(BuildEntry(timestamp: baseTime.AddSeconds(idx), eventType: (int)eventType, description: $"Testing {eventType}"));
        var allEntries = await _provider.QueryEntriesAsync(new AuditLogFilter(), CancellationToken.None);
        allEntries.Should().HaveCount(eventTypes.Length, because: "every AuditEventType must produce one storable entry");
        allEntries.Select(e => (AuditEventType)e.EventType).Distinct().Should().BeEquivalentTo(eventTypes);
    }

    // INT-004-4: Retention policy removes entries older than 6 years
    [Fact]
    public async Task AuditTrail_RetentionPolicy_RemovesOldEntries()
    {
        var oldTs = DateTimeOffset.UtcNow.AddYears(-7);
        var recentTs = DateTimeOffset.UtcNow.AddDays(-30);
        for (int i = 0; i < 3; i++) PlantEntry(BuildEntry(timestamp: oldTs.AddSeconds(i), description: $"Old {i}"));
        for (int i = 0; i < 3; i++) PlantEntry(BuildEntry(timestamp: recentTs.AddSeconds(i), description: $"Recent {i}"));
        (await _provider.QueryEntriesAsync(new AuditLogFilter(), CancellationToken.None)).Should().HaveCount(6);
        var deleted = await _provider.EnforceRetentionPolicyAsync(CancellationToken.None);
        deleted.Should().Be(3, because: "3 entries older than 6 years must be purged");
        (await _provider.QueryEntriesAsync(new AuditLogFilter(), CancellationToken.None)).Should().HaveCount(3);
    }

    // INT-004-5: Concurrent writes preserve all entries
    [Fact]
    public async Task AuditTrail_ConcurrentWrites_AllEntriesPreserved()
    {
        const int n = 20;
        var writeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HnVue", "AuditLogs");
        var entries = Enumerable.Range(0, n).Select(i => BuildEntry(timestamp: DateTimeOffset.UtcNow.AddMilliseconds(i * 10), description: $"Concurrent {i}")).ToList();
        await Task.WhenAll(entries.Select(e => _provider.WriteEntryAsync(e, CancellationToken.None)));
        var files = entries.Select(e => Path.Combine(writeDir, e.Timestamp.UtcDateTime.ToString("yyyyMMdd"), $"{e.Timestamp.UtcDateTime:yyyyMMddHHmmss}_{e.EntryId}.audit")).ToList();
        try { foreach (var f in files) File.Exists(f).Should().BeTrue(because: $"concurrent write must persist {Path.GetFileName(f)}"); }
        finally { foreach (var f in files.Where(File.Exists)) { try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch (IOException) { } } }
    }
}
