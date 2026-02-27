using Dicom;
using HnVue.Dicom.Configuration;

namespace HnVue.Dicom.Storage;

/// <summary>
/// DICOM Storage SCU providing C-STORE operations for image transmission (FR-DICOM-01, FR-DICOM-02).
/// </summary>
public interface IStorageScu
{
    /// <summary>
    /// Transmits a single DICOM object to the specified destination via C-STORE.
    /// Transfer syntax negotiation and transcoding are handled automatically.
    /// </summary>
    /// <param name="dicomFile">The DICOM file to transmit.</param>
    /// <param name="destination">The target Storage SCP.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if the C-STORE response status is Success (0x0000) or Warning;
    /// <see langword="false"/> if the operation failed.
    /// </returns>
    Task<bool> StoreAsync(
        DicomFile dicomFile,
        DicomDestination destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to transmit a DICOM object; on failure, enqueues the item for retry
    /// with exponential back-off (FR-DICOM-08).
    /// PHI must not appear in logs at INFO/WARN/ERROR level (NFR-SEC-01).
    /// </summary>
    /// <param name="dicomFile">The DICOM file to transmit.</param>
    /// <param name="destination">The target Storage SCP.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreWithRetryAsync(
        DicomFile dicomFile,
        DicomDestination destination,
        CancellationToken cancellationToken = default);
}
