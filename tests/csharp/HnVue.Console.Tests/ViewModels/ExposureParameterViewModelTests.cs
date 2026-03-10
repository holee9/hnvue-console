using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for ExposureParameterViewModel.
/// SPEC-UI-001: FR-UI-07 Exposure Parameter Display.
/// </summary>
[Trait("Requirement", "NFR-UI-07")]
public class ExposureParameterViewModelTests : ViewModelTestBase
{
    private readonly Mock<IExposureService> _mockExposureService;

    public ExposureParameterViewModelTests()
    {
        _mockExposureService = CreateLooseMockService<IExposureService>();
    }

    private ExposureParameterViewModel CreateViewModel()
    {
        return new ExposureParameterViewModel(_mockExposureService.Object);
    }

    // --- Constructor / Defaults ---

    [Fact]
    public void Constructor_Initializes_Default_Parameters()
    {
        var vm = CreateViewModel();

        Assert.Equal(120, vm.KVp);
        Assert.Equal(100, vm.MA);
        Assert.Equal(100, vm.ExposureTimeMs);
        Assert.Equal(100, vm.SourceImageDistanceCm);
        Assert.Equal(FocalSpotSize.Large, vm.FocalSpotSize);
        Assert.False(vm.IsReadOnly);
    }

    [Fact]
    public void Constructor_Initializes_Default_Ranges()
    {
        var vm = CreateViewModel();

        Assert.Equal(40, vm.Ranges.KvpRange.Min);
        Assert.Equal(150, vm.Ranges.KvpRange.Max);
        Assert.Equal(10, vm.Ranges.MaRange.Min);
        Assert.Equal(630, vm.Ranges.MaRange.Max);
        Assert.Equal(1, vm.Ranges.TimeRangeMs.Min);
        Assert.Equal(5000, vm.Ranges.TimeRangeMs.Max);
        Assert.Equal(100, vm.Ranges.SidRangeCm.Min);
        Assert.Equal(180, vm.Ranges.SidRangeCm.Max);
    }

    [Fact]
    public void Constructor_Creates_All_Commands()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.IncreaseKVpCommand);
        Assert.NotNull(vm.DecreaseKVpCommand);
        Assert.NotNull(vm.IncreaseMACommand);
        Assert.NotNull(vm.DecreaseMACommand);
        Assert.NotNull(vm.IncreaseTimeCommand);
        Assert.NotNull(vm.DecreaseTimeCommand);
        Assert.NotNull(vm.ApplyCommand);
    }

    [Fact]
    public void Constructor_Throws_On_Null_ExposureService()
    {
        Assert.Throws<ArgumentNullException>(() => new ExposureParameterViewModel(null!));
    }

    // --- kVp Validation ---

    [Fact]
    public void IncreaseKVpCommand_Increments_KVp()
    {
        var vm = CreateViewModel();
        var initial = vm.KVp;

        vm.IncreaseKVpCommand.Execute(null);

        Assert.Equal(initial + 1, vm.KVp);
    }

    [Fact]
    public void DecreaseKVpCommand_Decrements_KVp()
    {
        var vm = CreateViewModel();
        var initial = vm.KVp;

        vm.DecreaseKVpCommand.Execute(null);

        Assert.Equal(initial - 1, vm.KVp);
    }

    [Fact]
    public void KVp_Cannot_Exceed_Max()
    {
        var vm = CreateViewModel();
        vm.KVp = 150; // Set to max

        Assert.False(vm.IncreaseKVpCommand.CanExecute(null));
    }

    [Fact]
    public void KVp_Cannot_Go_Below_Min()
    {
        var vm = CreateViewModel();
        vm.KVp = 40; // Set to min

        Assert.False(vm.DecreaseKVpCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(80)]
    [InlineData(120)]
    [InlineData(150)]
    public void KVp_Accepts_Values_Within_Range(int kvp)
    {
        var vm = CreateViewModel();

        vm.KVp = kvp;

        Assert.Equal(kvp, vm.KVp);
    }

    [Fact]
    public void KVp_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.KVp = 80);

        Assert.Contains("KVp", changedProperties);
        Assert.Contains("Parameters", changedProperties);
        Assert.Contains("Mas", changedProperties);
    }

    // --- mAs Validation ---

    [Fact]
    public void IncreaseMACommand_Increments_MA_By_10()
    {
        var vm = CreateViewModel();
        var initial = vm.MA;

        vm.IncreaseMACommand.Execute(null);

        Assert.Equal(initial + 10, vm.MA);
    }

    [Fact]
    public void DecreaseMACommand_Decrements_MA_By_10()
    {
        var vm = CreateViewModel();
        var initial = vm.MA;

        vm.DecreaseMACommand.Execute(null);

        Assert.Equal(initial - 10, vm.MA);
    }

    [Fact]
    public void MA_Cannot_Exceed_Max()
    {
        var vm = CreateViewModel();
        vm.MA = 630;

        Assert.False(vm.IncreaseMACommand.CanExecute(null));
    }

    [Fact]
    public void MA_Cannot_Go_Below_Min()
    {
        var vm = CreateViewModel();
        vm.MA = 10;

        Assert.False(vm.DecreaseMACommand.CanExecute(null));
    }

    [Fact]
    public void MA_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.MA = 200);

        Assert.Contains("MA", changedProperties);
        Assert.Contains("Parameters", changedProperties);
        Assert.Contains("Mas", changedProperties);
    }

    // --- Time Validation ---

    [Fact]
    public void IncreaseTimeCommand_Increments_Time_By_10()
    {
        var vm = CreateViewModel();
        var initial = vm.ExposureTimeMs;

        vm.IncreaseTimeCommand.Execute(null);

        Assert.Equal(initial + 10, vm.ExposureTimeMs);
    }

    [Fact]
    public void DecreaseTimeCommand_Decrements_Time_By_10()
    {
        var vm = CreateViewModel();
        var initial = vm.ExposureTimeMs;

        vm.DecreaseTimeCommand.Execute(null);

        Assert.Equal(initial - 10, vm.ExposureTimeMs);
    }

    [Fact]
    public void Time_Cannot_Exceed_Max()
    {
        var vm = CreateViewModel();
        vm.ExposureTimeMs = 5000;

        Assert.False(vm.IncreaseTimeCommand.CanExecute(null));
    }

    [Fact]
    public void Time_Cannot_Go_Below_Min()
    {
        var vm = CreateViewModel();
        vm.ExposureTimeMs = 1;

        Assert.False(vm.DecreaseTimeCommand.CanExecute(null));
    }

    // --- mAs Calculation ---

    [Fact]
    public void Mas_Calculated_From_MA_And_Time()
    {
        var vm = CreateViewModel();
        vm.MA = 200;
        vm.ExposureTimeMs = 500;

        // mAs = mA * time / 1000 = 200 * 500 / 1000 = 100
        Assert.Equal(100.0, vm.Mas);
    }

    [Theory]
    [InlineData(100, 100, 10.0)]
    [InlineData(200, 250, 50.0)]
    [InlineData(400, 50, 20.0)]
    public void Mas_Calculation_Is_Correct(int ma, int timeMs, double expectedMas)
    {
        var vm = CreateViewModel();
        vm.MA = ma;
        vm.ExposureTimeMs = timeMs;

        Assert.Equal(expectedMas, vm.Mas);
    }

    // --- Invalid Parameter Detection ---

    [Fact]
    public void IncreaseKVp_Clamps_At_Max_Range()
    {
        var vm = CreateViewModel();
        vm.KVp = 149;

        vm.IncreaseKVpCommand.Execute(null);

        Assert.Equal(150, vm.KVp);
        Assert.False(vm.IncreaseKVpCommand.CanExecute(null));
    }

    [Fact]
    public void DecreaseKVp_Clamps_At_Min_Range()
    {
        var vm = CreateViewModel();
        vm.KVp = 41;

        vm.DecreaseKVpCommand.Execute(null);

        Assert.Equal(40, vm.KVp);
        Assert.False(vm.DecreaseKVpCommand.CanExecute(null));
    }

    // --- Property Change Notifications ---

    [Fact]
    public void ExposureTimeMs_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.ExposureTimeMs = 200);

        Assert.Contains("ExposureTimeMs", changedProperties);
        Assert.Contains("Parameters", changedProperties);
        Assert.Contains("Mas", changedProperties);
    }

    [Fact]
    public void SourceImageDistanceCm_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.SourceImageDistanceCm = 150);

        Assert.Contains("SourceImageDistanceCm", changedProperties);
        Assert.Contains("Parameters", changedProperties);
    }

    [Fact]
    public void FocalSpotSize_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.FocalSpotSize = FocalSpotSize.Small);

        Assert.Contains("FocalSpotSize", changedProperties);
        Assert.Contains("Parameters", changedProperties);
    }

    [Fact]
    public void IsReadOnly_Raises_PropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.IsReadOnly = true);

        Assert.Contains("IsReadOnly", changedProperties);
    }

    // --- Parameter Reset (SetAecMode) ---

    [Fact]
    public void SetAecMode_Enabled_Sets_IsReadOnly_True()
    {
        var vm = CreateViewModel();

        vm.SetAecMode(true);

        Assert.True(vm.IsReadOnly);
        Assert.True(vm.Parameters.IsAecMode);
    }

    [Fact]
    public void SetAecMode_Disabled_Sets_IsReadOnly_False()
    {
        var vm = CreateViewModel();
        vm.SetAecMode(true);

        vm.SetAecMode(false);

        Assert.False(vm.IsReadOnly);
        Assert.False(vm.Parameters.IsAecMode);
    }

    [Fact]
    public void SetAecMode_Raises_PropertyChanged_For_Parameters()
    {
        var vm = CreateViewModel();
        var changedProperties = GetChangedProperties(vm, () => vm.SetAecMode(true));

        Assert.Contains("Parameters", changedProperties);
        Assert.Contains("IsReadOnly", changedProperties);
    }

    // --- Apply Command ---

    [Fact]
    public async Task ApplyCommand_Calls_ExposureService()
    {
        _mockExposureService
            .Setup(s => s.SetExposureParametersAsync(It.IsAny<ExposureParameters>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();
        vm.KVp = 100;

        vm.ApplyCommand.Execute(null);
        await Task.Delay(100);

        _mockExposureService.Verify(
            s => s.SetExposureParametersAsync(
                It.Is<ExposureParameters>(p => p.KVp == 100),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_Does_Not_Throw()
    {
        var vm = CreateViewModel();
        var exception = Record.Exception(() => vm.Dispose());

        Assert.Null(exception);
    }
}
