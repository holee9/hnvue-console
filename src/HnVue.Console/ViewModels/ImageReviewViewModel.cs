using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Rendering;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Image review ViewModel for viewing, measuring, and QC.
/// SPEC-UI-001: FR-UI-03, 04, 05 Image Viewer, Measurement, QC.
/// </summary>
public class ImageReviewViewModel : ViewModelBase
{
    /// <summary>
    /// Raised when navigation to another view is requested.
    /// The string argument is the view name (e.g., "Acquisition").
    /// </summary>
    public event EventHandler<string>? NavigationRequested;

    /// <summary>
    /// Raised when the current image has been updated and should be reloaded.
    /// The string argument is the new image ID.
    /// </summary>
    public event EventHandler<string>? ImageUpdateRequested;
    private readonly IImageService _imageService;
    private readonly IQCService _qcService;
    private readonly MeasurementOverlayService _measurementService;
    private readonly GrayscaleRenderer _renderer;
    private readonly WindowLevelTransform _windowLevelTransform;

    private string _currentImageId = string.Empty;
    private ImageData? _currentImage;
    private WriteableBitmap? _bitmap;
    private bool _isImageLoaded;
    private MeasurementType _selectedMeasurementTool;
    private bool _isMeasuring;
    private string _qcNotes = string.Empty;

    // Measurement state
    private readonly List<Point> _measurementPoints = new();
    private double _zoomFactor = 1.0;
    private double _panX = 0.0;
    private double _panY = 0.0;
    private ImageOrientation _orientation = ImageOrientation.None;

    /// <summary>
    /// Initializes a new instance of <see cref="ImageReviewViewModel"/>.
    /// </summary>
    public ImageReviewViewModel(
        IImageService imageService,
        IQCService qcService,
        MeasurementOverlayService measurementService,
        GrayscaleRenderer renderer,
        WindowLevelTransform windowLevelTransform)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _qcService = qcService ?? throw new ArgumentNullException(nameof(qcService));
        _measurementService = measurementService ?? throw new ArgumentNullException(nameof(measurementService));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _windowLevelTransform = windowLevelTransform ?? throw new ArgumentNullException(nameof(windowLevelTransform));

        Measurements = new ObservableCollection<MeasurementOverlay>();

        // Commands - Window/Level
        IncreaseWindowCommand = new RelayCommand(() => AdjustWindowLevel(0, 100), () => _isImageLoaded);
        DecreaseWindowCommand = new RelayCommand(() => AdjustWindowLevel(0, -100), () => _isImageLoaded);
        IncreaseLevelCommand = new RelayCommand(() => AdjustWindowLevel(100, 0), () => _isImageLoaded);
        DecreaseLevelCommand = new RelayCommand(() => AdjustWindowLevel(-100, 0), () => _isImageLoaded);
        ResetWindowLevelCommand = new RelayCommand(() => ResetWindowLevel(), () => _isImageLoaded);

        // Commands - Zoom/Pan
        ZoomInCommand = new RelayCommand(() => AdjustZoom(1.25), () => _isImageLoaded);
        ZoomOutCommand = new RelayCommand(() => AdjustZoom(0.8), () => _isImageLoaded);
        ResetZoomCommand = new RelayCommand(() => ResetZoom(), () => _isImageLoaded);
        PanLeftCommand = new RelayCommand(() => AdjustPan(-50, 0), () => _isImageLoaded);
        PanRightCommand = new RelayCommand(() => AdjustPan(50, 0), () => _isImageLoaded);
        PanUpCommand = new RelayCommand(() => AdjustPan(0, -50), () => _isImageLoaded);
        PanDownCommand = new RelayCommand(() => AdjustPan(0, 50), () => _isImageLoaded);

        // Commands - Orientation
        RotateLeftCommand = new RelayCommand(() => Rotate(-90), () => _isImageLoaded);
        RotateRightCommand = new RelayCommand(() => Rotate(90), () => _isImageLoaded);
        FlipHorizontalCommand = new RelayCommand(() => Flip(ImageOrientation.FlipHorizontal), () => _isImageLoaded);
        FlipVerticalCommand = new RelayCommand(() => Flip(ImageOrientation.FlipVertical), () => _isImageLoaded);
        ResetOrientationCommand = new RelayCommand(() => ResetOrientation(), () => _isImageLoaded);

        // Commands - Measurement
        SelectDistanceToolCommand = new RelayCommand(() => SelectMeasurementTool(MeasurementType.Distance));
        SelectAngleToolCommand = new RelayCommand(() => SelectMeasurementTool(MeasurementType.Angle));
        SelectCobbToolCommand = new RelayCommand(() => SelectMeasurementTool(MeasurementType.CobbAngle));
        SelectAnnotationToolCommand = new RelayCommand(() => SelectMeasurementTool(MeasurementType.Annotation));
        ClearMeasurementsCommand = new RelayCommand(() => ClearMeasurements(), () => _isImageLoaded);

        // Commands - QC
        AcceptImageCommand = new AsyncRelayCommand(ct => ExecuteAcceptAsync(ct), () => _isImageLoaded);
        RejectImageCommand = new AsyncRelayCommand(ct => ExecuteRejectAsync(ct), () => _isImageLoaded);
        ReprocessImageCommand = new AsyncRelayCommand(ct => ExecuteReprocessAsync(ct), () => _isImageLoaded);
    }

    /// <summary>
    /// Gets or sets the current image ID.
    /// </summary>
    public string CurrentImageId
    {
        get => _currentImageId;
        set => SetProperty(ref _currentImageId, value);
    }

    /// <summary>
    /// Gets or sets the current bitmap.
    /// </summary>
    public WriteableBitmap? Bitmap
    {
        get => _bitmap;
        set => SetProperty(ref _bitmap, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether an image is loaded.
    /// </summary>
    public bool IsImageLoaded
    {
        get => _isImageLoaded;
        set => SetProperty(ref _isImageLoaded, value);
    }

    /// <summary>
    /// Gets or sets the zoom factor.
    /// </summary>
    public double ZoomFactor
    {
        get => _zoomFactor;
        set => SetProperty(ref _zoomFactor, value);
    }

    /// <summary>
    /// Gets the window center value.
    /// </summary>
    public int WindowCenter => _windowLevelTransform.CurrentWindowLevel.WindowCenter;

    /// <summary>
    /// Gets the window width value.
    /// </summary>
    public int WindowWidth => _windowLevelTransform.CurrentWindowLevel.WindowWidth;

    /// <summary>
    /// Gets the current orientation.
    /// </summary>
    public ImageOrientation Orientation => _orientation;

    /// <summary>
    /// Gets or sets the selected measurement tool.
    /// </summary>
    public MeasurementType SelectedMeasurementTool
    {
        get => _selectedMeasurementTool;
        set => SetProperty(ref _selectedMeasurementTool, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether measuring is in progress.
    /// </summary>
    public bool IsMeasuring
    {
        get => _isMeasuring;
        set => SetProperty(ref _isMeasuring, value);
    }

    /// <summary>
    /// Gets the measurements collection.
    /// </summary>
    public ObservableCollection<MeasurementOverlay> Measurements { get; }

    /// <summary>
    /// Gets or sets the QC notes.
    /// </summary>
    public string QCNotes
    {
        get => _qcNotes;
        set => SetProperty(ref _qcNotes, value);
    }

    /// <summary>
    /// Gets the rejection reasons collection.
    /// </summary>
    public ObservableCollection<RejectionReason> RejectionReasons { get; } = new(Enum.GetValues<RejectionReason>().Cast<RejectionReason>());

    /// <summary>
    /// Gets or sets the selected rejection reason.
    /// </summary>
    public RejectionReason SelectedRejectionReason { get; set; } = RejectionReason.ExposureError;

    // Window/Level Commands
    public RelayCommand IncreaseWindowCommand { get; }
    public RelayCommand DecreaseWindowCommand { get; }
    public RelayCommand IncreaseLevelCommand { get; }
    public RelayCommand DecreaseLevelCommand { get; }
    public RelayCommand ResetWindowLevelCommand { get; }

    // Zoom/Pan Commands
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ResetZoomCommand { get; }
    public RelayCommand PanLeftCommand { get; }
    public RelayCommand PanRightCommand { get; }
    public RelayCommand PanUpCommand { get; }
    public RelayCommand PanDownCommand { get; }

    // Orientation Commands
    public RelayCommand RotateLeftCommand { get; }
    public RelayCommand RotateRightCommand { get; }
    public RelayCommand FlipHorizontalCommand { get; }
    public RelayCommand FlipVerticalCommand { get; }
    public RelayCommand ResetOrientationCommand { get; }

    // Measurement Commands
    public RelayCommand SelectDistanceToolCommand { get; }
    public RelayCommand SelectAngleToolCommand { get; }
    public RelayCommand SelectCobbToolCommand { get; }
    public RelayCommand SelectAnnotationToolCommand { get; }
    public RelayCommand ClearMeasurementsCommand { get; }

    // QC Commands
    public AsyncRelayCommand AcceptImageCommand { get; }
    public AsyncRelayCommand RejectImageCommand { get; }
    public AsyncRelayCommand ReprocessImageCommand { get; }

    /// <summary>
    /// Loads an image for review.
    /// </summary>
    public async Task LoadImageAsync(string imageId, CancellationToken ct = default)
    {
        try
        {
            CurrentImageId = imageId;
            _currentImage = await _imageService.GetImageAsync(imageId, ct);

            RenderImage();
            LoadMeasurements();

            IsImageLoaded = true;
            Debug.WriteLine($"[ImageReviewViewModel] Loaded image: {imageId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImageReviewViewModel] Failed to load image: {ex.Message}");
            IsImageLoaded = false;
        }
    }

    /// <summary>
    /// Renders the current image with window/level applied.
    /// </summary>
    private void RenderImage()
    {
        if (_currentImage == null)
            return;

        // Get current W/L from image or use default
        var wl = _currentImage.CurrentWindowLevel ?? new WindowLevel
        {
            WindowCenter = 32768,
            WindowWidth = 65536
        };

        // Update transform
        _windowLevelTransform.SetWindowLevel(wl.WindowCenter, wl.WindowWidth);

        // Render using GrayscaleRenderer
        _renderer.Render(
            _currentImage.PixelData,
            _currentImage.Width,
            _currentImage.Height,
            _currentImage.BitsPerPixel,
            wl.WindowCenter,
            wl.WindowWidth);

        Bitmap = _renderer.Bitmap;
        OnPropertyChanged(nameof(WindowCenter));
        OnPropertyChanged(nameof(WindowWidth));
    }

    /// <summary>
    /// Loads measurements for the current image.
    /// </summary>
    private void LoadMeasurements()
    {
        Measurements.Clear();

        if (string.IsNullOrEmpty(_currentImageId))
            return;

        var measurements = _measurementService.GetMeasurements(_currentImageId);
        foreach (var m in measurements)
        {
            Measurements.Add(m);
        }

        Debug.WriteLine($"[ImageReviewViewModel] Loaded {measurements.Count} measurements");
    }

    /// <summary>
    /// Adjusts window/level settings.
    /// </summary>
    private void AdjustWindowLevel(int levelDelta, int windowDelta)
    {
        var current = _windowLevelTransform.CurrentWindowLevel;
        int newCenter = Math.Clamp(current.WindowCenter + levelDelta, 1, 65535);
        int newWidth = Math.Clamp(current.WindowWidth + windowDelta, 1, 65535);

        _windowLevelTransform.SetWindowLevel(newCenter, newWidth);

        // Re-render image
        RenderImage();

        // Update service
        if (_currentImage != null)
        {
            _ = Task.Run(async () =>
            {
                await _imageService.ApplyWindowLevelAsync(_currentImage.ImageId, current, CancellationToken.None);
            });
        }
    }

    /// <summary>
    /// Resets window/level to default.
    /// </summary>
    private void ResetWindowLevel()
    {
        _windowLevelTransform.SetWindowLevel(32768, 65536);
        RenderImage();
    }

    /// <summary>
    /// Adjusts zoom factor.
    /// </summary>
    private void AdjustZoom(double factor)
    {
        ZoomFactor = Math.Clamp(_zoomFactor * factor, 0.1, 10.0);
        Debug.WriteLine($"[ImageReviewViewModel] Zoom: {ZoomFactor:F2}x");
    }

    /// <summary>
    /// Resets zoom to 1.0.
    /// </summary>
    private void ResetZoom()
    {
        ZoomFactor = 1.0;
        _panX = 0;
        _panY = 0;
        Debug.WriteLine("[ImageReviewViewModel] Zoom reset");
    }

    /// <summary>
    /// Adjusts pan position.
    /// </summary>
    private void AdjustPan(double deltaX, double deltaY)
    {
        _panX += deltaX;
        _panY += deltaY;
        Debug.WriteLine($"[ImageReviewViewModel] Pan: ({_panX:F0}, {_panY:F0})");
    }

    /// <summary>
    /// Rotates the image.
    /// </summary>
    private void Rotate(int degrees)
    {
        _orientation = degrees switch
        {
            -90 => ImageOrientation.Rotate270,
            90 => ImageOrientation.Rotate90,
            180 => ImageOrientation.Rotate180,
            _ => _orientation
        };

        _ = Task.Run(async () =>
        {
            if (_currentImage != null)
            {
                await _imageService.SetOrientationAsync(_currentImage.ImageId, _orientation, CancellationToken.None);
            }
        });

        Debug.WriteLine($"[ImageReviewViewModel] Rotated: {_orientation}");
    }

    /// <summary>
    /// Flips the image.
    /// </summary>
    private void Flip(ImageOrientation flipType)
    {
        _orientation = flipType;

        _ = Task.Run(async () =>
        {
            if (_currentImage != null)
            {
                await _imageService.SetOrientationAsync(_currentImage.ImageId, _orientation, CancellationToken.None);
            }
        });

        Debug.WriteLine($"[ImageReviewViewModel] Flipped: {_orientation}");
    }

    /// <summary>
    /// Resets orientation to none.
    /// </summary>
    private void ResetOrientation()
    {
        _orientation = ImageOrientation.None;
        Debug.WriteLine("[ImageReviewViewModel] Orientation reset");
    }

    /// <summary>
    /// Selects a measurement tool.
    /// </summary>
    private void SelectMeasurementTool(MeasurementType tool)
    {
        SelectedMeasurementTool = tool;
        IsMeasuring = true;
        _measurementPoints.Clear();
        Debug.WriteLine($"[ImageReviewViewModel] Selected tool: {tool}");
    }

    /// <summary>
    /// Handles mouse click for measurement point placement.
    /// </summary>
    public void HandleMeasurementClick(double x, double y)
    {
        if (!IsMeasuring)
            return;

        var point = new Point { X = x, Y = y };
        _measurementPoints.Add(point);

        Debug.WriteLine($"[ImageReviewViewModel] Measurement point {(_measurementPoints.Count)}: ({x:F0}, {y:F0})");

        // Check if measurement is complete
        if (IsMeasurementComplete())
        {
            CompleteMeasurement();
        }
    }

    /// <summary>
    /// Checks if the current measurement has enough points.
    /// </summary>
    private bool IsMeasurementComplete()
    {
        return _selectedMeasurementTool switch
        {
            MeasurementType.Distance => _measurementPoints.Count == 2,
            MeasurementType.Angle => _measurementPoints.Count == 3,
            MeasurementType.CobbAngle => _measurementPoints.Count == 4,
            MeasurementType.Annotation => _measurementPoints.Count >= 1,
            _ => false
        };
    }

    /// <summary>
    /// Completes the current measurement.
    /// </summary>
    private void CompleteMeasurement()
    {
        if (_currentImage == null || _measurementPoints.Count == 0)
            return;

        string displayValue = _selectedMeasurementTool switch
        {
            MeasurementType.Distance => CalculateDistance(),
            MeasurementType.Angle => CalculateAngle(),
            MeasurementType.CobbAngle => CalculateCobbAngle(),
            MeasurementType.Annotation => "Annotation",
            _ => "Unknown"
        };

        var measurement = _measurementService.AddMeasurement(
            _currentImage.ImageId,
            _selectedMeasurementTool,
            _measurementPoints.ToList(),
            displayValue);

        Measurements.Add(measurement);

        // Reset measurement state
        IsMeasuring = false;
        _measurementPoints.Clear();

        Debug.WriteLine($"[ImageReviewViewModel] Completed measurement: {displayValue}");
    }

    /// <summary>
    /// Calculates distance measurement.
    /// </summary>
    private string CalculateDistance()
    {
        if (_measurementPoints.Count < 2)
            return "N/A";

        var distance = _measurementService.CalculateDistance(
            _measurementPoints[0],
            _measurementPoints[1],
            _currentImage!.PixelSpacing);

        return $"{distance:F1} mm";
    }

    /// <summary>
    /// Calculates angle measurement.
    /// </summary>
    private string CalculateAngle()
    {
        if (_measurementPoints.Count < 3)
            return "N/A";

        var angle = _measurementService.CalculateAngle(
            _measurementPoints[0],
            _measurementPoints[1],
            _measurementPoints[2]);

        return $"{angle:F1}°";
    }

    /// <summary>
    /// Calculates Cobb angle measurement.
    /// </summary>
    private string CalculateCobbAngle()
    {
        if (_measurementPoints.Count < 4)
            return "N/A";

        var cobb = _measurementService.CalculateCobbAngle(
            _measurementPoints[0],
            _measurementPoints[1],
            _measurementPoints[2],
            _measurementPoints[3]);

        return $"{cobb:F1}° Cobb";
    }

    /// <summary>
    /// Clears all measurements.
    /// </summary>
    private void ClearMeasurements()
    {
        if (_currentImage != null)
        {
            _measurementService.ClearMeasurements(_currentImage.ImageId);
        }

        Measurements.Clear();
        Debug.WriteLine("[ImageReviewViewModel] Measurements cleared");
    }

    /// <summary>
    /// Executes accept QC action.
    /// </summary>
    private async Task ExecuteAcceptAsync(CancellationToken ct)
    {
        if (_currentImage == null)
            return;

        try
        {
            var result = await _qcService.AcceptImageAsync(_currentImage.ImageId, ct);

            if (result.Success)
            {
                Debug.WriteLine($"[ImageReviewViewModel] Image accepted: {_currentImage.ImageId}");
                NavigationRequested?.Invoke(this, "Worklist");
            }
            else
            {
                Debug.WriteLine($"[ImageReviewViewModel] Accept failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImageReviewViewModel] Accept error: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes reject QC action.
    /// </summary>
    private async Task ExecuteRejectAsync(CancellationToken ct)
    {
        if (_currentImage == null)
            return;

        try
        {
            var result = await _qcService.RejectImageAsync(
                _currentImage.ImageId,
                SelectedRejectionReason,
                QCNotes,
                ct);

            if (result.Success)
            {
                Debug.WriteLine($"[ImageReviewViewModel] Image rejected: {_currentImage.ImageId} - {SelectedRejectionReason}");
                NavigationRequested?.Invoke(this, "Acquisition");
            }
            else
            {
                Debug.WriteLine($"[ImageReviewViewModel] Reject failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImageReviewViewModel] Reject error: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes reprocess QC action.
    /// </summary>
    private async Task ExecuteReprocessAsync(CancellationToken ct)
    {
        if (_currentImage == null)
            return;

        try
        {
            var result = await _qcService.ReprocessImageAsync(_currentImage.ImageId, ct);

            if (result.Success)
            {
                Debug.WriteLine($"[ImageReviewViewModel] Image reprocessing: {_currentImage.ImageId}");
                ImageUpdateRequested?.Invoke(this, _currentImage.ImageId);
            }
            else
            {
                Debug.WriteLine($"[ImageReviewViewModel] Reprocess failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImageReviewViewModel] Reprocess error: {ex.Message}");
        }
    }
}
