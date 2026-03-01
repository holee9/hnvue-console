using System.Collections.ObjectModel;
using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Audit log ViewModel with filtering and pagination.
/// SPEC-UI-001: FR-UI-13 Audit Log Viewer.
/// </summary>
public class AuditLogViewModel : ViewModelBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ObservableCollection<AuditLogEntry> _logEntries;
    private bool _isLoading;
    private AuditLogFilter _filter = new();
    private int _currentPage = 1;
    private int _pageSize = 50;
    private int _totalCount;
    private int _totalPages;

    /// <summary>
    /// Initializes a new instance of <see cref="AuditLogViewModel"/>.
    /// </summary>
    public AuditLogViewModel(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logEntries = new ObservableCollection<AuditLogEntry>();

        SearchCommand = new AsyncRelayCommand(
            ct => ExecuteSearchAsync(ct),
            () => !IsLoading);
        NextPageCommand = new AsyncRelayCommand(
            ct => ExecuteNextPageAsync(ct),
            () => !IsLoading && HasMorePages);
        PreviousPageCommand = new AsyncRelayCommand(
            ct => ExecutePreviousPageAsync(ct),
            () => !IsLoading && CurrentPage > 1);
        ExportCommand = new AsyncRelayCommand(
            ct => ExecuteExportAsync(ct),
            () => !IsLoading);
        ClearFilterCommand = new RelayCommand(() => ExecuteClearFilter());
    }

    /// <summary>
    /// Gets the log entries collection.
    /// </summary>
    public ObservableCollection<AuditLogEntry> LogEntries => _logEntries;

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
                SearchCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
                PreviousPageCommand.RaiseCanExecuteChanged();
                ExportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => SetProperty(ref _pageSize, value);
    }

    /// <summary>
    /// Gets or sets the total count of entries.
    /// </summary>
    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    public int TotalPages
    {
        get => _totalPages;
        set => SetProperty(ref _totalPages, value);
    }

    /// <summary>
    /// Gets a value indicating whether there are more pages.
    /// </summary>
    public bool HasMorePages => CurrentPage < TotalPages;

    /// <summary>
    /// Gets or sets the filter start date.
    /// </summary>
    public DateTimeOffset? FilterStartDate
    {
        get => _filter.StartDate;
        set
        {
            if (SetProperty(ref _filter, _filter with { StartDate = value }))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the filter end date.
    /// </summary>
    public DateTimeOffset? FilterEndDate
    {
        get => _filter.EndDate;
        set
        {
            if (SetProperty(ref _filter, _filter with { EndDate = value }))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the filter event type.
    /// </summary>
    public AuditEventType? FilterEventType
    {
        get => _filter.EventType;
        set
        {
            if (SetProperty(ref _filter, _filter with { EventType = value }))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the filter outcome.
    /// </summary>
    public AuditOutcome? FilterOutcome
    {
        get => _filter.Outcome;
        set
        {
            if (SetProperty(ref _filter, _filter with { Outcome = value }))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the filter user ID.
    /// </summary>
    public string? FilterUserId
    {
        get => _filter.UserId;
        set
        {
            if (SetProperty(ref _filter, _filter with { UserId = value }))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the filter patient ID.
    /// </summary>
    public string? FilterPatientId
    {
        get => _filter.PatientId;
        set
        {
            if (SetProperty(ref _filter, _filter with { PatientId = value }))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the search command.
    /// </summary>
    public AsyncRelayCommand SearchCommand { get; }

    /// <summary>
    /// Gets the next page command.
    /// </summary>
    public AsyncRelayCommand NextPageCommand { get; }

    /// <summary>
    /// Gets the previous page command.
    /// </summary>
    public AsyncRelayCommand PreviousPageCommand { get; }

    /// <summary>
    /// Gets the export command.
    /// </summary>
    public AsyncRelayCommand ExportCommand { get; }

    /// <summary>
    /// Gets the clear filter command.
    /// </summary>
    public RelayCommand ClearFilterCommand { get; }

    /// <summary>
    /// Gets the status message text.
    /// </summary>
    public string StatusMessage => TotalCount > 0
        ? $"Showing {LogEntries.Count} of {TotalCount} entries (Page {CurrentPage} of {TotalPages})"
        : "No entries found";

    /// <summary>
    /// Initializes the ViewModel.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // Set default date range to last 30 days
        FilterEndDate = DateTimeOffset.Now;
        FilterStartDate = DateTimeOffset.Now.AddDays(-30);

        await LoadEntriesAsync(ct);
    }

    /// <summary>
    /// Executes search command.
    /// </summary>
    private async Task ExecuteSearchAsync(CancellationToken ct)
    {
        CurrentPage = 1;
        await LoadEntriesAsync(ct);
    }

    /// <summary>
    /// Executes next page command.
    /// </summary>
    private async Task ExecuteNextPageAsync(CancellationToken ct)
    {
        if (HasMorePages)
        {
            CurrentPage++;
            await LoadEntriesAsync(ct);
        }
    }

    /// <summary>
    /// Executes previous page command.
    /// </summary>
    private async Task ExecutePreviousPageAsync(CancellationToken ct)
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadEntriesAsync(ct);
        }
    }

    /// <summary>
    /// Executes export command.
    /// </summary>
    private async Task ExecuteExportAsync(CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            var data = await _auditLogService.ExportLogsAsync(_filter, ct);
            Debug.WriteLine($"Exported {data.Length} bytes of audit log data");
            // TODO: Show save file dialog and write data
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Export failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes clear filter command.
    /// </summary>
    private void ExecuteClearFilter()
    {
        FilterStartDate = DateTimeOffset.Now.AddDays(-30);
        FilterEndDate = DateTimeOffset.Now;
        FilterEventType = null;
        FilterOutcome = null;
        FilterUserId = null;
        FilterPatientId = null;

        CurrentPage = 1;
        _ = Task.Run(() => LoadEntriesAsync(default));
    }

    /// <summary>
    /// Loads audit log entries based on current filter and page.
    /// </summary>
    private async Task LoadEntriesAsync(CancellationToken ct)
    {
        IsLoading = true;
        _logEntries.Clear();

        try
        {
            var result = await _auditLogService.GetLogsPagedAsync(CurrentPage, PageSize, _filter, ct);

            foreach (var entry in result.Entries)
            {
                _logEntries.Add(entry);
            }

            TotalCount = result.TotalCount;
            TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);

            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(HasMorePages));
            NextPageCommand.RaiseCanExecuteChanged();
            PreviousPageCommand.RaiseCanExecuteChanged();

            Debug.WriteLine($"Loaded {result.Entries.Count} entries (page {CurrentPage} of {TotalPages})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load audit log entries: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Gets the brush resource key for an audit outcome.
    /// </summary>
    public static string GetOutcomeBrushKey(AuditOutcome outcome) => outcome switch
    {
        AuditOutcome.Success => "SuccessBrush",
        AuditOutcome.Warning => "WarningBrush",
        AuditOutcome.Failure => "ErrorBrush",
        _ => "DisabledTextBrush"
    };
}
