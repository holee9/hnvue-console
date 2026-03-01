namespace HnVue.Dicom.Worklist;

/// <summary>
/// Result of a worklist query operation.
/// Provides graceful degradation by returning empty results instead of throwing exceptions.
/// </summary>
/// <remarks>
/// @MX:NOTE Error handling - WorklistQueryResult provides graceful degradation
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-406
/// </remarks>
public record WorklistQueryResult
{
    /// <summary>
    /// Gets whether the query completed successfully.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the worklist items returned by the query.
    /// Empty if the query failed or no items were found.
    /// </summary>
    public required WorklistItem[] Items { get; init; }

    /// <summary>
    /// Gets the error message if the query failed.
    /// Null if the query succeeded.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result with the specified items.
    /// </summary>
    public static WorklistQueryResult Successful(WorklistItem[] items) =>
        new()
        {
            IsSuccess = true,
            Items = items,
            ErrorMessage = null
        };

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static WorklistQueryResult Failed(string errorMessage) =>
        new()
        {
            IsSuccess = false,
            Items = Array.Empty<WorklistItem>(),
            ErrorMessage = errorMessage
        };
}
