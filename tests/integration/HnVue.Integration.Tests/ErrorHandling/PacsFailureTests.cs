using System.IO;
using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HnVue.Integration.Tests.ErrorHandling;

/// <summary>
/// INT-005: PACS Communication Failure Integration Tests.
/// Validates error handling logic when DICOM network operations fail.
/// Strategy: NSubstitute mocks for IImageService / IAuditLogService to simulate network failures
/// without requiring Docker. Tests verify fault isolation, data-loss prevention, retry semantics,
/// and proper state transitions mandated by IEC 62304 Class B/C and SPEC-INTEGRATION-001.
/// </summary>
public sealed class PacsFailureTests
{
    // -----------------------------------------------------------------------
    // Test infrastructure
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lightweight in-memory retry queue that survives transient PACS failures.
    /// Represents the minimal contract the GUI layer expects from the storage layer.
    /// </summary>
    private sealed class InMemoryRetryQueue
    {
        private readonly Queue<PendingCStoreOperation> _queue = new();

        public void Enqueue(PendingCStoreOperation op) => _queue.Enqueue(op);

        public bool TryDequeue(out PendingCStoreOperation? op)
        {
            if (_queue.Count > 0)
            {
                op = _queue.Dequeue();
                return true;
            }
            op = null;
            return false;
        }

        public int Count => _queue.Count;
    }

    /// <summary>
    /// Represents a DICOM C-STORE operation pending retry after a transient failure.
    /// </summary>
    private sealed record PendingCStoreOperation(
        string StudyId,
        string PatientId,
        byte[] Payload,
        int AttemptCount,
        DateTimeOffset EnqueuedAt);

    // Status values for the PACS store operation.
    private enum PacsStoreStatus { Success, Failed, PermanentlyFailed }

    /// <summary>
    /// Simulates the orchestrator used by the GUI layer:
    /// wraps IImageService (representing the PACS connection) with retry queue
    /// and operator-notification semantics.
    /// </summary>
    private sealed class PacsStoreOrchestrator
    {
        private const int MaxRetries = 3;

        private readonly IImageService _imageService;
        private readonly IAuditLogService _auditLog;
        private readonly InMemoryRetryQueue _retryQueue;

        public List<string> OperatorNotifications { get; } = new();

        public PacsStoreOrchestrator(
            IImageService imageService,
            IAuditLogService auditLog,
            InMemoryRetryQueue retryQueue)
        {
            _imageService = imageService;
            _auditLog = auditLog;
            _retryQueue = retryQueue;
        }

        /// <summary>
        /// Attempts to retrieve and store a study via PACS.
        /// On first failure the operation is enqueued for retry.
        /// Returns the current status after the first attempt.
        /// </summary>
        public async Task<PacsStoreStatus> TrySendAsync(
            string studyId, string patientId, byte[] payload,
            CancellationToken ct = default)
        {
            try
            {
                // GetCurrentImageAsync represents any PACS network operation
                // (C-STORE, C-FIND, etc.). We use the existing interface method
                // rather than introducing a new interface.
                await _imageService.GetCurrentImageAsync(studyId, ct);
                return PacsStoreStatus.Success;
            }
            catch (Exception ex) when (IsTransientNetworkException(ex))
            {
                // Enqueue for retry so data is not lost.
                _retryQueue.Enqueue(new PendingCStoreOperation(
                    studyId, patientId, payload, 1, DateTimeOffset.UtcNow));

                await _auditLog.LogAsync(
                    AuditEventType.DataExport,
                    "system",
                    "System",
                    $"PACS C-STORE failed for study {studyId}; enqueued for retry",
                    AuditOutcome.Failure,
                    patientId,
                    studyId,
                    ct);

                return PacsStoreStatus.Failed;
            }
        }

        /// <summary>
        /// Processes the next queued operation, retrying up to <see cref="MaxRetries"/> times.
        /// Moves permanently failed operations to the operator notification list.
        /// </summary>
        public async Task<PacsStoreStatus> RetryQueuedAsync(CancellationToken ct = default)
        {
            if (!_retryQueue.TryDequeue(out var op) || op is null)
                return PacsStoreStatus.Success;

            int attempt = op.AttemptCount;
            while (attempt <= MaxRetries)
            {
                try
                {
                    await _imageService.GetCurrentImageAsync(op.StudyId, ct);
                    return PacsStoreStatus.Success;
                }
                catch (Exception ex) when (IsTransientNetworkException(ex))
                {
                    if (attempt >= MaxRetries)
                    {
                        OperatorNotifications.Add($"PERMANENT FAILURE: study {op.StudyId}");

                        await _auditLog.LogAsync(
                            AuditEventType.DataExport,
                            "system",
                            "System",
                            $"PACS C-STORE permanently failed for study {op.StudyId} after {MaxRetries} attempts",
                            AuditOutcome.Failure,
                            op.PatientId,
                            op.StudyId,
                            ct);

                        return PacsStoreStatus.PermanentlyFailed;
                    }

                    attempt++;
                }
            }

            return PacsStoreStatus.PermanentlyFailed;
        }

        // Determines whether an exception represents a transient network condition.
        private static bool IsTransientNetworkException(Exception ex) =>
            ex is IOException or OperationCanceledException or InvalidOperationException or TimeoutException;
    }

    // -----------------------------------------------------------------------
    // Shared fields
    // -----------------------------------------------------------------------

    private readonly IImageService _imageService;
    private readonly IAuditLogService _auditLog;
    private readonly InMemoryRetryQueue _retryQueue;
    private readonly PacsStoreOrchestrator _orchestrator;

    private const string TestStudyId = "STUDY-PACS-001";
    private const string TestPatientId = "PT000001";
    private static readonly byte[] TestPayload = System.Text.Encoding.UTF8.GetBytes("DICM-dummy-payload");

    public PacsFailureTests()
    {
        _imageService = Substitute.For<IImageService>();
        _auditLog = Substitute.For<IAuditLogService>();
        _retryQueue = new InMemoryRetryQueue();
        _orchestrator = new PacsStoreOrchestrator(_imageService, _auditLog, _retryQueue);

        // Default: audit log returns a deterministic entry ID.
        _auditLog
            .LogAsync(
                Arg.Any<AuditEventType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AuditOutcome>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("LOG-TEST-001"));
    }

    // -----------------------------------------------------------------------
    // INT-005-1
    // -----------------------------------------------------------------------

    /// <summary>
    /// When PACS is unreachable, a C-STORE attempt must fail gracefully and
    /// enqueue the operation in the retry queue rather than propagating an exception.
    /// SPEC-INTEGRATION-001 / IEC 62304 §5.7 – graceful fault handling.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task PacsUnreachable_OnCStore_ShouldEnqueueForRetry()
    {
        // Arrange – PACS service throws a network error.
        _imageService
            .GetCurrentImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("PACS unreachable: connection refused"));

        // Act
        var status = await _orchestrator.TrySendAsync(TestStudyId, TestPatientId, TestPayload);

        // Assert
        status.Should().Be(PacsStoreStatus.Failed,
            because: "a transient PACS error must not crash the GUI – it transitions to Failed state");

        _retryQueue.Count.Should().Be(1,
            because: "the failed operation must be preserved in the retry queue");

        // Verify the failure was audited.
        await _auditLog
            .Received(1)
            .LogAsync(
                AuditEventType.DataExport,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Is<string>(d => d.Contains("enqueued for retry")),
                AuditOutcome.Failure,
                TestPatientId,
                TestStudyId,
                Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // INT-005-2
    // -----------------------------------------------------------------------

    /// <summary>
    /// Data must not be lost when PACS is unreachable.
    /// The retry queue must retain the original payload, study ID, and patient ID.
    /// SPEC-INTEGRATION-001 – data-loss prevention.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task PacsUnreachable_OnCStore_ShouldNotLoseData()
    {
        // Arrange – simulate unreachable PACS.
        _imageService
            .GetCurrentImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Network timeout"));

        // Act
        await _orchestrator.TrySendAsync(TestStudyId, TestPatientId, TestPayload);

        // Assert – the queued item must preserve all original data fields.
        _retryQueue.TryDequeue(out var queued).Should().BeTrue();
        queued.Should().NotBeNull();
        queued!.StudyId.Should().Be(TestStudyId);
        queued.PatientId.Should().Be(TestPatientId);
        queued.Payload.Should().BeEquivalentTo(TestPayload,
            because: "no bytes of the DICOM payload must be discarded on transient failure");
        queued.AttemptCount.Should().Be(1,
            because: "this is the first attempt, so attempt count must equal 1");
    }

    // -----------------------------------------------------------------------
    // INT-005-3
    // -----------------------------------------------------------------------

    /// <summary>
    /// After PACS recovers, a queued retry must complete successfully.
    /// SPEC-INTEGRATION-001 – automatic recovery path.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task PacsRecovery_AfterRetry_ShouldComplete()
    {
        // Arrange – first call fails, PACS then recovers (subsequent calls succeed).
        _imageService
            .GetCurrentImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("PACS unreachable"));

        await _orchestrator.TrySendAsync(TestStudyId, TestPatientId, TestPayload);

        _retryQueue.Count.Should().Be(1, because: "setup should have enqueued one operation");

        // Simulate PACS recovery – subsequent calls succeed.
        _imageService
            .GetCurrentImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ImageData?>(null));   // null is a valid "no current image" response

        // Act
        var retryStatus = await _orchestrator.RetryQueuedAsync();

        // Assert
        retryStatus.Should().Be(PacsStoreStatus.Success,
            because: "retry must succeed once PACS becomes reachable again");

        _retryQueue.Count.Should().Be(0,
            because: "the successfully re-sent operation must be removed from the queue");
    }

    // -----------------------------------------------------------------------
    // INT-005-4
    // -----------------------------------------------------------------------

    /// <summary>
    /// When maximum retry count is exceeded the operation must transition to
    /// PERMANENTLY_FAILED state and the operator must be notified.
    /// SPEC-INTEGRATION-001 – max-retry exhaustion handling.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task MaxRetriesExceeded_ShouldTransitionToFailedState()
    {
        // Arrange – PACS is always unreachable.
        _imageService
            .GetCurrentImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("PACS permanently unreachable"));

        // First attempt – enqueues.
        await _orchestrator.TrySendAsync(TestStudyId, TestPatientId, TestPayload);

        // Act – retry until max retries are exhausted.
        // RetryQueuedAsync internally loops up to MaxRetries times; it will return
        // PermanentlyFailed once all internal attempts fail.
        var finalStatus = await _orchestrator.RetryQueuedAsync();

        // Assert
        finalStatus.Should().Be(PacsStoreStatus.PermanentlyFailed,
            because: "operations that exhaust all retries must be permanently failed");

        _orchestrator.OperatorNotifications.Should().ContainSingle(
            because: "exactly one permanent-failure notification must be generated");

        _orchestrator.OperatorNotifications[0].Should().Contain(TestStudyId,
            because: "operator notification must identify the affected study");

        // Verify the permanent failure was audited.
        await _auditLog
            .Received()
            .LogAsync(
                AuditEventType.DataExport,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Is<string>(d => d.Contains("permanently failed")),
                AuditOutcome.Failure,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }
}
