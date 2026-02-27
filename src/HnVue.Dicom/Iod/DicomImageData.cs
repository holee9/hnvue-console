namespace HnVue.Dicom.Iod;

/// <summary>
/// Immutable image data record containing all attributes required to construct a DX or CR DICOM IOD.
/// Passed to IImageBuilder implementations to produce a conformant DicomFile.
/// </summary>
public record DicomImageData
{
    // --- Patient Module ---

    /// <summary>Patient ID (0010,0020). Type 2.</summary>
    public required string PatientId { get; init; }

    /// <summary>Patient Name (0010,0010). Type 2. DICOM Person Name format (Family^Given^Middle^Prefix^Suffix).</summary>
    public required string PatientName { get; init; }

    /// <summary>Patient Birth Date (0010,0030). Type 2. Null if unknown.</summary>
    public DateOnly? PatientBirthDate { get; init; }

    /// <summary>Patient Sex (0010,0040). Type 2. Allowed values: M, F, O. Null if unknown.</summary>
    public string? PatientSex { get; init; }

    // --- General Study Module ---

    /// <summary>Study Instance UID (0020,000D). Type 1. Must be globally unique.</summary>
    public required string StudyInstanceUid { get; init; }

    /// <summary>Accession Number (0008,0050). Type 2. Empty string if not from worklist.</summary>
    public string AccessionNumber { get; init; } = string.Empty;

    /// <summary>Study Date (0008,0020). Type 2. Null if not available.</summary>
    public DateOnly? StudyDate { get; init; }

    /// <summary>Study Time (0008,0030). Type 2. Null if not available.</summary>
    public TimeOnly? StudyTime { get; init; }

    // --- General Series Module ---

    /// <summary>Series Instance UID (0020,000E). Type 1. Must be globally unique.</summary>
    public required string SeriesInstanceUid { get; init; }

    /// <summary>Modality (0008,0060). Type 1. Must be "DX" or "CR".</summary>
    public required string Modality { get; init; }

    // --- SOP Common Module ---

    /// <summary>SOP Instance UID (0008,0018). Type 1. Must be globally unique.</summary>
    public required string SopInstanceUid { get; init; }

    // --- General Image Module ---

    /// <summary>Acquisition Date (0008,0022). Type 3. Populated from system clock at acquisition.</summary>
    public DateOnly? AcquisitionDate { get; init; }

    /// <summary>Acquisition Time (0008,0032). Type 3. Populated from system clock at acquisition.</summary>
    public TimeOnly? AcquisitionTime { get; init; }

    // --- Image Plane / Pixel Attributes ---

    /// <summary>Body Part Examined (0018,0015). Type 2C. DICOM Part 16 Annex B code.</summary>
    public string? BodyPartExamined { get; init; }

    /// <summary>Rows (0028,0010). Type 1. Number of pixel rows.</summary>
    public required ushort Rows { get; init; }

    /// <summary>Columns (0028,0011). Type 1. Number of pixel columns.</summary>
    public required ushort Columns { get; init; }

    /// <summary>Bits Allocated (0028,0100). Type 1. Typically 16 for DX/CR.</summary>
    public ushort BitsAllocated { get; init; } = 16;

    /// <summary>Bits Stored (0028,0101). Type 1. Number of bits actually used.</summary>
    public ushort BitsStored { get; init; } = 12;

    /// <summary>High Bit (0028,0102). Type 1. Typically BitsStored - 1.</summary>
    public ushort HighBit { get; init; } = 11;

    /// <summary>Pixel Representation (0028,0103). Type 1. 0 = unsigned, 1 = two's complement.</summary>
    public ushort PixelRepresentation { get; init; } = 0;

    /// <summary>Photometric Interpretation (0028,0004). Type 1. Default: MONOCHROME2.</summary>
    public string PhotometricInterpretation { get; init; } = "MONOCHROME2";

    /// <summary>Pixel Data (7FE0,0010). Type 1. Device-corrected image bytes (raw pixel array).</summary>
    public required byte[] PixelData { get; init; }

    // --- DX / CR Acquisition Geometry ---

    /// <summary>Pixel Spacing (0028,0030). Type 1C (DX) / 2 (CR). Row spacing then column spacing in mm.</summary>
    public (decimal RowSpacingMm, decimal ColumnSpacingMm)? PixelSpacing { get; init; }

    /// <summary>Imager Pixel Spacing (0018,1164). Type 3. Physical detector pixel spacing in mm.</summary>
    public (decimal RowSpacingMm, decimal ColumnSpacingMm)? ImagerPixelSpacing { get; init; }

    // --- X-Ray Acquisition Parameters ---

    /// <summary>KVP (0018,0060). Type 2. Peak kilo voltage at the generator.</summary>
    public decimal? KvP { get; init; }

    /// <summary>Exposure in mAs (0018,1153). Type 3. X-ray tube exposure in milliampere-seconds.</summary>
    public decimal? ExposureInMas { get; init; }

    /// <summary>Focal Spot(s) (0018,1190). Type 3. Nominal focal spot size in mm.</summary>
    public decimal[]? FocalSpots { get; init; }

    /// <summary>Distance Source to Detector (0018,1110). Type 3. In mm.</summary>
    public decimal? DistanceSourceToDetectorMm { get; init; }

    // --- DX-specific ---

    /// <summary>
    /// Presentation Intent Type (0008,0068). Required for DX IOD.
    /// "FOR PRESENTATION" (default) or "FOR PROCESSING".
    /// </summary>
    public string PresentationIntentType { get; init; } = "FOR PRESENTATION";

    // --- CR-specific ---

    /// <summary>Plate Type (0018,1004). Type 3 for CR. Storage phosphor plate type.</summary>
    public string? PlateType { get; init; }

    /// <summary>Image Laterality (0020,0062). Type 2C for CR. Allowed: R, L, U, B.</summary>
    public string? ImageLaterality { get; init; }
}
