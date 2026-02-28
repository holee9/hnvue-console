namespace HnVue.Workflow.Recovery;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HnVue.Workflow.Journal;
using HnVue.Workflow.StateMachine;

/// <summary>
/// Crash recovery service for workflow state restoration.
///
/// SPEC-WORKFLOW-001 NFR-WF-02: Crash Recovery
///
/// Detects incomplete workflows from journal entries and provides recovery options.
/// </summary>
// @MX:NOTE: Crash recovery service analyzes journal to detect incomplete workflows
// @MX:REASON: Recovery is critical for patient safety - ensures no study is lost after crashes
public class CrashRecoveryService : IAsyncDisposable
{
    private readonly IWorkflowJournal _journal;
    private readonly ILogger<CrashRecoveryService> _logger;
    private bool _disposed;

    // Safety-critical states that require special handling
    private static readonly WorkflowState[] SafetyCriticalStates = new[]
    {
        WorkflowState.ExposureTrigger,
        WorkflowState.PositionAndPreview
    };

    public CrashRecoveryService(
        ILogger<CrashRecoveryService> logger,
        IWorkflowJournal journal)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    }

    /// <summary>
    /// Detects incomplete workflow from journal entries.
    /// Returns null if journal is empty or workflow completed successfully.
    /// </summary>
    public async Task<IncompleteWorkflowState?> DetectIncompleteWorkflowAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var entries = await _journal.ReadAllAsync(cancellationToken);

        if (entries.Length == 0)
        {
            _logger.LogInformation("Journal is empty - no incomplete workflow to recover");
            return null;
        }

        var lastEntry = entries.Last();

        // Workflow completed if last state is IDLE
        if (lastEntry.ToState == WorkflowState.Idle)
        {
            _logger.LogInformation("Last journal entry is IDLE - workflow completed successfully");
            return null;
        }

        // Detect safety-critical state
        var isSafetyCritical = SafetyCriticalStates.Contains(lastEntry.ToState);

        _logger.LogWarning(
            "Incomplete workflow detected: LastState={LastState}, StudyUID={StudyUID}, IsSafetyCritical={IsSafetyCritical}",
            lastEntry.ToState,
            lastEntry.StudyInstanceUID ?? "null",
            isSafetyCritical);

        return new IncompleteWorkflowState
        {
            LastState = lastEntry.ToState,
            StudyInstanceUID = lastEntry.StudyInstanceUID,
            LastTimestamp = lastEntry.Timestamp,
            OperatorId = lastEntry.OperatorId,
            IsSafetyCritical = isSafetyCritical,
            RecoveryOptions = GenerateRecoveryOptions(lastEntry.ToState, isSafetyCritical)
        };
    }

    /// <summary>
    /// Detects recovery state - alias for DetectIncompleteWorkflowAsync.
    /// </summary>
    public Task<IncompleteWorkflowState?> DetectRecoveryStateAsync(
        CancellationToken cancellationToken = default)
    {
        return DetectIncompleteWorkflowAsync(cancellationToken);
    }

    /// <summary>
    /// Clears all journal entries after successful recovery.
    /// </summary>
    public async Task ClearJournalAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogInformation("Clearing workflow journal after recovery");
        await _journal.ClearAsync(cancellationToken);
    }

    /// <summary>
    /// Gets recovery history for debugging/audit purposes.
    /// </summary>
    public async Task<WorkflowJournalEntry[]> GetRecoveryHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return await _journal.ReadAllAsync(cancellationToken);
    }

    private RecoveryOption[] GenerateRecoveryOptions(WorkflowState lastState, bool isSafetyCritical)
    {
        var options = new System.Collections.Generic.List<RecoveryOption>();

        // Option 1: Always allow abort to IDLE for safety
        options.Add(new RecoveryOption
        {
            OptionType = RecoveryOptionType.AbortToIdle,
            Description = "Abort current workflow and return to IDLE state",
            RequiresConfirmation = true,
            IsDefault = isSafetyCritical // Default to abort for safety-critical states
        });

        // Option 2: Review and decide (always available)
        options.Add(new RecoveryOption
        {
            OptionType = RecoveryOptionType.ReviewAndDecide,
            Description = "Review incomplete state and decide next action",
            RequiresConfirmation = true,
            IsDefault = !isSafetyCritical // Default to review for non-safety-critical states
        });

        // Option 3: Resume from last state (only for safe states)
        if (!isSafetyCritical)
        {
            options.Add(new RecoveryOption
            {
                OptionType = RecoveryOptionType.ResumeFromLastState,
                Description = $"Resume workflow from {lastState} state",
                RequiresConfirmation = true,
                IsDefault = false
            });
        }

        return options.ToArray();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CrashRecoveryService));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Journal is injected, don't dispose it here
        _disposed = true;
        await Task.CompletedTask;
    }
}

/// <summary>
/// Represents an incomplete workflow state detected during crash recovery.
/// </summary>
public class IncompleteWorkflowState
{
    /// <summary>
    /// The last state before crash.
    /// </summary>
    public required WorkflowState LastState { get; init; }

    /// <summary>
    /// The study instance UID (if available).
    /// </summary>
    public string? StudyInstanceUID { get; init; }

    /// <summary>
    /// Timestamp of the last transition.
    /// </summary>
    public DateTime LastTimestamp { get; init; }

    /// <summary>
    /// Operator who was performing the workflow.
    /// </summary>
    public required string OperatorId { get; init; }

    /// <summary>
    /// Whether the last state is safety-critical (EXPOSURE_TRIGGER, POSITION_AND_PREVIEW).
    /// </summary>
    public required bool IsSafetyCritical { get; init; }

    /// <summary>
    /// Whether recovery is needed (true when LastState != IDLE).
    /// </summary>
    public bool RecoveryNeeded => LastState != WorkflowState.Idle;

    /// <summary>
    /// Available recovery options.
    /// </summary>
    public required RecoveryOption[] RecoveryOptions { get; init; }
}

/// <summary>
/// Recovery option for incomplete workflow.
/// </summary>
public class RecoveryOption
{
    /// <summary>
    /// Type of recovery option.
    /// </summary>
    public required RecoveryOptionType OptionType { get; init; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Whether this option requires explicit operator confirmation.
    /// </summary>
    public required bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Whether this is the default recommended option.
    /// </summary>
    public required bool IsDefault { get; init; }
}

/// <summary>
/// Types of recovery options.
/// </summary>
public enum RecoveryOptionType
{
    /// <summary>
    /// Abort current workflow and return to IDLE.
    /// </summary>
    AbortToIdle,

    /// <summary>
    /// Review incomplete state and decide next action.
    /// </summary>
    ReviewAndDecide,

    /// <summary>
    /// Resume workflow from last state.
    /// </summary>
    ResumeFromLastState
}
