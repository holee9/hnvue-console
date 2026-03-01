using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock system status service for development.
/// SPEC-UI-001: FR-UI-12 System Status Dashboard.
/// </summary>
public class MockSystemStatusService : ISystemStatusService
{
    private readonly SystemOverallStatus _mockStatus;

    public MockSystemStatusService()
    {
        _mockStatus = new SystemOverallStatus
        {
            OverallHealth = ComponentHealth.Healthy,
            CanInitiateExposure = true,
            ActiveAlerts = Array.Empty<string>(),
            UpdatedAt = DateTimeOffset.Now,
            ComponentStatuses = new List<ComponentStatus>
            {
                new()
                {
                    ComponentId = "XrayGenerator",
                    Type = ComponentType.XrayGenerator,
                    Health = ComponentHealth.Healthy,
                    StatusMessage = "Operational - 80kV / 100mA ready",
                    UpdatedAt = DateTimeOffset.Now.AddMinutes(-1)
                },
                new()
                {
                    ComponentId = "Detector",
                    Type = ComponentType.Detector,
                    Health = ComponentHealth.Healthy,
                    StatusMessage = "Ready - Last calibration: 2025-02-15",
                    UpdatedAt = DateTimeOffset.Now.AddMinutes(-2)
                },
                new()
                {
                    ComponentId = "Collimator",
                    Type = ComponentType.Collimator,
                    Health = ComponentHealth.Healthy,
                    StatusMessage = "Position: Normal - Field: 35x43cm",
                    UpdatedAt = DateTimeOffset.Now.AddMinutes(-1)
                },
                new()
                {
                    ComponentId = "Network",
                    Type = ComponentType.Network,
                    Health = ComponentHealth.Healthy,
                    StatusMessage = "Connected - Latency: 12ms",
                    UpdatedAt = DateTimeOffset.Now.AddSeconds(-30)
                },
                new()
                {
                    ComponentId = "DicomService",
                    Type = ComponentType.DicomService,
                    Health = ComponentHealth.Healthy,
                    StatusMessage = "MWL/MPPS active - AE: HNVUE_CONSOLE",
                    UpdatedAt = DateTimeOffset.Now.AddSeconds(-45)
                },
                new()
                {
                    ComponentId = "CoreEngine",
                    Type = ComponentType.CoreEngine,
                    Health = ComponentHealth.Healthy,
                    StatusMessage = "Running v1.0.0 - Uptime: 4h 32m",
                    UpdatedAt = DateTimeOffset.Now.AddMinutes(-5)
                },
                new()
                {
                    ComponentId = "DoseService",
                    Type = ComponentType.DoseService,
                    Health = ComponentHealth.Healthy,
                    StatusMessage = "Dose tracking active - Daily: 12.3 mGy",
                    UpdatedAt = DateTimeOffset.Now.AddMinutes(-3)
                }
            }
        };
    }

    public Task<SystemOverallStatus> GetOverallStatusAsync(CancellationToken ct)
    {
        return Task.FromResult(_mockStatus);
    }

    public Task<ComponentStatus?> GetComponentStatusAsync(string componentId, CancellationToken ct)
    {
        var component = _mockStatus.ComponentStatuses.FirstOrDefault(c => c.ComponentId == componentId);
        return Task.FromResult(component);
    }

    public async IAsyncEnumerable<StatusUpdate> SubscribeStatusUpdatesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Simulate periodic updates
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(5000, ct);

            // Randomly update one component
            var random = new Random();
            var index = random.Next(_mockStatus.ComponentStatuses.Count);
            var component = _mockStatus.ComponentStatuses[index];

            yield return new StatusUpdate
            {
                Component = component with
                {
                    UpdatedAt = DateTimeOffset.Now
                },
                RequiresExposureHalt = false
            };
        }
    }

    public Task<bool> CanInitiateExposureAsync(CancellationToken ct)
    {
        return Task.FromResult(_mockStatus.CanInitiateExposure);
    }
}
