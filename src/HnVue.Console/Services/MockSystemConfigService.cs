using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock system configuration service for development.
/// SPEC-UI-001: FR-UI-08 System Configuration.
/// </summary>
public class MockSystemConfigService : ISystemConfigService
{
    private readonly SystemConfig _mockConfig;

    public MockSystemConfigService()
    {
        _mockConfig = new SystemConfig
        {
            Calibration = new CalibrationConfig
            {
                LastCalibrationDate = new DateTimeOffset(2025, 02, 15, 10, 30, 0, TimeSpan.Zero),
                NextCalibrationDueDate = new DateTimeOffset(2025, 08, 15, 0, 0, 0, TimeSpan.Zero),
                IsCalibrationValid = true,
                Status = CalibrationStatus.Valid
            },
            Network = new NetworkConfig
            {
                DicomAeTitle = "HNVUE_CONSOLE",
                DicomPort = "104",
                PacsHostName = "pacs.hospital.local",
                PacsPort = 11112,
                MwlEnabled = true
            },
            Users = new UserConfig
            {
                Users = new List<User>
                {
                    new()
                    {
                        UserId = "admin",
                        UserName = "System Administrator",
                        Role = UserRole.Administrator,
                        IsActive = true
                    },
                    new()
                    {
                        UserId = "supervisor1",
                        UserName = "Dr. Smith",
                        Role = UserRole.Supervisor,
                        IsActive = true
                    },
                    new()
                    {
                        UserId = "operator1",
                        UserName = "Technician Johnson",
                        Role = UserRole.Operator,
                        IsActive = true
                    }
                }
            },
            Logging = new LoggingConfig
            {
                MinimumLogLevel = LogLevel.Information,
                RetentionDays = 90,
                EnableRemoteLogging = true
            }
        };
    }

    public Task<SystemConfig> GetConfigAsync(CancellationToken ct)
    {
        return Task.FromResult(_mockConfig);
    }

    public Task<object> GetConfigSectionAsync(ConfigSection section, CancellationToken ct)
    {
        object result = section switch
        {
            ConfigSection.Calibration => _mockConfig.Calibration!,
            ConfigSection.Network => _mockConfig.Network!,
            ConfigSection.Users => _mockConfig.Users!,
            ConfigSection.Logging => _mockConfig.Logging!,
            _ => throw new ArgumentException($"Unknown section: {section}")
        };
        return Task.FromResult(result);
    }

    public Task UpdateConfigAsync(ConfigUpdate update, CancellationToken ct)
    {
        // Mock implementation - just log the update
        System.Diagnostics.Debug.WriteLine($"Config update: {update.Section}");
        return Task.CompletedTask;
    }

    public Task StartCalibrationAsync(CancellationToken ct)
    {
        // Mock implementation
        return Task.Delay(100, ct);
    }

    public Task<CalibrationConfig> GetCalibrationStatusAsync(CancellationToken ct)
    {
        return Task.FromResult(_mockConfig.Calibration!);
    }

    public Task<bool> ValidateNetworkConfigAsync(NetworkConfig config, CancellationToken ct)
    {
        // Mock implementation - always true
        return Task.FromResult(true);
    }
}
