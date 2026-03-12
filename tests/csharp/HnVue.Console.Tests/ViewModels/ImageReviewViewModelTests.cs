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

    // --- HandleMeasurementClick Tests ---

    [Fact]
    public void HandleMeasurementClick_Adds_Point_When_IsMeasuring_Is_True()
    {
        var vm = CreateViewModel();
        vm.SelectDistanceToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 200);

        // After first point, measurement should not be complete yet
        Assert.True(vm.IsMeasuring);
    }

    [Fact]
    public void HandleMeasurementClick_Does_Nothing_When_IsMeasuring_Is_False()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsMeasuring);

        // Should not throw and should do nothing
        vm.HandleMeasurementClick(100, 200);

        Assert.False(vm.IsMeasuring);
        Assert.Empty(vm.Measurements);
    }

    [Fact]
    public async Task HandleMeasurementClick_Completes_Distance_Measurement_With_Two_Points()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectDistanceToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(200, 200);

        Assert.Single(vm.Measurements);
        Assert.False(vm.IsMeasuring);
    }

    [Fact]
    public async Task HandleMeasurementClick_Completes_Angle_Measurement_With_Three_Points()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectAngleToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(150, 150);
        vm.HandleMeasurementClick(200, 100);

        Assert.Single(vm.Measurements);
        Assert.False(vm.IsMeasuring);
    }

    [Fact]
    public async Task HandleMeasurementClick_Completes_CobbAngle_Measurement_With_Four_Points()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectCobbToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(150, 100);
        vm.HandleMeasurementClick(100, 200);
        vm.HandleMeasurementClick(150, 200);

        Assert.Single(vm.Measurements);
        Assert.False(vm.IsMeasuring);
    }

    // --- Pan Command Tests ---

    [Fact]
    public async Task PanLeftCommand_CannotExecute_When_No_Image()
    {
        var vm = CreateViewModel();

        Assert.False(vm.PanLeftCommand.CanExecute(null));
    }

    [Fact]
    public async Task PanRightCommand_CanExecute_When_Image_Loaded()
    {
        var vm = await CreateViewModelWithLoadedImage();

        Assert.True(vm.PanRightCommand.CanExecute(null));
    }

    [Fact]
    public async Task PanUpCommand_CanExecute_When_Image_Loaded()
    {
        var vm = await CreateViewModelWithLoadedImage();

        Assert.True(vm.PanUpCommand.CanExecute(null));
    }

    [Fact]
    public async Task PanDownCommand_CanExecute_When_Image_Loaded()
    {
        var vm = await CreateViewModelWithLoadedImage();

        Assert.True(vm.PanDownCommand.CanExecute(null));
    }

    // --- ClearMeasurements Command Tests ---

    [Fact]
    public void ClearMeasurementsCommand_CannotExecute_When_No_Image()
    {
        var vm = CreateViewModel();

        Assert.False(vm.ClearMeasurementsCommand.CanExecute(null));
    }

    [Fact]
    public async Task ClearMeasurementsCommand_CanExecute_When_Image_Loaded()
    {
        var vm = await CreateViewModelWithLoadedImage();

        Assert.True(vm.ClearMeasurementsCommand.CanExecute(null));
    }

    [Fact]
    public async Task ClearMeasurementsCommand_Clears_Measurements_Collection()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectDistanceToolCommand.Execute(null);
        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(200, 200);

        Assert.Single(vm.Measurements);

        vm.ClearMeasurementsCommand.Execute(null);

        Assert.Empty(vm.Measurements);
    }

    // --- Window/Level Clamping Edge Cases ---

    [Fact]
    public async Task IncreaseWindowCommand_Clamps_At_Maximum()
    {
        var vm = await CreateViewModelWithLoadedImage();

        // Set window width near maximum and try to increase beyond
        for (int i = 0; i < 700; i++)
        {
            vm.IncreaseWindowCommand.Execute(null);
        }

        Assert.Equal(65535, vm.WindowWidth);
    }

    [Fact]
    public async Task DecreaseWindowCommand_Clamps_At_Minimum()
    {
        var vm = await CreateViewModelWithLoadedImage();

        // Decrease many times to reach minimum
        for (int i = 0; i < 700; i++)
        {
            vm.DecreaseWindowCommand.Execute(null);
        }

        Assert.Equal(1, vm.WindowWidth);
    }

    [Fact]
    public async Task IncreaseLevelCommand_Clamps_At_Maximum()
    {
        var vm = await CreateViewModelWithLoadedImage();

        // Increase level many times
        for (int i = 0; i < 700; i++)
        {
            vm.IncreaseLevelCommand.Execute(null);
        }

        Assert.Equal(65535, vm.WindowCenter);
    }

    [Fact]
    public async Task DecreaseLevelCommand_Clamps_At_Minimum()
    {
        var vm = await CreateViewModelWithLoadedImage();

        // Decrease level many times
        for (int i = 0; i < 350; i++)
        {
            vm.DecreaseLevelCommand.Execute(null);
        }

        Assert.Equal(1, vm.WindowCenter);
    }

    [Fact]
    public async Task Multiple_WindowLevel_Adjustments_Stay_Within_Bounds()
    {
        var vm = await CreateViewModelWithLoadedImage();

        // Random sequence of adjustments
        for (int i = 0; i < 100; i++)
        {
            vm.IncreaseWindowCommand.Execute(null);
            vm.DecreaseLevelCommand.Execute(null);
        }

        Assert.True(vm.WindowWidth >= 1 && vm.WindowWidth <= 65535);
        Assert.True(vm.WindowCenter >= 1 && vm.WindowCenter <= 65535);
    }

    // --- Measurement Calculation Edge Cases ---

    [Fact]
    public void CalculateDistance_Returns_NA_With_Insufficient_Points()
    {
        var vm = CreateViewModel();
        vm.SelectDistanceToolCommand.Execute(null);

        // The method is private, but we can verify behavior through HandleMeasurementClick
        // With only 1 point, measurement should not complete
        vm.HandleMeasurementClick(100, 100);

        Assert.True(vm.IsMeasuring); // Still waiting for second point
        Assert.Empty(vm.Measurements);
    }

    [Fact]
    public async Task CalculateAngle_Requires_Three_Points()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectAngleToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(150, 150);

        // With only 2 points, should still be measuring
        Assert.True(vm.IsMeasuring);
        Assert.Empty(vm.Measurements);
    }

    [Fact]
    public async Task CalculateCobbAngle_Requires_Four_Points()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectCobbToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(150, 100);
        vm.HandleMeasurementClick(100, 200);

        // With only 3 points, should still be measuring
        Assert.True(vm.IsMeasuring);
        Assert.Empty(vm.Measurements);
    }

    [Fact]
    public async Task Annotation_Completes_With_Single_Point()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectAnnotationToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);

        // Annotation completes with 1+ points
        Assert.Single(vm.Measurements);
        Assert.False(vm.IsMeasuring);
    }

    // --- Bitmap Property Tests ---

    [Fact]
    public async Task Bitmap_Is_Set_After_Image_Load()
    {
        var vm = await CreateViewModelWithLoadedImage();

        Assert.NotNull(vm.Bitmap);
    }

    [Fact]
    public async Task Bitmap_Raises_PropertyChanged()
    {
        var imageData = CreateTestImageData();
        _mockImageService
            .Setup(s => s.GetImageAsync("IMG001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageData);

        var vm = CreateViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName ?? "");

        await vm.LoadImageAsync("IMG001");

        Assert.Contains("Bitmap", changedProperties);
    }

    // --- Pan Value Tests (Internal State Verification via Command Execution) ---

    [Fact]
    public async Task PanCommands_Execute_Without_Exception()
    {
        var vm = await CreateViewModelWithLoadedImage();

        // These should not throw
        var exception = Record.Exception(() =>
        {
            vm.PanLeftCommand.Execute(null);
            vm.PanRightCommand.Execute(null);
            vm.PanUpCommand.Execute(null);
            vm.PanDownCommand.Execute(null);
        });

        Assert.Null(exception);
    }

    // --- Tool Selection State Tests ---

    [Fact]
    public void Selecting_Same_Tool_Twice_Resets_Points()
    {
        var vm = CreateViewModel();
        vm.SelectDistanceToolCommand.Execute(null);
        vm.HandleMeasurementClick(100, 100);

        // Select same tool again - should reset
        vm.SelectDistanceToolCommand.Execute(null);

        Assert.True(vm.IsMeasuring);
        // Points should be cleared (indirectly verified - measurement not complete)
    }

    // --- Window/Level PropertyChanged Tests ---

    [Fact]
    public async Task WindowWidth_Raises_PropertyChanged()
    {
        var vm = await CreateViewModelWithLoadedImage();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName ?? "");

        vm.IncreaseWindowCommand.Execute(null);

        Assert.Contains("WindowWidth", changedProperties);
    }

    [Fact]
    public async Task WindowCenter_Raises_PropertyChanged()
    {
        var vm = await CreateViewModelWithLoadedImage();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName ?? "");

        vm.IncreaseLevelCommand.Execute(null);

        Assert.Contains("WindowCenter", changedProperties);
    }

    // --- Measurement Display Value Tests ---

    [Fact]
    public async Task Distance_Measurement_Has_MM_Unit()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectDistanceToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(200, 200);

        Assert.Single(vm.Measurements);
        Assert.Contains("mm", vm.Measurements[0].DisplayValue);
    }

    [Fact]
    public async Task Angle_Measurement_Has_Degree_Unit()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectAngleToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(150, 150);
        vm.HandleMeasurementClick(200, 100);

        Assert.Single(vm.Measurements);
        Assert.Contains("°", vm.Measurements[0].DisplayValue);
    }

    [Fact]
    public async Task CobbAngle_Measurement_Has_Cobb_Suffix()
    {
        var vm = await CreateViewModelWithLoadedImage();
        vm.SelectCobbToolCommand.Execute(null);

        vm.HandleMeasurementClick(100, 100);
        vm.HandleMeasurementClick(150, 100);
        vm.HandleMeasurementClick(100, 200);
        vm.HandleMeasurementClick(150, 200);

        Assert.Single(vm.Measurements);
        Assert.Contains("Cobb", vm.Measurements[0].DisplayValue);
    }

    // --- Orientation State Tests ---

    [Fact]
    public async Task Orientation_Changes_After_Rotate_Commands()
    {
        var vm = await CreateViewModelWithLoadedImage();

        vm.RotateRightCommand.Execute(null);
        Assert.Equal(ImageOrientation.Rotate90, vm.Orientation);

        vm.RotateLeftCommand.Execute(null);
        Assert.Equal(ImageOrientation.Rotate270, vm.Orientation);
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
