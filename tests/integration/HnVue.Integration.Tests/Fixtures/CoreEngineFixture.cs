using HnVue.Console.Models;
using HnVue.Console.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HnVue.Integration.Tests.Fixtures;

/// <summary>
/// CoreEngineFixture – lightweight in-process simulation of the gRPC Core Engine lifecycle.
/// Provides shared, controllable mock services to integration tests that need to exercise
/// gRPC connection management without a real server process.
///
/// Usage (shared collection fixture):
/// <code>
/// [Collection("CoreEngine")]
/// public class MyTests
/// {
///     public MyTests(CoreEngineFixture fixture) { ... }
/// }
/// </code>
///
/// Usage (per-test, not shared):
/// <code>
/// var fixture = new CoreEngineFixture();
/// await fixture.InitializeAsync();
/// // ... use fixture.StatusService, fixture.SimulateDisconnect(), etc.
/// await fixture.DisposeAsync();
/// </code>
/// </summary>
public sealed class CoreEngineFixture : IAsyncLifetime
{
    // -----------------------------------------------------------------------
    // Public mock services — tests may configure additional returns/exceptions.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mock <see cref="ISystemStatusService"/> that simulates Core Engine heartbeat responses.
    /// Initially configured to succeed with a healthy status.
    /// Call <see cref="SimulateDisconnect"/> to make it throw.
    /// Call <see cref="SimulateReconnect"/> to restore success responses.
    /// </summary>
    public ISystemStatusService StatusService { get; } = Substitute.For<ISystemStatusService>();

    /// <summary>
    /// Mock <see cref="IAuditLogService"/> pre-configured to accept all log calls.
    /// Tests may use <c>Received()</c> to assert specific audit events were raised.
    /// </summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>
    /// Mock <see cref="IImageService"/> representing the DICOM image channel.
    /// Initially configured to return <see langword="null"/> for all queries
    /// (no current image — a valid "empty" response).
    /// </summary>
    public IImageService ImageService { get; } = Substitute.For<IImageService>();

    // -----------------------------------------------------------------------
    // State accessors
    // -----------------------------------------------------------------------

    /// <summary>
    /// Indicates whether the simulated Core Engine is currently reachable.
    /// </summary>
    public bool IsConnected { get; private set; } = true;

    /// <summary>
    /// Total number of simulated disconnect events since fixture creation.
    /// </summary>
    public int DisconnectCount { get; private set; }

    // -----------------------------------------------------------------------
    // IAsyncLifetime – xUnit fixture lifecycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Initialises all mock services with default healthy-state responses.
    /// Called automatically by xUnit before the first test in the collection.
    /// </summary>
    public Task InitializeAsync()
    {
        ApplyConnectedDefaults();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up any residual state. No real resources to release.
    /// Called automatically by xUnit after all tests in the collection complete.
    /// </summary>
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // Lifecycle simulation helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Simulates Core Engine going offline: configures all services to throw
    /// <see cref="InvalidOperationException"/> (representing a gRPC UNAVAILABLE status).
    /// </summary>
    public void SimulateDisconnect()
    {
        IsConnected = false;
        DisconnectCount++;

        StatusService
            .GetOverallStatusAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gRPC Core Engine: connection refused"));

        StatusService
            .CanInitiateExposureAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gRPC Core Engine: connection refused"));

        StatusService
            .GetComponentStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gRPC Core Engine: connection refused"));

        ImageService
            .GetCurrentImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gRPC Core Engine: connection refused"));

        ImageService
            .GetImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gRPC Core Engine: connection refused"));
    }

    /// <summary>
    /// Simulates Core Engine coming back online: restores all services to healthy defaults.
    /// </summary>
    public void SimulateReconnect()
    {
        IsConnected = true;
        ApplyConnectedDefaults();
    }

    /// <summary>
    /// Simulates a transient network blip: disconnects then immediately reconnects.
    /// Useful for testing retry logic without a prolonged downtime scenario.
    /// </summary>
    public void SimulateTransientBlip()
    {
        SimulateDisconnect();
        SimulateReconnect();
    }

    // -----------------------------------------------------------------------
    // Assertion helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Asserts (via NSubstitute) that the audit log received at least one call whose
    /// description contains <paramref name="keyword"/>.
    /// </summary>
    /// <param name="keyword">Substring expected in the event description.</param>
    public async Task AssertAuditedAsync(string keyword)
    {
        await AuditLog
            .Received()
            .LogAsync(
                Arg.Any<AuditEventType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Is<string>(d => d.Contains(keyword, StringComparison.OrdinalIgnoreCase)),
                Arg.Any<AuditOutcome>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Resets all NSubstitute call counters on shared mocks.
    /// Call this in between tests that share the same fixture instance to prevent
    /// <c>Received()</c> counts from accumulating across test boundaries.
    /// </summary>
    public void ClearReceivedCalls()
    {
        StatusService.ClearReceivedCalls();
        AuditLog.ClearReceivedCalls();
        ImageService.ClearReceivedCalls();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Configures all services to return valid healthy-state responses.
    /// Called at startup and after <see cref="SimulateReconnect"/>.
    /// </summary>
    private void ApplyConnectedDefaults()
    {
        // Audit log – always succeeds, returns deterministic entry ID.
        AuditLog
            .LogAsync(
                Arg.Any<AuditEventType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AuditOutcome>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult($"LOG-{Guid.NewGuid():N}".Substring(0, 12)));

        // Status service – healthy system, exposure allowed.
        var healthyStatus = new SystemOverallStatus
        {
            OverallHealth = ComponentHealth.Healthy,
            ComponentStatuses = Array.Empty<ComponentStatus>(),
            CanInitiateExposure = true,
            ActiveAlerts = Array.Empty<string>(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        StatusService
            .GetOverallStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(healthyStatus));

        StatusService
            .CanInitiateExposureAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        StatusService
            .GetComponentStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ComponentStatus?>(null));

        // Image service – no current image (valid empty state).
        ImageService
            .GetCurrentImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ImageData?>(null));
    }
}
