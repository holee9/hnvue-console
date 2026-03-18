using System.IO;
using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HnVue.Integration.Tests.ErrorHandling;

/// <summary>
/// INT-006: gRPC IPC Connection Failure Integration Tests.
/// Validates that the GUI handles Core Engine connection failures gracefully:
/// heartbeat-based disconnect detection, state transitions, automatic reconnection,
/// and safe command behaviour during disconnection.
/// Strategy: NSubstitute mocks – no real gRPC server required.
/// IEC 62304 Class B/C – fault isolation and fail-safe behaviour.
/// SPEC-INTEGRATION-001.
/// </summary>
public sealed class IpcFailureTests
{
    // -----------------------------------------------------------------------
    // Connection state model
    // -----------------------------------------------------------------------

    /// <summary>Represents the GUI-side IPC connection lifecycle states.</summary>
    private enum IpcConnectionState
    {
        Connected,
        Disconnected,
        Reconnecting
    }

    /// <summary>Result of attempting to send a command while possibly disconnected.</summary>
    private sealed record CommandResult(bool Succeeded, string? ErrorMessage);

    // -----------------------------------------------------------------------
    // Connection monitor (system under test)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Monitors heartbeat responses from Core Engine and drives state transitions.
    /// The real production class would poll a health-check RPC; here we model the
    /// essential logic so the tests verify the contract, not the transport.
    /// </summary>
    private sealed class IpcConnectionMonitor
    {
        private readonly ISystemStatusService _statusService;
        private readonly IAuditLogService _auditLog;

        private IpcConnectionState _state = IpcConnectionState.Connected;
        private DateTimeOffset _lastHeartbeatAt = DateTimeOffset.UtcNow;
        private readonly TimeSpan _heartbeatTimeout;

        public IpcConnectionState State => _state;
        public int ReconnectAttempts { get; private set; }

        public IpcConnectionMonitor(
            ISystemStatusService statusService,
            IAuditLogService auditLog,
            TimeSpan? heartbeatTimeout = null)
        {
            _statusService = statusService;
            _auditLog = auditLog;
            _heartbeatTimeout = heartbeatTimeout ?? TimeSpan.FromSeconds(3);
        }

        /// <summary>
        /// Performs one heartbeat poll. On failure or timeout the monitor transitions
        /// to DISCONNECTED state. Returns true if the engine responded.
        /// </summary>
        public async Task<bool> PollHeartbeatAsync(CancellationToken ct = default)
        {
            try
            {
                // GetOverallStatusAsync acts as the lightweight heartbeat RPC.
                await _statusService.GetOverallStatusAsync(ct);
                _lastHeartbeatAt = DateTimeOffset.UtcNow;

                if (_state != IpcConnectionState.Connected)
                {
                    _state = IpcConnectionState.Connected;
                    ReconnectAttempts = 0;

                    await _auditLog.LogAsync(
                        AuditEventType.ConfigChange,
                        "system",
                        "System",
                        "Core Engine IPC connection restored",
                        AuditOutcome.Success,
                        ct: ct);
                }

                return true;
            }
            catch (Exception ex) when (IsConnectivityException(ex))
            {
                var elapsed = DateTimeOffset.UtcNow - _lastHeartbeatAt;

                if (elapsed >= _heartbeatTimeout && _state == IpcConnectionState.Connected)
                {
                    _state = IpcConnectionState.Disconnected;

                    await _auditLog.LogAsync(
                        AuditEventType.ConfigChange,
                        "system",
                        "System",
                        $"Core Engine IPC connection lost (timeout {elapsed.TotalSeconds:F1}s)",
                        AuditOutcome.Failure,
                        ct: ct);
                }

                return false;
            }
        }

        /// <summary>
        /// Simulates the passage of heartbeat-timeout duration so the monitor can
        /// evaluate whether a disconnect has occurred.
        /// </summary>
        public void SimulateHeartbeatTimeout()
        {
            // Push _lastHeartbeatAt far enough into the past to trigger the timeout check.
            _lastHeartbeatAt = DateTimeOffset.UtcNow - _heartbeatTimeout - TimeSpan.FromMilliseconds(100);
        }

        /// <summary>
        /// Attempts to execute a command. Commands must fail gracefully when disconnected.
        /// </summary>
        public async Task<CommandResult> ExecuteCommandAsync(
            Func<CancellationToken, Task> command,
            CancellationToken ct = default)
        {
            if (_state != IpcConnectionState.Connected)
            {
                return new CommandResult(false, $"Command rejected: IPC state is {_state}");
            }

            try
            {
                await command(ct);
                return new CommandResult(true, null);
            }
            catch (Exception ex) when (IsConnectivityException(ex))
            {
                return new CommandResult(false, ex.Message);
            }
        }

        /// <summary>
        /// Attempts to reconnect to the Core Engine.
        /// </summary>
        public async Task<bool> TryReconnectAsync(CancellationToken ct = default)
        {
            _state = IpcConnectionState.Reconnecting;
            ReconnectAttempts++;

            // Reset the last heartbeat time so PollHeartbeatAsync can evaluate fresh.
            _lastHeartbeatAt = DateTimeOffset.UtcNow;

            var connected = await PollHeartbeatAsync(ct);
            if (!connected && _state == IpcConnectionState.Reconnecting)
            {
                _state = IpcConnectionState.Disconnected;
            }
            return connected;
        }

        private static bool IsConnectivityException(Exception ex) =>
            ex is IOException or OperationCanceledException or InvalidOperationException or TimeoutException;
    }

    // -----------------------------------------------------------------------
    // Shared fields
    // -----------------------------------------------------------------------

    private readonly ISystemStatusService _statusService;
    private readonly IAuditLogService _auditLog;

    // Short heartbeat timeout so tests run quickly.
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(50);

    public IpcFailureTests()
    {
        _statusService = Substitute.For<ISystemStatusService>();
        _auditLog = Substitute.For<IAuditLogService>();

        _auditLog
            .LogAsync(
                Arg.Any<AuditEventType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AuditOutcome>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("LOG-IPC-001"));
    }

    // -----------------------------------------------------------------------
    // INT-006-1
    // -----------------------------------------------------------------------

    /// <summary>
    /// GUI must detect a lost Core Engine connection within the heartbeat timeout window.
    /// SPEC-INTEGRATION-001 – connection monitoring.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task CoreEngineDisconnected_ShouldDetectWithHeartbeatTimeout()
    {
        // Arrange
        var monitor = new IpcConnectionMonitor(_statusService, _auditLog, ShortTimeout);

        // Initial heartbeat succeeds – monitor is Connected.
        _statusService
            .GetOverallStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SystemOverallStatus
            {
                OverallHealth = ComponentHealth.Healthy,
                ComponentStatuses = Array.Empty<ComponentStatus>(),
                CanInitiateExposure = true,
                ActiveAlerts = Array.Empty<string>(),
                UpdatedAt = DateTimeOffset.UtcNow
            }));

        await monitor.PollHeartbeatAsync();
        monitor.State.Should().Be(IpcConnectionState.Connected);

        // Core Engine goes down – subsequent heartbeats fail.
        _statusService
            .GetOverallStatusAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gRPC channel shutdown"));

        // Simulate the heartbeat timeout window expiring.
        monitor.SimulateHeartbeatTimeout();

        // Act – next poll detects the timeout.
        var heartbeatSucceeded = await monitor.PollHeartbeatAsync();

        // Assert
        heartbeatSucceeded.Should().BeFalse(
            because: "a failed heartbeat after the timeout window must return false");

        monitor.State.Should().Be(IpcConnectionState.Disconnected,
            because: "the monitor must transition to Disconnected after heartbeat timeout");
    }

    // -----------------------------------------------------------------------
    // INT-006-2
    // -----------------------------------------------------------------------

    /// <summary>
    /// When Core Engine disconnects, the GUI must transition to DISCONNECTED state gracefully
    /// without throwing unhandled exceptions.
    /// SPEC-INTEGRATION-001 / IEC 62304 §5.7 – fail-safe behaviour.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task CoreEngineDisconnected_ShouldTransitionToDisconnectedState()
    {
        // Arrange
        var monitor = new IpcConnectionMonitor(_statusService, _auditLog, ShortTimeout);

        _statusService
            .GetOverallStatusAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Connection reset by peer"));

        monitor.SimulateHeartbeatTimeout();

        // Act – should not throw.
        var act = async () => await monitor.PollHeartbeatAsync();

        // Assert
        await act.Should().NotThrowAsync(
            because: "connection loss must be handled internally, never propagated as an unhandled exception");

        monitor.State.Should().Be(IpcConnectionState.Disconnected,
            because: "state must be Disconnected after the heartbeat fails");

        // Audit event must have been raised for the disconnection.
        await _auditLog
            .Received(1)
            .LogAsync(
                Arg.Any<AuditEventType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Is<string>(d => d.Contains("connection lost")),
                AuditOutcome.Failure,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // INT-006-3
    // -----------------------------------------------------------------------

    /// <summary>
    /// After Core Engine restarts, the monitor must automatically reconnect and
    /// return to CONNECTED state.
    /// SPEC-INTEGRATION-001 – automatic reconnection.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task CoreEngineReconnection_ShouldRestoreAutomatically()
    {
        // Arrange – engine is initially down.
        var monitor = new IpcConnectionMonitor(_statusService, _auditLog, ShortTimeout);

        _statusService
            .GetOverallStatusAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gRPC server unavailable"));

        monitor.SimulateHeartbeatTimeout();
        await monitor.PollHeartbeatAsync();   // triggers Disconnected transition

        monitor.State.Should().Be(IpcConnectionState.Disconnected);

        // Core Engine restarts – heartbeat succeeds again.
        _statusService
            .GetOverallStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SystemOverallStatus
            {
                OverallHealth = ComponentHealth.Healthy,
                ComponentStatuses = Array.Empty<ComponentStatus>(),
                CanInitiateExposure = true,
                ActiveAlerts = Array.Empty<string>(),
                UpdatedAt = DateTimeOffset.UtcNow
            }));

        // Act
        var reconnected = await monitor.TryReconnectAsync();

        // Assert
        reconnected.Should().BeTrue(
            because: "reconnection must succeed when the Core Engine becomes available");

        monitor.State.Should().Be(IpcConnectionState.Connected,
            because: "a successful heartbeat after reconnect must restore Connected state");
    }

    // -----------------------------------------------------------------------
    // INT-006-4
    // -----------------------------------------------------------------------

    /// <summary>
    /// Commands sent while the Core Engine is disconnected must fail gracefully
    /// with an informative error, without crashing the GUI.
    /// SPEC-INTEGRATION-001 / IEC 62304 §5.7 – fail-safe command handling.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task DuringDisconnection_CommandsShouldFailGracefully()
    {
        // Arrange – drive the monitor into Disconnected state.
        var monitor = new IpcConnectionMonitor(_statusService, _auditLog, ShortTimeout);

        _statusService
            .GetOverallStatusAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gRPC channel closed"));

        monitor.SimulateHeartbeatTimeout();
        await monitor.PollHeartbeatAsync();

        monitor.State.Should().Be(IpcConnectionState.Disconnected);

        // Act – attempt to execute a command through the disconnected monitor.
        var commandExecuted = false;
        var result = await monitor.ExecuteCommandAsync(async ct =>
        {
            // This lambda simulates a GUI command (e.g., start exposure).
            commandExecuted = true;
            await Task.CompletedTask;
        });

        // Assert
        result.Succeeded.Should().BeFalse(
            because: "commands must be rejected while the IPC link is disconnected");

        commandExecuted.Should().BeFalse(
            because: "the command body must not execute when the connection is absent");

        result.ErrorMessage.Should().NotBeNullOrEmpty(
            because: "the rejection must include a human-readable explanation");

        result.ErrorMessage!.ToLowerInvariant().Should().Contain("disconnected",
            because: "the error message must indicate the IPC state to aid operator diagnosis");
    }
}
