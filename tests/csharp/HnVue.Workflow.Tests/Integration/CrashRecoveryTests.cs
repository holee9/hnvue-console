using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using HnVue.Workflow.Recovery;

namespace HnVue.Workflow.Tests.Integration;

/// <summary>
/// Tests for crash recovery and journal replay functionality.
/// SPEC-WORKFLOW-001 NFR-WF-02: Crash Recovery
///
/// Requirements:
/// - NFR-WF-02-a: Read journal on startup to detect in-progress study
/// - NFR-WF-02-b: Restore study context and present recovery options
/// - NFR-WF-02-c: No automatic hardware re-trigger without operator confirmation
/// - NFR-WF-02-d: Recovery completion within 5 seconds
/// </summary>
public class CrashRecoveryTests
{
    private readonly Mock<ILogger<CrashRecoveryService>> _loggerMock;
    private readonly Mock<IWorkflowJournal> _journalMock;

    public CrashRecoveryTests()
    {
        _loggerMock = new Mock<ILogger<CrashRecoveryService>>();
        _journalMock = new Mock<IWorkflowJournal>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new CrashRecoveryService(null!, _journalMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullJournal_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new CrashRecoveryService(_loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("journal");
    }

    [Fact]
    public async Task DetectRecoveryStateAsync_WithNoJournalEntries_ShouldReturnNull()
    {
        // Arrange
        var service = CreateService();
        _journalMock
            .Setup(j => j.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkflowJournalEntry>());

        // Act
        var result = await service.DetectRecoveryStateAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectRecoveryStateAsync_WithCompletedStudy_ShouldReturnNull()
    {
        // Arrange
        var service = CreateService();
        var completedEntry = new WorkflowJournalEntry
        {
            TransitionId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-10),
            FromState = WorkflowState.PacsExport,
            ToState = WorkflowState.Idle,
            Trigger = "ExportComplete",
            StudyInstanceUID = "1.2.3.4.5.100",
            Metadata = new Dictionary<string, object>(),
            OperatorId = "operator123"
        };

        _journalMock
            .Setup(j => j.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { completedEntry });

        // Act
        var result = await service.DetectRecoveryStateAsync();

        // Assert
        result.Should().BeNull("study was completed normally");
    }

    [Fact]
    public async Task DetectRecoveryStateAsync_WithIncompleteStudy_ShouldReturnRecoveryState()
    {
        // Arrange
        var service = CreateService();
        var incompleteEntry = new WorkflowJournalEntry
        {
            TransitionId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            FromState = WorkflowState.ProtocolSelect,
            ToState = WorkflowState.PositionAndPreview,
            Trigger = "OperatorReady",
            StudyInstanceUID = "1.2.3.4.5.100",
            Metadata = new Dictionary<string, object>
            {
                ["PatientID"] = "PATIENT001",
                ["PatientName"] = "Test^Patient"
            },
            OperatorId = "operator123"
        };

        _journalMock
            .Setup(j => j.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { incompleteEntry });

        // Act
        var result = await service.DetectRecoveryStateAsync();

        // Assert
        result.Should().NotBeNull();
        result!.RecoveryNeeded.Should().BeTrue();
        result.LastState.Should().Be(WorkflowState.PositionAndPreview);
        result.StudyInstanceUID.Should().Be("1.2.3.4.5.100");
        result.IsSafetyCritical.Should().BeTrue("PositionAndPreview is a safety-critical state");
    }

    [Fact]
    public async Task DetectRecoveryStateAsync_WithSafetyCriticalState_ShouldProvideAbortAsDefault()
    {
        // Arrange
        var service = CreateService();
        var criticalEntry = new WorkflowJournalEntry
        {
            TransitionId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-1),
            FromState = WorkflowState.PositionAndPreview,
            ToState = WorkflowState.ExposureTrigger,
            Trigger = "ExposureRequested",
            StudyInstanceUID = "1.2.3.4.5.100",
            Metadata = new Dictionary<string, object>(),
            OperatorId = "operator123"
        };

        _journalMock
            .Setup(j => j.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { criticalEntry });

        // Act
        var result = await service.DetectRecoveryStateAsync();

        // Assert
        result.Should().NotBeNull();
        result!.IsSafetyCritical.Should().BeTrue();
        result.RecoveryOptions.Should().Contain(o => o.IsDefault && o.OptionType == RecoveryOptionType.AbortToIdle,
            "safety-critical states should default to abort for safety");
    }

    [Fact]
    public async Task DetectRecoveryStateAsync_WithNonCriticalState_ShouldProvideAllOptions()
    {
        // Arrange
        var service = CreateService();
        var nonCriticalEntry = new WorkflowJournalEntry
        {
            TransitionId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            FromState = WorkflowState.PatientSelect,
            ToState = WorkflowState.ProtocolSelect,
            Trigger = "PatientConfirmed",
            StudyInstanceUID = "1.2.3.4.5.100",
            Metadata = new Dictionary<string, object>(),
            OperatorId = "operator123"
        };

        _journalMock
            .Setup(j => j.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { nonCriticalEntry });

        // Act
        var result = await service.DetectRecoveryStateAsync();

        // Assert
        result.Should().NotBeNull();
        result!.IsSafetyCritical.Should().BeFalse();
        result.RecoveryOptions.Should().HaveCountGreaterOrEqualTo(2,
            "non-critical states should provide multiple recovery options");
    }

    [Fact]
    public async Task RecoveryDetection_ShouldCompleteWithin5Seconds()
    {
        // Arrange
        var service = CreateService();
        var entries = GenerateJournalEntryCount(100); // Simulate large journal

        _journalMock
            .Setup(j => j.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries.ToArray());

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await service.DetectRecoveryStateAsync();
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessOrEqualTo(5000,
            "NFR-WF-02-d: Recovery should complete within 5 seconds");
    }

    [Fact]
    public async Task ClearJournalAsync_ShouldClearAllEntries()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.ClearJournalAsync();

        // Assert
        _journalMock.Verify(j => j.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecoveryHistoryAsync_ShouldReturnAllEntries()
    {
        // Arrange
        var service = CreateService();
        var entries = GenerateJournalEntryCount(10);

        _journalMock
            .Setup(j => j.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries.ToArray());

        // Act
        var result = await service.GetRecoveryHistoryAsync();

        // Assert
        result.Should().HaveCount(10);
    }

    private CrashRecoveryService CreateService()
    {
        return new CrashRecoveryService(_loggerMock.Object, _journalMock.Object);
    }

    private List<WorkflowJournalEntry> GenerateJournalEntryCount(int count)
    {
        var entries = new List<WorkflowJournalEntry>();
        for (int i = 0; i < count; i++)
        {
            entries.Add(new WorkflowJournalEntry
            {
                TransitionId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddMinutes(-count + i),
                FromState = WorkflowState.Idle,
                ToState = WorkflowState.WorklistSync,
                Trigger = "WorklistSyncRequested",
                StudyInstanceUID = $"1.2.3.4.5.{i}",
                Metadata = new Dictionary<string, object>(),
                OperatorId = "operator123"
            });
        }
        return entries;
    }
}
