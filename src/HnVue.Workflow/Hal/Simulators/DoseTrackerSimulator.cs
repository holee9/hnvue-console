namespace HnVue.Workflow.Hal.Simulators;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HnVue.Workflow.Interfaces;

/// <summary>
/// Simulator for the dose tracker.
/// </summary>
/// <remarks>
/// @MX:NOTE: Dose tracker simulator - test double for IDoseTracker
/// @MX:SPEC: SPEC-WORKFLOW-001 TASK-403
///
/// This simulator provides realistic dose tracking behavior for testing:
/// - Dose accumulation per study
/// - Dose limit enforcement
/// - Dose history tracking
/// - Safety-critical dose limit validation
/// </remarks>
public sealed class DoseTrackerSimulator : IDoseTracker
{
    private readonly object _lock = new();
    private readonly List<DoseEntry> _doseHistory = new();
    private double _totalDap;
    private string? _currentStudyId;
    private double? _doseLimit;

    /// <summary>
    /// Initializes a new instance of the DoseTrackerSimulator class.
    /// </summary>
    public DoseTrackerSimulator()
    {
    }

    /// <summary>
    /// Initializes the simulator asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: InitializeAsync - initializes simulator state
    /// </remarks>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Dose tracker doesn't need initialization, just return completed task
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: RecordDoseAsync - tracks radiation exposure
    /// @MX:WARN: Safety-critical - dose accumulation monitoring
    /// </remarks>
    public Task RecordDoseAsync(DoseEntry doseEntry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Validate input
        if (doseEntry.Dap < 0)
        {
            throw new ArgumentException("DAP cannot be negative", nameof(doseEntry));
        }

        lock (_lock)
        {
            _doseHistory.Add(doseEntry);

            // Update current study if changed
            if (_currentStudyId != doseEntry.StudyId)
            {
                // Switch to new study - reset totals
                _currentStudyId = doseEntry.StudyId;
                _totalDap = 0;
            }

            _totalDap += doseEntry.Dap;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<CumulativeDose> GetCumulativeDoseAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var cumulative = new CumulativeDose
            {
                StudyId = _currentStudyId ?? string.Empty,
                TotalDap = _totalDap,
                ExposureCount = _doseHistory.Count(d => d.StudyId == _currentStudyId),
                IsWithinLimits = !_doseLimit.HasValue || _totalDap <= _doseLimit.Value,
                DoseLimit = _doseLimit
            };

            return Task.FromResult(cumulative);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: IsWithinDoseLimitsAsync - prevents excessive radiation exposure
    /// @MX:WARN: Safety-critical - dose limit enforcement
    /// </remarks>
    public Task<bool> IsWithinDoseLimitsAsync(DoseEntry proposedDose, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_doseLimit.HasValue)
            {
                // No limit set, always within limits
                return Task.FromResult(true);
            }

            // Calculate what the total would be after this exposure
            double newTotal = _totalDap;

            // Check if this is for the current study
            if (_currentStudyId == proposedDose.StudyId)
            {
                newTotal += proposedDose.Dap;
            }
            else
            {
                // Different study, only the proposed dose matters
                newTotal = proposedDose.Dap;
            }

            return Task.FromResult(newTotal <= _doseLimit.Value);
        }
    }

    /// <summary>
    /// Sets the dose limit for the current study.
    /// </summary>
    /// <param name="limit">The dose limit in µGy·m², or null to remove the limit.</param>
    /// <remarks>
    /// @MX:ANCHOR: SetDoseLimit - controls dose limit enforcement
    /// @MX:WARN: Safety-critical - dose limit configuration affects safety
    /// </remarks>
    public void SetDoseLimit(double? limit)
    {
        lock (_lock)
        {
            _doseLimit = limit;
        }
    }

    /// <summary>
    /// Gets the cumulative dose synchronously for testing.
    /// </summary>
    /// <returns>The cumulative dose information.</returns>
    /// <remarks>
    /// @MX:NOTE: GetCumulativeDoseSync - testing helper method
    /// </remarks>
    public CumulativeDose GetCumulativeDoseSync()
    {
        lock (_lock)
        {
            return new CumulativeDose
            {
                StudyId = _currentStudyId ?? string.Empty,
                TotalDap = _totalDap,
                ExposureCount = _doseHistory.Count(d => d.StudyId == _currentStudyId),
                IsWithinLimits = !_doseLimit.HasValue || _totalDap <= _doseLimit.Value,
                DoseLimit = _doseLimit
            };
        }
    }

    /// <summary>
    /// Gets the dose history.
    /// </summary>
    /// <returns>List of all recorded dose entries.</returns>
    /// <remarks>
    /// @MX:NOTE: GetDoseHistory - returns dose entry history
    /// </remarks>
    public List<DoseEntry> GetDoseHistory()
    {
        lock (_lock)
        {
            return new List<DoseEntry>(_doseHistory);
        }
    }

    /// <summary>
    /// Resets the simulator to initial state.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _doseHistory.Clear();
            _totalDap = 0;
            _currentStudyId = null;
            _doseLimit = null;
        }

        return Task.CompletedTask;
    }
}
