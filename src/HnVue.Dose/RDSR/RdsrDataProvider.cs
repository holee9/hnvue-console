using HnVue.Dicom.Rdsr;
using HnVue.Dose.Interfaces;
using Microsoft.Extensions.Logging;

namespace HnVue.Dose.RDSR;

/// <summary>
/// Implements IRdsrDataProvider to expose dose data to HnVue.Dicom for RDSR generation.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of IRdsrDataProvider - HnVue.Dicom integration
/// @MX:REASON: Primary interface for exposing dose data to DICOM module
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-02, FR-DOSE-07
///
/// This is the bridge between HnVue.Dose (dose calculation, persistence, DRL alerts)
/// and HnVue.Dicom (RDSR building, DICOM C-STORE).
///
/// Thread-safe: All methods are thread-safe for concurrent calls.
/// </remarks>
public sealed class RdsrDataProvider : IRdsrDataProvider, IDisposable
{
    private readonly IDoseRecordRepository _repository;
    private readonly ILogger<RdsrDataProvider> _logger;
    private readonly StudyClosedObservable _studyClosedObservable;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the RdsrDataProvider class.
    /// </summary>
    /// <param name="repository">Dose record repository</param>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
    public RdsrDataProvider(IDoseRecordRepository repository, ILogger<RdsrDataProvider> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _studyClosedObservable = new StudyClosedObservable();

        _logger.LogInformation("RdsrDataProvider initialized");
    }

    /// <summary>
    /// Gets an observable stream of study closure events.
    /// </summary>
    /// <remarks>
    /// DICOM consumers subscribe to receive notifications when dose studies close.
    /// </remarks>
    public IObservable<StudyCompletedEvent> StudyClosed => _studyClosedObservable.AsObservable();

    /// <summary>
    /// Retrieves a summary of accumulated dose data for a completed study.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>StudyDoseSummary with cumulative metrics, or null if study not found</returns>
    /// <remarks>
    /// Returns null if studyInstanceUid has no recorded dose events.
    /// Returned data is immutable and safe for concurrent access.
    /// </remarks>
    public async Task<StudyDoseSummary?> GetStudyDoseSummaryAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();

        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            throw new ArgumentException("Study Instance UID is required.", nameof(studyInstanceUid));
        }

        try
        {
            var exposures = await _repository.GetByStudyAsync(studyInstanceUid, cancellationToken);

            if (exposures.Count == 0)
            {
                _logger.LogDebug("No dose records found for study: {StudyUid}", studyInstanceUid);
                return null;
            }

            if (exposures.Count == 0)
            {
                _logger.LogDebug("No dose records found for study: {StudyUid}", studyInstanceUid);
                return null;
            }

            // Calculate cumulative DAP from effective DAP values
            var cumulativeDap = exposures.Sum(r => r.EffectiveDapGyCm2);
            var firstExposure = exposures.MinBy(r => r.TimestampUtc)!;
            var lastExposure = exposures.MaxBy(r => r.TimestampUtc)!;

            var summary = new StudyDoseSummary
            {
                StudyInstanceUid = studyInstanceUid,
                PatientId = firstExposure.PatientId,
                Modality = "DX", // Default modality for X-ray
                TotalDapGyCm2 = cumulativeDap,
                ExposureCount = exposures.Count,
                StudyStartTimeUtc = firstExposure.TimestampUtc,
                StudyEndTimeUtc = lastExposure.TimestampUtc,
                DrlExceeded = exposures.Any(r => r.DrlExceedance)
            };

            _logger.LogDebug(
                "Study dose summary retrieved: Study={StudyUid}, Patient={PatientId}, TotalDAP={TotalDap}Gy·cm², Exposures={Count}",
                studyInstanceUid, summary.PatientId, cumulativeDap, exposures.Count);

            return summary;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to retrieve study dose summary: {StudyUid}", studyInstanceUid);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all exposure records for a study, in chronological order.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read-only list of DoseRecord for each exposure, sorted by TimestampUtc ascending</returns>
    /// <remarks>
    /// Returns an empty list if no exposures are found.
    /// Records include both calculated and measured DAP values (if meter was used).
    /// </remarks>
    public async Task<IReadOnlyList<HnVue.Dicom.Rdsr.DoseRecord>> GetStudyExposureRecordsAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();

        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            throw new ArgumentException("Study Instance UID is required.", nameof(studyInstanceUid));
        }

        try
        {
            var doseRecords = await _repository.GetByStudyAsync(studyInstanceUid, cancellationToken);

            // Return exposures in chronological order
            var sortedRecords = doseRecords
                .OrderBy(r => r.TimestampUtc)
                .ToList();

            _logger.LogDebug(
                "Study exposure records retrieved: Study={StudyUid}, Count={Count}",
                studyInstanceUid, sortedRecords.Count);

            return sortedRecords;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to retrieve study exposure records: {StudyUid}", studyInstanceUid);
            throw;
        }
    }

    /// <summary>
    /// Signals that a study dose session is complete and may be queried for export.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="patientId">Patient ID for this study</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Called by the DOSE module when a study is closed.
    /// The DICOM module can then request RDSR generation.
    /// </remarks>
    public async Task NotifyStudyClosedAsync(
        string studyInstanceUid,
        string patientId,
        CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();

        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            throw new ArgumentException("Study Instance UID is required.", nameof(studyInstanceUid));
        }

        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient ID is required.", nameof(patientId));
        }

        // Get exposure records to calculate summary data for the event
        var exposures = await _repository.GetByStudyAsync(studyInstanceUid, cancellationToken);
        var exposureCount = exposures.Count;
        var totalDap = exposures.Sum(r => r.EffectiveDapGyCm2);

        lock (_lock)
        {
            var eventArgs = new StudyCompletedEvent
            {
                StudyInstanceUid = studyInstanceUid,
                PatientId = patientId,
                ClosedAtUtc = DateTime.UtcNow,
                ExposureCount = exposureCount,
                TotalDapGyCm2 = totalDap
            };

            _studyClosedObservable.Notify(eventArgs);

            _logger.LogInformation(
                "Study closed notification published: Study={StudyUid}, Patient={PatientId}, Exposures={Count}, TotalDAP={TotalDap}Gy·cm²",
                studyInstanceUid, patientId, exposureCount, totalDap);
        }
    }

    /// <summary>
    /// Verifies the provider has not been disposed.
    /// </summary>
    private void VerifyNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RdsrDataProvider));
        }
    }

    /// <summary>
    /// Disposes the provider.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogDebug("RdsrDataProvider disposing");

            _studyClosedObservable.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Internal observable for study closure events.
    /// </summary>
    private sealed class StudyClosedObservable : IObservable<StudyCompletedEvent>, IDisposable
    {
        private readonly List<IObserver<StudyCompletedEvent>> _observers = new();
        private readonly object _lock = new();
        private bool _disposed;

        public IObservable<StudyCompletedEvent> AsObservable()
        {
            return this;
        }

        public IDisposable Subscribe(IObserver<StudyCompletedEvent> observer)
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

        public void Notify(StudyCompletedEvent eventArgs)
        {
            List<IObserver<StudyCompletedEvent>> observersCopy;

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
                    observer.OnNext(eventArgs);
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

        public void Unsubscribe(IObserver<StudyCompletedEvent> observer)
        {
            lock (_lock)
            {
                _observers.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly StudyClosedObservable _observable;
            private readonly IObserver<StudyCompletedEvent> _observer;
            private bool _disposed;

            public Subscription(StudyClosedObservable observable, IObserver<StudyCompletedEvent> observer)
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

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            private EmptyDisposable() { }
            public void Dispose() { }
        }
    }
}
