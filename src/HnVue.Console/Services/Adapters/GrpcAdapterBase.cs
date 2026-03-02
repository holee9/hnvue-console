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
        logger.LogInformation("gRPC channel created for {Address}", address);
    }

    /// <summary>
    /// Creates a typed gRPC client bound to the shared channel.
    /// </summary>
    protected T CreateClient<T>() where T : Grpc.Core.ClientBase<T>
    {
        return (T)Activator.CreateInstance(typeof(T), _channel)!;
    }

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
