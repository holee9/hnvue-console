using System.Collections.ObjectModel;
using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Worklist display ViewModel.
/// SPEC-UI-001: FR-UI-02 Worklist Display.
/// </summary>
public class WorklistViewModel : ViewModelBase
{
    /// <summary>
    /// Raised when navigation to another view is requested.
    /// The string argument is the view name (e.g., "Acquisition").
    /// </summary>
    public event EventHandler<string>? NavigationRequested;

    /// <summary>
    /// Raised when an error occurs that should be displayed to the user.
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Gets or sets the selected worklist item (procedure) for navigation context.
    /// </summary>
    public WorklistItem? SelectedItem { get; private set; }
    private readonly IWorklistService _worklistService;
    private string _selectedPatientId = string.Empty;
    private bool _isLoading;
    private DateTimeOffset _lastRefreshed;

    /// <summary>
    /// Initializes a new instance of <see cref="WorklistViewModel"/>.
    /// </summary>
    public WorklistViewModel(IWorklistService worklistService)
    {
        _worklistService = worklistService ?? throw new ArgumentNullException(nameof(worklistService));
        WorklistItems = new ObservableCollection<WorklistItem>();

        RefreshCommand = new AsyncRelayCommand(
            ct => ExecuteRefreshAsync(ct),
            () => !IsLoading);

        SelectProcedureCommand = new RelayCommand<object?>(
            p => ExecuteSelectProcedure(p),
            p => p is WorklistItem);
    }

    /// <summary>
    /// Gets the worklist items collection.
    /// </summary>
    public ObservableCollection<WorklistItem> WorklistItems { get; }

    /// <summary>
    /// Gets or sets the selected patient ID filter.
    /// </summary>
    public string SelectedPatientId
    {
        get => _selectedPatientId;
        set => SetProperty(ref _selectedPatientId, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a refresh is in progress.
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
    /// Gets or sets the last refresh timestamp.
    /// </summary>
    public DateTimeOffset LastRefreshed
    {
        get => _lastRefreshed;
        set => SetProperty(ref _lastRefreshed, value);
    }

    /// <summary>
    /// Gets the last refreshed display string.
    /// </summary>
    public string LastRefreshedDisplay => LastRefreshed == DateTimeOffset.MinValue
        ? "Never"
        : LastRefreshed.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Gets the refresh command.
    /// </summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Gets the select procedure command.
    /// </summary>
    public RelayCommand<object?> SelectProcedureCommand { get; }

    /// <summary>
    /// Executes worklist refresh.
    /// </summary>
    private async Task ExecuteRefreshAsync(CancellationToken ct)
    {
        IsLoading = true;
        WorklistItems.Clear();

        try
        {
            var request = new WorklistRefreshRequest
            {
                Since = LastRefreshed == DateTimeOffset.MinValue ? null : LastRefreshed
            };

            var result = await _worklistService.RefreshWorklistAsync(request, ct);

            foreach (var item in result.Items)
            {
                WorklistItems.Add(item);
            }

            LastRefreshed = result.RefreshedAt;
            OnPropertyChanged(nameof(LastRefreshedDisplay));

            Debug.WriteLine($"Worklist refreshed: {result.Items.Count} items");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Worklist refresh failed: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Selects a worklist procedure and navigates to acquisition.
    /// </summary>
    private void ExecuteSelectProcedure(object? parameter)
    {
        if (parameter is not WorklistItem item)
            return;

        SelectedPatientId = item.PatientId;
        SelectedItem = item;
        Debug.WriteLine($"Selected procedure {item.ProcedureId} for patient {item.PatientName}");
        NavigationRequested?.Invoke(this, "Acquisition");
    }

    /// <summary>
    /// Loads the worklist when the view is activated.
    /// </summary>
    public async Task ActivateAsync(CancellationToken ct = default)
    {
        if (WorklistItems.Count == 0)
        {
            await ExecuteRefreshAsync(ct);
        }
    }
}
