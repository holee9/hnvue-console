namespace HnVue.Workflow.Journal;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for workflow journal persistence.
///
/// SPEC-WORKFLOW-001 NFR-WF-01: Atomic, Logged State Transitions
/// SPEC-WORKFLOW-001 NFR-WF-02: Crash Recovery
///
/// The journal provides write-ahead logging for all state transitions,
/// ensuring crash recovery capability and audit trail integrity.
/// </summary>
public interface IWorkflowJournal
{
    /// <summary>
    /// Writes a journal entry atomically.
    /// </summary>
    /// <param name="entry">The journal entry to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteEntryAsync(WorkflowJournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all journal entries for recovery purposes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WorkflowJournalEntry[]> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the most recent journal entry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WorkflowJournalEntry?> ReadLastEntryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Alias for ReadLastEntryAsync for backward compatibility.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WorkflowJournalEntry?> GetLastEntryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the journal has any entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> HasEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all journal entries.
    /// Used after successful crash recovery.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
