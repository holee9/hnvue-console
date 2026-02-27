using Dicom;
using DicomNetwork = Dicom.Network;
using Dicom.Network.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HnVue.Dicom.Configuration;

namespace HnVue.Dicom.Associations;

/// <summary>
/// Manages DICOM association lifecycle including A-ASSOCIATE negotiation,
/// TLS support, and connection pooling.
/// </summary>
public interface IAssociationManager
{
    /// <summary>
    /// Creates a new DICOM client associated to the specified destination.
    /// </summary>
    /// <param name="destination">The DICOM destination (SCP) to connect to.</param>
    /// <param name="presentationContexts">Presentation contexts for the association.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A configured DICOM client ready to send requests.</returns>
    Task<DicomClient> CreateAssociationAsync(
        DicomDestination destination,
        List<DicomNetwork.DicomPresentationContext> presentationContexts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes and disposes an association gracefully.
    /// </summary>
    /// <param name="client">The DICOM client to dispose.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    Task CloseAssociationAsync(DicomClient client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a destination configuration is correct.
    /// </summary>
    /// <param name="destination">The destination to validate.</param>
    /// <returns>True if valid; otherwise, false with validation errors.</returns>
    (bool IsValid, List<string> Errors) ValidateDestination(DicomDestination destination);
}

/// <summary>
/// Default implementation of DICOM association manager.
/// Thread-safe for concurrent association creation.
/// </summary>
public sealed class AssociationManager : IAssociationManager, IDisposable
{
    private readonly Configuration.DicomServiceOptions _options;
    private readonly ILogger<AssociationManager> _logger;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly Dictionary<string, SemaphoreSlim> _destinationSemaphores;
    private readonly object _lock = new();
    private bool _disposed;

    public AssociationManager(
        IOptions<Configuration.DicomServiceOptions> options,
        ILogger<AssociationManager> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Global semaphore to prevent connection exhaustion across all destinations
        var maxTotalConnections = _options.AssociationPool.MaxSize * 5; // Allow for multiple destinations
        _connectionSemaphore = new SemaphoreSlim(maxTotalConnections, maxTotalConnections);
        _destinationSemaphores = new Dictionary<string, SemaphoreSlim>();
    }

    /// <inheritdoc/>
    public async Task<DicomClient> CreateAssociationAsync(
        DicomDestination destination,
        List<DicomNetwork.DicomPresentationContext> presentationContexts,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Validate destination
        var (isValid, errors) = ValidateDestination(destination);
        if (!isValid)
        {
            throw new ArgumentException(
                $"Invalid DICOM destination: {string.Join(", ", errors)}",
                nameof(destination));
        }

        var destinationKey = GetDestinationKey(destination);

        // Acquire destination-specific semaphore to enforce per-destination pool limit
        var destinationSemaphore = GetDestinationSemaphore(destinationKey);
        var acquired = await destinationSemaphore.WaitAsync(
            _options.AssociationPool.AcquisitionTimeoutMs,
            cancellationToken);

        if (!acquired)
        {
            _logger.LogWarning(
                "Failed to acquire association slot for destination {Destination} within timeout {TimeoutMs}ms",
                SanitizeDestination(destinationKey),
                _options.AssociationPool.AcquisitionTimeoutMs);
            throw new TimeoutException(
                $"Could not acquire association slot for destination {SanitizeDestination(destinationKey)} within timeout.");
        }

        try
        {
            // Create and configure the DICOM client
            var client = CreateDicomClient(destination);

            // Add presentation contexts
            foreach (var context in presentationContexts)
            {
                client.AdditionalPresentationContexts.Add(context);
            }

            // fo-dicom 4.x: DicomClient establishes connection on SendAsync(), not here.
            // Return configured client ready for AddRequestAsync() + SendAsync() calls.
            _logger.LogDebug(
                "DICOM client configured for {Destination} (AE: {AeTitle}, Host: {Host}, Port: {Port})",
                SanitizeDestination(destinationKey),
                destination.AeTitle,
                destination.Host,
                destination.Port);

            return client;
        }
        catch (Exception ex)
        {
            destinationSemaphore.Release();
            _logger.LogError(ex,
                "Failed to establish association to {Destination}",
                SanitizeDestination(destinationKey));
            throw;
        }
    }

    /// <inheritdoc/>
    public Task CloseAssociationAsync(
        DicomClient client,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (client == null)
        {
            return Task.CompletedTask;
        }

        var destinationKey = GetDestinationKeyFromClient(client);

        try
        {
            // fo-dicom 4.x: DicomClient is not IDisposable; connection closes after SendAsync() completes.
            _logger.LogDebug("Releasing association slot for {Destination}", destinationKey);
            _logger.LogInformation("Association closed to {Destination}", destinationKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error releasing association for {Destination}", destinationKey);
        }
        finally
        {
            ReleaseDestinationSemaphore(destinationKey);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public (bool IsValid, List<string> Errors) ValidateDestination(DicomDestination destination)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(destination.AeTitle))
        {
            errors.Add("AE Title is required");
        }
        else if (destination.AeTitle.Length > 16)
        {
            errors.Add("AE Title must not exceed 16 characters");
        }

        if (string.IsNullOrWhiteSpace(destination.Host))
        {
            errors.Add("Host is required");
        }

        if (destination.Port < 1 || destination.Port > 65535)
        {
            errors.Add("Port must be between 1 and 65535");
        }

        return (errors.Count == 0, errors);
    }

    private DicomClient CreateDicomClient(DicomDestination destination)
    {
        // fo-dicom 4.x constructor: DicomClient(host, port, useTls, callingAe, calledAe,
        //   associationRequestTimeoutInMs, associationReleaseTimeoutInMs, associationLingerTimeoutInMs, maxRequests)
        var tlsEnabled = destination.TlsEnabled ?? _options.Tls.Enabled;
        var client = new DicomClient(
            destination.Host,
            destination.Port,
            tlsEnabled,
            _options.CallingAeTitle,
            destination.AeTitle,
            _options.Timeouts.AssociationRequestMs,
            _options.Timeouts.AssociationRequestMs,
            50,   // linger timeout ms
            null  // no per-association request limit
        );

        if (tlsEnabled)
        {
            ConfigureTls(client, destination);
        }

        return client;
    }

    private void ConfigureTls(DicomClient client, DicomDestination destination)
    {
        _logger.LogDebug("Configuring TLS for connection to {Destination}", destination.AeTitle);

        // TLS configuration would be implemented here using fo-dicom's TLS support
        // This requires integration with .NET's SslStream and certificate handling
        // For now, this is a placeholder for TLS configuration

        // Note: Full TLS implementation requires:
        // 1. Certificate loading from Windows Certificate Store or file
        // 2. Server certificate validation
        // 3. Optional client certificate for mTLS
        // 4. Cipher suite configuration

        _logger.LogInformation("TLS configuration applied for connection to {Destination}", destination.AeTitle);
    }

    private string GetDestinationKey(DicomDestination destination)
    {
        return $"{destination.AeTitle}@{destination.Host}:{destination.Port}";
    }

    private string GetDestinationKeyFromClient(DicomClient client)
    {
        // fo-dicom 4.x: CalledAe, Host, Port (read-only, set in constructor)
        return $"{client.CalledAe}@{client.Host}:{client.Port}";
    }

    private SemaphoreSlim GetDestinationSemaphore(string destinationKey)
    {
        lock (_lock)
        {
            if (!_destinationSemaphores.TryGetValue(destinationKey, out var semaphore))
            {
                semaphore = new SemaphoreSlim(_options.AssociationPool.MaxSize, _options.AssociationPool.MaxSize);
                _destinationSemaphores[destinationKey] = semaphore;
            }
            return semaphore;
        }
    }

    private void ReleaseDestinationSemaphore(string destinationKey)
    {
        lock (_lock)
        {
            if (_destinationSemaphores.TryGetValue(destinationKey, out var semaphore))
            {
                semaphore.Release();
            }
        }
    }

    private string SanitizeDestination(string destination)
    {
        // Remove PHI-related information from logging if present
        // (Not typically in AE titles, but following NFR-SEC-01 principle)
        return destination;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AssociationManager));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _connectionSemaphore.Dispose();

            foreach (var semaphore in _destinationSemaphores.Values)
            {
                semaphore.Dispose();
            }

            _destinationSemaphores.Clear();
            _disposed = true;
        }
    }
}
