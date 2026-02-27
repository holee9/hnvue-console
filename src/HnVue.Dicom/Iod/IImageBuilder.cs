using Dicom;

namespace HnVue.Dicom.Iod;

/// <summary>
/// Builds a conformant DICOM file from typed image data.
/// Implementations produce IOD-specific DicomFile instances (DX, CR, etc.)
/// following DICOM PS 3.3 composite IOD requirements.
/// </summary>
/// <typeparam name="TData">The image data record type consumed by this builder.</typeparam>
public interface IImageBuilder<TData>
    where TData : DicomImageData
{
    /// <summary>
    /// Constructs a conformant DICOM file from the provided image data.
    /// </summary>
    /// <param name="imageData">The image data record containing all required attributes.</param>
    /// <returns>A complete <see cref="DicomFile"/> ready for C-STORE transmission.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageData"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when mandatory Type 1 attributes are missing or pixel data is empty.
    /// </exception>
    DicomFile Build(TData imageData);
}
