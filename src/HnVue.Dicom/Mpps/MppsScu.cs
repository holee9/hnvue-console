using Dicom;
using Dicom.Network;
using DicomClient = Dicom.Network.Client.DicomClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DicomServiceOptions = HnVue.Dicom.Configuration.DicomServiceOptions;
using DicomDestination = HnVue.Dicom.Configuration.DicomDestination;

namespace HnVue.Dicom.Mpps;

/// <summary>
/// Implements Modality Performed Procedure Step (MPPS) SCU operations using fo-dicom 4.x.
/// Sends N-CREATE and N-SET requests to the configured MPPS SCP.
/// IHE SWF RAD-6 (IN PROGRESS) and RAD-7 (COMPLETED / DISCONTINUED).
/// PHI must NOT appear in logs at INFO/WARN/ERROR level per NFR-SEC-01.
/// </summary>
// @MX:ANCHOR: [AUTO] Primary public API for MPPS lifecycle reporting — called by HnVue.Core acquisition workflow
// @MX:REASON: All procedure step state transitions pass through this class; breaking changes impact IHE SWF conformance
public sealed class MppsScu : IMppsScu
{
    /// <summary>MPPS SOP Class UID per DICOM PS3.4 F.7.2.1 and IHE SWF.</summary>
    private const string MppsSopClassUid = "1.2.840.10008.3.1.2.3.3";

    private readonly DicomServiceOptions _options;
    private readonly ILogger<MppsScu> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MppsScu"/>.
    /// </summary>
    public MppsScu(
        IOptions<DicomServiceOptions> options,
        ILogger<MppsScu> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> CreateProcedureStepAsync(
        MppsData data, CancellationToken cancellationToken = default)
    {
        var scp = RequireMppsScp();

        // Generate a new SOP Instance UID for the MPPS object
        var sopInstanceUid = DicomUID.Generate().UID;

        _logger.LogInformation(
            "Sending MPPS N-CREATE (IN PROGRESS) to SCP {AeTitle}@{Host}:{Port} for SOP instance {SopInstanceUid}",
            scp.AeTitle, scp.Host, scp.Port, sopInstanceUid);

        ushort? failureStatus = null;

        var request = new DicomNCreateRequest(
            new DicomUID(MppsSopClassUid, "MPPS", DicomUidType.SOPClass),
            new DicomUID(sopInstanceUid, "MPPS Instance", DicomUidType.SOPInstance));

        request.Dataset = BuildNCreateDataset(data, sopInstanceUid);

        request.OnResponseReceived += (req, response) =>
        {
            if (response.Status != DicomStatus.Success)
            {
                failureStatus = response.Status.Code;
                _logger.LogError(
                    "MPPS N-CREATE failed with status 0x{StatusCode:X4} from SCP {AeTitle}",
                    response.Status.Code, scp.AeTitle);
            }
            else
            {
                _logger.LogInformation(
                    "MPPS N-CREATE succeeded on SCP {AeTitle} for SOP instance {SopInstanceUid}",
                    scp.AeTitle, sopInstanceUid);
            }
        };

        var client = CreateClient(scp);
        await client.AddRequestAsync(request).ConfigureAwait(false);
        await client.SendAsync(cancellationToken).ConfigureAwait(false);

        if (failureStatus.HasValue)
        {
            throw new DicomMppsException(
                failureStatus.Value,
                $"MPPS SCP {scp.AeTitle} rejected N-CREATE with status 0x{failureStatus.Value:X4}.");
        }

        return sopInstanceUid;
    }

    /// <inheritdoc/>
    public async Task SetProcedureStepInProgressAsync(
        string sopInstanceUid, MppsData data, CancellationToken cancellationToken = default)
    {
        await SendNSetAsync(sopInstanceUid, data, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CompleteProcedureStepAsync(
        string sopInstanceUid, MppsData data, CancellationToken cancellationToken = default)
    {
        await SendNSetAsync(sopInstanceUid, data, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DiscontinueProcedureStepAsync(
        string sopInstanceUid, string reason, CancellationToken cancellationToken = default)
    {
        var discontinueData = new MppsData(
            PatientId: string.Empty,
            StudyInstanceUid: string.Empty,
            SeriesInstanceUid: string.Empty,
            PerformedProcedureStepId: string.Empty,
            PerformedProcedureStepDescription: reason,
            StartDateTime: DateTime.UtcNow,
            EndDateTime: DateTime.UtcNow,
            Status: MppsStatus.Discontinued,
            ExposureData: Array.Empty<ExposureData>());

        await SendNSetAsync(sopInstanceUid, discontinueData, reason, cancellationToken).ConfigureAwait(false);
    }

    // @MX:NOTE: [AUTO] N-SET dataset only includes attributes being updated; not a full MPPS dataset
    private async Task SendNSetAsync(
        string sopInstanceUid,
        MppsData data,
        string? discontinuationReason = null,
        CancellationToken cancellationToken = default)
    {
        var scp = RequireMppsScp();

        _logger.LogInformation(
            "Sending MPPS N-SET to SCP {AeTitle}@{Host}:{Port} for SOP instance {SopInstanceUid} with status {Status}",
            scp.AeTitle, scp.Host, scp.Port, sopInstanceUid, data.Status);

        ushort? failureStatus = null;

        var request = new DicomNSetRequest(
            new DicomUID(MppsSopClassUid, "MPPS", DicomUidType.SOPClass),
            new DicomUID(sopInstanceUid, "MPPS Instance", DicomUidType.SOPInstance));

        request.Dataset = BuildNSetDataset(data, discontinuationReason);

        request.OnResponseReceived += (req, response) =>
        {
            if (response.Status != DicomStatus.Success)
            {
                failureStatus = response.Status.Code;
                _logger.LogError(
                    "MPPS N-SET failed with status 0x{StatusCode:X4} from SCP {AeTitle}",
                    response.Status.Code, scp.AeTitle);
            }
            else
            {
                _logger.LogInformation(
                    "MPPS N-SET succeeded on SCP {AeTitle} for SOP instance {SopInstanceUid}",
                    scp.AeTitle, sopInstanceUid);
            }
        };

        var client = CreateClient(scp);
        await client.AddRequestAsync(request).ConfigureAwait(false);
        await client.SendAsync(cancellationToken).ConfigureAwait(false);

        if (failureStatus.HasValue)
        {
            throw new DicomMppsException(
                failureStatus.Value,
                $"MPPS SCP {scp.AeTitle} rejected N-SET with status 0x{failureStatus.Value:X4}.");
        }
    }

    // @MX:NOTE: [AUTO] N-CREATE dataset must include all mandatory MPPS attributes per DICOM PS3.3 C.7.6.3
    private DicomDataset BuildNCreateDataset(MppsData data, string sopInstanceUid)
    {
        var dataset = new DicomDataset();

        // SOP Instance UID
        dataset.AddOrUpdate(DicomTag.SOPClassUID, MppsSopClassUid);
        dataset.AddOrUpdate(DicomTag.SOPInstanceUID, sopInstanceUid);

        // Performed Procedure Step attributes
        dataset.AddOrUpdate(DicomTag.PerformedProcedureStepStatus, "IN PROGRESS");
        dataset.AddOrUpdate(DicomTag.PerformedProcedureStepID, data.PerformedProcedureStepId);
        dataset.AddOrUpdate(DicomTag.PerformedProcedureStepDescription, data.PerformedProcedureStepDescription);

        // Start date / time — split into separate DA and TM tags
        dataset.AddOrUpdate(DicomTag.PerformedProcedureStepStartDate,
            data.StartDateTime.ToString("yyyyMMdd"));
        dataset.AddOrUpdate(DicomTag.PerformedProcedureStepStartTime,
            data.StartDateTime.ToString("HHmmss"));

        // Performed Station AE Title
        dataset.AddOrUpdate(DicomTag.PerformedStationAETitle, _options.CallingAeTitle);

        // Study reference
        dataset.AddOrUpdate(DicomTag.StudyInstanceUID, data.StudyInstanceUid);

        // Empty performed series sequence (filled at completion via N-SET)
        dataset.AddOrUpdate(new DicomSequence(DicomTag.PerformedSeriesSequence));

        return dataset;
    }

    // @MX:NOTE: [AUTO] N-SET dataset for COMPLETED must include series/image references per FR-DICOM-04
    private DicomDataset BuildNSetDataset(MppsData data, string? discontinuationReason)
    {
        var dataset = new DicomDataset();

        var statusString = data.Status switch
        {
            MppsStatus.InProgress => "IN PROGRESS",
            MppsStatus.Completed => "COMPLETED",
            MppsStatus.Discontinued => "DISCONTINUED",
            _ => "IN PROGRESS"
        };

        dataset.AddOrUpdate(DicomTag.PerformedProcedureStepStatus, statusString);

        if (data.EndDateTime.HasValue)
        {
            dataset.AddOrUpdate(DicomTag.PerformedProcedureStepEndDate,
                data.EndDateTime.Value.ToString("yyyyMMdd"));
            dataset.AddOrUpdate(DicomTag.PerformedProcedureStepEndTime,
                data.EndDateTime.Value.ToString("HHmmss"));
        }

        if (!string.IsNullOrEmpty(discontinuationReason))
        {
            dataset.AddOrUpdate(DicomTag.PerformedProcedureStepDescription, discontinuationReason);
        }

        // For COMPLETED, include series and image references per FR-DICOM-04
        if (data.Status == MppsStatus.Completed && data.ExposureData.Count > 0)
        {
            var seriesItems = data.ExposureData
                .GroupBy(e => e.SeriesInstanceUid)
                .Select(grp =>
                {
                    var seriesDataset = new DicomDataset();
                    seriesDataset.AddOrUpdate(DicomTag.SeriesInstanceUID, grp.Key);

                    var referencedSops = grp.Select(e =>
                    {
                        var sopDataset = new DicomDataset();
                        sopDataset.AddOrUpdate(DicomTag.ReferencedSOPClassUID, e.SopClassUid);
                        sopDataset.AddOrUpdate(DicomTag.ReferencedSOPInstanceUID, e.SopInstanceUid);
                        return sopDataset;
                    }).ToArray();

                    seriesDataset.AddOrUpdate(new DicomSequence(
                        DicomTag.ReferencedImageSequence, referencedSops));

                    return seriesDataset;
                })
                .ToArray();

            dataset.AddOrUpdate(new DicomSequence(DicomTag.PerformedSeriesSequence, seriesItems));
        }

        return dataset;
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

    private DicomDestination RequireMppsScp()
    {
        if (_options.MppsScp is null)
        {
            throw new InvalidOperationException(
                "MppsScp is not configured. Set DicomServiceOptions.MppsScp before sending MPPS requests.");
        }

        return _options.MppsScp;
    }
}
