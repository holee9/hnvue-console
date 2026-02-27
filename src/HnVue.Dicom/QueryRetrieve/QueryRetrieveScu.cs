using System.Runtime.CompilerServices;
using Dicom;
using Dicom.Network;
using DicomClient = Dicom.Network.Client.DicomClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HnVue.Dicom.Worklist;
using DicomServiceOptions = HnVue.Dicom.Configuration.DicomServiceOptions;

namespace HnVue.Dicom.QueryRetrieve;

/// <summary>
/// Implements Study Root Query/Retrieve SCU operations using fo-dicom 4.x.
/// Sends C-FIND requests and C-MOVE requests to the configured Query/Retrieve SCP
/// per FR-DICOM-06 (C-FIND) and FR-DICOM-07 (C-MOVE), IHE PIR profile.
/// PHI must NOT appear in logs at INFO/WARN/ERROR level per NFR-SEC-01.
/// </summary>
// @MX:ANCHOR: [AUTO] Study Root QR SCU — integration point for image retrieval workflow (FR-DICOM-06/07)
// @MX:REASON: fan_in >= 3 (DicomServiceFacade, IHE PIR workflow, test layer). C-FIND and C-MOVE share same SCP config.
public sealed class QueryRetrieveScu : IQueryRetrieveScu
{
    /// <summary>Study Root Query/Retrieve – FIND SOP Class UID (DICOM PS3.4 C.6.2.1.2).</summary>
    private const string StudyRootFindSopClassUid = "1.2.840.10008.5.1.4.1.2.2.1";

    /// <summary>Study Root Query/Retrieve – MOVE SOP Class UID (DICOM PS3.4 C.6.2.1.3).</summary>
    private const string StudyRootMoveSopClassUid = "1.2.840.10008.5.1.4.1.2.2.2";

    private readonly DicomServiceOptions _options;
    private readonly ILogger<QueryRetrieveScu> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="QueryRetrieveScu"/>.
    /// </summary>
    public QueryRetrieveScu(
        IOptions<DicomServiceOptions> options,
        ILogger<QueryRetrieveScu> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StudyResult> FindStudiesAsync(
        StudyQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_options.QueryRetrieveScp is null)
        {
            throw new InvalidOperationException(
                "QueryRetrieveScp is not configured. Set DicomServiceOptions.QueryRetrieveScp before querying.");
        }

        var scp = _options.QueryRetrieveScp;

        _logger.LogInformation(
            "Starting Study Root C-FIND query to QR SCP {AeTitle}@{Host}:{Port}",
            scp.AeTitle, scp.Host, scp.Port);

        var results = System.Threading.Channels.Channel.CreateUnbounded<StudyResult>();
        Exception? queryException = null;

        var cfindRequest = BuildCFindRequest(query);

        cfindRequest.OnResponseReceived += (request, response) =>
        {
            // C-FIND PENDING — accumulate dataset
            if (response.Status == DicomStatus.Pending && response.Dataset is not null)
            {
                var item = ParseStudyResult(response.Dataset);
                if (item is not null)
                {
                    results.Writer.TryWrite(item);
                }
                return;
            }

            // C-FIND SUCCESS — complete
            if (response.Status == DicomStatus.Success)
            {
                _logger.LogInformation(
                    "Study Root C-FIND completed successfully to {AeTitle}",
                    scp.AeTitle);
                results.Writer.Complete();
                return;
            }

            // C-FIND failure — propagate as exception
            var statusCode = response.Status.Code;
            _logger.LogError(
                "Study Root C-FIND failed with status 0x{StatusCode:X4} from SCP {AeTitle}",
                statusCode, scp.AeTitle);

            queryException = new DicomQueryRetrieveException(
                statusCode,
                $"QR SCP {scp.AeTitle} returned failure status 0x{statusCode:X4} for C-FIND.");
            results.Writer.Complete(queryException);
        };

        var client = new DicomClient(
            scp.Host,
            scp.Port,
            useTls: scp.TlsEnabled ?? _options.Tls.Enabled,
            callingAe: _options.CallingAeTitle,
            calledAe: scp.AeTitle);

        client.NegotiateAsyncOps();
        await client.AddRequestAsync(cfindRequest).ConfigureAwait(false);

        var sendTask = client.SendAsync(cancellationToken);

        await foreach (var item in results.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }

        try
        {
            await sendTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Network error during Study Root C-FIND to QR SCP {AeTitle}@{Host}:{Port}",
                scp.AeTitle, scp.Host, scp.Port);
            throw;
        }

        if (queryException is not null)
        {
            throw queryException;
        }
    }

    /// <inheritdoc/>
    public async Task MoveStudyAsync(
        string studyInstanceUid,
        string destinationAeTitle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(studyInstanceUid);
        ArgumentNullException.ThrowIfNull(destinationAeTitle);

        if (_options.QueryRetrieveScp is null)
        {
            throw new InvalidOperationException(
                "QueryRetrieveScp is not configured. Set DicomServiceOptions.QueryRetrieveScp before retrieving.");
        }

        var scp = _options.QueryRetrieveScp;

        _logger.LogInformation(
            "Starting Study Root C-MOVE for study to destination {DestAe} via QR SCP {AeTitle}@{Host}:{Port}",
            destinationAeTitle, scp.AeTitle, scp.Host, scp.Port);

        DicomQueryRetrieveException? moveException = null;

        var cmoveRequest = BuildCMoveRequest(studyInstanceUid, destinationAeTitle);

        cmoveRequest.OnResponseReceived += (request, response) =>
        {
            // C-MOVE PENDING — sub-operations in progress, no action needed
            if (response.Status == DicomStatus.Pending)
            {
                return;
            }

            // C-MOVE SUCCESS — completed
            if (response.Status == DicomStatus.Success)
            {
                _logger.LogInformation(
                    "Study Root C-MOVE completed successfully via {AeTitle}",
                    scp.AeTitle);
                return;
            }

            // C-MOVE failure
            var statusCode = response.Status.Code;
            _logger.LogError(
                "Study Root C-MOVE failed with status 0x{StatusCode:X4} from SCP {AeTitle}",
                statusCode, scp.AeTitle);

            moveException = new DicomQueryRetrieveException(
                statusCode,
                $"QR SCP {scp.AeTitle} returned failure status 0x{statusCode:X4} for C-MOVE.");
        };

        var client = new DicomClient(
            scp.Host,
            scp.Port,
            useTls: scp.TlsEnabled ?? _options.Tls.Enabled,
            callingAe: _options.CallingAeTitle,
            calledAe: scp.AeTitle);

        client.NegotiateAsyncOps();
        await client.AddRequestAsync(cmoveRequest).ConfigureAwait(false);

        try
        {
            await client.SendAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Network error during Study Root C-MOVE via QR SCP {AeTitle}@{Host}:{Port}",
                scp.AeTitle, scp.Host, scp.Port);
            throw;
        }

        if (moveException is not null)
        {
            throw moveException;
        }
    }

    // @MX:NOTE: [AUTO] Study Root C-FIND uses STUDY query retrieve level per DICOM PS3.4 C.6.2.1.2
    private static DicomCFindRequest BuildCFindRequest(StudyQuery query)
    {
        var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Study);

        // Patient-level return keys
        request.Dataset.AddOrUpdate(DicomTag.PatientID, query.PatientId ?? string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.PatientName, string.Empty);

        // Study-level query/return keys
        request.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, query.StudyInstanceUid ?? string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.AccessionNumber, query.AccessionNumber ?? string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.ModalitiesInStudy, query.Modality ?? string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.StudyDescription, string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.NumberOfStudyRelatedSeries, string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.NumberOfStudyRelatedInstances, string.Empty);

        // Date range filter
        var dateValue = query.StudyDate is not null
            ? query.StudyDate.ToDicomRangeString()
            : string.Empty;
        request.Dataset.AddOrUpdate(DicomTag.StudyDate, dateValue);

        return request;
    }

    // @MX:NOTE: [AUTO] C-MOVE uses STUDY level with StudyInstanceUID and destination AE Title
    private static DicomCMoveRequest BuildCMoveRequest(string studyInstanceUid, string destinationAeTitle)
    {
        return new DicomCMoveRequest(destinationAeTitle, studyInstanceUid);
    }

    // @MX:NOTE: [AUTO] PHI attributes (PatientName, PatientID) are never logged per NFR-SEC-01
    private StudyResult? ParseStudyResult(DicomDataset dataset)
    {
        try
        {
            var studyInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, (string?)null);
            if (string.IsNullOrEmpty(studyInstanceUid))
            {
                _logger.LogWarning("QR C-FIND response item missing StudyInstanceUID — item skipped");
                return null;
            }

            var patientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, (string?)null);
            var patientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, (string?)null);
            var accessionNumber = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, (string?)null);
            var modality = dataset.GetSingleValueOrDefault(DicomTag.ModalitiesInStudy, (string?)null);
            var studyDescription = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, (string?)null);

            DateOnly? studyDate = null;
            var dateStr = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty);
            if (!string.IsNullOrEmpty(dateStr) && dateStr.Length == 8
                && DateOnly.TryParseExact(dateStr, "yyyyMMdd", out var parsed))
            {
                studyDate = parsed;
            }

            int? numSeries = null;
            var seriesStr = dataset.GetSingleValueOrDefault(DicomTag.NumberOfStudyRelatedSeries, string.Empty);
            if (int.TryParse(seriesStr, out var seriesCount))
            {
                numSeries = seriesCount;
            }

            int? numInstances = null;
            var instancesStr = dataset.GetSingleValueOrDefault(DicomTag.NumberOfStudyRelatedInstances, string.Empty);
            if (int.TryParse(instancesStr, out var instanceCount))
            {
                numInstances = instanceCount;
            }

            return new StudyResult(
                StudyInstanceUid: studyInstanceUid,
                PatientId: patientId,
                PatientName: patientName,
                AccessionNumber: accessionNumber,
                Modality: modality,
                StudyDate: studyDate,
                StudyDescription: studyDescription,
                NumberOfStudyRelatedSeries: numSeries,
                NumberOfStudyRelatedInstances: numInstances);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse a QR C-FIND response item — item skipped");
            return null;
        }
    }
}
