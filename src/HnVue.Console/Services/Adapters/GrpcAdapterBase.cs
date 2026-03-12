using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// Base class for gRPC service adapters.
/// Manages channel lifecycle and client creation.
/// </summary>
public abstract class GrpcAdapterBase : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes the gRPC channel from configuration.
    /// </summary>
    /// <param name="configuration">Application configuration (reads GrpcServer:Address).</param>
    /// <param name="logger">Logger for channel diagnostics.</param>
    protected GrpcAdapterBase(IConfiguration configuration, ILogger logger)
    {
        var address = configuration["GrpcServer:Address"] ?? "http://localhost:50051";
        _channel = GrpcChannel.ForAddress(address);
        _logger = logger;
        _logger.LogInformation("gRPC channel created for {Address}", address);
    }

    /// <summary>
    /// Creates a typed gRPC client bound to the shared channel.
    /// </summary>
    protected T CreateClient<T>() where T : Grpc.Core.ClientBase<T>
    {
        return (T)Activator.CreateInstance(typeof(T), _channel)!;
    }

    /// <summary>
    /// @MX:NOTE Tests gRPC channel connectivity by making a lightweight health check.
    /// Returns true if server responds, false if unreachable or times out.
    /// </summary>
    /// <param name="timeoutMs">Connection timeout in milliseconds (default: 2000ms).</param>
    /// <returns>True if gRPC server is reachable, false otherwise.</returns>
    protected async Task<bool> TryConnectAsync(int timeoutMs = 2000)
    {
        try
        {
            // Use channel state to check connectivity
            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            await _channel.ConnectAsync(cts.Token);
            var state = _channel.State;
            return state == ConnectivityState.Ready || state == ConnectivityState.Idle;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "gRPC connectivity check failed");
            return false;
        }
    }

    /// <summary>
    /// @MX:NOTE Gets current channel connectivity state.
    /// </summary>
    protected ConnectivityState GetChannelState() => _channel.State;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the gRPC channel.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _channel.Dispose();
        }
        _disposed = true;
    }
}
