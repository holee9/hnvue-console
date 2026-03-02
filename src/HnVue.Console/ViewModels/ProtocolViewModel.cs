using System.Collections.ObjectModel;
using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Protocol selection ViewModel for body part and projection.
/// SPEC-UI-001: FR-UI-06 Protocol Selection.
/// </summary>
public class ProtocolViewModel : ViewModelBase
{
    /// <summary>
    /// Raised when a protocol preset is selected and exposure parameters should be updated.
    /// The ProtocolPreset argument contains the recommended exposure parameters.
    /// </summary>
    public event EventHandler<ProtocolPreset>? ProtocolPresetSelected;
    private readonly IProtocolService _protocolService;
    private ProtocolSelection? _selectedProtocol;
    private ProtocolPreset? _selectedProtocolPreset;
    private bool _isLoading;

    /// <summary>
    /// Initializes a new instance of <see cref="ProtocolViewModel"/>.
    /// </summary>
    public ProtocolViewModel(IProtocolService protocolService)
    {
        _protocolService = protocolService ?? throw new ArgumentNullException(nameof(protocolService));
        BodyParts = new ObservableCollection<BodyPart>();
        Projections = new ObservableCollection<Projection>();

        SelectProtocolCommand = new AsyncRelayCommand(
            ct => ExecuteSelectProtocolAsync(ct),
            () => _selectedProtocol != null && !IsLoading);
    }

    /// <summary>
    /// Gets the available body parts.
    /// </summary>
    public ObservableCollection<BodyPart> BodyParts { get; }

    /// <summary>
    /// Gets the available projections.
    /// </summary>
    public ObservableCollection<Projection> Projections { get; }

    /// <summary>
    /// Gets or sets the selected body part.
    /// </summary>
    public BodyPart? SelectedBodyPart { get; set; }

    /// <summary>
    /// Gets or sets the selected projection.
    /// </summary>
    public Projection? SelectedProjection { get; set; }

    /// <summary>
    /// Gets or sets the selected protocol preset.
    /// </summary>
    public ProtocolPreset? SelectedProtocol
    {
        get => _selectedProtocolPreset;
        set
        {
            if (value != null)
            {
                _selectedProtocol = new ProtocolSelection { BodyPartCode = value.BodyPartCode, ProjectionCode = value.ProjectionCode };
                _selectedProtocolPreset = value;
            }
            else
            {
                _selectedProtocol = null;
                _selectedProtocolPreset = null;
            }
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether a selection is in progress.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Gets the select protocol command.
    /// </summary>
    public AsyncRelayCommand SelectProtocolCommand { get; }

    /// <summary>
    /// Loads body parts.
    /// </summary>
    public async Task LoadBodyPartsAsync(CancellationToken ct = default)
    {
        BodyParts.Clear();

        try
        {
            var parts = await _protocolService.GetBodyPartsAsync(ct);
            foreach (var part in parts)
            {
                BodyParts.Add(part);
            }
            Debug.WriteLine($"Loaded {BodyParts.Count} body parts");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load body parts: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads projections for the specified body part.
    /// </summary>
    public async Task LoadProjectionsAsync(string bodyPartCode, CancellationToken ct = default)
    {
        Projections.Clear();

        try
        {
            var projections = await _protocolService.GetProjectionsAsync(bodyPartCode, ct);
            foreach (var proj in projections)
            {
                Projections.Add(proj);
            }
            Debug.WriteLine($"Loaded {Projections.Count} projections for {bodyPartCode}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load projections: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets protocol preset for body part and projection.
    /// </summary>
    public async Task<ProtocolPreset?> GetPresetAsync(string bodyPartCode, string projectionCode, CancellationToken ct = default)
    {
        try
        {
            return await _protocolService.GetProtocolPresetAsync(bodyPartCode, projectionCode, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get preset: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Executes protocol selection.
    /// </summary>
    private async Task ExecuteSelectProtocolAsync(CancellationToken ct)
    {
        if (_selectedProtocol == null)
            return;

        IsLoading = true;

        try
        {
            var result = await _protocolService.SelectProtocolAsync(_selectedProtocol, ct);

            Debug.WriteLine($"Protocol selected: {result.Preset.ProtocolId}");
            ProtocolPresetSelected?.Invoke(this, result.Preset);

            _selectedProtocol = new ProtocolSelection
            {
                BodyPartCode = result.Preset.BodyPartCode,
                ProjectionCode = result.Preset.ProjectionCode
            };
            _selectedProtocolPreset = result.Preset;
            OnPropertyChanged(nameof(SelectedProtocol));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Protocol selection failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
