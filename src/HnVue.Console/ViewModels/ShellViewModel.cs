using System.Diagnostics;
using HnVue.Console.Commands;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Shell ViewModel managing navigation state and global status badge.
/// SPEC-UI-001 Shell Infrastructure.
/// </summary>
public class ShellViewModel : ViewModelBase
{
    private string _currentView = "Patient";
    private string _currentPatientName = "No patient selected";
    private string _currentStudyId = "";
    private int _selectedLocaleIndex = 0;
    private SystemStatus _overallStatus = SystemStatus.Unknown;

    /// <summary>
    /// Gets the navigation command.
    /// </summary>
    public RelayCommand<object?> NavigateCommand { get; }

    /// <summary>
    /// Gets or sets the current view name.
    /// </summary>
    public string CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    /// <summary>
    /// Gets or sets the current patient name for display in status bar.
    /// </summary>
    public string CurrentPatientName
    {
        get => _currentPatientName;
        set => SetProperty(ref _currentPatientName, value);
    }

    /// <summary>
    /// Gets or sets the current study ID for display in status bar.
    /// </summary>
    public string CurrentStudyId
    {
        get => _currentStudyId;
        set => SetProperty(ref _currentStudyId, value);
    }

    /// <summary>
    /// Gets or sets the selected locale index (0 = Korean, 1 = English).
    /// </summary>
    public int SelectedLocaleIndex
    {
        get => _selectedLocaleIndex;
        set
        {
            if (SetProperty(ref _selectedLocaleIndex, value))
            {
                OnLocaleChanged(value == 0 ? "ko-KR" : "en-US");
            }
        }
    }

    /// <summary>
    /// Gets or sets the overall system status for the traffic light badge.
    /// </summary>
    public SystemStatus OverallStatus
    {
        get => _overallStatus;
        set => SetProperty(ref _overallStatus, value);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ShellViewModel"/>.
    /// </summary>
    public ShellViewModel()
    {
        NavigateCommand = new RelayCommand<object?>(p => ExecuteNavigate(p), CanNavigate);
    }

    /// <summary>
    /// Determines whether navigation to the specified view is allowed.
    /// </summary>
    private bool CanNavigate(object? parameter)
    {
        if (parameter is not string viewName)
            return false;

        // Prevent navigation to Acquisition if system is in error state
        if (viewName == "Acquisition" && OverallStatus == SystemStatus.Error)
            return false;

        return true;
    }

    /// <summary>
    /// Executes navigation to the specified view.
    /// </summary>
    private void ExecuteNavigate(object? parameter)
    {
        if (parameter is not string viewName)
            return;

        Debug.WriteLine($"Navigating to: {viewName}");
        CurrentView = viewName;

        // In full implementation, this would load the corresponding View
        // into the ContentRegion via a navigation service
    }

    /// <summary>
    /// Raised when the locale changes and all resource bindings should be refreshed.
    /// The string argument is the new locale code (e.g., "ko-KR" or "en-US").
    /// </summary>
    public event EventHandler<string>? LocaleChanged;

    /// <summary>
    /// Called when the locale is changed.
    /// </summary>
    /// <param name="locale">The new locale (e.g., "ko-KR" or "en-US").</param>
    private void OnLocaleChanged(string locale)
    {
        Debug.WriteLine($"Locale changed to: {locale}");
        LocaleChanged?.Invoke(this, locale);
    }
}

/// <summary>
/// System status enum for traffic light badge.
/// </summary>
public enum SystemStatus
{
    Unknown,
    Healthy,
    Warning,
    Error
}
