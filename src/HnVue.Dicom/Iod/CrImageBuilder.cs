using Dicom;
using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Iod;

/// <summary>
/// Builds conformant DICOM files for the Computed Radiography (CR) Image IOD.
/// SOP Class UID: 1.2.840.10008.5.1.4.1.1.1 (Computed Radiography Image Storage)
///
/// Implements mandatory Type 1 and Type 2 attributes per DICOM PS 3.3 C.8.1.1 (CR IOD).
/// </summary>
public sealed class CrImageBuilder : IImageBuilder<DicomImageData>
{
    private readonly ILogger<CrImageBuilder> _logger;

    /// <summary>SOP Class UID for Computed Radiography Image Storage.</summary>
    public static readonly DicomUID CrImageStorageSopClass =
        DicomUID.Parse("1.2.840.10008.5.1.4.1.1.1");

    /// <summary>
    /// Initializes a new instance of the <see cref="CrImageBuilder"/> class.
    /// </summary>
    /// <param name="logger">Logger for build diagnostics.</param>
    public CrImageBuilder(ILogger<CrImageBuilder> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public DicomFile Build(DicomImageData imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        if (imageData.PixelData.Length == 0)
        {
            throw new InvalidOperationException("Pixel data must not be empty.");
        }

        _logger.LogDebug(
            "Building CR IOD: Rows={Rows}, Columns={Columns}, SopUid={SopUid}",
            imageData.Rows,
            imageData.Columns,
            imageData.SopInstanceUid);

        var dataset = new DicomDataset();

        PopulateSopCommonModule(dataset, imageData);
        PopulatePatientModule(dataset, imageData);
        PopulateGeneralStudyModule(dataset, imageData);
        PopulateGeneralSeriesModule(dataset, imageData);
        PopulateCrSeriesModule(dataset, imageData);
        PopulateGeneralImageModule(dataset, imageData);
        PopulateCrImageModule(dataset, imageData);
        PopulateImagePixelModule(dataset, imageData);
        PopulateXRayAcquisitionModule(dataset, imageData);

        var file = new DicomFile(dataset);

        _logger.LogDebug("CR IOD built successfully for SopUid={SopUid}", imageData.SopInstanceUid);

        return file;
    }

    private static void PopulateSopCommonModule(DicomDataset dataset, DicomImageData imageData)
    {
        // SOP Class UID (0008,0016) - Type 1
        dataset.Add(DicomTag.SOPClassUID, CrImageStorageSopClass);

        // SOP Instance UID (0008,0018) - Type 1
        dataset.Add(DicomTag.SOPInstanceUID, imageData.SopInstanceUid);

        // Specific Character Set (0008,0005) - Type 1C
        dataset.Add(DicomTag.SpecificCharacterSet, "ISO_IR 6");
    }

    private static void PopulatePatientModule(DicomDataset dataset, DicomImageData imageData)
    {
        // Patient Name (0010,0010) - Type 2
        dataset.Add(DicomTag.PatientName, imageData.PatientName);

        // Patient ID (0010,0020) - Type 2
        dataset.Add(DicomTag.PatientID, imageData.PatientId);

        // Patient Birth Date (0010,0030) - Type 2
        dataset.Add(DicomTag.PatientBirthDate, imageData.PatientBirthDate?.ToString("yyyyMMdd") ?? string.Empty);

        // Patient Sex (0010,0040) - Type 2
        dataset.Add(DicomTag.PatientSex, imageData.PatientSex ?? string.Empty);
    }

    private static void PopulateGeneralStudyModule(DicomDataset dataset, DicomImageData imageData)
    {
        // Study Instance UID (0020,000D) - Type 1
        dataset.Add(DicomTag.StudyInstanceUID, imageData.StudyInstanceUid);

        // Study Date (0008,0020) - Type 2
        dataset.Add(DicomTag.StudyDate, imageData.StudyDate?.ToString("yyyyMMdd") ?? string.Empty);

        // Study Time (0008,0030) - Type 2
        dataset.Add(DicomTag.StudyTime, imageData.StudyTime?.ToString("HHmmss") ?? string.Empty);

        // Accession Number (0008,0050) - Type 2
        dataset.Add(DicomTag.AccessionNumber, imageData.AccessionNumber);

        // Referring Physician's Name (0008,0090) - Type 2
        dataset.Add(DicomTag.ReferringPhysicianName, string.Empty);

        // Study ID (0020,0010) - Type 2
        dataset.Add(DicomTag.StudyID, string.Empty);
    }

    private static void PopulateGeneralSeriesModule(DicomDataset dataset, DicomImageData imageData)
    {
        // Modality (0008,0060) - Type 1: Must be "CR"
        dataset.Add(DicomTag.Modality, "CR");

        // Series Instance UID (0020,000E) - Type 1
        dataset.Add(DicomTag.SeriesInstanceUID, imageData.SeriesInstanceUid);

        // Series Number (0020,0011) - Type 2
        dataset.Add(DicomTag.SeriesNumber, string.Empty);
    }

    private static void PopulateCrSeriesModule(DicomDataset dataset, DicomImageData imageData)
    {
        // Body Part Examined (0018,0015) - Type 2
        dataset.Add(DicomTag.BodyPartExamined, imageData.BodyPartExamined ?? string.Empty);

        // View Position (0018,5101) - Type 2
        dataset.Add(DicomTag.ViewPosition, string.Empty);
    }

    private static void PopulateGeneralImageModule(DicomDataset dataset, DicomImageData imageData)
    {
        // Instance Number (0020,0013) - Type 2
        dataset.Add(DicomTag.InstanceNumber, string.Empty);

        // Content Date (0008,0023) - Type 2C
        var acqDate = imageData.AcquisitionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        dataset.Add(DicomTag.ContentDate, acqDate.ToString("yyyyMMdd"));

        // Content Time (0008,0033) - Type 2C
        var acqTime = imageData.AcquisitionTime ?? TimeOnly.FromDateTime(DateTime.UtcNow);
        dataset.Add(DicomTag.ContentTime, acqTime.ToString("HHmmss"));

        // Acquisition Date (0008,0022) - Type 3
        dataset.Add(DicomTag.AcquisitionDate, acqDate.ToString("yyyyMMdd"));

        // Acquisition Time (0008,0032) - Type 3
        dataset.Add(DicomTag.AcquisitionTime, acqTime.ToString("HHmmss"));

        // Image Type (0008,0008) - Type 1
        dataset.Add(DicomTag.ImageType, new[] { "ORIGINAL", "PRIMARY" });
    }

    private static void PopulateCrImageModule(DicomDataset dataset, DicomImageData imageData)
    {
        // Pixel Spacing (0028,0030) - Type 2
        if (imageData.PixelSpacing.HasValue)
        {
            dataset.Add(DicomTag.PixelSpacing,
                new[] { imageData.PixelSpacing.Value.RowSpacingMm, imageData.PixelSpacing.Value.ColumnSpacingMm });
        }
        else
        {
            dataset.Add(DicomTag.PixelSpacing, Array.Empty<decimal>());
        }

        // Plate Type (0018,1004) - Type 3
        if (!string.IsNullOrEmpty(imageData.PlateType))
        {
            dataset.Add(DicomTag.PlateType, imageData.PlateType);
        }

        // Image Laterality (0020,0062) - Type 2C
        if (!string.IsNullOrEmpty(imageData.ImageLaterality))
        {
            dataset.Add(DicomTag.ImageLaterality, imageData.ImageLaterality);
        }

        // Burned In Annotation (0008,2111) - Type 1
        dataset.Add(DicomTag.BurnedInAnnotation, "NO");
    }

    private static void PopulateImagePixelModule(DicomDataset dataset, DicomImageData imageData)
    {
        // Samples Per Pixel (0028,0002) - Type 1
        dataset.Add(DicomTag.SamplesPerPixel, (ushort)1);

        // Photometric Interpretation (0028,0004) - Type 1
        dataset.Add(DicomTag.PhotometricInterpretation, imageData.PhotometricInterpretation);

        // Rows (0028,0010) - Type 1
        dataset.Add(DicomTag.Rows, imageData.Rows);

        // Columns (0028,0011) - Type 1
        dataset.Add(DicomTag.Columns, imageData.Columns);

        // Bits Allocated (0028,0100) - Type 1
        dataset.Add(DicomTag.BitsAllocated, imageData.BitsAllocated);

        // Bits Stored (0028,0101) - Type 1
        dataset.Add(DicomTag.BitsStored, imageData.BitsStored);

        // High Bit (0028,0102) - Type 1
        dataset.Add(DicomTag.HighBit, imageData.HighBit);

        // Pixel Representation (0028,0103) - Type 1
        dataset.Add(DicomTag.PixelRepresentation, imageData.PixelRepresentation);

        // Pixel Data (7FE0,0010) - Type 1
        // Convert byte[] to ushort[] for 16-bit grayscale pixel data
        var pixelWords = new ushort[imageData.PixelData.Length / 2];
        Buffer.BlockCopy(imageData.PixelData, 0, pixelWords, 0, imageData.PixelData.Length);
        dataset.Add(new DicomOtherWord(DicomTag.PixelData, pixelWords));
    }

    private static void PopulateXRayAcquisitionModule(DicomDataset dataset, DicomImageData imageData)
    {
        // KVP (0018,0060) - Type 2
        if (imageData.KvP.HasValue)
        {
            dataset.Add(DicomTag.KVP, imageData.KvP.Value);
        }
        else
        {
            dataset.Add(DicomTag.KVP, string.Empty);
        }

        // Exposure in mAs (0018,1153) - Type 3
        if (imageData.ExposureInMas.HasValue)
        {
            dataset.Add(DicomTag.ExposureInuAs, (int)(imageData.ExposureInMas.Value * 1000));
        }

        // Focal Spot(s) (0018,1190) - Type 3
        if (imageData.FocalSpots is { Length: > 0 })
        {
            dataset.Add(DicomTag.FocalSpots, imageData.FocalSpots);
        }

        // Distance Source to Detector (0018,1110) - Type 3
        if (imageData.DistanceSourceToDetectorMm.HasValue)
        {
            dataset.Add(DicomTag.DistanceSourceToDetector, imageData.DistanceSourceToDetectorMm.Value);
        }
    }
}
