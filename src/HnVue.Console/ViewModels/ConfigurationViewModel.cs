using System.Collections.ObjectModel;
using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Configuration ViewModel with tabbed sections.
/// SPEC-UI-001: FR-UI-08 System Configuration.
/// </summary>
public class ConfigurationViewModel : ViewModelBase
{
    private readonly ISystemConfigService _configService;
    private readonly IUserService _userService;
    private SystemConfig? _config;
    private UserRole? _currentUserRole;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private int _selectedTabIndex;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationViewModel"/>.
    /// </summary>
    public ConfigurationViewModel(ISystemConfigService configService, IUserService userService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));

        AvailableSections = new ObservableCollection<ConfigSectionItem>();
        _selectedTabIndex = -1;

        SaveCommand = new AsyncRelayCommand(
            ct => ExecuteSaveAsync(ct),
            () => !IsLoading);
        RefreshCommand = new AsyncRelayCommand(
            ct => ExecuteRefreshAsync(ct),
            () => !IsLoading);
        StartCalibrationCommand = new AsyncRelayCommand(
            ct => ExecuteStartCalibrationAsync(ct),
            () => !IsLoading);
    }

    /// <summary>
    /// Gets the available configuration sections.
    /// </summary>
    public ObservableCollection<ConfigSectionItem> AvailableSections { get; }

    /// <summary>
    /// Gets or sets the current system configuration.
    /// </summary>
    public SystemConfig? Config
    {
        get => _config;
        set => SetProperty(ref _config, value);
    }

    /// <summary>
    /// Gets or sets the current user role.
    /// </summary>
    public UserRole? CurrentUserRole
    {
        get => _currentUserRole;
        set => SetProperty(ref _currentUserRole, value);
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
                SaveCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
                StartCalibrationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets or sets the selected tab index.
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    /// <summary>
    /// Gets the save command.
    /// </summary>
    public AsyncRelayCommand SaveCommand { get; }

    /// <summary>
    /// Gets the refresh command.
    /// </summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Gets the start calibration command.
    /// </summary>
    public AsyncRelayCommand StartCalibrationCommand { get; }

    /// <summary>
    /// Gets a value indicating whether calibration tab is visible.
    /// </summary>
    public bool IsCalibrationVisible => CurrentUserRole >= UserRole.ServiceEngineer;

    /// <summary>
    /// Gets a value indicating whether network tab is visible.
    /// </summary>
    public bool IsNetworkVisible => CurrentUserRole >= UserRole.Supervisor;

    /// <summary>
    /// Gets a value indicating whether users tab is visible.
    /// </summary>
    public bool IsUsersVisible => CurrentUserRole >= UserRole.Administrator;

    /// <summary>
    /// Gets a value indicating whether logging tab is visible.
    /// </summary>
    public bool IsLoggingVisible => CurrentUserRole >= UserRole.Supervisor;

    /// <summary>
    /// Initializes the ViewModel.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await LoadUserRoleAsync(ct);
        await LoadConfigAsync(ct);
        UpdateVisibleSections();
    }

    /// <summary>
    /// Executes save command.
    /// </summary>
    private async Task ExecuteSaveAsync(CancellationToken ct)
    {
        if (Config == null || SelectedTabIndex < 0 || SelectedTabIndex >= AvailableSections.Count)
            return;

        IsLoading = true;
        StatusMessage = "Saving configuration...";

        try
        {
            var section = AvailableSections[SelectedTabIndex].Section;
            var update = new ConfigUpdate
            {
                Section = section,
                UpdateData = GetSectionData(section)
            };

            await _configService.UpdateConfigAsync(update, ct);
            StatusMessage = $"Configuration saved successfully.";
            Debug.WriteLine($"Configuration saved for section: {section}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
            Debug.WriteLine($"Configuration save failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes refresh command.
    /// </summary>
    private async Task ExecuteRefreshAsync(CancellationToken ct)
    {
        await LoadConfigAsync(ct);
        StatusMessage = "Configuration refreshed.";
    }

    /// <summary>
    /// Executes start calibration command.
    /// </summary>
    private async Task ExecuteStartCalibrationAsync(CancellationToken ct)
    {
        IsLoading = true;
        StatusMessage = "Starting calibration...";

        try
        {
            await _configService.StartCalibrationAsync(ct);
            StatusMessage = "Calibration started successfully.";
            Debug.WriteLine("Calibration started");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Calibration failed: {ex.Message}";
            Debug.WriteLine($"Calibration failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads the current user role.
    /// </summary>
    private async Task LoadUserRoleAsync(CancellationToken ct)
    {
        try
        {
            CurrentUserRole = await _userService.GetCurrentUserRoleAsync(ct);
            OnPropertyChanged(nameof(IsCalibrationVisible));
            OnPropertyChanged(nameof(IsNetworkVisible));
            OnPropertyChanged(nameof(IsUsersVisible));
            OnPropertyChanged(nameof(IsLoggingVisible));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load user role: {ex.Message}");
            CurrentUserRole = UserRole.Operator; // Default to least privilege
        }
    }

    /// <summary>
    /// Loads the system configuration.
    /// </summary>
    private async Task LoadConfigAsync(CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            Config = await _configService.GetConfigAsync(ct);
            Debug.WriteLine("Configuration loaded successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load configuration: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Updates the visible configuration sections based on user role.
    /// </summary>
    private void UpdateVisibleSections()
    {
        AvailableSections.Clear();

        if (IsCalibrationVisible)
        {
            AvailableSections.Add(new ConfigSectionItem(ConfigSection.Calibration, "Calibration", 0));
        }
        if (IsNetworkVisible)
        {
            AvailableSections.Add(new ConfigSectionItem(ConfigSection.Network, "Network", 1));
        }
        if (IsUsersVisible)
        {
            AvailableSections.Add(new ConfigSectionItem(ConfigSection.Users, "Users", 2));
        }
        if (IsLoggingVisible)
        {
            AvailableSections.Add(new ConfigSectionItem(ConfigSection.Logging, "Logging", 3));
        }

        // Select first available tab
        if (AvailableSections.Count > 0)
        {
            SelectedTabIndex = 0;
        }
    }

    /// <summary>
    /// Gets the section data for update.
    /// </summary>
    private object GetSectionData(ConfigSection section)
    {
        return section switch
        {
            ConfigSection.Calibration => Config?.Calibration ?? new CalibrationConfig
            {
                LastCalibrationDate = DateTimeOffset.Now.AddMonths(-6),
                NextCalibrationDueDate = DateTimeOffset.Now.AddMonths(6),
                IsCalibrationValid = true,
                Status = CalibrationStatus.Valid
            },
            ConfigSection.Network => Config?.Network ?? new NetworkConfig
            {
                DicomAeTitle = "HNVUE_CONSOLE",
                DicomPort = "104",
                PacsHostName = "pacs.hospital.local",
                PacsPort = 11112,
                MwlEnabled = true
            },
            ConfigSection.Users => Config?.Users ?? new UserConfig
            {
                Users = Array.Empty<User>()
            },
            ConfigSection.Logging => Config?.Logging ?? new LoggingConfig
            {
                MinimumLogLevel = LogLevel.Information,
                RetentionDays = 90,
                EnableRemoteLogging = false
            },
            _ => throw new ArgumentException($"Unknown section: {section}")
        };
    }
}

/// <summary>
/// Configuration section item for tab display.
/// </summary>
public record ConfigSectionItem(ConfigSection Section, string DisplayName, int DisplayOrder);
