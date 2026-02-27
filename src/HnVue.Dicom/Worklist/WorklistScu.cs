using System.Runtime.CompilerServices;
using Dicom;
using Dicom.Network;
using DicomClient = Dicom.Network.Client.DicomClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DicomServiceOptions = HnVue.Dicom.Configuration.DicomServiceOptions;
using DicomDestination = HnVue.Dicom.Configuration.DicomDestination;

namespace HnVue.Dicom.Worklist;

/// <summary>
/// Implements Modality Worklist SCU operations using fo-dicom 4.x.
/// Sends C-FIND requests to the configured Worklist SCP per IHE SWF RAD-5.
/// PHI must NOT appear in logs at INFO/WARN/ERROR level per NFR-SEC-01.
/// </summary>
// @MX:ANCHOR: [AUTO] Primary public API for worklist queries — multiple callers expected from HnVue.Core
// @MX:REASON: QueryAsync is the single integration point between HnVue.Core and the worklist SCP
public sealed class WorklistScu : IWorklistScu
{
    /// <summary>Modality Worklist SOP Class UID (DICOM PS3.4 K.6.1.1).</summary>
    private const string WorklistSopClassUid = "1.2.840.10008.5.1.4.31";

    private readonly DicomServiceOptions _options;
    private readonly ILogger<WorklistScu> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="WorklistScu"/>.
    /// </summary>
    public WorklistScu(
        IOptions<DicomServiceOptions> options,
        ILogger<WorklistScu> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorklistItem> QueryAsync(
        WorklistQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_options.WorklistScp is null)
        {
            throw new InvalidOperationException(
                "WorklistScp is not configured. Set DicomServiceOptions.WorklistScp before querying the worklist.");
        }

        var scp = _options.WorklistScp;

        _logger.LogInformation(
            "Starting Modality Worklist C-FIND query to SCP {AeTitle}@{Host}:{Port}",
            scp.AeTitle, scp.Host, scp.Port);

        var results = System.Threading.Channels.Channel.CreateUnbounded<WorklistItem>();
        Exception? queryException = null;

        var cfindRequest = BuildCFindRequest(query);

        cfindRequest.OnResponseReceived += (request, response) =>
        {
            // C-FIND PENDING — accumulate dataset
            if (response.Status == DicomStatus.Pending && response.Dataset is not null)
            {
                var item = ParseWorklistItem(response.Dataset);
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
                    "Modality Worklist C-FIND completed successfully to {AeTitle}",
                    scp.AeTitle);
                results.Writer.Complete();
                return;
            }

            // C-FIND failure — propagate as exception per FR-DICOM-03 UNWANTED
            var statusCode = response.Status.Code;
            _logger.LogError(
                "Modality Worklist C-FIND failed with status 0x{StatusCode:X4} from SCP {AeTitle}",
                statusCode, scp.AeTitle);

            queryException = new DicomWorklistException(
                statusCode,
                $"Worklist SCP {scp.AeTitle} returned failure status 0x{statusCode:X4}.");
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

        // Send the request; failures surface through the channel writer
        var sendTask = client.SendAsync(cancellationToken);

        await foreach (var item in results.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }

        // Ensure the send has completed and surface any network exception
        try
        {
            await sendTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Network error during Modality Worklist C-FIND to SCP {AeTitle}@{Host}:{Port}",
                scp.AeTitle, scp.Host, scp.Port);
            throw;
        }

        if (queryException is not null)
        {
            throw queryException;
        }
    }

    // @MX:NOTE: [AUTO] Query dataset uses wildcard matching per DICOM PS3.4 C.2.2.1
    // Only non-null query fields emit actual values; null fields emit empty/wildcard elements.
    private DicomCFindRequest BuildCFindRequest(WorklistQuery query)
    {
        var request = new DicomCFindRequest(DicomQueryRetrieveLevel.NotApplicable);

        // Worklist query model must be Modality Worklist (not Study Root)
        request.Dataset.AddOrUpdate(DicomTag.QueryRetrieveLevel, string.Empty);

        // Patient-level attributes (return keys)
        request.Dataset.AddOrUpdate(DicomTag.PatientID, query.PatientId ?? string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.PatientName, string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.PatientBirthDate, string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.PatientSex, string.Empty);

        // Study-level return keys
        request.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.AccessionNumber, string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.RequestedProcedureID, string.Empty);
        request.Dataset.AddOrUpdate(DicomTag.RequestedProcedureDescription, string.Empty);

        // Scheduled Procedure Step Sequence (0040,0100) — mandatory query container
        var scheduledStep = new DicomDataset();

        // Station AE Title filter: use provided AeTitle or fall back to calling AE
        scheduledStep.AddOrUpdate(DicomTag.ScheduledStationAETitle,
            query.AeTitle ?? _options.CallingAeTitle);

        // Modality filter
        scheduledStep.AddOrUpdate(DicomTag.Modality, query.Modality ?? string.Empty);

        // Date range
        var dateRange = query.ScheduledDate ?? DateRange.Today();
        scheduledStep.AddOrUpdate(DicomTag.ScheduledProcedureStepStartDate, dateRange.ToDicomRangeString());
        scheduledStep.AddOrUpdate(DicomTag.ScheduledProcedureStepStartTime, string.Empty);

        // Return keys within the sequence
        scheduledStep.AddOrUpdate(DicomTag.ScheduledProcedureStepID, string.Empty);
        scheduledStep.AddOrUpdate(DicomTag.ScheduledProcedureStepDescription, string.Empty);
        scheduledStep.AddOrUpdate(DicomTag.ScheduledPerformingPhysicianName, string.Empty);

        request.Dataset.AddOrUpdate(DicomTag.ScheduledProcedureStepSequence,
            new DicomSequence(DicomTag.ScheduledProcedureStepSequence, scheduledStep));

        return request;
    }

    // @MX:NOTE: [AUTO] PHI attributes (PatientName, PatientID, BirthDate) are never logged per NFR-SEC-01
    private WorklistItem? ParseWorklistItem(DicomDataset dataset)
    {
        try
        {
            var patientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
            var patientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty);
            var accessionNumber = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
            var requestedProcedureId = dataset.GetSingleValueOrDefault(DicomTag.RequestedProcedureID, string.Empty);
            var studyInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, (string?)null);

            DateOnly? birthDate = null;
            var birthDateStr = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, string.Empty);
            if (!string.IsNullOrEmpty(birthDateStr) && birthDateStr.Length == 8
                && DateOnly.TryParseExact(birthDateStr, "yyyyMMdd", out var parsed))
            {
                birthDate = parsed;
            }

            var patientSex = dataset.GetSingleValueOrDefault(DicomTag.PatientSex, (string?)null);

            ScheduledProcedureStep? step = null;
            if (dataset.Contains(DicomTag.ScheduledProcedureStepSequence))
            {
                var seq = dataset.GetSequence(DicomTag.ScheduledProcedureStepSequence);
                var stepDataset = seq.Items.FirstOrDefault();
                if (stepDataset is not null)
                {
                    step = ParseScheduledStep(stepDataset);
                }
            }

            if (step is null)
            {
                _logger.LogWarning(
                    "Worklist response item missing ScheduledProcedureStepSequence — item skipped");
                return null;
            }

            return new WorklistItem(
                PatientId: patientId,
                PatientName: patientName,
                BirthDate: birthDate,
                PatientSex: patientSex,
                StudyInstanceUid: studyInstanceUid,
                AccessionNumber: accessionNumber,
                RequestedProcedureId: requestedProcedureId,
                ScheduledProcedureStep: step);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse a worklist response item — item skipped");
            return null;
        }
    }

    private static ScheduledProcedureStep ParseScheduledStep(DicomDataset stepDataset)
    {
        var stepId = stepDataset.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepID, string.Empty);
        var description = stepDataset.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepDescription, string.Empty);
        var modality = stepDataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty);
        var performingPhysician = stepDataset.GetSingleValueOrDefault(DicomTag.ScheduledPerformingPhysicianName, (string?)null);

        DateTime? scheduledDateTime = null;
        var dateStr = stepDataset.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepStartDate, string.Empty);
        var timeStr = stepDataset.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepStartTime, string.Empty);

        if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 8
            && DateOnly.TryParseExact(dateStr[..8], "yyyyMMdd", out var stepDate))
        {
            if (!string.IsNullOrEmpty(timeStr) && timeStr.Length >= 6
                && TimeOnly.TryParseExact(timeStr[..6], "HHmmss", out var stepTime))
            {
                scheduledDateTime = stepDate.ToDateTime(stepTime);
            }
            else
            {
                scheduledDateTime = stepDate.ToDateTime(TimeOnly.MinValue);
            }
        }

        return new ScheduledProcedureStep(
            StepId: stepId,
            Description: description,
            DateTime: scheduledDateTime,
            PerformingPhysician: performingPhysician,
            Modality: modality);
    }
}
