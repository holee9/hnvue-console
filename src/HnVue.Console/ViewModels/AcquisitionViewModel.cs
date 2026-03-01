using System.Diagnostics;
using System.Windows.Media.Imaging;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Acquisition screen ViewModel composing sub-ViewModels.
/// SPEC-UI-001: FR-UI-06, 07, 09, 10, 11 (Protocol, Exposure, Preview, Dose, AEC).
/// </summary>
public class AcquisitionViewModel : ViewModelBase
{
    private readonly IExposureService _exposureService;
    private readonly IProtocolService _protocolService;
    private readonly IAECService _aecService;
    private readonly IDoseService _doseService;
    private readonly CancellationTokenSource _previewCancellation;
    private WriteableBitmap? _previewBitmap;
    private bool _isPreviewActive;
    private bool _isExposing;
    private string _selectedProcedureId = string.Empty;

    /// <summary>
    /// Initializes a new instance of <see cref="AcquisitionViewModel"/>.
    /// </summary>
    public AcquisitionViewModel(
        IExposureService exposureService,
        IProtocolService protocolService,
        IAECService aecService,
        IDoseService doseService,
        AECViewModel aecViewModel,
        ExposureParameterViewModel exposureParameterViewModel,
        ProtocolViewModel protocolViewModel,
        DoseViewModel doseViewModel)
    {
        _exposureService = exposureService ?? throw new ArgumentNullException(nameof(exposureService));
        _protocolService = protocolService ?? throw new ArgumentNullException(nameof(protocolService));
        _aecService = aecService ?? throw new ArgumentNullException(nameof(aecService));
        _doseService = doseService ?? throw new ArgumentNullException(nameof(doseService));

        _previewCancellation = new CancellationTokenSource();

        // Get sub-ViewModels from DI
        AECViewModel = aecViewModel ?? throw new ArgumentNullException(nameof(aecViewModel));
        ExposureParameterViewModel = exposureParameterViewModel ?? throw new ArgumentNullException(nameof(exposureParameterViewModel));
        ProtocolViewModel = protocolViewModel ?? throw new ArgumentNullException(nameof(protocolViewModel));
        DoseViewModel = doseViewModel ?? throw new ArgumentNullException(nameof(doseViewModel));

        // Commands
        StartPreviewCommand = new AsyncRelayCommand(
            ct => ExecuteStartPreviewAsync(ct),
            () => !IsPreviewActive);

        StopPreviewCommand = new AsyncRelayCommand(
            ct => ExecuteStopPreviewAsync(ct),
            () => IsPreviewActive);

        TriggerExposureCommand = new AsyncRelayCommand(
            ct => ExecuteTriggerExposureAsync(ct),
            () => IsPreviewActive && !IsExposing && !string.IsNullOrEmpty(_selectedProcedureId));

        // Subscribe to AEC changes to update exposure parameters
        Task.Run(async () => await SubscribeAecChangesAsync(_previewCancellation.Token));
    }

    /// <summary>
    /// Gets the AEC sub-ViewModel.
    /// </summary>
    public AECViewModel AECViewModel { get; }

    /// <summary>
    /// Gets the Exposure Parameter sub-ViewModel.
    /// </summary>
    public ExposureParameterViewModel ExposureParameterViewModel { get; }

    /// <summary>
    /// Gets the Protocol sub-ViewModel.
    /// </summary>
    public ProtocolViewModel ProtocolViewModel { get; }

    /// <summary>
    /// Gets the Dose sub-ViewModel.
    /// </summary>
    public DoseViewModel DoseViewModel { get; }

    /// <summary>
    /// Gets or sets the current preview bitmap.
    /// </summary>
    public WriteableBitmap? PreviewBitmap
    {
        get => _previewBitmap;
        set => SetProperty(ref _previewBitmap, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether preview is active.
    /// </summary>
    public bool IsPreviewActive
    {
        get => _isPreviewActive;
        set => SetProperty(ref _isPreviewActive, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether an exposure is in progress.
    /// </summary>
    public bool IsExposing
    {
        get => _isExposing;
        set => SetProperty(ref _isExposing, value);
    }

    /// <summary>
    /// Gets or sets the selected procedure ID for exposure.
    /// </summary>
    public string SelectedProcedureId
    {
        get => _selectedProcedureId;
        set => SetProperty(ref _selectedProcedureId, value);
    }

    /// <summary>
    /// Gets the start preview command.
    /// </summary>
    public AsyncRelayCommand StartPreviewCommand { get; }

    /// <summary>
    /// Gets the stop preview command.
    /// </summary>
    public AsyncRelayCommand StopPreviewCommand { get; }

    /// <summary>
    /// Gets the trigger exposure command.
    /// </summary>
    public AsyncRelayCommand TriggerExposureCommand { get; }

    /// <summary>
    /// Initializes the acquisition screen.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            // Load body parts for protocol selection
            await ProtocolViewModel.LoadBodyPartsAsync(ct);

            // Start dose monitoring
            _ = Task.Run(async () => await DoseViewModel.StartDoseUpdatesAsync(ct), ct);

            Debug.WriteLine("[AcquisitionViewModel] Initialized");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AcquisitionViewModel] Initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the selected procedure for exposure.
    /// </summary>
    public void SetProcedure(string procedureId, string studyId)
    {
        SelectedProcedureId = procedureId;
        Debug.WriteLine($"[AcquisitionViewModel] Procedure set: {procedureId}");

        // Reset cumulative dose for new study
        if (!string.IsNullOrEmpty(studyId))
        {
            _ = Task.Run(async () => await DoseViewModel.ResetCumulativeDoseAsync(studyId));
        }
    }

    /// <summary>
    /// Starts the preview stream.
    /// </summary>
    private async Task ExecuteStartPreviewAsync(CancellationToken ct)
    {
        try
        {
            IsPreviewActive = true;
            Debug.WriteLine("[AcquisitionViewModel] Starting preview");

            await foreach (var frame in _exposureService.SubscribePreviewFramesAsync(ct))
            {
                if (!IsPreviewActive)
                    break;

                // Update preview bitmap on UI thread
                UpdatePreviewBitmap(frame);

                // 200ms timing contract for frame updates
                await Task.Delay(50, ct); // Cap at ~20 FPS
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[AcquisitionViewModel] Preview canceled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AcquisitionViewModel] Preview error: {ex.Message}");
        }
        finally
        {
            IsPreviewActive = false;
        }
    }

    /// <summary>
    /// Stops the preview stream.
    /// </summary>
    private async Task ExecuteStopPreviewAsync(CancellationToken ct)
    {
        IsPreviewActive = false;
        _previewCancellation.Cancel();
        await Task.CompletedTask;
        Debug.WriteLine("[AcquisitionViewModel] Preview stopped");
    }

    /// <summary>
    /// Triggers an exposure.
    /// </summary>
    private async Task ExecuteTriggerExposureAsync(CancellationToken ct)
    {
        if (IsExposing)
            return;

        IsExposing = true;

        try
        {
            var request = new ExposureTriggerRequest
            {
                StudyId = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                ProtocolId = ProtocolViewModel.SelectedProtocol?.ProtocolId ?? "DEFAULT",
                Parameters = ExposureParameterViewModel.Parameters
            };

            Debug.WriteLine($"[AcquisitionViewModel] Triggering exposure: {request.ProtocolId}");

            var result = await _exposureService.TriggerExposureAsync(request, ct);

            if (result.Success)
            {
                Debug.WriteLine($"[AcquisitionViewModel] Exposure complete: {result.ImageId}");
                // TODO: Navigate to image review
            }
            else
            {
                Debug.WriteLine($"[AcquisitionViewModel] Exposure failed: {result.ErrorMessage}");
                // TODO: Show error dialog
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AcquisitionViewModel] Exposure error: {ex.Message}");
        }
        finally
        {
            IsExposing = false;
        }
    }

    /// <summary>
    /// Updates the preview bitmap from frame data.
    /// </summary>
    private void UpdatePreviewBitmap(PreviewFrame frame)
    {
        try
        {
            // Create or update WriteableBitmap
            if (_previewBitmap == null ||
                _previewBitmap.PixelWidth != frame.Width ||
                _previewBitmap.PixelHeight != frame.Height)
            {
                _previewBitmap = new WriteableBitmap(
                    frame.Width, frame.Height,
                    96, 96,
                    System.Windows.Media.PixelFormats.Gray8,
                    null);
            }

            // Write pixel data
            _previewBitmap.WritePixels(
                new System.Windows.Int32Rect(0, 0, frame.Width, frame.Height),
                frame.PixelData,
                frame.Width,
                0);

            PreviewBitmap = _previewBitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AcquisitionViewModel] Failed to update preview: {ex.Message}");
        }
    }

    /// <summary>
    /// Subscribes to AEC state changes.
    /// </summary>
    private async Task SubscribeAecChangesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var isAecEnabled in _aecService.SubscribeAECStateChangesAsync(ct))
            {
                // Update ExposureParameterViewModel when AEC state changes
                ExposureParameterViewModel.SetAecMode(isAecEnabled);

                Debug.WriteLine($"[AcquisitionViewModel] AEC state changed: {isAecEnabled}");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        _previewCancellation.Cancel();
        _previewCancellation.Dispose();
    }
}
