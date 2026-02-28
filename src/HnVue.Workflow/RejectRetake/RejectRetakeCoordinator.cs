namespace HnVue.Workflow.RejectRetake;

using HnVue.Workflow.Study;

/// <summary>
/// Coordinates the image rejection and retake workflow.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Reject/Retake coordinator - manages retake workflow state
/// @MX:REASON: Workflow-critical - ensures proper retake state transitions
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-08
///
/// This coordinator manages:
/// - Rejection reason recording
/// - Dose information preservation from rejected exposures
/// - Retake authorization and tracking
/// - Retake limit enforcement
/// </remarks>
public sealed class RejectRetakeCoordinator
{
    private readonly Dictionary<string, StudyRetakeState> _studyStates = new();
    private readonly object _lock = new();
    private readonly RetakeLimitConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="RejectRetakeCoordinator"/> class.
    /// </summary>
    /// <param name="configuration">The retake limit configuration.</param>
    public RejectRetakeCoordinator(RetakeLimitConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Records an image rejection.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="exposureIndex">The exposure index (sequence number).</param>
    /// <param name="reason">The rejection reason.</param>
    /// <param name="operatorId">The operator who rejected the image.</param>
    /// <returns>The retake authorization result.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Reject recording - tracks image rejection
    /// </remarks>
    public RetakeAuthorization RecordRejection(
        string studyId,
        int exposureIndex,
        RejectReason reason,
        string operatorId)
    {
        lock (_lock)
        {
            var state = _studyStates.GetValueOrDefault(studyId);
            if (state == null)
            {
                state = new StudyRetakeState { StudyId = studyId };
                _studyStates[studyId] = state;
            }

            var rejection = new RejectionRecord
            {
                RejectionId = Guid.NewGuid().ToString(),
                ExposureIndex = exposureIndex,
                Reason = reason,
                OperatorId = operatorId,
                Timestamp = DateTimeOffset.UtcNow
            };

            state.Rejections.Add(rejection);

            // Check retake limits
            var canRetake = state.Rejections.Count < _configuration.MaxRetakesPerStudy;

            return new RetakeAuthorization
            {
                CanRetake = canRetake,
                RejectionId = rejection.RejectionId,
                RetakesRemaining = _configuration.MaxRetakesPerStudy - state.Rejections.Count,
                Reason = canRetake ? null : "Maximum retakes exceeded"
            };
        }
    }

    /// <summary>
    /// Authorizes a retake for a rejected exposure.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="rejectionId">The rejection identifier.</param>
    /// <param name="authorizerId">The operator authorizing the retake.</param>
    /// <returns>True if authorized; false otherwise.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Retake authorization - approves retake workflow
    /// @MX:WARN: Safety-critical - additional radiation exposure approval
    /// </remarks>
    public bool AuthorizeRetake(string studyId, string rejectionId, string authorizerId)
    {
        lock (_lock)
        {
            if (!_studyStates.TryGetValue(studyId, out var state))
            {
                return false;
            }

            var rejection = state.Rejections.FirstOrDefault(r => r.RejectionId == rejectionId);
            if (rejection == null)
            {
                return false;
            }

            // Update rejection record
            rejection.AuthorizedForRetake = true;
            rejection.AuthorizedBy = authorizerId;
            rejection.AuthorizationTime = DateTimeOffset.UtcNow;

            return true;
        }
    }

    /// <summary>
    /// Completes a retake exposure.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="retakeExposure">The retake exposure record.</param>
    /// <returns>The cumulative retake count for the study.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Retake completion - records successful retake
    /// @MX:WARN: Safety-critical - tracks additional radiation dose
    /// </remarks>
    public int CompleteRetake(string studyId, ExposureRecord retakeExposure)
    {
        lock (_lock)
        {
            if (!_studyStates.TryGetValue(studyId, out var state))
            {
                state = new StudyRetakeState { StudyId = studyId };
                _studyStates[studyId] = state;
            }

            state.CompletedRetakes += 1;
            state.RetakeExposures.Add(retakeExposure);

            return state.CompletedRetakes;
        }
    }

    /// <summary>
    /// Gets the rejection history for a study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <returns>A read-only list of rejections.</returns>
    public IReadOnlyList<RejectionRecord> GetRejectionHistory(string studyId)
    {
        lock (_lock)
        {
            if (_studyStates.TryGetValue(studyId, out var state))
            {
                return state.Rejections.AsReadOnly();
            }
            return new List<RejectionRecord>().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the retake statistics for a study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <returns>The retake statistics, or null if the study doesn't exist.</returns>
    public RetakeStatistics? GetRetakeStatistics(string studyId)
    {
        lock (_lock)
        {
            if (!_studyStates.TryGetValue(studyId, out var state))
            {
                return null;
            }

            return new RetakeStatistics
            {
                StudyId = studyId,
                TotalRejections = state.Rejections.Count,
                CompletedRetakes = state.CompletedRetakes,
                PendingRetakes = state.Rejections.Count(r => !r.AuthorizedForRetake),
                AuthorizedRetakes = state.Rejections.Count(r => r.AuthorizedForRetake && !IsRetakeCompleted(r, state)),
                RejectionsByReason = state.Rejections
                    .GroupBy(r => r.Reason.ToString())
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
    }

    /// <summary>
    /// Clears the retake state for a completed study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    public void ClearStudy(string studyId)
    {
        lock (_lock)
        {
            _studyStates.Remove(studyId);
        }
    }

    private bool IsRetakeCompleted(RejectionRecord rejection, StudyRetakeState state)
    {
        return state.RetakeExposures.Any(e => e.ExposureIndex == rejection.ExposureIndex);
    }
}

/// <summary>
/// Represents the state of reject/retake for a study.
/// </summary>
/// <remarks>
/// @MX:NOTE: Study retake state - tracks retake workflow state
/// </remarks>
internal sealed class StudyRetakeState
{
    public required string StudyId { get; init; }
    public List<RejectionRecord> Rejections { get; } = new();
    public int CompletedRetakes { get; set; }
    public List<ExposureRecord> RetakeExposures { get; } = new();
}

/// <summary>
/// Represents a rejection record.
/// </summary>
/// <remarks>
/// @MX:NOTE: Rejection record - tracks individual image rejection
/// </remarks>
public sealed class RejectionRecord
{
    /// <summary>
    /// Gets or sets the unique rejection identifier.
    /// </summary>
    public required string RejectionId { get; init; }

    /// <summary>
    /// Gets or sets the exposure index that was rejected.
    /// </summary>
    public required int ExposureIndex { get; init; }

    /// <summary>
    /// Gets or sets the rejection reason.
    /// </summary>
    public required RejectReason Reason { get; init; }

    /// <summary>
    /// Gets or sets the operator who rejected the image.
    /// </summary>
    public required string OperatorId { get; init; }

    /// <summary>
    /// Gets or sets the rejection timestamp.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the rejection is authorized for retake.
    /// </summary>
    public bool AuthorizedForRetake { get; set; }

    /// <summary>
    /// Gets or sets the operator who authorized the retake.
    /// </summary>
    public string? AuthorizedBy { get; set; }

    /// <summary>
    /// Gets or sets the authorization timestamp.
    /// </summary>
    public DateTimeOffset? AuthorizationTime { get; set; }
}

/// <summary>
/// Represents retake authorization result.
/// </summary>
/// <remarks>
/// @MX:NOTE: Retake authorization - retake permission outcome
/// </remarks>
public sealed class RetakeAuthorization
{
    /// <summary>
    /// Gets or sets a value indicating whether retake is authorized.
    /// </summary>
    public required bool CanRetake { get; init; }

    /// <summary>
    /// Gets or sets the rejection identifier.
    /// </summary>
    public required string RejectionId { get; init; }

    /// <summary>
    /// Gets or sets the remaining retakes allowed.
    /// </summary>
    public required int RetakesRemaining { get; init; }

    /// <summary>
    /// Gets or sets the reason if retake is not authorized.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Represents retake statistics for a study.
/// </summary>
/// <remarks>
/// @MX:NOTE: Retake statistics - retake workflow metrics
/// </remarks>
public sealed class RetakeStatistics
{
    /// <summary>
    /// Gets or sets the study identifier.
    /// </summary>
    public required string StudyId { get; init; }

    /// <summary>
    /// Gets or sets the total number of rejections.
    /// </summary>
    public required int TotalRejections { get; init; }

    /// <summary>
    /// Gets or sets the number of completed retakes.
    /// </summary>
    public required int CompletedRetakes { get; init; }

    /// <summary>
    /// Gets or sets the number of pending retakes.
    /// </summary>
    public required int PendingRetakes { get; init; }

    /// <summary>
    /// Gets or sets the number of authorized but not completed retakes.
    /// </summary>
    public required int AuthorizedRetakes { get; init; }

    /// <summary>
    /// Gets or sets the breakdown of rejections by reason.
    /// </summary>
    public required Dictionary<string, int> RejectionsByReason { get; init; }
}

/// <summary>
/// Configuration for retake limits.
/// </summary>
/// <remarks>
/// @MX:NOTE: Retake limit configuration - retake safety thresholds
/// </remarks>
public sealed class RetakeLimitConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of retakes allowed per study.
    /// </summary>
    public int MaxRetakesPerStudy { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of retakes allowed per exposure.
    /// </summary>
    public int MaxRetakesPerExposure { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether retake requires supervisor authorization.
    /// </summary>
    public bool RequireSupervisorAuthorization { get; set; } = false;
}
