using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Worklist service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-02 Worklist Display.
/// </summary>
public interface IWorklistService
{
    /// <summary>
    /// Gets the current modality worklist.
    /// </summary>
    Task<IReadOnlyList<WorklistItem>> GetWorklistAsync(CancellationToken ct);

    /// <summary>
    /// Refreshes the worklist from the MWL SCP.
    /// </summary>
    Task<WorklistRefreshResult> RefreshWorklistAsync(WorklistRefreshRequest request, CancellationToken ct);

    /// <summary>
    /// Selects a worklist item for the current study.
    /// </summary>
    Task SelectWorklistItemAsync(string procedureId, CancellationToken ct);
}
