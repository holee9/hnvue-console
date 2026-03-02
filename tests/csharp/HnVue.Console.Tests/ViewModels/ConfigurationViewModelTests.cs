using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for ConfigurationViewModel.
/// SPEC-UI-001: FR-UI-08 System Configuration.
/// </summary>
public class ConfigurationViewModelTests : ViewModelTestBase
{
    private readonly Mock<ISystemConfigService> _mockConfigService;
    private readonly Mock<IUserService> _mockUserService;

    public ConfigurationViewModelTests()
    {
        _mockConfigService = CreateMockService<ISystemConfigService>();
        _mockUserService = CreateMockService<IUserService>();
    }

    /// <summary>
    /// Creates a valid SystemConfig for testing with all required fields populated.
    /// </summary>
    private static SystemConfig CreateValidSystemConfig()
    {
        return new SystemConfig
        {
            Calibration = new CalibrationConfig
            {
                LastCalibrationDate = DateTimeOffset.Now.AddMonths(-1),
                NextCalibrationDueDate = DateTimeOffset.Now.AddMonths(11),
                IsCalibrationValid = true,
                Status = CalibrationStatus.Valid
            },
            Network = new NetworkConfig
            {
                DicomAeTitle = "TEST_AE",
                DicomPort = "104",
                PacsHostName = "localhost",
                PacsPort = 11112,
                MwlEnabled = true
            },
            Users = new UserConfig
            {
                Users = new List<User>()
            },
            Logging = new LoggingConfig
            {
                MinimumLogLevel = HnVue.Console.Models.LogLevel.Information,
                RetentionDays = 30,
                EnableRemoteLogging = false
            }
        };
    }

    [Fact]
    public void Constructor_Initializes_Collections()
    {
        // Arrange & Act
        var viewModel = new ConfigurationViewModel(
            _mockConfigService.Object,
            _mockUserService.Object);

        // Assert
        Assert.NotNull(viewModel.AvailableSections);
    }

    [Fact]
    public async Task InitializeAsync_Loads_Config_And_Role()
    {
        // Arrange
        _mockUserService
            .Setup(s => s.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Supervisor);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidSystemConfig());

        var viewModel = new ConfigurationViewModel(
            _mockConfigService.Object,
            _mockUserService.Object);

        // Act
        await viewModel.InitializeAsync(TestCancellationToken);

        // Assert
        Assert.Equal(UserRole.Supervisor, viewModel.CurrentUserRole);
        Assert.NotNull(viewModel.Config);
    }

    [Theory]
    [InlineData(UserRole.ServiceEngineer, true, true, true, true)]
    [InlineData(UserRole.Supervisor, false, true, false, true)]
    [InlineData(UserRole.Administrator, false, true, true, true)]
    [InlineData(UserRole.Operator, false, false, false, false)]
    public async Task Role_Based_Visibility_Works_Correctly(
        UserRole role,
        bool expectCalibration,
        bool expectNetwork,
        bool expectUsers,
        bool expectLogging)
    {
        // Arrange
        _mockUserService
            .Setup(s => s.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidSystemConfig());

        var viewModel = new ConfigurationViewModel(
            _mockConfigService.Object,
            _mockUserService.Object);

        // Act
        await viewModel.InitializeAsync(TestCancellationToken);

        // Assert
        Assert.Equal(expectCalibration, viewModel.IsCalibrationVisible);
        Assert.Equal(expectNetwork, viewModel.IsNetworkVisible);
        Assert.Equal(expectUsers, viewModel.IsUsersVisible);
        Assert.Equal(expectLogging, viewModel.IsLoggingVisible);
    }

    [Fact]
    public async Task SaveCommand_Updates_Config()
    {
        // Arrange
        _mockUserService
            .Setup(s => s.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Administrator);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidSystemConfig());

        _mockConfigService
            .Setup(s => s.UpdateConfigAsync(It.IsAny<ConfigUpdate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new ConfigurationViewModel(
            _mockConfigService.Object,
            _mockUserService.Object);

        await viewModel.InitializeAsync(TestCancellationToken);
        viewModel.SelectedTabIndex = 1; // Network tab

        // Act
        viewModel.SaveCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        _mockConfigService.Verify(
            s => s.UpdateConfigAsync(It.IsAny<ConfigUpdate>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartCalibrationCommand_Initiates_Calibration()
    {
        // Arrange
        _mockUserService
            .Setup(s => s.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.ServiceEngineer);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidSystemConfig());

        _mockConfigService
            .Setup(s => s.StartCalibrationAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new ConfigurationViewModel(
            _mockConfigService.Object,
            _mockUserService.Object);

        await viewModel.InitializeAsync(TestCancellationToken);

        // Act
        viewModel.StartCalibrationCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        _mockConfigService.Verify(
            s => s.StartCalibrationAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
