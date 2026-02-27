using Dicom;
using Dicom.Imaging.Codec;
using Dicom.Network;
using Dicom.Network.Client;
using HnVue.Dicom.Associations;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// Aliases to resolve fo-dicom 4.x namespace ambiguities
using HnVueDicomConfig = HnVue.Dicom.Configuration.DicomServiceOptions;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace HnVue.Dicom.Storage;

/// <summary>
/// DICOM C-STORE SCU implementation supporting transfer syntax negotiation,
/// in-memory transcoding, and durable retry queue integration.
/// </summary>
/// <remarks>
/// Transfer syntax proposal priority (FR-DICOM-02):
///   1. JPEG 2000 Lossless Only
///   2. JPEG Lossless Non-Hierarchical (FOP / Process 14, SV1)
///   3. Explicit VR Little Endian
///   4. Implicit VR Little Endian (mandatory fallback)
///
/// PHI must NOT appear in log output at INFO/WARN/ERROR levels (NFR-SEC-01).
/// </remarks>
// @MX:ANCHOR: [AUTO] StoreAsync is the primary image transmission entry point.
// @MX:REASON: fan_in >= 3 expected (StoreWithRetryAsync, DicomServiceFacade, integration tests)
// @MX:NOTE: [AUTO] fo-dicom 4.x DicomClient constructor: new DicomClient(host, port, useTls, callingAe, calledAe)
//           fo-dicom 4.x AddRequest is synchronous; SendAsync triggers the association.
public sealed class StorageScu : IStorageScu
{
    private static readonly DicomTransferSyntax[] ProposedTransferSyntaxes =
    {
        DicomTransferSyntax.JPEG2000Lossless,
        DicomTransferSyntax.JPEGProcess14SV1,
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.ImplicitVRLittleEndian,
    };

    private readonly HnVueDicomConfig _options;
    private readonly IAssociationManager _associationManager;
    private readonly ITransmissionQueue _transmissionQueue;
    private readonly ILogger<StorageScu> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="StorageScu"/>.
    /// </summary>
    /// <param name="options">DICOM service configuration (AE title, TLS, timeouts).</param>
    /// <param name="associationManager">Manages DICOM association lifecycle.</param>
    /// <param name="transmissionQueue">Persistent retry queue for failed operations.</param>
    /// <param name="logger">Logger for C-STORE lifecycle events (PHI excluded).</param>
    public StorageScu(
        IOptions<HnVueDicomConfig> options,
        IAssociationManager associationManager,
        ITransmissionQueue transmissionQueue,
        ILogger<StorageScu> logger)
    {
        _options = options.Value;
        _associationManager = associationManager;
        _transmissionQueue = transmissionQueue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> StoreAsync(
        DicomFile dicomFile,
        DicomDestination destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dicomFile);
        ArgumentNullException.ThrowIfNull(destination);

        var sopClassUid = dicomFile.Dataset.GetString(DicomTag.SOPClassUID);

        _logger.LogDebug(
            "C-STORE initiated (SopClass: {SopClass}, Destination: {Destination}:{Port})",
            sopClassUid,
            destination.Host,
            destination.Port);

        var tlsEnabled = destination.TlsEnabled ?? _options.Tls.Enabled;

        // fo-dicom 4.x DicomClient constructor signature:
        // DicomClient(host, port, useTls, callingAe, calledAe)
        var client = new DicomClient(
            destination.Host,
            destination.Port,
            tlsEnabled,
            _options.CallingAeTitle,
            destination.AeTitle);

        // fo-dicom 4.x: Add presentation context then append transfer syntaxes via AddTransferSyntax()
        var pc = new DicomPresentationContext(1, DicomUID.Parse(sopClassUid));
        foreach (var ts in ProposedTransferSyntaxes)
        {
            pc.AddTransferSyntax(ts);
        }
        client.AdditionalPresentationContexts.Add(pc);

        DicomCStoreResponse? storeResponse = null;
        Exception? storeException = null;

        var request = new DicomCStoreRequest(dicomFile);

        request.OnResponseReceived = (req, response) =>
        {
            storeResponse = response;
        };

        await client.AddRequestAsync(request).ConfigureAwait(false);

        try
        {
            await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            storeException = ex;
            _logger.LogError(ex,
                "C-STORE network error (Destination: {Destination}:{Port})",
                destination.Host,
                destination.Port);
            return false;
        }

        if (storeException != null)
        {
            return false;
        }

        if (storeResponse == null)
        {
            _logger.LogError(
                "C-STORE received no response (Destination: {Destination}:{Port})",
                destination.Host,
                destination.Port);
            return false;
        }

        return HandleStoreResponse(storeResponse, destination);
    }

    /// <inheritdoc/>
    public async Task StoreWithRetryAsync(
        DicomFile dicomFile,
        DicomDestination destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dicomFile);
        ArgumentNullException.ThrowIfNull(destination);

        var sopInstanceUid = dicomFile.Dataset.GetString(DicomTag.SOPInstanceUID);
        var tempFilePath = GetOrWriteTempFile(dicomFile, sopInstanceUid);

        var success = await StoreAsync(dicomFile, destination, cancellationToken)
            .ConfigureAwait(false);

        if (!success)
        {
            _logger.LogWarning(
                "C-STORE failed; enqueuing for retry (Destination: {Destination}:{Port})",
                destination.Host,
                destination.Port);

            await _transmissionQueue.EnqueueAsync(
                sopInstanceUid,
                tempFilePath,
                destination.AeTitle,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Transcodes <paramref name="dicomFile"/> pixel data to <paramref name="targetSyntax"/> in memory.
    /// Used when the SCP only accepts a lower-priority transfer syntax than the source file.
    /// </summary>
    /// <param name="dicomFile">The source DICOM file.</param>
    /// <param name="targetSyntax">The transfer syntax accepted by the remote SCP.</param>
    /// <returns>A new <see cref="DicomFile"/> encoded in <paramref name="targetSyntax"/>.</returns>
    /// <exception cref="DicomCodecException">Thrown when transcoding fails.</exception>
    public static DicomFile TranscodeInMemory(DicomFile dicomFile, DicomTransferSyntax targetSyntax)
    {
        // fo-dicom 4.x: Use DicomTranscoder for in-memory codec transcoding
        var transcoder = new DicomTranscoder(dicomFile.Dataset.InternalTransferSyntax, targetSyntax);
        return transcoder.Transcode(dicomFile);
    }

    private bool HandleStoreResponse(DicomCStoreResponse response, DicomDestination destination)
    {
        var status = response.Status;

        if (status == DicomStatus.Success)
        {
            _logger.LogInformation(
                "C-STORE success (Destination: {Destination}:{Port})",
                destination.Host,
                destination.Port);
            return true;
        }

        // Warning statuses: treat as success with a warning log (spec section 4.5.1)
        if (status.State == DicomState.Warning)
        {
            _logger.LogWarning(
                "C-STORE completed with warning status 0x{StatusCode:X4} (Destination: {Destination}:{Port})",
                status.Code,
                destination.Host,
                destination.Port);
            return true;
        }

        // Failure statuses
        _logger.LogError(
            "C-STORE failed with status 0x{StatusCode:X4} category {StatusState} (Destination: {Destination}:{Port})",
            status.Code,
            status.State,
            destination.Host,
            destination.Port);

        return false;
    }

    private string GetOrWriteTempFile(DicomFile dicomFile, string sopInstanceUid)
    {
        // If the file already has a known path (loaded from disk), use that
        if (!string.IsNullOrWhiteSpace(dicomFile.File?.Name))
        {
            return dicomFile.File.Name;
        }

        // Otherwise write a temp copy for the queue reference
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HnVue",
            "DicomTemp");
        Directory.CreateDirectory(tempDir);

        var tempPath = Path.Combine(tempDir, $"{sopInstanceUid}.dcm");
        dicomFile.Save(tempPath);
        return tempPath;
    }
}
