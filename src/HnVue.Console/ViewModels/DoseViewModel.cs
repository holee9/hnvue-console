using System.Collections.ObjectModel;
using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Dose display ViewModel for current and cumulative dose.
/// SPEC-UI-001: FR-UI-10 Dose Display.
/// </summary>
public class DoseViewModel : ViewModelBase
{
    private readonly IDoseService _doseService;
    private DoseDisplay _doseDisplay = new()
    {
        CurrentDose = new DoseValue { Value = 0m, Unit = DoseUnit.MilliGraySquareCm, MeasuredAt = DateTime.UtcNow },
        CumulativeDose = new DoseValue { Value = 0m, Unit = DoseUnit.MilliGraySquareCm, MeasuredAt = DateTime.UtcNow },
        StudyId = string.Empty,
        ExposureCount = 0
    };
    private DoseAlertThreshold _alertThreshold = new()
    {
        WarningThreshold = 2.0m,
        ErrorThreshold = 5.0m,
        Unit = DoseUnit.MilliGraySquareCm
    };
    private bool _hasAlert;

    /// <summary>
    /// Initializes a new instance of <see cref="DoseViewModel"/>.
    /// </summary>
    public DoseViewModel(IDoseService doseService)
    {
        _doseService = doseService ?? throw new ArgumentNullException(nameof(doseService));

        AcknowledgeAlertCommand = new RelayCommand(
            () => ExecuteAcknowledgeAlert(),
            () => _hasAlert);

        // Initialize default threshold
        _alertThreshold = new DoseAlertThreshold
        {
            WarningThreshold = 2.0m,
            ErrorThreshold = 5.0m,
            Unit = DoseUnit.MilliGraySquareCm
        };
    }

    /// <summary>
    /// Gets or sets the current dose display.
    /// </summary>
    public DoseDisplay DoseDisplay
    {
        get => _doseDisplay;
        set => SetProperty(ref _doseDisplay, value);
    }

    /// <summary>
    /// Gets or sets the current dose value.
    /// </summary>
    public string CurrentDoseValue => $"{_doseDisplay.CurrentDose.Value:F2} {_doseDisplay.CurrentDose.Unit}";

    /// <summary>
    /// Gets or sets the cumulative dose value.
    /// </summary>
    public string CumulativeDoseValue => $"{_doseDisplay.CumulativeDose.Value:F2} {_doseDisplay.CumulativeDose.Unit}";

    /// <summary>
    /// Gets or sets the dose alert threshold.
    /// </summary>
    public DoseAlertThreshold AlertThreshold
    {
        get => _alertThreshold;
        set => SetProperty(ref _alertThreshold, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether an alert is active.
    /// </summary>
    public bool HasAlert
    {
        get => _hasAlert;
        set => SetProperty(ref _hasAlert, value);
    }

    /// <summary>
    /// Gets or sets the alert message.
    /// </summary>
    public string AlertMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alert level (Warning/Error).
    /// </summary>
    public string AlertLevel { get; set; } = string.Empty;

    /// <summary>
    /// Gets the acknowledge alert command.
    /// </summary>
    public RelayCommand AcknowledgeAlertCommand { get; }

    /// <summary>
    /// Starts dose update subscription.
    /// </summary>
    public async Task StartDoseUpdatesAsync(CancellationToken ct = default)
    {
        try
        {
            // Get initial display
            var display = await _doseService.GetCurrentDoseDisplayAsync(ct);
            DoseDisplay = display;

            // Subscribe to updates
            await foreach (var update in _doseService.SubscribeDoseUpdatesAsync(ct))
            {
                // Update display with new dose
                var cumulativeDose = new DoseValue
                {
                    Value = update.CumulativeDose.Value,
                    Unit = update.CumulativeDose.Unit,
                    MeasuredAt = update.CumulativeDose.MeasuredAt
                };

                DoseDisplay = DoseDisplay with
                {
                    CurrentDose = update.NewDose,
                    CumulativeDose = cumulativeDose
                };

                // Check alert thresholds
                UpdateAlertStatus(update);

                OnPropertyChanged(nameof(CurrentDoseValue));
                OnPropertyChanged(nameof(CumulativeDoseValue));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Dose update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates alert status based on dose thresholds.
    /// </summary>
    private void UpdateAlertStatus(DoseUpdate update)
    {
        if (update.IsErrorThresholdExceeded)
        {
            HasAlert = true;
            AlertLevel = "ERROR";
            AlertMessage = $"DOSE EXCEEDED: {update.CumulativeDose.Value} {update.CumulativeDose.Unit}";
        }
        else if (update.IsWarningThresholdExceeded)
        {
            HasAlert = true;
            AlertLevel = "WARNING";
            AlertMessage = $"Dose warning: {update.CumulativeDose.Value} {update.CumulativeDose.Unit}";
        }
        else
        {
            HasAlert = false;
            AlertMessage = string.Empty;
        }

        AcknowledgeAlertCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Executes alert acknowledgment.
    /// </summary>
    private void ExecuteAcknowledgeAlert()
    {
        Debug.WriteLine($"Alert acknowledged: {AlertMessage}");
        HasAlert = false;
        AlertMessage = string.Empty;
        AlertLevel = string.Empty;
    }

    /// <summary>
    /// Resets cumulative dose for a new study.
    /// </summary>
    public async Task ResetCumulativeDoseAsync(string studyId, CancellationToken ct = default)
    {
        try
        {
            await _doseService.ResetCumulativeDoseAsync(studyId, ct);
            Debug.WriteLine($"Cumulative dose reset for study: {studyId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to reset dose: {ex.Message}");
        }
    }
}
