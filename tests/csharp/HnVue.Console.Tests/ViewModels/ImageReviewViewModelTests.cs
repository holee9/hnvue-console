using HnVue.Console.Models;
using HnVue.Console.Rendering;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for ImageReviewViewModel.
/// SPEC-UI-001: FR-UI-03, 04, 05 Image Viewer, Measurement, QC.
/// </summary>
[Trait("Requirement", "NFR-UI-07")]
public class ImageReviewViewModelTests : ViewModelTestBase
{
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IQCService> _mockQcService;
    private readonly MeasurementOverlayService _measurementService;
    private readonly GrayscaleRenderer _renderer;
    private readonly WindowLevelTransform _windowLevelTransform;

    public ImageReviewViewModelTests()
    {
        _mockImageService = CreateLooseMockService<IImageService>();
        _mockQcService = CreateLooseMockService<IQCService>();
        _measurementService = new MeasurementOverlayService();
        _renderer = new GrayscaleRenderer();
        _windowLevelTransform = new WindowLevelTransform();
    }

    private ImageReviewViewModel CreateViewModel()
    {
        return new ImageReviewViewModel(
            _mockImageService.Object,
            _mockQcService.Object,
            _measurementService,
            _renderer,
            _windowLevelTransform);
    }

    private static ImageData CreateTestImageData(string imageId = "IMG001")
    {
        return new ImageData
        {
            ImageId = imageId,
            PixelData = new byte[512 * 512 * 2],
            Width = 512,
            Height = 512,
            BitsPerPixel = 16,
            PixelSpacing = new PixelSpacing { RowSpacingMm = 0.1m, ColumnSpacingMm = 0.1m },
            CurrentWindowLevel = new WindowLevel { WindowCenter = 32768, WindowWidth = 65536 }
        };
    }

    // --- Constructor / Initialization ---

    [Fact]
    public void Constructor_Initializes_Default_Values()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        Assert.False(vm.IsImageLoaded);
        Assert.Equal(1.0, vm.ZoomFactor);
        Assert.Equal(ImageOrientation.None, vm.Orientation);
        Assert.NotNull(vm.Measurements);
        Assert.Empty(vm.Measurements);
        Assert.Equal(string.Empty, vm.CurrentImageId);
        Assert.False(vm.IsMeasuring);
        Assert.Equal(string.Empty, vm.QCNotes);
    }

    [Fact]
    public void Constructor_Creates_All_Commands()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.IncreaseWindowCommand);
        Assert.NotNull(vm.DecreaseWindowCommand);
        Assert.NotNull(vm.IncreaseLevelCommand);
        Assert.NotNull(vm.DecreaseLevelCommand);
        Assert.NotNull(vm.ResetWindowLevelCommand);
        Assert.NotNull(vm.ZoomInCommand);
        Assert.NotNull(vm.ZoomOutCommand);
        Assert.NotNull(vm.ResetZoomCommand);
        Assert.NotNull(vm.RotateLeftCommand);
        Assert.NotNull(vm.RotateRightCommand);
        Assert.NotNull(vm.FlipHorizontalCommand);
        Assert.NotNull(vm.FlipVerticalCommand);
        Assert.NotNull(vm.SelectDistanceToolCommand);
        Assert.NotNull(vm.SelectAngleToolCommand);
        Assert.NotNull(vm.SelectCobbToolCommand);
        Assert.NotNull(vm.SelectAnnotationToolCommand);
        Assert.NotNull(vm.ClearMeasurementsCommand);
        Assert.NotNull(vm.AcceptImageCommand);
        Assert.NotNull(vm.RejectImageCommand);
        Assert.NotNull(vm.ReprocessImageCommand);
    }

    [Fact]
    public void Constructor_Throws_On_Null_ImageService()
    {
        Assert.Throws<ArgumentNullException>(() => new ImageReviewViewModel(
            null!, _mockQcService.Object, _measurementService, _renderer, _windowLevelTransform));
    }

    [Fact]
    public void Constructor_Throws_On_Null_QCService()
    {
        Assert.Throws<ArgumentNullException>(() => new ImageReviewViewModel(
            _mockImageService.Object, null!, _measurementService, _renderer, _windowLevelTransform));
    }

    // --- Image Loading ---

    [Fact]
    public async Task LoadImageAsync_Sets_IsImageLoaded_True_On_Success()
    {
        var imageData = CreateTestImageData();
        _mockImageService
            .Setup(s => s.GetImageAsync("IMG001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageData);

        var vm = CreateViewModel();

        await vm.LoadImageAsync("IMG001");

        Assert.True(vm.IsImageLoaded);
        Assert.Equal("IMG001", vm.CurrentImageId);
    }

    [Fact]
    public async Task LoadImageAsync_Sets_IsImageLoaded_False_On_Failure()
    {
        _mockImageService
            .Setup(s => s.GetImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var vm = CreateViewModel();

        await vm.LoadImageAsync("INVALID");

        Assert.False(vm.IsImageLoaded);
    }

    [Fact]
    public async Task LoadImageAsync_Raises_PropertyChanged_For_IsImageLoaded()
    {
        var imageData = CreateTestImageData();
        _mockImageService
            .Setup(s => s.GetImageAsync("IMG001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageData);

        var vm = CreateViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName ?? "");

        await vm.LoadImageAsync("IMG001");

        Assert.Contains("IsImageLoaded", changedProperties);
        Assert.Contains("CurrentImageId", changedProperties);
    }

    // --- Window/Level ---

    [Fact]
    public async Task WindowLevel_Commands_CannotExecute_When_No_Image_Loaded()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IncreaseWindowCommand.CanExecute(null));
        Assert.False(vm.DecreaseWindowCommand.CanExecute(null));
        Assert.False(vm.IncreaseLevelCommand.CanExecute(null));
        Assert.False(vm.DecreaseLevelCommand.CanExecute(null));
        Assert.False(vm.ResetWindowLevelCommand.CanExecute(null));
    }

    [Fact]
    public async Task WindowLevel_Commands_CanExecute_When_Image_Loaded()
    {
        var vm = await CreateViewModelWithLoadedImage();

        Assert.True(vm.IncreaseWindowCommand.CanExecute(null));
        Assert.True(vm.DecreaseWindowCommand.CanExecute(null));
        Assert.True(vm.IncreaseLevelCommand.CanExecute(null));
        Assert.True(vm.DecreaseLevelCommand.CanExecute(null));
        Assert.True(vm.ResetWindowLevelCommand.CanExecute(null));
    }

    [Fact]
    public async Task IncreaseWindowCommand_Changes_WindowWidth()
    {
        var vm = await CreateViewModelWithLoadedImage();
        var initialWidth = vm.WindowWidth;

        vm.IncreaseWindowCommand.Execute(null);

        // WindowWidth should change (increased by 100 per AdjustWindowLevel call)
        Assert.NotEqual(initialWidth, vm.WindowWidth);
    }

    [Fact]
    public async Task ResetWindowLevelCommand_Resets_To_Defaults()
    {
        var vm = await CreateViewModelWithLoadedImage();

        // Change window level first
        vm.IncreaseWindowCommand.Execute(null);

        // Reset
        vm.ResetWindowLevelCommand.Execute(null);

        Assert.Equal(32768, vm.WindowCenter);
        Assert.Equal(65536, vm.WindowWidth);
    }

    // --- Zoom/Pan ---

    [Fact]
    public async Task ZoomInCommand_Increases_ZoomFactor()
    {
        var vm = await CreateViewModelWithLoadedImage();
        var initial = vm.ZoomFactor;

        vm.ZoomInCommand.Execute(null);

        Assert.True(vm.ZoomFactor > initial);
    }

    [Fact]
    public async Task ZoomOutCommand_Decreases_ZoomFactor()
    {
        var vm = await CreateViewModelWithLoadedImage();
        var initial = vm.ZoomFactor;

        vm.ZoomOutCommand.Execute(null);

        Assert.True(vm.ZoomFactor < initial);
    }

    [Fact]
    public async Task ResetZoomCommand_Resets_To_1()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.ZoomInCommand.Execute(null);

        vm.ResetZoomCommand.Execute(null);

        Assert.Equal(1.0, vm.ZoomFactor);
    }

    [Fact]
    public async Task ZoomFactor_Raises_PropertyChanged()
    {
        var vm = await CreateViewModelWithLoadedImage();
        var changedProperties = GetChangedProperties(vm, () => vm.ZoomInCommand.Execute(null));

        Assert.Contains("ZoomFactor", changedProperties);
    }

    // --- Rotate/Flip ---

    [Fact]
    public async Task RotateRightCommand_Sets_Orientation_Rotate90()
    {
        var vm = await CreateViewModelWithLoadedImage();

        vm.RotateRightCommand.Execute(null);

        Assert.Equal(ImageOrientation.Rotate90, vm.Orientation);
    }

    [Fact]
    public async Task RotateLeftCommand_Sets_Orientation_Rotate270()
    {
        var vm = await CreateViewModelWithLoadedImage();

        vm.RotateLeftCommand.Execute(null);

        Assert.Equal(ImageOrientation.Rotate270, vm.Orientation);
    }

    [Fact]
    public async Task FlipHorizontalCommand_Sets_Orientation()
    {
        var vm = await CreateViewModelWithLoadedImage();

        vm.FlipHorizontalCommand.Execute(null);

        Assert.Equal(ImageOrientation.FlipHorizontal, vm.Orientation);
    }

    [Fact]
    public async Task FlipVerticalCommand_Sets_Orientation()
    {
        var vm = await CreateViewModelWithLoadedImage();

        vm.FlipVerticalCommand.Execute(null);

        Assert.Equal(ImageOrientation.FlipVertical, vm.Orientation);
    }

    [Fact]
    public async Task ResetOrientationCommand_Resets_To_None()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.RotateRightCommand.Execute(null);

        vm.ResetOrientationCommand.Execute(null);

        Assert.Equal(ImageOrientation.None, vm.Orientation);
    }

    // --- Measurement Tool Selection ---

    [Fact]
    public void SelectDistanceToolCommand_Sets_SelectedTool_And_IsMeasuring()
    {
        var vm = CreateViewModel();

        vm.SelectDistanceToolCommand.Execute(null);

        Assert.Equal(MeasurementType.Distance, vm.SelectedMeasurementTool);
        Assert.True(vm.IsMeasuring);
    }

    [Fact]
    public void SelectAngleToolCommand_Sets_Correct_Tool()
    {
        var vm = CreateViewModel();

        vm.SelectAngleToolCommand.Execute(null);

        Assert.Equal(MeasurementType.Angle, vm.SelectedMeasurementTool);
        Assert.True(vm.IsMeasuring);
    }

    [Fact]
    public void SelectCobbToolCommand_Sets_Correct_Tool()
    {
        var vm = CreateViewModel();

        vm.SelectCobbToolCommand.Execute(null);

        Assert.Equal(MeasurementType.CobbAngle, vm.SelectedMeasurementTool);
        Assert.True(vm.IsMeasuring);
    }

    [Fact]
    public void SelectAnnotationToolCommand_Sets_Correct_Tool()
    {
        var vm = CreateViewModel();

        vm.SelectAnnotationToolCommand.Execute(null);

        Assert.Equal(MeasurementType.Annotation, vm.SelectedMeasurementTool);
        Assert.True(vm.IsMeasuring);
    }

    [Fact]
    public void SelectedMeasurementTool_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        // SelectAnnotationToolCommand selects a non-default tool; capture changes from that call
        var changedProperties = GetChangedProperties(vm, () => vm.SelectAnnotationToolCommand.Execute(null));

        // SelectedMeasurementTool changes from Distance(default=0) to Annotation(3)
        Assert.Contains("SelectedMeasurementTool", changedProperties);
        Assert.Contains("IsMeasuring", changedProperties);
    }

    // --- QC Operations ---

    [Fact]
    public async Task AcceptImageCommand_Invokes_QCService_And_Navigates()
    {
        var vm = await CreateViewModelWithLoadedImage();
        string? navigatedTo = null;
        vm.NavigationRequested += (_, view) => navigatedTo = view;

        _mockQcService
            .Setup(s => s.AcceptImageAsync("IMG001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QCActionResult { Success = true, ImageId = "IMG001" });

        vm.AcceptImageCommand.Execute(null);
        await Task.Delay(100);

        _mockQcService.Verify(s => s.AcceptImageAsync("IMG001", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Worklist", navigatedTo);
    }

    [Fact]
    public async Task RejectImageCommand_Invokes_QCService_With_Reason()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectedRejectionReason = RejectionReason.PatientMotion;
        vm.QCNotes = "Motion detected";
        string? navigatedTo = null;
        vm.NavigationRequested += (_, view) => navigatedTo = view;

        _mockQcService
            .Setup(s => s.RejectImageAsync("IMG001", RejectionReason.PatientMotion, "Motion detected", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QCActionResult { Success = true, ImageId = "IMG001" });

        vm.RejectImageCommand.Execute(null);
        await Task.Delay(100);

        _mockQcService.Verify(
            s => s.RejectImageAsync("IMG001", RejectionReason.PatientMotion, "Motion detected", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal("Acquisition", navigatedTo);
    }

    [Fact]
    public async Task ReprocessImageCommand_Invokes_QCService()
    {
        var vm = await CreateViewModelWithLoadedImage();
        string? updatedImageId = null;
        vm.ImageUpdateRequested += (_, id) => updatedImageId = id;

        _mockQcService
            .Setup(s => s.ReprocessImageAsync("IMG001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QCActionResult { Success = true, ImageId = "IMG001" });

        vm.ReprocessImageCommand.Execute(null);
        await Task.Delay(100);

        _mockQcService.Verify(s => s.ReprocessImageAsync("IMG001", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("IMG001", updatedImageId);
    }

    [Fact]
    public void QC_Commands_CannotExecute_When_No_Image()
    {
        var vm = CreateViewModel();

        Assert.False(vm.AcceptImageCommand.CanExecute(null));
        Assert.False(vm.RejectImageCommand.CanExecute(null));
        Assert.False(vm.ReprocessImageCommand.CanExecute(null));
    }

    // --- PropertyChanged ---

    [Fact]
    public void QCNotes_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.QCNotes = "Test note");

        Assert.Contains("QCNotes", changedProperties);
    }

    [Fact]
    public void RejectionReasons_Contains_All_Enum_Values()
    {
        var vm = CreateViewModel();

        var allReasons = Enum.GetValues<RejectionReason>();
        Assert.Equal(allReasons.Length, vm.RejectionReasons.Count);
    }

    // --- Helper ---

    private async Task<ImageReviewViewModel> CreateViewModelWithLoadedImage()
    {
        var imageData = CreateTestImageData();
        _mockImageService
            .Setup(s => s.GetImageAsync("IMG001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageData);

        var vm = CreateViewModel();
        await vm.LoadImageAsync("IMG001");
        return vm;
    }
}
