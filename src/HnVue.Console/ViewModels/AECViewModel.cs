using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// AEC (Automatic Exposure Control) ViewModel.
/// SPEC-UI-001: FR-UI-08 AEC Toggle.
/// </summary>
public class AECViewModel : ViewModelBase
{
    private readonly IAECService _aecService;
    private bool _isAecEnabled;

    /// <summary>
    /// Initializes a new instance of <see cref="AECViewModel"/>.
    /// </summary>
    public AECViewModel(IAECService aecService)
    {
        _aecService = aecService ?? throw new ArgumentNullException(nameof(aecService));

        ToggleAecCommand = new RelayCommand(
            () => ExecuteToggleAec(),
            () => true);
    }

    /// <summary>
    /// Gets or sets a value indicating whether AEC is enabled.
    /// </summary>
    public bool IsAecEnabled
    {
        get => _isAecEnabled;
        set => SetProperty(ref _isAecEnabled, value);
    }

    /// <summary>
    /// Gets the toggle AEC command.
    /// </summary>
    public RelayCommand ToggleAecCommand { get; }

    /// <summary>
    /// Executes AEC toggle.
    /// </summary>
    private async void ExecuteToggleAec()
    {
        try
        {
            if (_isAecEnabled)
            {
                await _aecService.DisableAECAsync(CancellationToken.None);
            }
            else
            {
                await _aecService.EnableAECAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AEC toggle failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets AEC mode from external source (e.g., protocol preset).
    /// </summary>
    public async Task SetAecModeAsync(bool isAec)
    {
        try
        {
            if (isAec)
            {
                await _aecService.EnableAECAsync(CancellationToken.None);
            }
            else
            {
                await _aecService.DisableAECAsync(CancellationToken.None);
            }
            IsAecEnabled = isAec;
            Debug.WriteLine($"AEC mode set to: {isAec}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set AEC mode: {ex.Message}");
        }
    }
}
