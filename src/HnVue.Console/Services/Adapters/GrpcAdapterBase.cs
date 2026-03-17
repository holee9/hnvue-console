using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Configuration;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// Base class for gRPC service adapters.
/// Manages channel lifecycle and client creation with TLS/mTLS support.
/// SPEC-SECURITY-001: Secure gRPC communication.
/// </summary>
public abstract class GrpcAdapterBase : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ILogger _logger;
    private readonly GrpcSecurityOptions _securityOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes the gRPC channel from configuration with TLS/mTLS support.
    /// </summary>
    /// <param name="configuration">Application configuration (reads GrpcServer:Address and GrpcSecurity).</param>
    /// <param name="logger">Logger for channel diagnostics.</param>
    protected GrpcAdapterBase(IConfiguration configuration, ILogger logger)
    {
        _securityOptions = configuration.GetSection("GrpcSecurity").Get<GrpcSecurityOptions>() ?? new GrpcSecurityOptions();

        // Debug logging for E2E testing
        System.Console.WriteLine($"[GrpcAdapterBase] EnableTls={_securityOptions.EnableTls}, EnableMutualTls={_securityOptions.EnableMutualTls}");

        if (!_securityOptions.Validate())
        {
            throw new InvalidOperationException("Invalid gRPC security configuration. Check GrpcSecurity settings.");
        }

        var address = configuration["GrpcServer:Address"] ?? "http://localhost:50051";
        _channel = CreateSecureChannel(address, _securityOptions);
        _logger = logger;

        var tlsInfo = _securityOptions.EnableTls
            ? $"TLS {_securityOptions.MinTlsVersion}{(_securityOptions.EnableMutualTls ? " + mTLS" : "")}"
            : "No TLS";
        _logger.LogInformation("gRPC channel created for {Address} ({TlsMode})", address, tlsInfo);
    }

    /// <summary>
    /// Creates a gRPC channel with TLS/mTLS configuration.
    /// </summary>
    private static GrpcChannel CreateSecureChannel(string address, GrpcSecurityOptions options)
    {
        var channelOptions = new GrpcChannelOptions();

        if (options.EnableTls)
        {
            var credentials = LoadSslCredentials(options);
            channelOptions.Credentials = credentials;
            channelOptions.HttpHandler = CreateHttpHandler(options);
        }

        return GrpcChannel.ForAddress(address, channelOptions);
    }

    /// <summary>
    /// Loads SSL credentials for TLS/mTLS.
    /// </summary>
    private static SslCredentials LoadSslCredentials(GrpcSecurityOptions options)
    {
        var clientCertificate = options.LoadClientCertificate();
        var rootCertificate = options.LoadRootCertificate();

        if (clientCertificate != null && rootCertificate != null)
        {
            // mTLS with custom CA
            var keyCertPair = new KeyCertificatePair(
                rootCertificate.ExportCertificatePem(),
                clientCertificate.ExportCertificatePem());
            return new SslCredentials(rootCertificate.ExportCertificatePem(), keyCertPair);
        }
        else if (rootCertificate != null)
        {
            // TLS with custom CA (server-only authentication)
            return new SslCredentials(rootCertificate.ExportCertificatePem());
        }
        else
        {
            // Default system trust store
            return new SslCredentials();
        }
    }

    /// <summary>
    /// Creates HTTP handler with TLS configuration.
    /// </summary>
    private static HttpMessageHandler CreateHttpHandler(GrpcSecurityOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = options.MinTlsVersion == TlsVersion.Tls13
                    ? SslProtocols.Tls13
                    : SslProtocols.Tls12,

                CertificateRevocationCheckMode = options.CheckCertificateRevocation
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck,

                RemoteCertificateValidationCallback = options.SkipServerCertificateValidation
                    ? (_, _, _, _) => true
                    : CreateCertificateValidator(options)
            }
        };

        return handler;
    }

    /// <summary>
    /// Creates a certificate validation callback for custom validation.
    /// </summary>
    private static System.Net.Security.RemoteCertificateValidationCallback? CreateCertificateValidator(GrpcSecurityOptions options)
    {
        if (options.AllowedServerCertificateThumbprints.Count == 0 &&
            options.AllowedSubjectNames.Count == 0)
        {
            return null; // Use default validation
        }

        return (sender, certificate, chain, errors) =>
        {
            if (certificate == null) return false;

            var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate);

            // Check thumbprint whitelist
            if (options.AllowedServerCertificateThumbprints.Count > 0)
            {
                var thumbprint = cert.Thumbprint?.ToUpperInvariant();
                if (thumbprint == null || !options.AllowedServerCertificateThumbprints.Contains(thumbprint))
                {
                    return false;
                }
            }

            // Check subject name whitelist
            if (options.AllowedSubjectNames.Count > 0)
            {
                var subject = cert.Subject;
                if (!options.AllowedSubjectNames.Any(allowed => subject.Contains(allowed)))
                {
                    return false;
                }
            }

            return true;
        };
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
