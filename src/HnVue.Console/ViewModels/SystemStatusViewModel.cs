using System.Collections.ObjectModel;
using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// System status ViewModel.
/// SPEC-UI-001: FR-UI-12 System Status Dashboard.
/// </summary>
public class SystemStatusViewModel : ViewModelBase
{
    /// <summary>
    /// Raised when a status update requires an immediate exposure halt.
    /// The ComponentStatus argument identifies the component that caused the halt.
    /// </summary>
    public event EventHandler<ComponentStatus>? ExposureHaltRequired;
    private readonly ISystemStatusService _systemStatusService;
    private SystemOverallStatus? _overallStatus;
    private bool _isLoading;
    private ComponentHealth _overallHealth;

    /// <summary>
    /// Initializes a new instance of <see cref="SystemStatusViewModel"/>.
    /// </summary>
    public SystemStatusViewModel(ISystemStatusService systemStatusService)
    {
        _systemStatusService = systemStatusService ?? throw new ArgumentNullException(nameof(systemStatusService));
        ComponentStatuses = new ObservableCollection<ComponentStatus>();

        RefreshCommand = new AsyncRelayCommand(
            ct => ExecuteRefreshAsync(ct),
            () => !IsLoading);
    }

    /// <summary>
    /// Gets the component statuses collection.
    /// </summary>
    public ObservableCollection<ComponentStatus> ComponentStatuses { get; }

    /// <summary>
    /// Gets or sets the overall system status.
    /// </summary>
    public SystemOverallStatus? OverallStatus
    {
        get => _overallStatus;
        set => SetProperty(ref _overallStatus, value);
    }

    /// <summary>
    /// Gets or sets the overall system health.
    /// </summary>
    public ComponentHealth OverallHealth
    {
        get => _overallHealth;
        set => SetProperty(ref _overallHealth, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether data is loading.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether exposure can be initiated.
    /// </summary>
    public bool CanInitiateExposure => OverallStatus?.CanInitiateExposure ?? false;

    /// <summary>
    /// Gets the refresh command.
    /// </summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Gets the overall health brush resource key.
    /// </summary>
    public string OverallHealthBrushKey => OverallHealth switch
    {
        ComponentHealth.Healthy => "SuccessBrush",
        ComponentHealth.Degraded => "WarningBrush",
        ComponentHealth.Error => "ErrorBrush",
        ComponentHealth.Offline => "SecondaryBrush",
        _ => "DisabledTextBrush"
    };

    /// <summary>
    /// Initializes the ViewModel and starts status updates.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await LoadStatusAsync(ct);
        _ = Task.Run(() => StartStatusUpdatesAsync(ct), ct);
    }

    /// <summary>
    /// Executes refresh command.
    /// </summary>
    private async Task ExecuteRefreshAsync(CancellationToken ct)
    {
        await LoadStatusAsync(ct);
    }

    /// <summary>
    /// Loads the current system status.
    /// </summary>
    private async Task LoadStatusAsync(CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            var status = await _systemStatusService.GetOverallStatusAsync(ct);
            OverallStatus = status;
            OverallHealth = status.OverallHealth;

            ComponentStatuses.Clear();
            foreach (var component in status.ComponentStatuses)
            {
                ComponentStatuses.Add(component);
            }

            OnPropertyChanged(nameof(CanInitiateExposure));
            OnPropertyChanged(nameof(OverallHealthBrushKey));

            Debug.WriteLine($"System status loaded: {status.OverallHealth}, {status.ComponentStatuses.Count} components");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load system status: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Starts listening for status updates.
    /// </summary>
    private async Task StartStatusUpdatesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var update in _systemStatusService.SubscribeStatusUpdatesAsync(ct))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateComponentStatus(update.Component);
                    if (update.RequiresExposureHalt)
                    {
                        Debug.WriteLine($"Status update requires exposure halt: {update.Component.ComponentId}");
                        ExposureHaltRequired?.Invoke(this, update.Component);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Status updates failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates a component status in the collection.
    /// </summary>
    private void UpdateComponentStatus(ComponentStatus updated)
    {
        var existing = ComponentStatuses.FirstOrDefault(c => c.ComponentId == updated.ComponentId);
        if (existing != null)
        {
            var index = ComponentStatuses.IndexOf(existing);
            ComponentStatuses[index] = updated;
        }
        else
        {
            ComponentStatuses.Add(updated);
        }

        // Recalculate overall health
        if (ComponentStatuses.Count > 0)
        {
            OverallHealth = ComponentStatuses.Max(c => c.Health);
            OnPropertyChanged(nameof(OverallHealthBrushKey));
        }
    }
}
