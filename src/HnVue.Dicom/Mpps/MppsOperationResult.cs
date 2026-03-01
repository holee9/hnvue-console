namespace HnVue.Dicom.Mpps;

/// <summary>
/// Result of an MPPS operation (N-CREATE or N-SET).
/// </summary>
/// <remarks>
/// @MX:NOTE Error handling - MPPS operations return results instead of throwing
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-407
/// </remarks>
public record MppsOperationResult
{
    /// <summary>
    /// Gets whether the operation completed successfully.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the SOP Instance UID for N-CREATE operations.
    /// Null for N-SET operations or when the operation failed.
    /// </summary>
    public string? SopInstanceUid { get; init; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// Null if the operation succeeded.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result for N-CREATE operations.
    /// </summary>
    public static MppsOperationResult CreateSuccess(string sopInstanceUid) =>
        new()
        {
            IsSuccess = true,
            SopInstanceUid = sopInstanceUid,
            ErrorMessage = null
        };

    /// <summary>
    /// Creates a successful result for N-SET operations.
    /// </summary>
    public static MppsOperationResult UpdateSuccess() =>
        new()
        {
            IsSuccess = true,
            SopInstanceUid = null,
            ErrorMessage = null
        };

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static MppsOperationResult Failed(string errorMessage) =>
        new()
        {
            IsSuccess = false,
            SopInstanceUid = null,
            ErrorMessage = errorMessage
        };
}
