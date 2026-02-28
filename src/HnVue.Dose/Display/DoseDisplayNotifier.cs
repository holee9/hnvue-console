using HnVue.Dicom.Rdsr;
using HnVue.Dose.Interfaces;
using Microsoft.Extensions.Logging;

namespace HnVue.Dose.Display;

/// <summary>
/// Publishes dose display updates to GUI layer via observable stream.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of dose display notifications - IEC 60601-2-54 compliance
/// @MX:REASON: Critical implementation of SPEC-DOSE-001 FR-DOSE-04
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-04
///
/// Uses custom Observable implementation for thread-safe event pattern.
/// GUI layer subscribes to DoseUpdates to receive real-time dose updates.
///
/// Display requirements per IEC 60601-2-54:
/// - Update within 1 second of exposure completion
/// - SI units (Gy·cm² or mGy·cm²)
/// - At least 2 decimal places precision
/// </remarks>
public sealed class DoseDisplayNotifier : IDoseDisplayNotifier, IDisposable
{
    private readonly ILogger<DoseDisplayNotifier> _logger;
    private readonly DoseUpdateObservable _updateObservable;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DoseDisplayNotifier class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
    public DoseDisplayNotifier(ILogger<DoseDisplayNotifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _updateObservable = new DoseUpdateObservable();

        _logger.LogInformation("DoseDisplayNotifier initialized");
    }

    /// <summary>
    /// Gets the observable stream of dose display updates.
    /// </summary>
    /// <remarks>
    /// GUI layer subscribes to this IObservable to receive real-time updates.
    /// Each emission contains current exposure DAP and cumulative study DAP.
    /// </remarks>
    public IObservable<DoseDisplayUpdate> DoseUpdates
    {
        get
        {
            VerifyNotDisposed();
            return _updateObservable;
        }
    }

    /// <summary>
    /// Publishes a dose display update to all subscribers.
    /// </summary>
    /// <param name="update">Dose display update containing current and cumulative DAP values</param>
    /// <exception cref="ArgumentNullException">Thrown when update is null</exception>
    /// <exception cref="ObjectDisposedException">Thrown when notifier is disposed</exception>
    public void Publish(DoseDisplayUpdate update)
    {
        VerifyNotDisposed();

        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        lock (_lock)
        {
            _updateObservable.Notify(update);

            _logger.LogDebug(
                "Dose display update published: ExposureId={ExposureId}, ExposureDAP={ExposureDap}Gy·cm², CumulativeDAP={CumulativeDap}Gy·cm²",
                update.ExposureEventId, update.ExposureDapGyCm2, update.StudyCumulativeDapGyCm2);
        }
    }

    /// <summary>
    /// Clears the dose display (no active study state).
    /// </summary>
    /// <remarks>
    /// Publishes a cleared update when no active study session is open.
    /// See SPEC-DOSE-001 FR-DOSE-04-D.
    /// </remarks>
    public void ClearDisplay()
    {
        VerifyNotDisposed();

        var clearedUpdate = new DoseDisplayUpdate
        {
            ExposureEventId = Guid.Empty,
            ExposureDapGyCm2 = 0m,
            StudyCumulativeDapGyCm2 = 0m,
            StudyExposureCount = 0,
            StudyInstanceUid = null,
            PatientId = null,
            TimestampUtc = DateTime.UtcNow,
            DrlExceeded = false,
            DrlThresholdGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            HasActiveStudy = false
        };

        Publish(clearedUpdate);
        _logger.LogDebug("Dose display cleared (no active study)");
    }

    /// <summary>
    /// Disposes the notifier and terminates the observable stream.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogDebug("DoseDisplayNotifier disposing");

            _updateObservable.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Verifies the notifier has not been disposed.
    /// </summary>
    private void VerifyNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DoseDisplayNotifier));
        }
    }

    /// <summary>
    /// Custom observable implementation for dose display updates.
    /// </summary>
    private sealed class DoseUpdateObservable : IObservable<DoseDisplayUpdate>, IDisposable
    {
        private readonly List<IObserver<DoseDisplayUpdate>> _observers = new();
        private readonly object _lock = new();
        private bool _disposed;

        public IDisposable Subscribe(IObserver<DoseDisplayUpdate> observer)
        {
            if (observer is null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            lock (_lock)
            {
                if (_disposed)
                {
                    observer.OnCompleted();
                    return EmptyDisposable.Instance;
                }

                _observers.Add(observer);
                return new Subscription(this, observer);
            }
        }

        public void Notify(DoseDisplayUpdate update)
        {
            List<IObserver<DoseDisplayUpdate>> observersCopy;

            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                observersCopy = _observers.ToList();
            }

            foreach (var observer in observersCopy)
            {
                try
                {
                    observer.OnNext(update);
                }
                catch (Exception ex)
                {
                    // Observer threw exception - notify and continue
                    observer.OnError(ex);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                foreach (var observer in _observers)
                {
                    observer.OnCompleted();
                }

                _observers.Clear();
                _disposed = true;
            }
        }

        private void Unsubscribe(IObserver<DoseDisplayUpdate> observer)
        {
            lock (_lock)
            {
                _observers.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly DoseUpdateObservable _observable;
            private readonly IObserver<DoseDisplayUpdate> _observer;
            private bool _disposed;

            public Subscription(DoseUpdateObservable observable, IObserver<DoseDisplayUpdate> observer)
            {
                _observable = observable;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _observable.Unsubscribe(_observer);
                _disposed = true;
            }
        }

        /// <summary>
        /// Empty disposable for no-op subscriptions.
        /// </summary>
        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();

            private EmptyDisposable() { }

            public void Dispose() { }
        }
    }
}
