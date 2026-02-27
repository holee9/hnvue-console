using Dicom;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Iod;
using HnVue.Dicom.Mpps;
using HnVue.Dicom.Rdsr;
using HnVue.Dicom.Storage;
using HnVue.Dicom.StorageCommit;
using HnVue.Dicom.Uid;
using HnVue.Dicom.Worklist;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HnVue.Dicom.Facade;

/// <summary>
/// Single entry point aggregating all DICOM services for HnVue.Core callers.
/// Orchestrates IOD construction, SCU operations, and retry queue integration.
/// </summary>
// @MX:ANCHOR: [AUTO] Primary DICOM API surface; all HnVue.Core DICOM interactions pass through here.
// @MX:REASON: fan_in >= 3 (Core layer, dose module, test layer). Changes here affect full SWF + REM workflow.
public sealed class DicomServiceFacade : IDicomServiceFacade
{
    private readonly IStorageScu _storageScu;
    private readonly IWorklistScu _worklistScu;
    private readonly IMppsScu _mppsScu;
    private readonly IStorageCommitScu _storageCommitScu;
    private readonly IRdsrBuilder _rdsrBuilder;
    private readonly IUidGenerator _uidGenerator;
    private readonly DicomServiceOptions _options;
    private readonly ILogger<DicomServiceFacade> _logger;

    private readonly DxImageBuilder _dxBuilder;
    private readonly CrImageBuilder _crBuilder;

    /// <summary>
    /// Initializes a new instance of <see cref="DicomServiceFacade"/>.
    /// </summary>
    public DicomServiceFacade(
        IStorageScu storageScu,
        IWorklistScu worklistScu,
        IMppsScu mppsScu,
        IStorageCommitScu storageCommitScu,
        IRdsrBuilder rdsrBuilder,
        IUidGenerator uidGenerator,
        IOptions<DicomServiceOptions> options,
        ILogger<DicomServiceFacade> logger,
        ILogger<DxImageBuilder> dxLogger,
        ILogger<CrImageBuilder> crLogger)
    {
        _storageScu = storageScu;
        _worklistScu = worklistScu;
        _mppsScu = mppsScu;
        _storageCommitScu = storageCommitScu;
        _rdsrBuilder = rdsrBuilder;
        _uidGenerator = uidGenerator;
        _options = options.Value;
        _logger = logger;
        _dxBuilder = new DxImageBuilder(dxLogger);
        _crBuilder = new CrImageBuilder(crLogger);
    }

    /// <inheritdoc/>
    public async Task<string> StoreImageAsync(DicomImageData imageData, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        var dicomFile = BuildDicomFile(imageData);
        var sopInstanceUid = dicomFile.Dataset.GetString(DicomTag.SOPInstanceUID);

        if (_options.StorageDestinations.Count == 0)
        {
            _logger.LogWarning("No storage destinations configured. Image will not be transmitted.");
            return sopInstanceUid;
        }

        // Transmit to all configured destinations with retry support
        foreach (var destination in _options.StorageDestinations)
        {
            _logger.LogDebug(
                "Storing image to destination AeTitle={AeTitle}, SopUid={SopUid}",
                destination.AeTitle,
                sopInstanceUid);

            await _storageScu.StoreWithRetryAsync(dicomFile, destination, ct).ConfigureAwait(false);
        }

        return sopInstanceUid;
    }

    /// <inheritdoc/>
    public async Task<IList<WorklistItem>> FetchWorklistAsync(WorklistQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var results = new List<WorklistItem>();

        await foreach (var item in _worklistScu.QueryAsync(query, ct).ConfigureAwait(false))
        {
            results.Add(item);
        }

        _logger.LogDebug("Worklist query returned {Count} items", results.Count);

        return results;
    }

    /// <inheritdoc/>
    public Task<string> StartProcedureStepAsync(MppsData data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return _mppsScu.CreateProcedureStepAsync(data, ct);
    }

    /// <inheritdoc/>
    public Task CompleteProcedureStepAsync(string mppsUid, MppsData data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mppsUid);
        ArgumentNullException.ThrowIfNull(data);
        return _mppsScu.CompleteProcedureStepAsync(mppsUid, data, ct);
    }

    /// <inheritdoc/>
    public Task<string> RequestStorageCommitAsync(
        IEnumerable<string> sopInstanceUids,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sopInstanceUids);

        // Storage Commitment requires (SopClassUid, SopInstanceUid) pairs.
        // For DX For Presentation, use the known SOP class UID as default.
        // In production use, callers should supply SOP class UIDs matched to each instance.
        var sopPairs = sopInstanceUids.Select(uid =>
            (SopClassUid: DxImageBuilder.DxForPresentationSopClass.UID, SopInstanceUid: uid));

        return _storageCommitScu.RequestCommitAsync(sopPairs, ct);
    }

    /// <inheritdoc/>
    public async Task<RdsrExportResult> ExportStudyDoseAsync(
        string studyInstanceUid,
        IRdsrDataProvider provider,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(studyInstanceUid);
        ArgumentNullException.ThrowIfNull(provider);

        try
        {
            var summary = await provider.GetStudyDoseSummaryAsync(studyInstanceUid, ct)
                .ConfigureAwait(false);

            if (summary is null)
            {
                _logger.LogWarning(
                    "No dose data found for study {StudyUid}; RDSR export skipped.",
                    studyInstanceUid);

                return new RdsrExportResult
                {
                    Success = false,
                    ErrorMessage = $"No dose data found for study {studyInstanceUid}"
                };
            }

            var exposures = await provider.GetStudyExposureRecordsAsync(studyInstanceUid, ct)
                .ConfigureAwait(false);

            var rdsrFile = _rdsrBuilder.Build(summary, exposures);
            var rdsrSopUid = rdsrFile.Dataset.GetString(DicomTag.SOPInstanceUID);

            if (_options.StorageDestinations.Count == 0)
            {
                _logger.LogWarning(
                    "No storage destinations configured. RDSR {SopUid} will not be transmitted.",
                    rdsrSopUid);

                return new RdsrExportResult
                {
                    Success = false,
                    RdsrSopInstanceUid = rdsrSopUid,
                    ErrorMessage = "No storage destinations configured."
                };
            }

            foreach (var destination in _options.StorageDestinations)
            {
                _logger.LogDebug(
                    "Transmitting RDSR {SopUid} to destination {AeTitle}",
                    rdsrSopUid,
                    destination.AeTitle);

                await _storageScu.StoreWithRetryAsync(rdsrFile, destination, ct).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "RDSR export completed for study {StudyUid}, SopUid={SopUid}",
                studyInstanceUid,
                rdsrSopUid);

            return new RdsrExportResult
            {
                Success = true,
                RdsrSopInstanceUid = rdsrSopUid
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RDSR export failed for study {StudyUid}",
                studyInstanceUid);

            return new RdsrExportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private DicomFile BuildDicomFile(DicomImageData imageData)
    {
        return imageData.Modality switch
        {
            "DX" => _dxBuilder.Build(imageData),
            "CR" => _crBuilder.Build(imageData),
            _ => throw new NotSupportedException(
                $"Modality '{imageData.Modality}' is not supported. Supported modalities: DX, CR.")
        };
    }
}
