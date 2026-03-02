using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Exposure parameter ViewModel for kVp, mA, time, SID, FSS.
/// SPEC-UI-001: FR-UI-07 Exposure Parameter Display.
/// </summary>
public class ExposureParameterViewModel : ViewModelBase
{
    private readonly IExposureService _exposureService;
    private ExposureParameters _parameters;
    private ExposureParameterRange _ranges;
    private bool _isReadOnly;

    /// <summary>
    /// Initializes a new instance of <see cref="ExposureParameterViewModel"/>.
    /// </summary>
    public ExposureParameterViewModel(IExposureService exposureService)
    {
        _exposureService = exposureService ?? throw new ArgumentNullException(nameof(exposureService));

        // Initialize with default values
        _parameters = new ExposureParameters
        {
            KVp = 120,
            MA = 100,
            ExposureTimeMs = 100,
            SourceImageDistanceCm = 100,
            FocalSpotSize = FocalSpotSize.Large,
            IsAecMode = false
        };

        // Initialize default ranges
        _ranges = new ExposureParameterRange
        {
            KvpRange = new IntRange { Min = 40, Max = 150 },
            MaRange = new IntRange { Min = 10, Max = 630 },
            TimeRangeMs = new IntRange { Min = 1, Max = 5000 },
            SidRangeCm = new IntRange { Min = 100, Max = 180 }
        };

        // Commands
        IncreaseKVpCommand = new RelayCommand(() => UpdateKVp(_parameters.KVp + 1), () => CanIncreaseKVp());
        DecreaseKVpCommand = new RelayCommand(() => UpdateKVp(_parameters.KVp - 1), () => CanDecreaseKVp());
        IncreaseMACommand = new RelayCommand(() => UpdateMA(_parameters.MA + 10), () => CanIncreaseMA());
        DecreaseMACommand = new RelayCommand(() => UpdateMA(_parameters.MA - 10), () => CanDecreaseMA());
        IncreaseTimeCommand = new RelayCommand(() => UpdateTime(_parameters.ExposureTimeMs + 10), () => CanIncreaseTime());
        DecreaseTimeCommand = new RelayCommand(() => UpdateTime(_parameters.ExposureTimeMs - 10), () => CanDecreaseTime());
        ApplyCommand = new AsyncRelayCommand(ct => ApplyParametersAsync(ct), () => !_isReadOnly && HasChanges);
    }

    /// <summary>
    /// Gets or sets the current exposure parameters.
    /// </summary>
    public ExposureParameters Parameters
    {
        get => _parameters;
        set => SetProperty(ref _parameters, value);
    }

    /// <summary>
    /// Gets the exposure parameter ranges for validation.
    /// </summary>
    public ExposureParameterRange Ranges => _ranges;

    /// <summary>
    /// Gets or sets a value indicating whether fields are read-only (AEC mode).
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetProperty(ref _isReadOnly, value);
    }

    /// <summary>
    /// Gets or sets kVp value.
    /// </summary>
    public int KVp
    {
        get => _parameters.KVp;
        set
        {
            if (_parameters.KVp != value)
            {
                _parameters = _parameters with { KVp = value };
                OnPropertyChanged(nameof(KVp));
                OnPropertyChanged(nameof(Parameters));
                OnPropertyChanged(nameof(Mas));
            }
        }
    }

    /// <summary>
    /// Gets or sets mA value.
    /// </summary>
    public int MA
    {
        get => _parameters.MA;
        set
        {
            if (_parameters.MA != value)
            {
                _parameters = _parameters with { MA = value };
                OnPropertyChanged(nameof(MA));
                OnPropertyChanged(nameof(Parameters));
                OnPropertyChanged(nameof(Mas));
            }
        }
    }

    /// <summary>
    /// Gets or sets exposure time in ms.
    /// </summary>
    public int ExposureTimeMs
    {
        get => _parameters.ExposureTimeMs;
        set
        {
            if (_parameters.ExposureTimeMs != value)
            {
                _parameters = _parameters with { ExposureTimeMs = value };
                OnPropertyChanged(nameof(ExposureTimeMs));
                OnPropertyChanged(nameof(Parameters));
                OnPropertyChanged(nameof(Mas));
            }
        }
    }

    /// <summary>
    /// Gets or sets source image distance in cm.
    /// </summary>
    public int SourceImageDistanceCm
    {
        get => _parameters.SourceImageDistanceCm;
        set
        {
            if (_parameters.SourceImageDistanceCm != value)
            {
                _parameters = _parameters with { SourceImageDistanceCm = value };
                OnPropertyChanged(nameof(SourceImageDistanceCm));
                OnPropertyChanged(nameof(Parameters));
            }
        }
    }

    /// <summary>
    /// Gets or sets focal spot size.
    /// </summary>
    public FocalSpotSize FocalSpotSize
    {
        get => _parameters.FocalSpotSize;
        set
        {
            if (_parameters.FocalSpotSize != value)
            {
                _parameters = _parameters with { FocalSpotSize = value };
                OnPropertyChanged(nameof(FocalSpotSize));
                OnPropertyChanged(nameof(Parameters));
            }
        }
    }

    /// <summary>
    /// Gets the mAs value (calculated from MA and time).
    /// </summary>
    public double Mas => _parameters.MA * _parameters.ExposureTimeMs / 1000.0;

    /// <summary>
    /// Gets increase kVp command.
    /// </summary>
    public RelayCommand IncreaseKVpCommand { get; }

    /// <summary>
    /// Gets decrease kVp command.
    /// </summary>
    public RelayCommand DecreaseKVpCommand { get; }

    /// <summary>
    /// Gets increase mA command.
    /// </summary>
    public RelayCommand IncreaseMACommand { get; }

    /// <summary>
    /// Gets decrease mA command.
    /// </summary>
    public RelayCommand DecreaseMACommand { get; }

    /// <summary>
    /// Gets increase time command.
    /// </summary>
    public RelayCommand IncreaseTimeCommand { get; }

    /// <summary>
    /// Gets decrease time command.
    /// </summary>
    public RelayCommand DecreaseTimeCommand { get; }

    /// <summary>
    /// Gets the apply command.
    /// </summary>
    public AsyncRelayCommand ApplyCommand { get; }

    private ExposureParameters? _appliedParameters;

    private bool HasChanges => _appliedParameters == null || _appliedParameters != _parameters;

    private bool CanIncreaseKVp() => _parameters.KVp < _ranges.KvpRange.Max;
    private bool CanDecreaseKVp() => _parameters.KVp > _ranges.KvpRange.Min;
    private bool CanIncreaseMA() => _parameters.MA < _ranges.MaRange.Max;
    private bool CanDecreaseMA() => _parameters.MA > _ranges.MaRange.Min;
    private bool CanIncreaseTime() => _parameters.ExposureTimeMs < _ranges.TimeRangeMs.Max;
    private bool CanDecreaseTime() => _parameters.ExposureTimeMs > _ranges.TimeRangeMs.Min;

    private void UpdateKVp(int value)
    {
        KVp = Math.Clamp(value, _ranges.KvpRange.Min, _ranges.KvpRange.Max);
        OnPropertyChanged(nameof(Mas));
        IncreaseKVpCommand.RaiseCanExecuteChanged();
        DecreaseKVpCommand.RaiseCanExecuteChanged();
    }

    private void UpdateMA(int value)
    {
        MA = Math.Clamp(value, _ranges.MaRange.Min, _ranges.MaRange.Max);
        OnPropertyChanged(nameof(Mas));
        IncreaseMACommand.RaiseCanExecuteChanged();
        DecreaseMACommand.RaiseCanExecuteChanged();
    }

    private void UpdateTime(int value)
    {
        ExposureTimeMs = Math.Clamp(value, _ranges.TimeRangeMs.Min, _ranges.TimeRangeMs.Max);
        OnPropertyChanged(nameof(Mas));
        IncreaseTimeCommand.RaiseCanExecuteChanged();
        DecreaseTimeCommand.RaiseCanExecuteChanged();
    }

    private async Task ApplyParametersAsync(CancellationToken ct)
    {
        try
        {
            await _exposureService.SetExposureParametersAsync(_parameters, ct);
            _appliedParameters = _parameters;
            ApplyCommand.RaiseCanExecuteChanged();
            Debug.WriteLine($"Exposure parameters applied: kVp={_parameters.KVp}, mA={_parameters.MA}, time={_parameters.ExposureTimeMs}ms");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply exposure parameters: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets AEC mode (makes mA and time read-only).
    /// </summary>
    public void SetAecMode(bool isAec)
    {
        Parameters = _parameters with { IsAecMode = isAec };
        IsReadOnly = isAec;
        Debug.WriteLine($"AEC mode: {(isAec ? "ENABLED" : "DISABLED")}");
    }
}
