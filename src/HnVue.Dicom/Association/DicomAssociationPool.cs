using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using DicomNetwork = Dicom.Network;
using Dicom.Network.Client;
using HnVue.Dicom.Configuration;
using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Associations;

/// <summary>
/// Pool for managing DICOM associations with connection limits and lifecycle management.
/// </summary>
/// <remarks>
/// @MX:ANCHOR Pool management - Manages DICOM association lifecycle
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-409
///
/// Features:
/// - Association lifecycle management
/// - Connection pooling (max 5 associations)
/// - 10 second timeout for acquisition
/// - Clean shutdown
/// </remarks>
public sealed class DicomAssociationPool : IDisposable
{
    private const int MaxPoolSize = 5;
    private const int AcquisitionTimeoutMs = 10000; // 10 seconds

    private readonly IAssociationManager _associationManager;
    private readonly ILogger<DicomAssociationPool> _logger;
    private readonly ConcurrentDictionary<string, PoolEntry> _pool;
    private readonly SemaphoreSlim _poolSemaphore;
    private readonly CancellationTokenSource _shutdownCts;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomAssociationPool"/> class.
    /// </summary>
    /// <param name="associationManager">The inner association manager.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DicomAssociationPool(
        IAssociationManager associationManager,
        ILogger<DicomAssociationPool> logger)
    {
        _associationManager = associationManager ?? throw new ArgumentNullException(nameof(associationManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _pool = new ConcurrentDictionary<string, PoolEntry>();
        _poolSemaphore = new SemaphoreSlim(MaxPoolSize, MaxPoolSize);
        _shutdownCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Gets the current number of associations in the pool.
    /// </summary>
    public int PoolCount => _pool.Count;

    /// <summary>
    /// Acquires an association from the pool or creates a new one.
    /// </summary>
    /// <param name="destination">The DICOM destination.</param>
    /// <param name="presentationContexts">The presentation contexts for the association.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A DICOM client ready for use.</returns>
    /// <remarks>
    /// @MX:ANCHOR Pool management - Acquires associations with pool limit enforcement
    ///
    /// Throws TimeoutException if the association cannot be acquired within 10 seconds.
    /// </remarks>
    public async Task<DicomClient> AcquireAssociationAsync(
        DicomDestination destination,
        List<DicomNetwork.DicomPresentationContext> presentationContexts,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var destinationKey = GetDestinationKey(destination);

        // Acquire pool slot with timeout
        using var timeoutCts = new CancellationTokenSource(AcquisitionTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdownCts.Token,
            timeoutCts.Token);

        try
        {
            await _poolSemaphore.WaitAsync(linkedCts.Token);

            // Try to get existing association from pool
            if (_pool.TryRemove(destinationKey, out var entry))
            {
                _logger.LogDebug(
                    "Reusing pooled association for {Destination}",
                    destinationKey);

                return entry.Client;
            }

            // Create new association
            _logger.LogInformation(
                "Creating new association for {Destination} (Pool size: {PoolCount}/{MaxPoolSize})",
                destinationKey,
                _pool.Count,
                MaxPoolSize);

            var client = await _associationManager.CreateAssociationAsync(
                destination,
                presentationContexts,
                linkedCts.Token);

            return client;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Could not acquire association slot for {destinationKey} within {AcquisitionTimeoutMs}ms");
        }
    }

    /// <summary>
    /// Releases an association back to the pool for reuse.
    /// </summary>
    /// <param name="client">The DICOM client to release.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <remarks>
    /// @MX:ANCHOR Pool management - Releases associations back to pool
    /// </remarks>
    public Task ReleaseAssociationAsync(
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
            // Add back to pool for reuse
            var entry = new PoolEntry
            {
                Client = client,
                AcquiredAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            _pool.TryAdd(destinationKey, entry);

            _logger.LogDebug(
                "Released association to pool for {Destination} (Pool size: {PoolCount}/{MaxPoolSize})",
                destinationKey,
                _pool.Count,
                MaxPoolSize);
        }
        finally
        {
            _poolSemaphore.Release();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Shuts down the association pool and releases all resources.
    /// </summary>
    /// <remarks>
    /// @MX:ANCHOR Pool management - Clean shutdown releases all associations
    /// </remarks>
    public async Task ShutdownAsync()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Shutting down association pool ({Count} associations)", _pool.Count);

        _shutdownCts.Cancel();

        // Close all pooled associations
        foreach (var entry in _pool.Values)
        {
            try
            {
                await _associationManager.CloseAssociationAsync(entry.Client);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing association during shutdown");
            }
        }

        _pool.Clear();

        _logger.LogInformation("Association pool shutdown complete");
    }

    private string GetDestinationKey(DicomDestination destination)
    {
        return $"{destination.AeTitle}@{destination.Host}:{destination.Port}";
    }

    private string GetDestinationKeyFromClient(DicomClient client)
    {
        return $"{client.CalledAe}@{client.Host}:{client.Port}";
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DicomAssociationPool));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _shutdownCts.Cancel();
        _poolSemaphore.Dispose();
        _shutdownCts.Dispose();
        _disposed = true;
    }

    private class PoolEntry
    {
        public required DicomClient Client { get; init; }
        public DateTime AcquiredAt { get; set; }
        public DateTime LastUsed { get; set; }
    }
}

/// <summary>
/// High-level manager for DICOM associations.
/// </summary>
/// <remarks>
/// @MX:ANCHOR Association management - High-level association lifecycle management
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-409
/// </remarks>
public sealed class DicomAssociationManager
{
    private readonly DicomAssociationPool _pool;
    private readonly ILogger<DicomAssociationManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomAssociationManager"/> class.
    /// </summary>
    /// <param name="pool">The association pool.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DicomAssociationManager(
        DicomAssociationPool pool,
        ILogger<DicomAssociationManager> logger)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Acquires an association from the pool.
    /// </summary>
    public Task<DicomClient> AcquireAssociationAsync(
        DicomDestination destination,
        List<DicomNetwork.DicomPresentationContext> presentationContexts,
        CancellationToken cancellationToken = default)
    {
        return _pool.AcquireAssociationAsync(destination, presentationContexts, cancellationToken);
    }

    /// <summary>
    /// Releases an association back to the pool.
    /// </summary>
    public Task ReleaseAssociationAsync(
        DicomClient client,
        CancellationToken cancellationToken = default)
    {
        _pool.ReleaseAssociationAsync(client, cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shuts down the association pool.
    /// </summary>
    public Task ShutdownAsync()
    {
        return _pool.ShutdownAsync();
    }
}
