namespace HnVue.Workflow.Tests.Recovery;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Workflow.StateMachine;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

/// <summary>
/// Unit tests for CrashRecoveryService.
/// Tests crash recovery detection, state restoration, and recovery options.
///
/// SPEC-WORKFLOW-001 NFR-WF-02: Crash Recovery
/// </summary>
public class CrashRecoveryServiceTests : IAsyncDisposable
{
    private readonly string _journalPath;
    private readonly Mock<ILogger<CrashRecoveryService>> _loggerMock;
    private readonly SqliteWorkflowJournal _journal;
    private readonly CrashRecoveryService _recoveryService;

    public CrashRecoveryServiceTests()
    {
        _journalPath = Path.Combine(Path.GetTempPath(), $"recovery-test-{Guid.NewGuid()}.db");
        _journal = new SqliteWorkflowJournal(_journalPath);
        _loggerMock = new Mock<ILogger<CrashRecoveryService>>();
        _recoveryService = new CrashRecoveryService(_loggerMock.Object, _journal);
    }

    [Fact]
    public async Task DetectIncompleteWorkflowAsync_WhenJournalIsEmpty_ShouldReturnNull()
    {
        // Act
        var result = await _recoveryService.DetectIncompleteWorkflowAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectIncompleteWorkflowAsync_WithCompleteWorkflow_ShouldReturnNull()
    {
        // Arrange - Create a complete workflow that ends in IDLE
        var entries = new[]
        {
            CreateJournalEntry(WorkflowState.Idle, WorkflowState.WorklistSync),
            CreateJournalEntry(WorkflowState.WorklistSync, WorkflowState.PatientSelect),
            CreateJournalEntry(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect),
            CreateJournalEntry(WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview),
            CreateJournalEntry(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger),
            CreateJournalEntry(WorkflowState.ExposureTrigger, WorkflowState.QcReview),
            CreateJournalEntry(WorkflowState.QcReview, WorkflowState.MppsComplete),
            CreateJournalEntry(WorkflowState.MppsComplete, WorkflowState.PacsExport),
            CreateJournalEntry(WorkflowState.PacsExport, WorkflowState.Idle)
        };

        foreach (var entry in entries)
        {
            await _journal.WriteEntryAsync(entry);
        }

        // Act
        var result = await _recoveryService.DetectIncompleteWorkflowAsync();

        // Assert
        result.Should().BeNull("workflow completed successfully and returned to IDLE");
    }

    [Fact]
    public async Task DetectIncompleteWorkflowAsync_WithIncompleteWorkflow_ShouldReturnRecoveryState()
    {
        // Arrange - Workflow stuck in QC_REVIEW
        var entries = new[]
        {
            CreateJournalEntry(WorkflowState.Idle, WorkflowState.WorklistSync),
            CreateJournalEntry(WorkflowState.WorklistSync, WorkflowState.PatientSelect),
            CreateJournalEntry(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect),
            CreateJournalEntry(WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview),
            CreateJournalEntry(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger),
            CreateJournalEntry(WorkflowState.ExposureTrigger, WorkflowState.QcReview)
        };

        foreach (var entry in entries)
        {
            await _journal.WriteEntryAsync(entry);
        }

        // Act
        var result = await _recoveryService.DetectIncompleteWorkflowAsync();

        // Assert
        result.Should().NotBeNull();
        result!.LastState.Should().Be(WorkflowState.QcReview);
        result.StudyInstanceUID.Should().NotBeNull();
    }

    [Fact]
    public async Task DetectIncompleteWorkflowAsync_ShouldIncludeRecoveryOptions()
    {
        // Arrange - Workflow stuck in EXPOSURE_TRIGGER (safety-critical state)
        var entries = new[]
        {
            CreateJournalEntry(WorkflowState.Idle, WorkflowState.WorklistSync, studyUid: "1.2.3.4.5"),
            CreateJournalEntry(WorkflowState.WorklistSync, WorkflowState.PatientSelect, studyUid: "1.2.3.4.5"),
            CreateJournalEntry(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, studyUid: "1.2.3.4.5"),
            CreateJournalEntry(WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview, studyUid: "1.2.3.4.5"),
            CreateJournalEntry(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, studyUid: "1.2.3.4.5")
        };

        foreach (var entry in entries)
        {
            await _journal.WriteEntryAsync(entry);
        }

        // Act
        var result = await _recoveryService.DetectIncompleteWorkflowAsync();

        // Assert
        result.Should().NotBeNull();
        result!.RecoveryOptions.Should().NotBeEmpty();
        result.RecoveryOptions.Should().Contain(o => o.OptionType == RecoveryOptionType.AbortToIdle);
        result.RecoveryOptions.Should().Contain(o => o.OptionType == RecoveryOptionType.ReviewAndDecide);
    }

    [Fact]
    public async Task ClearJournalAsync_ShouldRemoveAllEntries()
    {
        // Arrange
        var entries = new[]
        {
            CreateJournalEntry(WorkflowState.Idle, WorkflowState.WorklistSync),
            CreateJournalEntry(WorkflowState.WorklistSync, WorkflowState.PatientSelect)
        };

        foreach (var entry in entries)
        {
            await _journal.WriteEntryAsync(entry);
        }

        // Act
        await _recoveryService.ClearJournalAsync();

        // Assert
        var allEntries = await _journal.ReadAllAsync();
        allEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecoveryHistoryAsync_ShouldReturnAllTransitions()
    {
        // Arrange
        var entries = new[]
        {
            CreateJournalEntry(WorkflowState.Idle, WorkflowState.WorklistSync),
            CreateJournalEntry(WorkflowState.WorklistSync, WorkflowState.PatientSelect),
            CreateJournalEntry(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect)
        };

        foreach (var entry in entries)
        {
            await _journal.WriteEntryAsync(entry);
        }

        // Act
        var history = await _recoveryService.GetRecoveryHistoryAsync();

        // Assert
        history.Should().HaveCount(3);
        history[0].FromState.Should().Be(WorkflowState.Idle);
        history[1].FromState.Should().Be(WorkflowState.WorklistSync);
        history[2].FromState.Should().Be(WorkflowState.PatientSelect);
    }

    [Fact]
    public async Task DetectIncompleteWorkflowAsync_ForExposureTriggerState_ShouldMarkAsSafetyCritical()
    {
        // Arrange - Workflow stuck in EXPOSURE_TRIGGER (safety-critical)
        var entries = new[]
        {
            CreateJournalEntry(WorkflowState.Idle, WorkflowState.WorklistSync),
            CreateJournalEntry(WorkflowState.WorklistSync, WorkflowState.PatientSelect),
            CreateJournalEntry(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect),
            CreateJournalEntry(WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview),
            CreateJournalEntry(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger)
        };

        foreach (var entry in entries)
        {
            await _journal.WriteEntryAsync(entry);
        }

        // Act
        var result = await _recoveryService.DetectIncompleteWorkflowAsync();

        // Assert
        result.Should().NotBeNull();
        result!.IsSafetyCritical.Should().BeTrue("EXPOSURE_TRIGGER is a safety-critical state");
    }

    private WorkflowJournalEntry CreateJournalEntry(
        WorkflowState from,
        WorkflowState to,
        string? studyUid = null)
    {
        return new WorkflowJournalEntry
        {
            TransitionId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            FromState = from,
            ToState = to,
            Trigger = "TestTrigger",
            GuardResults = Array.Empty<GuardResult>(),
            OperatorId = "test-operator",
            StudyInstanceUID = studyUid ?? "1.2.3.4.5",
            Metadata = new Dictionary<string, object>(),
            Category = from == WorkflowState.PositionAndPreview && to == WorkflowState.ExposureTrigger
                ? LogCategory.SAFETY
                : LogCategory.WORKFLOW
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _recoveryService.DisposeAsync();
        await _journal.DisposeAsync();
        if (File.Exists(_journalPath))
        {
            try { File.Delete(_journalPath); }
            catch { /* Ignore cleanup failures */ }
        }
    }
}
