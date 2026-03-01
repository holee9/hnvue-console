namespace HnVue.Workflow.ViewModels;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// ViewModel for dose indicator display component.
/// SPEC-WORKFLOW-001 TASK-414: Dose Indicator Display Component
/// </summary>
/// <remarks>
/// @MX:WARN: Dose limit enforcement - tracks accumulated dose and enforces safety limits
/// Warning state at 80% of dose limit, alarm state at 100%
/// Implements IEC 60601-2-54 dose monitoring requirements
/// </remarks>
public sealed class DoseIndicatorViewModel : INotifyPropertyChanged
{
    private const decimal WarningThresholdPercent = 0.8m;
    private const decimal AlarmThresholdPercent = 1.0m;
    private const decimal DefaultDoseLimitMGy = 125.0m; // Typical DR dose limit

    private decimal _studyTotalMGy;
    private decimal _dailyTotalMGy;
    private decimal _doseLimitMGy;
    private bool _isInWarningState;
    private bool _isInAlarmState;

    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoseIndicatorViewModel"/> class.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Initialize with zero dose and default dose limit
    /// </remarks>
    public DoseIndicatorViewModel()
    {
        _studyTotalMGy = 0.0m;
        _dailyTotalMGy = 0.0m;
        _doseLimitMGy = DefaultDoseLimitMGy;
        _isInWarningState = false;
        _isInAlarmState = false;
    }

    /// <summary>
    /// Gets or sets the study total dose in mGy.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Study total dose - cumulative for current study
    /// </remarks>
    public decimal StudyTotalMGy
    {
        get => _studyTotalMGy;
        private set
        {
            if (_studyTotalMGy != value)
            {
                _studyTotalMGy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DosePercentage));
                UpdateWarningAlarmStates();
            }
        }
    }

    /// <summary>
    /// Gets or sets the daily total dose in mGy.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Daily total dose - cumulative for current day
    /// </remarks>
    public decimal DailyTotalMGy
    {
        get => _dailyTotalMGy;
        private set
        {
            if (_dailyTotalMGy != value)
            {
                _dailyTotalMGy = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the dose limit in mGy.
    /// </summary>
    /// <remarks>
    /// @MX:WARN: Dose limit - configurable safety limit for dose accumulation
    /// Default is 125 mGy for digital radiography (IEC 60601-2-54)
    /// </remarks>
    public decimal DoseLimitMGy
    {
        get => _doseLimitMGy;
        set
        {
            if (_doseLimitMGy != value && value > 0)
            {
                _doseLimitMGy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DosePercentage));
                UpdateWarningAlarmStates();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the dose is in warning state (>= 80% of limit).
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Warning state - indicates approaching dose limit
    /// </remarks>
    public bool IsInWarningState
    {
        get => _isInWarningState;
        private set
        {
            if (_isInWarningState != value)
            {
                _isInWarningState = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the dose is in alarm state (>= 100% of limit).
    /// </summary>
    /// <remarks>
    /// @MX:WARN: Alarm state - dose limit exceeded, exposure should be blocked
    /// </remarks>
    public bool IsInAlarmState
    {
        get => _isInAlarmState;
        private set
        {
            if (_isInAlarmState != value)
            {
                _isInAlarmState = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the dose percentage relative to the limit.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Dose percentage - calculated for UI display (0-100%+)
    /// </remarks>
    public decimal DosePercentage
    {
        get
        {
            if (_doseLimitMGy <= 0)
            {
                return 0.0m;
            }
            return (_studyTotalMGy / _doseLimitMGy) * 100.0m;
        }
    }

    /// <summary>
    /// Updates the dose display values.
    /// </summary>
    /// <param name="studyTotalMGy">The study total dose in mGy.</param>
    /// <param name="dailyTotalMGy">The daily total dose in mGy.</param>
    /// <remarks>
    /// @MX:NOTE: Update dose display - called by workflow engine after each exposure
    /// Clamps negative values to zero for safety
    /// </remarks>
    public void UpdateDoseDisplay(decimal studyTotalMGy, decimal dailyTotalMGy)
    {
        // Clamp negative values to zero for safety
        var clampedStudyTotal = Math.Max(0.0m, studyTotalMGy);
        var clampedDailyTotal = Math.Max(0.0m, dailyTotalMGy);

        StudyTotalMGy = clampedStudyTotal;
        DailyTotalMGy = clampedDailyTotal;
    }

    /// <summary>
    /// Updates the warning and alarm states based on current dose.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Update warning/alarm states - evaluates dose against thresholds
    /// Warning at 80%, Alarm at 100%
    /// </remarks>
    private void UpdateWarningAlarmStates()
    {
        if (_doseLimitMGy <= 0)
        {
            IsInWarningState = false;
            IsInAlarmState = false;
            return;
        }

        var percentage = _studyTotalMGy / _doseLimitMGy;

        IsInAlarmState = percentage >= AlarmThresholdPercent;
        IsInWarningState = percentage >= WarningThresholdPercent;
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
