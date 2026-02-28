namespace HnVue.Workflow.Interfaces;

using System.Threading;
using System.Threading.Tasks;

// Temporary stubs for HAL interfaces referenced by pre-existing tests
// TODO: Implement actual HAL integration interfaces (Task #7)

public interface IHvgDriver
{
    Task<bool> TriggerExposureAsync(CancellationToken cancellationToken = default);
}

public interface IDetector
{
    Task<DetectorStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

public interface IDoseTracker
{
    // Dose tracking methods to be implemented
}

public enum DetectorStatus
{
    Ready,
    Busy,
    Error
}
