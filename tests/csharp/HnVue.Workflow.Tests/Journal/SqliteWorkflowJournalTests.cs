namespace HnVue.Workflow.Tests.Journal;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Workflow.StateMachine;
using Xunit;

/// <summary>
/// Unit tests for SqliteWorkflowJournal.
/// Tests journal persistence, crash recovery, and entry management.
///
/// SPEC-WORKFLOW-001 NFR-WF-01: Atomic, Logged State Transitions
/// SPEC-WORKFLOW-001 NFR-WF-02: Crash Recovery
/// </summary>
public class SqliteWorkflowJournalTests : IAsyncDisposable
{
    private readonly string _journalPath;
    private readonly SqliteWorkflowJournal _journal;

    public SqliteWorkflowJournalTests()
    {
        // Create a temporary journal file for each test
        _journalPath = Path.Combine(Path.GetTempPath(), $"test-journal-{Guid.NewGuid()}.db");
        _journal = new SqliteWorkflowJournal(_journalPath);
    }

    [Fact]
    public async Task WriteEntryAsync_ShouldPersistEntryToDatabase()
    {
        // Arrange
        var entry = CreateTestEntry();

        // Act
        await _journal.WriteEntryAsync(entry);

        // Assert
        var entries = await _journal.ReadAllAsync();
        entries.Should().ContainSingle();
        entries[0].TransitionId.Should().Be(entry.TransitionId);
        entries[0].FromState.Should().Be(entry.FromState);
        entries[0].ToState.Should().Be(entry.ToState);
    }

    [Fact]
    public async Task WriteEntryAsync_ShouldAssignSequentialTimestamps()
    {
        // Arrange
        var entry1 = CreateTestEntry();
        var entry2 = CreateTestEntry();

        // Act
        await Task.Delay(10); // Ensure time difference
        await _journal.WriteEntryAsync(entry1);
        await _journal.WriteEntryAsync(entry2);

        // Assert
        var entries = await _journal.ReadAllAsync();
        entries.Should().HaveCount(2);
        entries[0].Timestamp.Should().BeBefore(entries[1].Timestamp);
    }

    [Fact]
    public async Task ReadAllAsync_ShouldReturnAllEntriesInOrder()
    {
        // Arrange
        var entry1 = CreateTestEntry(WorkflowState.Idle, WorkflowState.WorklistSync);
        var entry2 = CreateTestEntry(WorkflowState.WorklistSync, WorkflowState.PatientSelect);
        var entry3 = CreateTestEntry(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect);

        await _journal.WriteEntryAsync(entry1);
        await _journal.WriteEntryAsync(entry2);
        await _journal.WriteEntryAsync(entry3);

        // Act
        var entries = await _journal.ReadAllAsync();

        // Assert
        entries.Should().HaveCount(3);
        entries[0].FromState.Should().Be(WorkflowState.Idle);
        entries[1].FromState.Should().Be(WorkflowState.WorklistSync);
        entries[2].FromState.Should().Be(WorkflowState.PatientSelect);
    }

    [Fact]
    public async Task ReadLastEntryAsync_WhenEmpty_ShouldReturnNull()
    {
        // Act
        var entry = await _journal.ReadLastEntryAsync();

        // Assert
        entry.Should().BeNull();
    }

    [Fact]
    public async Task ReadLastEntryAsync_WithEntries_ShouldReturnMostRecent()
    {
        // Arrange
        var entry1 = CreateTestEntry(WorkflowState.Idle, WorkflowState.WorklistSync);
        var entry2 = CreateTestEntry(WorkflowState.WorklistSync, WorkflowState.PatientSelect);

        await _journal.WriteEntryAsync(entry1);
        await _journal.WriteEntryAsync(entry2);

        // Act
        var lastEntry = await _journal.ReadLastEntryAsync();

        // Assert
        lastEntry.Should().NotBeNull();
        lastEntry!.FromState.Should().Be(WorkflowState.WorklistSync);
        lastEntry.ToState.Should().Be(WorkflowState.PatientSelect);
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllEntries()
    {
        // Arrange
        var entry1 = CreateTestEntry();
        var entry2 = CreateTestEntry();
        await _journal.WriteEntryAsync(entry1);
        await _journal.WriteEntryAsync(entry2);

        // Act
        await _journal.ClearAsync();

        // Assert
        var entries = await _journal.ReadAllAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteEntryAsync_ShouldPersistAllFields()
    {
        // Arrange
        var entry = new WorkflowJournalEntry
        {
            TransitionId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            FromState = WorkflowState.ProtocolSelect,
            ToState = WorkflowState.PositionAndPreview,
            Trigger = "ProtocolConfirmed",
            GuardResults = new[]
            {
                new GuardResult { GuardName = "ProtocolValid", Passed = true },
                new GuardResult { GuardName = "ExposureParamsInSafeRange", Passed = true }
            },
            OperatorId = "operator123",
            StudyInstanceUID = "1.2.3.4.5",
            Metadata = new Dictionary<string, object> { { "key", "value" } },
            Category = LogCategory.WORKFLOW
        };

        // Act
        await _journal.WriteEntryAsync(entry);

        // Assert
        var entries = await _journal.ReadAllAsync();
        var retrieved = entries.Single();

        retrieved.TransitionId.Should().Be(entry.TransitionId);
        retrieved.FromState.Should().Be(entry.FromState);
        retrieved.ToState.Should().Be(entry.ToState);
        retrieved.Trigger.Should().Be(entry.Trigger);
        retrieved.OperatorId.Should().Be(entry.OperatorId);
        retrieved.StudyInstanceUID.Should().Be(entry.StudyInstanceUID);
        retrieved.Metadata.Should().ContainKey("key").WhoseValue.Should().Be("value");
        retrieved.Category.Should().Be(entry.Category);
        retrieved.GuardResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task WriteEntryAsync_ShouldHandleConcurrentWrites()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 100).Select(i => CreateTestEntry()).ToList();

        // Act - Write all entries concurrently
        var writeTasks = tasks.Select(entry => _journal.WriteEntryAsync(entry));
        await Task.WhenAll(writeTasks);

        // Assert
        var entries = await _journal.ReadAllAsync();
        entries.Should().HaveCount(100);
    }

    [Fact]
    public void Constructor_ShouldCreateDatabaseFile()
    {
        // Assert
        File.Exists(_journalPath).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SqliteWorkflowJournal(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private WorkflowJournalEntry CreateTestEntry(
        WorkflowState? fromState = null,
        WorkflowState? toState = null)
    {
        return new WorkflowJournalEntry
        {
            TransitionId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            FromState = fromState ?? WorkflowState.Idle,
            ToState = toState ?? WorkflowState.WorklistSync,
            Trigger = "TestTrigger",
            GuardResults = Array.Empty<GuardResult>(),
            OperatorId = "test-operator",
            StudyInstanceUID = null,
            Metadata = new Dictionary<string, object>(),
            Category = LogCategory.WORKFLOW
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _journal.DisposeAsync();
        if (File.Exists(_journalPath))
        {
            try { File.Delete(_journalPath); }
            catch { /* Ignore cleanup failures */ }
        }
    }
}
