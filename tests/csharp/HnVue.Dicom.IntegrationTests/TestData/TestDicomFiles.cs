using Dicom;
using Dicom.Imaging.Codec;

namespace HnVue.Dicom.IntegrationTests.TestData;

/// <summary>
/// Factory for creating test DICOM files for integration testing.
/// All generated DICOM files are minimal but valid for their SOP class.
/// </summary>
public static class TestDicomFiles
{
    /// <summary>
    /// Creates a minimal Digital X-Ray (DX) presentation image.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID. If null, a random UID is generated.</param>
    /// <param name="patientName">The patient name (defaults to "TEST^PATIENT").</param>
    /// <param name="patientId">The patient ID (defaults to "TEST123").</param>
    /// <returns>A valid DX DICOM file.</returns>
    public static DicomFile CreateDxImage(
        string? sopInstanceUid = null,
        string patientName = "TEST^PATIENT",
        string patientId = "TEST123")
    {
        sopInstanceUid ??= DicomUID.Generate().UID;

        var dataset = new DicomDataset
        {
            // SOP Common
            { DicomTag.SOPClassUID, DicomUID.DigitalXRayImageStorageForPresentation },
            { DicomTag.SOPInstanceUID, sopInstanceUid },

            // Patient
            { DicomTag.PatientName, patientName },
            { DicomTag.PatientID, patientId },
            { DicomTag.PatientBirthDate, "20000101" },
            { DicomTag.PatientSex, "O" },

            // Study
            { DicomTag.StudyInstanceUID, "1.2.3.4.5.10" },
            { DicomTag.StudyDate, "20240101" },
            { DicomTag.StudyTime, "120000" },
            { DicomTag.AccessionNumber, "ACC123456" },
            { DicomTag.StudyDescription, "TEST STUDY" },

            // Series
            { DicomTag.SeriesInstanceUID, "1.2.3.4.5.11" },
            { DicomTag.Modality, "DX" },
            { DicomTag.SeriesNumber, 1 },
            { DicomTag.SeriesDescription, "TEST SERIES" },

            // Image
            { DicomTag.InstanceNumber, 1 },
            { DicomTag.ImageType, new string[] { "ORIGINAL", "PRIMARY" } },
            { DicomTag.ViewPosition, "AP" },

            // Pixel Data - 1x1 pixel grayscale image
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)1 },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)16 },
            { DicomTag.HighBit, (ushort)15 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.PixelData, new ushort[] { 0 } }
        };

        return new DicomFile(dataset);
    }

    /// <summary>
    /// Creates a minimal Computed Radiography (CR) image.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID. If null, a random UID is generated.</param>
    /// <returns>A valid CR DICOM file.</returns>
    public static DicomFile CreateCrImage(string? sopInstanceUid = null)
    {
        sopInstanceUid ??= DicomUID.Generate().UID;

        var dataset = new DicomDataset
        {
            // SOP Common
            { DicomTag.SOPClassUID, DicomUID.ComputedRadiographyImageStorage },
            { DicomTag.SOPInstanceUID, sopInstanceUid },

            // Patient
            { DicomTag.PatientName, "TEST^PATIENT" },
            { DicomTag.PatientID, "TEST123" },
            { DicomTag.PatientBirthDate, "20000101" },
            { DicomTag.PatientSex, "O" },

            // Study
            { DicomTag.StudyInstanceUID, "1.2.3.4.5.20" },
            { DicomTag.StudyDate, "20240101" },
            { DicomTag.StudyTime, "120000" },
            { DicomTag.AccessionNumber, "ACC123457" },

            // Series
            { DicomTag.SeriesInstanceUID, "1.2.3.4.5.21" },
            { DicomTag.Modality, "CR" },
            { DicomTag.SeriesNumber, 1 },

            // Image
            { DicomTag.InstanceNumber, 1 },

            // Pixel Data - 1x1 pixel grayscale image
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)1 },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)16 },
            { DicomTag.HighBit, (ushort)15 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.PixelData, new ushort[] { 0 } }
        };

        return new DicomFile(dataset);
    }

    /// <summary>
    /// Creates a DICOM file with a specific transfer syntax.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID.</param>
    /// <param name="transferSyntax">The desired transfer syntax.</param>
    /// <returns>A DICOM file in the specified transfer syntax.</returns>
    public static DicomFile CreateWithTransferSyntax(
        string sopInstanceUid,
        DicomTransferSyntax transferSyntax)
    {
        var dicomFile = CreateDxImage(sopInstanceUid);
        // Use DicomTranscoder for fo-dicom 4.x
        var transcoder = new DicomTranscoder(dicomFile.Dataset.InternalTransferSyntax, transferSyntax);
        return transcoder.Transcode(dicomFile);
    }

    /// <summary>
    /// Creates a DICOM file with JPEG 2000 Lossless transfer syntax.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID.</param>
    /// <returns>A DICOM file with JPEG 2000 Lossless encoding.</returns>
    public static DicomFile CreateJpeg2000Lossless(string? sopInstanceUid = null)
    {
        sopInstanceUid ??= DicomUID.Generate().UID;
        return CreateWithTransferSyntax(sopInstanceUid, DicomTransferSyntax.JPEG2000Lossless);
    }

    /// <summary>
    /// Creates a DICOM file with JPEG Lossless transfer syntax.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID.</param>
    /// <returns>A DICOM file with JPEG Lossless encoding.</returns>
    public static DicomFile CreateJpegLossless(string? sopInstanceUid = null)
    {
        sopInstanceUid ??= DicomUID.Generate().UID;
        return CreateWithTransferSyntax(sopInstanceUid, DicomTransferSyntax.JPEGProcess14SV1);
    }

    /// <summary>
    /// Creates a DICOM file with Implicit VR Little Endian transfer syntax.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID.</param>
    /// <returns>A DICOM file with Implicit VR Little Endian encoding.</returns>
    public static DicomFile CreateImplicitVRLittleEndian(string? sopInstanceUid = null)
    {
        sopInstanceUid ??= DicomUID.Generate().UID;
        return CreateWithTransferSyntax(sopInstanceUid, DicomTransferSyntax.ImplicitVRLittleEndian);
    }

    /// <summary>
    /// Creates a DICOM file with Explicit VR Little Endian transfer syntax.
    /// </summary>
    /// <param name="sopInstanceUid">The SOP Instance UID.</param>
    /// <returns>A DICOM file with Explicit VR Little Endian encoding.</returns>
    public static DicomFile CreateExplicitVRLittleEndian(string? sopInstanceUid = null)
    {
        sopInstanceUid ??= DicomUID.Generate().UID;
        return CreateWithTransferSyntax(sopInstanceUid, DicomTransferSyntax.ExplicitVRLittleEndian);
    }
}
