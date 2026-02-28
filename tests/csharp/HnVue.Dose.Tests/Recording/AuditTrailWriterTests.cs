using FluentAssertions;
using HnVue.Dose.Recording;
using HnVue.Dose.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Dose.Tests.Recording;

/// <summary>
/// Unit tests for AuditTrailWriter.
/// SPEC-DOSE-001 NFR-DOSE-04 Audit Trail with SHA-256 hash chain.
/// </summary>
public class AuditTrailWriterTests : IAsyncLifetime
{
    private readonly string _testAuditDirectory;
    private AuditTrailWriter _writer = null!;

    public AuditTrailWriterTests()
    {
        _testAuditDirectory = Path.Combine(Path.GetTempPath(), $"audit_tests_{Guid.NewGuid()}");
    }

    public Task InitializeAsync()
    {
        _writer = new AuditTrailWriter(
            NullLogger<AuditTrailWriter>.Instance,
            _testAuditDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _writer?.Dispose();

        if (Directory.Exists(_testAuditDirectory))
        {
            try
            {
                Directory.Delete(_testAuditDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        return Task.CompletedTask;
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesWriter()
    {
        // Act
        var writer = new AuditTrailWriter(
            NullLogger<AuditTrailWriter>.Instance,
            _testAuditDirectory);

        // Assert
        writer.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AuditTrailWriter(null!, _testAuditDirectory);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullDirectory_ThrowsArgumentException()
    {
        // Act
        var act = () => new AuditTrailWriter(
            NullLogger<AuditTrailWriter>.Instance,
            null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyDirectory_ThrowsArgumentException()
    {
        // Act
        var act = () => new AuditTrailWriter(
            NullLogger<AuditTrailWriter>.Instance,
            "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WriteEntry_WithValidParameters_WritesEntry()
    {
        // Act
        _writer.WriteEntry(
            AuditEventType.ExposureRecorded,
            AuditOutcome.Success,
            "operator123",
            DoseTestData.Uids.StudyInstanceUid,
            DoseTestData.Uids.PatientId,
            null,
            "Exposure completed");

        // Assert
        var files = Directory.GetFiles(_testAuditDirectory, "*.audit");
        files.Should().HaveCount(1);
    }

    [Fact]
    public void WriteEntry_WithNullDetails_ThrowsArgumentException()
    {
        // Act
        var act = () => _writer.WriteEntry(
            AuditEventType.ExposureRecorded,
            AuditOutcome.Success,
            null,
            null,
            null,
            null,
            null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WriteEntry_WithEmptyDetails_ThrowsArgumentException()
    {
        // Act
        var act = () => _writer.WriteEntry(
            AuditEventType.ExposureRecorded,
            AuditOutcome.Success,
            null,
            null,
            null,
            null,
            "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WriteEntry_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _writer.Dispose();

        // Act
        var act = () => _writer.WriteEntry(
            AuditEventType.ExposureRecorded,
            AuditOutcome.Success,
            null,
            null,
            null,
            null,
            "Test");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void WriteEntry_FirstEntry_UsesInitializationVector()
    {
        // Act
        _writer.WriteEntry(
            AuditEventType.ExposureRecorded,
            AuditOutcome.Success,
            null,
            DoseTestData.Uids.StudyInstanceUid,
            DoseTestData.Uids.PatientId,
            null,
            "First entry");

        // Assert
        var files = Directory.GetFiles(_testAuditDirectory, "*.audit");
        files.Should().HaveCount(1);

        var json = File.ReadAllText(files[0]);
        json.Should().Contain("previousRecordHash");
        // First entry should have empty previous hash or initialization vector
    }

    [Fact]
    public void WriteEntry_MultipleEntries_CreatesHashChain()
    {
        // Act
        _writer.WriteEntry(
            AuditEventType.ExposureRecorded,
            AuditOutcome.Success,
            null,
            DoseTestData.Uids.StudyInstanceUid,
            DoseTestData.Uids.PatientId,
            null,
            "First entry");

        _writer.WriteEntry(
            AuditEventType.RdsrGenerated,
            AuditOutcome.Success,
            null,
            DoseTestData.Uids.StudyInstanceUid,
            DoseTestData.Uids.PatientId,
            null,
            "Second entry");

        // Assert
        var files = Directory.GetFiles(_testAuditDirectory, "*.audit");
        files.Should().HaveCount(2);

        var json1 = File.ReadAllText(files[0]);
        var json2 = File.ReadAllText(files[1]);

        json1.Should().Contain("currentRecordHash");
        json2.Should().Contain("previousRecordHash");
    }

    [Fact]
    public void VerifyChain_WithEmptyAudit_ReturnsValid()
    {
        // Act - Create new writer with empty directory
        var newWriter = new AuditTrailWriter(
            NullLogger<AuditTrailWriter>.Instance,
            Path.Combine(Path.GetTempPath(), $"empty_audit_{Guid.NewGuid()}"));

        // Assert
        var result = newWriter.VerifyChain();
        result.IsValid.Should().BeTrue();
        result.Message.Should().Contain("empty");
    }

    [Fact]
    public void VerifyChain_WithValidEntries_ReturnsValid()
    {
        // Arrange - Use isolated writer for this test
        var isolatedDir = Path.Combine(Path.GetTempPath(), $"verify_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(isolatedDir);

        try
        {
            var isolatedWriter = new AuditTrailWriter(
                NullLogger<AuditTrailWriter>.Instance,
                isolatedDir);

            isolatedWriter.WriteEntry(
                AuditEventType.ExposureRecorded,
                AuditOutcome.Success,
                "operator1",
                DoseTestData.Uids.StudyInstanceUid,
                DoseTestData.Uids.PatientId,
                null,
                "Entry 1");

            isolatedWriter.WriteEntry(
                AuditEventType.RdsrGenerated,
                AuditOutcome.Success,
                "operator1",
                DoseTestData.Uids.StudyInstanceUid,
                DoseTestData.Uids.PatientId,
                null,
                "Entry 2");

            // Debug output - List files before verification
            var auditFiles = Directory.GetFiles(isolatedDir, "*.audit").OrderBy(f => f).ToList();
            Console.WriteLine($"Found {auditFiles.Count} audit files:");
            foreach (var file in auditFiles)
            {
                var json = File.ReadAllText(file);
                Console.WriteLine($"  File: {Path.GetFileName(file)}");
                Console.WriteLine($"    Content: {json}");
            }

            // Act
            var result = isolatedWriter.VerifyChain();

            // Debug output
            if (!result.IsValid)
            {
                Console.WriteLine($"Verification failed:");
                Console.WriteLine($"  BrokenAtFile: {result.BrokenAtFile}");
                Console.WriteLine($"  Message: {result.Message}");
            }

            // Assert
            result.IsValid.Should().BeTrue($"Verification failed: {result.Message}");
            result.BrokenAtFile.Should().BeNull();
            result.Message.Should().Contain("verified");
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(isolatedDir, true); } catch { }
        }
    }

    [Fact]
    public void VerifyChain_WithTamperedEntry_ReturnsInvalid()
    {
        // Arrange
        _writer.WriteEntry(
            AuditEventType.ExposureRecorded,
            AuditOutcome.Success,
            null,
            null,
            null,
            null,
            "Original entry");

        // Tamper with the file
        var files = Directory.GetFiles(_testAuditDirectory, "*.audit");
        var originalJson = File.ReadAllText(files[0]);
        var tamperedJson = originalJson.Replace("Original entry", "Tampered entry");
        File.WriteAllText(files[0], tamperedJson);

        // Create new writer to reload
        var newWriter = new AuditTrailWriter(
            NullLogger<AuditTrailWriter>.Instance,
            _testAuditDirectory);

        // Act
        var result = newWriter.VerifyChain();

        // Assert
        result.IsValid.Should().BeFalse();
        result.BrokenAtFile.Should().NotBeNull();
        result.Message.Should().Contain("mismatch");
    }

    [Fact]
    public void VerifyChain_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _writer.Dispose();

        // Act
        var act = () => _writer.VerifyChain();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void WriteEntry_AllEventTypes_Succeeds()
    {
        // Arrange
        var eventTypes = new[]
        {
            AuditEventType.ExposureRecorded,
            AuditEventType.RdsrGenerated,
            AuditEventType.ExportAttempted,
            AuditEventType.DrlExceeded,
            AuditEventType.ConfigChanged,
            AuditEventType.ReportGenerated
        };

        // Act & Assert
        foreach (var eventType in eventTypes)
        {
            var act = () => _writer.WriteEntry(
                eventType,
                AuditOutcome.Success,
                null,
                null,
                null,
                null,
                $"{eventType} test");

            act.Should().NotThrow();
        }

        var files = Directory.GetFiles(_testAuditDirectory, "*.audit");
        files.Should().HaveCount(eventTypes.Length);
    }

    [Fact]
    public void WriteEntry_WithFailureOutcome_IncludesErrorCode()
    {
        // Act
        _writer.WriteEntry(
            AuditEventType.ExportAttempted,
            AuditOutcome.Failure,
            null,
            DoseTestData.Uids.StudyInstanceUid,
            DoseTestData.Uids.PatientId,
            "E500",
            "Export failed due to network error");

        // Assert
        var files = Directory.GetFiles(_testAuditDirectory, "*.audit");
        files.Should().HaveCount(1);

        var json = File.ReadAllText(files[0]);
        json.Should().Contain("E500");
        json.Should().Contain("\"outcome\":1");  // 1 = Failure enum value
    }

    [Fact]
    public async Task WriteEntry_ThreadSafe_MultipleConcurrentWrites()
    {
        // Arrange
        var writeCount = 10;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < writeCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                _writer.WriteEntry(
                    AuditEventType.ExposureRecorded,
                    AuditOutcome.Success,
                    null,
                    null,
                    null,
                    null,
                    $"Concurrent entry {index}");
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert
        var files = Directory.GetFiles(_testAuditDirectory, "*.audit");
        files.Should().HaveCount(writeCount);
    }

    [Fact]
    public void WriteEntry_ReloadsLastHash_MaintainsChain()
    {
        // Arrange - Use isolated directory for this test
        var isolatedDir = Path.Combine(Path.GetTempPath(), $"reload_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(isolatedDir);

        try
        {
            var writer1 = new AuditTrailWriter(
                NullLogger<AuditTrailWriter>.Instance,
                isolatedDir);

            // Write first entry
            writer1.WriteEntry(
                AuditEventType.ExposureRecorded,
                AuditOutcome.Success,
                null,
                DoseTestData.Uids.StudyInstanceUid,
                DoseTestData.Uids.PatientId,
                null,
                "First entry");

            // Simulate restart by creating new writer instance
            var writer2 = new AuditTrailWriter(
                NullLogger<AuditTrailWriter>.Instance,
                isolatedDir);

            // Act - Write second entry with new writer instance
            writer2.WriteEntry(
                AuditEventType.RdsrGenerated,
                AuditOutcome.Success,
                null,
                DoseTestData.Uids.StudyInstanceUid,
                DoseTestData.Uids.PatientId,
                null,
                "Second entry");

            // Debug output - List files before verification
            var auditFiles = Directory.GetFiles(isolatedDir, "*.audit").OrderBy(f => f).ToList();
            Console.WriteLine($"Found {auditFiles.Count} audit files:");
            foreach (var file in auditFiles)
            {
                var json = File.ReadAllText(file);
                Console.WriteLine($"  File: {Path.GetFileName(file)}");
                Console.WriteLine($"    Content: {json}");
            }

            // Assert - Chain should be intact
            var result = writer2.VerifyChain();
            if (!result.IsValid)
            {
                Console.WriteLine($"Verification failed: {result.Message}");
            }
            result.IsValid.Should().BeTrue($"Verification failed: {result.Message}");
            result.BrokenAtFile.Should().BeNull();
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(isolatedDir, true); } catch { }
        }
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange & Act
        _writer.Dispose();
        var act = () => _writer.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("operator123")]
    [InlineData("ADMIN")]
    public void WriteEntry_WithVariousOperatorIds_Succeeds(string? operatorId)
    {
        // Act
        var act = () => _writer.WriteEntry(
            AuditEventType.ExposureRecorded,
            AuditOutcome.Success,
            operatorId,
            DoseTestData.Uids.StudyInstanceUid,
            DoseTestData.Uids.PatientId,
            null,
            "Test entry");

        // Assert
        act.Should().NotThrow();
    }
}
