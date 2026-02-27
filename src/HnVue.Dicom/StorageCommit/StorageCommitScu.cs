using Dicom;
using Dicom.Network;
using DicomClient = Dicom.Network.Client.DicomClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DicomServiceOptions = HnVue.Dicom.Configuration.DicomServiceOptions;
using DicomDestination = HnVue.Dicom.Configuration.DicomDestination;

namespace HnVue.Dicom.StorageCommit;

/// <summary>
/// Implements Storage Commitment SCU operations using fo-dicom 4.x.
/// Sends N-ACTION to request commitment and exposes an event for N-EVENT-REPORT responses.
/// SOP Class: 1.2.840.10008.1.3.10 (Storage Commitment Push Model).
/// IHE SWF RAD-10.
/// PHI must NOT appear in logs at INFO/WARN/ERROR level per NFR-SEC-01.
/// </summary>
// @MX:ANCHOR: [AUTO] Primary public API for storage commitment lifecycle — called after C-STORE completes
// @MX:REASON: CommitmentReceived event is the sole signal for archival confirmation; callers depend on this contract
// @MX:WARN: [AUTO] N-EVENT-REPORT is received on a separate, SCP-initiated association — event handler may run on background thread
// @MX:REASON: DICOM Storage Commitment Push Model requires the SCP to re-associate back to the SCU; thread-safety is caller responsibility
public sealed class StorageCommitScu : IStorageCommitScu, IAsyncDisposable
{
    /// <summary>Storage Commitment Push Model SOP Class UID.</summary>
    private const string StorageCommitSopClassUid = "1.2.840.10008.1.3.10";

    /// <summary>N-ACTION action type ID for Storage Commitment Request (DICOM PS3.4 J.3.2).</summary>
    private const ushort StorageCommitActionTypeId = 1;

    private readonly DicomServiceOptions _options;
    private readonly ILogger<StorageCommitScu> _logger;

    // @MX:NOTE: [AUTO] ConcurrentDictionary used for thread-safe access from N-EVENT-REPORT callback thread
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _pendingTransactions = new();

    private readonly CancellationTokenSource _disposeCts = new();

    /// <inheritdoc/>
    public event EventHandler<CommitmentReceivedEventArgs>? CommitmentReceived;

    /// <summary>
    /// Initializes a new instance of <see cref="StorageCommitScu"/>.
    /// </summary>
    public StorageCommitScu(
        IOptions<DicomServiceOptions> options,
        ILogger<StorageCommitScu> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> RequestCommitAsync(
        IEnumerable<(string SopClassUid, string SopInstanceUid)> sopInstances,
        CancellationToken cancellationToken = default)
    {
        if (_options.StorageDestinations is null || _options.StorageDestinations.Count == 0)
        {
            throw new InvalidOperationException(
                "No StorageDestinations configured. A Storage Commitment SCP destination is required.");
        }

        // @MX:NOTE: [AUTO] First configured storage destination is used as the commit SCP target
        var scp = _options.StorageDestinations[0];

        var transactionUid = DicomUID.Generate().UID;
        var instances = sopInstances.ToList();

        _logger.LogInformation(
            "Sending Storage Commitment N-ACTION to SCP {AeTitle}@{Host}:{Port} for {Count} SOP instance(s), TransactionUID={TransactionUid}",
            scp.AeTitle, scp.Host, scp.Port, instances.Count, transactionUid);

        ushort? failureStatus = null;

        var request = BuildNActionRequest(transactionUid, instances);

        request.OnResponseReceived += (req, response) =>
        {
            if (response.Status != DicomStatus.Success)
            {
                failureStatus = response.Status.Code;
                _logger.LogError(
                    "Storage Commitment N-ACTION failed with status 0x{StatusCode:X4} from SCP {AeTitle}",
                    response.Status.Code, scp.AeTitle);
            }
            else
            {
                var timeoutMs = _options.Timeouts.StorageCommitmentWaitMs;
                _pendingTransactions[transactionUid] = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);

                _logger.LogInformation(
                    "Storage Commitment N-ACTION accepted by SCP {AeTitle}; awaiting N-EVENT-REPORT for TransactionUID={TransactionUid}",
                    scp.AeTitle, transactionUid);
            }
        };

        var client = CreateClient(scp);
        await client.AddRequestAsync(request).ConfigureAwait(false);

        try
        {
            await client.SendAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Network error during Storage Commitment N-ACTION to SCP {AeTitle}@{Host}:{Port}",
                scp.AeTitle, scp.Host, scp.Port);
            throw;
        }

        if (failureStatus.HasValue)
        {
            throw new DicomStorageCommitException(
                failureStatus.Value,
                $"Storage Commitment SCP {scp.AeTitle} rejected N-ACTION with status 0x{failureStatus.Value:X4}.");
        }

        return transactionUid;
    }

    /// <summary>
    /// Processes an N-EVENT-REPORT request received from the SCP.
    /// May be called from a background association thread per DICOM PS3.4 J.3.3.
    /// </summary>
    internal void HandleNEventReport(DicomNEventReportRequest request)
    {
        if (request.Dataset is null)
        {
            _logger.LogWarning("Received Storage Commitment N-EVENT-REPORT with no dataset — ignored");
            return;
        }

        var transactionUid = request.Dataset.GetSingleValueOrDefault(DicomTag.TransactionUID, string.Empty);

        if (string.IsNullOrEmpty(transactionUid))
        {
            _logger.LogWarning(
                "Received Storage Commitment N-EVENT-REPORT missing TransactionUID — ignored");
            return;
        }

        _pendingTransactions.TryRemove(transactionUid, out _);

        var committedItems = new List<(string SopClassUid, string SopInstanceUid)>();
        var failedItems = new List<(string SopClassUid, string SopInstanceUid, ushort FailureReason)>();

        // Parse referenced SOP instances (committed)
        if (request.Dataset.Contains(DicomTag.ReferencedSOPSequence))
        {
            var seq = request.Dataset.GetSequence(DicomTag.ReferencedSOPSequence);
            foreach (var item in seq.Items)
            {
                var sopClass = item.GetSingleValueOrDefault(DicomTag.ReferencedSOPClassUID, string.Empty);
                var sopInstance = item.GetSingleValueOrDefault(DicomTag.ReferencedSOPInstanceUID, string.Empty);
                committedItems.Add((sopClass, sopInstance));
            }
        }

        // Parse failed SOP instances
        if (request.Dataset.Contains(DicomTag.FailedSOPSequence))
        {
            var seq = request.Dataset.GetSequence(DicomTag.FailedSOPSequence);
            foreach (var item in seq.Items)
            {
                var sopClass = item.GetSingleValueOrDefault(DicomTag.ReferencedSOPClassUID, string.Empty);
                var sopInstance = item.GetSingleValueOrDefault(DicomTag.ReferencedSOPInstanceUID, string.Empty);
                var failureReason = item.GetSingleValueOrDefault(DicomTag.FailureReason, (ushort)0);
                failedItems.Add((sopClass, sopInstance, failureReason));
            }
        }

        _logger.LogInformation(
            "Storage Commitment N-EVENT-REPORT received for TransactionUID={TransactionUid}: {CommittedCount} committed, {FailedCount} failed",
            transactionUid, committedItems.Count, failedItems.Count);

        if (failedItems.Count > 0)
        {
            // Log failure count only — SOP Instance UIDs must not appear in logs per NFR-SEC-01
            _logger.LogWarning(
                "Storage Commitment partial failure: {FailedCount} SOP instance(s) were NOT committed for TransactionUID={TransactionUid}",
                failedItems.Count, transactionUid);
        }

        OnCommitmentReceived(new CommitmentReceivedEventArgs(
            transactionUid,
            committedItems.AsReadOnly(),
            failedItems.AsReadOnly()));
    }

    private void OnCommitmentReceived(CommitmentReceivedEventArgs args)
    {
        try
        {
            CommitmentReceived?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception in CommitmentReceived event handler for TransactionUID={TransactionUid}",
                args.TransactionUid);
        }
    }

    private DicomNActionRequest BuildNActionRequest(
        string transactionUid,
        IList<(string SopClassUid, string SopInstanceUid)> instances)
    {
        var request = new DicomNActionRequest(
            new DicomUID(StorageCommitSopClassUid, "Storage Commitment Push Model", DicomUidType.SOPClass),
            new DicomUID("1.2.840.10008.1.3.10.1", "Storage Commitment Push Model Instance", DicomUidType.SOPInstance),
            StorageCommitActionTypeId);

        var dataset = new DicomDataset();
        dataset.AddOrUpdate(DicomTag.TransactionUID, transactionUid);

        var referencedSops = instances.Select(inst =>
        {
            var sopDataset = new DicomDataset();
            sopDataset.AddOrUpdate(DicomTag.ReferencedSOPClassUID, inst.SopClassUid);
            sopDataset.AddOrUpdate(DicomTag.ReferencedSOPInstanceUID, inst.SopInstanceUid);
            return sopDataset;
        }).ToArray();

        dataset.AddOrUpdate(new DicomSequence(DicomTag.ReferencedSOPSequence, referencedSops));

        request.Dataset = dataset;
        return request;
    }

    private DicomClient CreateClient(DicomDestination scp)
    {
        return new DicomClient(
            scp.Host,
            scp.Port,
            useTls: scp.TlsEnabled ?? _options.Tls.Enabled,
            callingAe: _options.CallingAeTitle,
            calledAe: scp.AeTitle);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
