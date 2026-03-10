using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for AcquisitionViewModel.
/// SPEC-UI-001: FR-UI-06, 07, 09, 10, 11 (Protocol, Exposure, Preview, Dose, AEC).
/// </summary>
[Trait("Requirement", "NFR-UI-07")]
public class AcquisitionViewModelTests : ViewModelTestBase
{
    private readonly Mock<IExposureService> _mockExposureService;
    private readonly Mock<IProtocolService> _mockProtocolService;
    private readonly Mock<IAECService> _mockAecService;
    private readonly Mock<IDoseService> _mockDoseService;
    private readonly Mock<AECViewModel> _mockAecViewModel;
    private readonly ExposureParameterViewModel _exposureParameterViewModel;
    private readonly Mock<ProtocolViewModel> _mockProtocolViewModel;
    private readonly Mock<DoseViewModel> _mockDoseViewModel;

    public AcquisitionViewModelTests()
    {
        _mockExposureService = CreateLooseMockService<IExposureService>();
        _mockProtocolService = CreateLooseMockService<IProtocolService>();
        _mockAecService = CreateLooseMockService<IAECService>();
        _mockDoseService = CreateLooseMockService<IDoseService>();

        // Sub-ViewModels - use loose mocks for dependencies
        var looseMockExposureService = CreateLooseMockService<IExposureService>();
        _exposureParameterViewModel = new ExposureParameterViewModel(looseMockExposureService.Object);

        _mockAecViewModel = new Mock<AECViewModel>(MockBehavior.Loose, _mockAecService.Object) { CallBase = false };
        _mockProtocolViewModel = new Mock<ProtocolViewModel>(MockBehavior.Loose, _mockProtocolService.Object) { CallBase = false };
        _mockDoseViewModel = new Mock<DoseViewModel>(MockBehavior.Loose, _mockDoseService.Object) { CallBase = false };

        // Prevent SubscribeAECStateChangesAsync from blocking
        _mockAecService
            .Setup(s => s.SubscribeAECStateChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable<bool>());
    }

    private AcquisitionViewModel CreateViewModel()
    {
        return new AcquisitionViewModel(
            _mockExposureService.Object,
            _mockProtocolService.Object,
            _mockAecService.Object,
            _mockDoseService.Object,
            _mockAecViewModel.Object,
            _exposureParameterViewModel,
            _mockProtocolViewModel.Object,
            _mockDoseViewModel.Object);
    }

    // --- Constructor / Initialization ---

    [Fact]
    public void Constructor_Initializes_Default_Values()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsPreviewActive);
        Assert.False(vm.IsExposing);
        Assert.Equal(string.Empty, vm.SelectedProcedureId);
        Assert.Null(vm.PreviewBitmap);
    }

    [Fact]
    public void Constructor_Creates_Commands()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.StartPreviewCommand);
        Assert.NotNull(vm.StopPreviewCommand);
        Assert.NotNull(vm.TriggerExposureCommand);
    }

    [Fact]
    public void Constructor_Assigns_SubViewModels()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.AECViewModel);
        Assert.NotNull(vm.ExposureParameterViewModel);
        Assert.NotNull(vm.ProtocolViewModel);
        Assert.NotNull(vm.DoseViewModel);
    }

    [Fact]
    public void Constructor_Throws_On_Null_ExposureService()
    {
        Assert.Throws<ArgumentNullException>(() => new AcquisitionViewModel(
            null!,
            _mockProtocolService.Object,
            _mockAecService.Object,
            _mockDoseService.Object,
            _mockAecViewModel.Object,
            _exposureParameterViewModel,
            _mockProtocolViewModel.Object,
            _mockDoseViewModel.Object));
    }

    [Fact]
    public void Constructor_Throws_On_Null_ProtocolService()
    {
        Assert.Throws<ArgumentNullException>(() => new AcquisitionViewModel(
            _mockExposureService.Object,
            null!,
            _mockAecService.Object,
            _mockDoseService.Object,
            _mockAecViewModel.Object,
            _exposureParameterViewModel,
            _mockProtocolViewModel.Object,
            _mockDoseViewModel.Object));
    }

    // --- Preview State ---

    [Fact]
    public void StartPreviewCommand_CanExecute_When_Not_Active()
    {
        var vm = CreateViewModel();

        Assert.True(vm.StartPreviewCommand.CanExecute(null));
    }

    [Fact]
    public void StopPreviewCommand_CannotExecute_When_Not_Active()
    {
        var vm = CreateViewModel();

        Assert.False(vm.StopPreviewCommand.CanExecute(null));
    }

    [Fact]
    public void IsPreviewActive_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.IsPreviewActive = true);

        Assert.Contains("IsPreviewActive", changedProperties);
    }

    [Fact]
    public void IsExposing_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.IsExposing = true);

        Assert.Contains("IsExposing", changedProperties);
    }

    // --- Exposure Command ---

    [Fact]
    public void TriggerExposureCommand_CannotExecute_When_No_Preview()
    {
        var vm = CreateViewModel();
        vm.SelectedProcedureId = "PROC001";

        // Preview is not active, so trigger should be disabled
        Assert.False(vm.TriggerExposureCommand.CanExecute(null));
    }

    [Fact]
    public void TriggerExposureCommand_CannotExecute_When_No_Procedure()
    {
        var vm = CreateViewModel();
        vm.IsPreviewActive = true;

        // No procedure selected
        Assert.False(vm.TriggerExposureCommand.CanExecute(null));
    }

    [Fact]
    public void TriggerExposureCommand_CannotExecute_When_Already_Exposing()
    {
        var vm = CreateViewModel();
        vm.IsPreviewActive = true;
        vm.SelectedProcedureId = "PROC001";
        vm.IsExposing = true;

        Assert.False(vm.TriggerExposureCommand.CanExecute(null));
    }

    // --- SetProcedure ---

    [Fact]
    public void SetProcedure_Updates_SelectedProcedureId()
    {
        var vm = CreateViewModel();

        vm.SetProcedure("PROC001", "STUDY001");

        Assert.Equal("PROC001", vm.SelectedProcedureId);
    }

    [Fact]
    public void SelectedProcedureId_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.SelectedProcedureId = "PROC001");

        Assert.Contains("SelectedProcedureId", changedProperties);
    }

    // --- Exposure Trigger ---

    [Fact]
    public async Task TriggerExposure_Navigates_On_Success()
    {
        var vm = CreateViewModel();
        vm.IsPreviewActive = true;
        vm.SelectedProcedureId = "PROC001";

        string? navigatedTo = null;
        vm.NavigationRequested += (_, view) => navigatedTo = view;

        _mockExposureService
            .Setup(s => s.TriggerExposureAsync(It.IsAny<ExposureTriggerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExposureTriggerResult { Success = true, ImageId = "IMG001", ErrorMessage = null });

        vm.TriggerExposureCommand.Execute(null);
        await Task.Delay(200);

        Assert.Equal("ImageReview", navigatedTo);
        Assert.False(vm.IsExposing);
    }

    [Fact]
    public async Task TriggerExposure_Fires_ErrorOccurred_On_Failure()
    {
        var vm = CreateViewModel();
        vm.IsPreviewActive = true;
        vm.SelectedProcedureId = "PROC001";

        string? errorMessage = null;
        vm.ErrorOccurred += (_, msg) => errorMessage = msg;

        _mockExposureService
            .Setup(s => s.TriggerExposureAsync(It.IsAny<ExposureTriggerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExposureTriggerResult { Success = false, ImageId = null, ErrorMessage = "Detector not ready" });

        vm.TriggerExposureCommand.Execute(null);
        await Task.Delay(200);

        Assert.Equal("Detector not ready", errorMessage);
        Assert.False(vm.IsExposing);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_Does_Not_Throw()
    {
        var vm = CreateViewModel();
        var exception = Record.Exception(() => vm.Dispose());

        Assert.Null(exception);
    }

    // --- Helper ---

    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
