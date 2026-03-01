using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Mpps;

/// <summary>
/// High-level client for Modality Performed Procedure Step (MPPS) operations.
/// Provides error handling and graceful degradation for MPPS N-CREATE/N-SET.
/// </summary>
/// <remarks>
/// @MX:NOTE Error handling - Client wraps MPPS SCU with graceful degradation
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-407
///
/// Features:
/// - N-CREATE at study start
/// - N-SET at exposure complete
/// - N-SET at study completion
/// - Error handling (continues workflow if MPPS unavailable)
/// </remarks>
public sealed class DicomMppsClient
{
    private readonly IMppsScu _mppsScu;
    private readonly ILogger<DicomMppsClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomMppsClient"/> class.
    /// </summary>
    /// <param name="mppsScu">The underlying MPPS SCU implementation.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DicomMppsClient(
        IMppsScu mppsScu,
        ILogger<DicomMppsClient> logger)
    {
        _mppsScu = mppsScu ?? throw new ArgumentNullException(nameof(mppsScu));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new MPPS procedure step at study start (N-CREATE).
    /// </summary>
    /// <param name="studyInstanceUid">The study instance UID.</param>
    /// <param name="seriesInstanceUid">The series instance UID.</param>
    /// <param name="performedProcedureStepId">The performed procedure step ID.</param>
    /// <param name="performedProcedureStepDescription">The performed procedure step description.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="MppsOperationResult"/> containing the SOP Instance UID on success.
    /// Returns a failed result if MPPS is unavailable.
    /// </returns>
    /// <remarks>
    /// @MX:NOTE Error handling - Returns failed result instead of throwing
    ///
    /// This method implements N-CREATE per IHE SWF RAD-6.
    /// </remarks>
    public async Task<MppsOperationResult> CreateMppsAsync(
        string studyInstanceUid,
        string seriesInstanceUid,
        string performedProcedureStepId,
        string performedProcedureStepDescription,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Creating MPPS (Study: {StudyUid}, Series: {SeriesUid}, StepId: {StepId})",
                studyInstanceUid,
                seriesInstanceUid,
                performedProcedureStepId);

            var data = new MppsData(
                PatientId: string.Empty, // Will be populated by workflow
                StudyInstanceUid: studyInstanceUid,
                SeriesInstanceUid: seriesInstanceUid,
                PerformedProcedureStepId: performedProcedureStepId,
                PerformedProcedureStepDescription: performedProcedureStepDescription,
                StartDateTime: DateTime.UtcNow,
                EndDateTime: null,
                Status: MppsStatus.InProgress,
                ExposureData: Array.Empty<ExposureData>());

            var sopInstanceUid = await _mppsScu.CreateProcedureStepAsync(data, cancellationToken);

            _logger.LogInformation(
                "MPPS created successfully (SopInstanceUid: {SopInstanceUid})",
                sopInstanceUid);

            return MppsOperationResult.CreateSuccess(sopInstanceUid);
        }
        catch (DicomMppsException ex)
        {
            _logger.LogError(
                ex,
                "MPPS N-CREATE failed with DICOM status 0x{StatusCode:X4}",
                ex.StatusCode);

            // Graceful degradation: allow workflow to continue
            return MppsOperationResult.Failed(
                $"MPPS N-CREATE failed: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "MPPS N-CREATE failed unexpectedly");

            // Graceful degradation: allow workflow to continue
            return MppsOperationResult.Failed(
                $"MPPS N-CREATE failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the MPPS procedure step when exposure is complete (N-SET).
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID from N-CREATE.</param>
    /// <param name="imageSopInstanceUid">The SOP Instance UID of the acquired image.</param>
    /// <param name="seriesInstanceUid">The series instance UID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="MppsOperationResult"/> indicating success or failure.
    /// </returns>
    /// <remarks>
    /// @MX:NOTE Error handling - Returns failed result instead of throwing
    ///
    /// This method implements N-SET with IN PROGRESS status.
    /// </remarks>
    public async Task<MppsOperationResult> UpdateExposureCompleteAsync(
        string sopInstanceUid,
        string imageSopInstanceUid,
        string seriesInstanceUid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Updating MPPS after exposure (SopInstanceUid: {SopInstanceUid}, Image: {ImageUid})",
                sopInstanceUid,
                imageSopInstanceUid);

            var exposureData = new ExposureData(
                SeriesInstanceUid: seriesInstanceUid,
                SopClassUid: "1.2.840.10008.5.1.4.1.1.1", // CR Image Storage
                SopInstanceUid: imageSopInstanceUid);

            var data = new MppsData(
                PatientId: string.Empty,
                StudyInstanceUid: string.Empty,
                SeriesInstanceUid: seriesInstanceUid,
                PerformedProcedureStepId: string.Empty,
                PerformedProcedureStepDescription: string.Empty,
                StartDateTime: DateTime.UtcNow,
                EndDateTime: null,
                Status: MppsStatus.InProgress,
                ExposureData: new[] { exposureData });

            await _mppsScu.SetProcedureStepInProgressAsync(sopInstanceUid, data, cancellationToken);

            _logger.LogInformation(
                "MPPS updated successfully after exposure (SopInstanceUid: {SopInstanceUid})",
                sopInstanceUid);

            return MppsOperationResult.UpdateSuccess();
        }
        catch (DicomMppsException ex)
        {
            _logger.LogError(
                ex,
                "MPPS N-SET (exposure complete) failed with DICOM status 0x{StatusCode:X4}",
                ex.StatusCode);

            // Graceful degradation: allow workflow to continue
            return MppsOperationResult.Failed(
                $"MPPS N-SET failed: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "MPPS N-SET (exposure complete) failed unexpectedly");

            // Graceful degradation: allow workflow to continue
            return MppsOperationResult.Failed(
                $"MPPS N-SET failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Completes the MPPS procedure step at study completion (N-SET with COMPLETED status).
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID from N-CREATE.</param>
    /// <param name="imageSopInstanceUids">All image SOP Instance UIDs in the study.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="MppsOperationResult"/> indicating success or failure.
    /// </returns>
    /// <remarks>
    /// @MX:NOTE Error handling - Returns failed result instead of throwing
    ///
    /// This method implements N-SET with COMPLETED status per IHE SWF RAD-7.
    /// </remarks>
    public async Task<MppsOperationResult> CompleteStudyAsync(
        string sopInstanceUid,
        string[] imageSopInstanceUids,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Completing MPPS (SopInstanceUid: {SopInstanceUid}, ImageCount: {ImageCount})",
                sopInstanceUid,
                imageSopInstanceUids.Length);

            var exposureData = imageSopInstanceUids
                .Select(uid => new ExposureData(
                    SeriesInstanceUid: string.Empty,
                    SopClassUid: "1.2.840.10008.5.1.4.1.1.1", // CR Image Storage
                    SopInstanceUid: uid))
                .ToArray();

            var data = new MppsData(
                PatientId: string.Empty,
                StudyInstanceUid: string.Empty,
                SeriesInstanceUid: string.Empty,
                PerformedProcedureStepId: string.Empty,
                PerformedProcedureStepDescription: string.Empty,
                StartDateTime: DateTime.UtcNow,
                EndDateTime: DateTime.UtcNow,
                Status: MppsStatus.Completed,
                ExposureData: exposureData);

            await _mppsScu.CompleteProcedureStepAsync(sopInstanceUid, data, cancellationToken);

            _logger.LogInformation(
                "MPPS completed successfully (SopInstanceUid: {SopInstanceUid})",
                sopInstanceUid);

            return MppsOperationResult.UpdateSuccess();
        }
        catch (DicomMppsException ex)
        {
            _logger.LogError(
                ex,
                "MPPS N-SET (complete) failed with DICOM status 0x{StatusCode:X4}",
                ex.StatusCode);

            // Graceful degradation: allow workflow to continue
            return MppsOperationResult.Failed(
                $"MPPS complete failed: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "MPPS N-SET (complete) failed unexpectedly");

            // Graceful degradation: allow workflow to continue
            return MppsOperationResult.Failed(
                $"MPPS complete failed: {ex.Message}");
        }
    }
}
