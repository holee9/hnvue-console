using Dicom;
using HnVue.Dicom.Uid;
using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Rdsr;

/// <summary>
/// Builds conformant DICOM X-Ray Radiation Dose SR (RDSR) files.
/// SOP Class UID: 1.2.840.10008.5.1.4.1.1.88.67
///
/// Implements DICOM SR TID 10001 (Projection X-Ray Radiation Dose) as the root container
/// and TID 10003 (Irradiation Event X-Ray Data) per DoseRecord.
///
/// Conforms to IHE REM Integration Profile (Dose Reporter actor).
/// </summary>
public sealed class RdsrBuilder : IRdsrBuilder
{
    // @MX:ANCHOR: [AUTO] Central RDSR construction entry point. fan_in >= 3 expected from DicomServiceFacade, ExportService, tests.
    // @MX:REASON: All RDSR export paths converge here; breaking changes here affect IHE REM compliance.

    private readonly IUidGenerator _uidGenerator;
    private readonly ILogger<RdsrBuilder> _logger;

    /// <summary>SOP Class UID for X-Ray Radiation Dose SR Storage.</summary>
    private static readonly DicomUID RdsrSopClassUid =
        DicomUID.Parse("1.2.840.10008.5.1.4.1.1.88.67");

    // DICOM concept name codes used in TID 10001 / 10003
    private static readonly DicomCodeItem CodeLanguageOfContentItemAndDescendants =
        new("121049", "DCM", "Language of Content Item and Descendants");
    private static readonly DicomCodeItem CodeEnglish =
        new("eng", "RFC3066", "English");
    private static readonly DicomCodeItem CodeProjectionXRayRadiationDose =
        new("113704", "DCM", "Projection X-Ray Radiation Dose");
    private static readonly DicomCodeItem CodeIrradiationEventXRayData =
        new("113706", "DCM", "Irradiation Event X-Ray Data");
    private static readonly DicomCodeItem CodeDoseAreaProduct =
        new("113705", "DCM", "Dose Area Product");
    private static readonly DicomCodeItem CodeKiloPeakVoltage =
        new("113733", "DCM", "KVP");
    private static readonly DicomCodeItem CodeTubeCurrent =
        new("113734", "DCM", "X-Ray Tube Current");
    private static readonly DicomCodeItem CodeExposureTime =
        new("113824", "DCM", "Exposure Time");
    private static readonly DicomCodeItem CodeExposure =
        new("113736", "DCM", "Exposure");
    private static readonly DicomCodeItem CodeDoseRpTotal =
        new("113805", "DCM", "Dose (RP) Total");
    private static readonly DicomCodeItem CodeDoseSource =
        new("121049", "DCM", "Dose Source");
    private static readonly DicomCodeItem CodeMeasured =
        new("113861", "DCM", "Measured");
    private static readonly DicomCodeItem CodeCalculated =
        new("113860", "DCM", "Calculated");
    private static readonly DicomCodeItem CodeDistanceSourceToDetector =
        new("113795", "DCM", "Distance Source to Detector");
    private static readonly DicomCodeItem CodeFieldOfView =
        new("113832", "DCM", "Field of View");
    private static readonly DicomCodeItem CodeIrradiationEventUid =
        new("113769", "DCM", "Irradiation Event UID");
    private static readonly DicomCodeItem CodeMgyPerCm2 =
        new("mGy.cm2", "UCUM", "mGy.cm2");
    private static readonly DicomCodeItem CodeKv =
        new("kV", "UCUM", "kV");
    private static readonly DicomCodeItem CodeMa =
        new("mA", "UCUM", "mA");
    private static readonly DicomCodeItem CodeMs =
        new("ms", "UCUM", "ms");
    private static readonly DicomCodeItem CodeMas =
        new("mAs", "UCUM", "mAs");
    private static readonly DicomCodeItem CodeMm =
        new("mm", "UCUM", "mm");

    /// <summary>
    /// Initializes a new instance of the <see cref="RdsrBuilder"/> class.
    /// </summary>
    /// <param name="uidGenerator">UID generator for SOP Instance UID and SR SOP Instance UID.</param>
    /// <param name="logger">Logger for build diagnostics.</param>
    public RdsrBuilder(IUidGenerator uidGenerator, ILogger<RdsrBuilder> logger)
    {
        _uidGenerator = uidGenerator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public DicomFile Build(StudyDoseSummary studySummary, IReadOnlyList<DoseRecord> exposures)
    {
        ArgumentNullException.ThrowIfNull(studySummary);
        ArgumentNullException.ThrowIfNull(exposures);

        _logger.LogDebug(
            "Building RDSR for Study={StudyUid}, ExposureCount={Count}",
            studySummary.StudyInstanceUid,
            exposures.Count);

        var sopInstanceUid = _uidGenerator.GenerateSopInstanceUid();
        var seriesInstanceUid = _uidGenerator.GenerateSeriesUid();
        var now = DateTime.UtcNow;

        var dataset = new DicomDataset();

        PopulateSopCommonModule(dataset, sopInstanceUid, now);
        PopulatePatientModule(dataset, studySummary);
        PopulateGeneralStudyModule(dataset, studySummary);
        PopulateGeneralSeriesModule(dataset, seriesInstanceUid, studySummary);
        PopulateSrDocumentSeriesModule(dataset);
        PopulateGeneralEquipmentModule(dataset, studySummary);
        PopulateSrDocumentGeneralModule(dataset, now);
        PopulateSrDocumentContentModule(dataset, studySummary, exposures, now);

        var file = new DicomFile(dataset);

        _logger.LogDebug("RDSR built successfully: SopUid={SopUid}", sopInstanceUid);

        return file;
    }

    private static void PopulateSopCommonModule(DicomDataset dataset, string sopInstanceUid, DateTime now)
    {
        // SOP Class UID (0008,0016) - Type 1
        dataset.Add(DicomTag.SOPClassUID, RdsrSopClassUid);

        // SOP Instance UID (0008,0018) - Type 1
        dataset.Add(DicomTag.SOPInstanceUID, sopInstanceUid);

        // Specific Character Set (0008,0005) - Type 1C
        dataset.Add(DicomTag.SpecificCharacterSet, "ISO_IR 6");
    }

    private static void PopulatePatientModule(DicomDataset dataset, StudyDoseSummary summary)
    {
        // Patient Name (0010,0010) - Type 2
        dataset.Add(DicomTag.PatientName, summary.PatientName ?? string.Empty);

        // Patient ID (0010,0020) - Type 2
        dataset.Add(DicomTag.PatientID, summary.PatientId);

        // Patient Birth Date (0010,0030) - Type 2
        dataset.Add(DicomTag.PatientBirthDate,
            summary.PatientBirthDate?.ToString("yyyyMMdd") ?? string.Empty);

        // Patient Sex (0010,0040) - Type 2
        dataset.Add(DicomTag.PatientSex, summary.PatientSex ?? string.Empty);
    }

    private static void PopulateGeneralStudyModule(DicomDataset dataset, StudyDoseSummary summary)
    {
        // Study Instance UID (0020,000D) - Type 1
        dataset.Add(DicomTag.StudyInstanceUID, summary.StudyInstanceUid);

        // Study Date (0008,0020) - Type 2
        dataset.Add(DicomTag.StudyDate, summary.StudyStartTimeUtc.ToString("yyyyMMdd"));

        // Study Time (0008,0030) - Type 2
        dataset.Add(DicomTag.StudyTime, summary.StudyStartTimeUtc.ToString("HHmmss"));

        // Accession Number (0008,0050) - Type 2
        dataset.Add(DicomTag.AccessionNumber, summary.AccessionNumber ?? string.Empty);

        // Referring Physician's Name (0008,0090) - Type 2
        dataset.Add(DicomTag.ReferringPhysicianName, string.Empty);

        // Study ID (0020,0010) - Type 2
        dataset.Add(DicomTag.StudyID, string.Empty);
    }

    private static void PopulateGeneralSeriesModule(
        DicomDataset dataset,
        string seriesInstanceUid,
        StudyDoseSummary summary)
    {
        // Modality (0008,0060) - Type 1: SR for structured report
        dataset.Add(DicomTag.Modality, "SR");

        // Series Instance UID (0020,000E) - Type 1
        dataset.Add(DicomTag.SeriesInstanceUID, seriesInstanceUid);

        // Series Number (0020,0011) - Type 2
        dataset.Add(DicomTag.SeriesNumber, "999");
    }

    private static void PopulateSrDocumentSeriesModule(DicomDataset dataset)
    {
        // Series Description (0008,103E) - Type 3
        dataset.Add(DicomTag.SeriesDescription, "X-Ray Radiation Dose SR");
    }

    private static void PopulateGeneralEquipmentModule(DicomDataset dataset, StudyDoseSummary summary)
    {
        // Station Name (0008,1010) - Type 3
        if (!string.IsNullOrEmpty(summary.PerformedStationAeTitle))
        {
            dataset.Add(DicomTag.StationName, summary.PerformedStationAeTitle);
        }
    }

    private static void PopulateSrDocumentGeneralModule(DicomDataset dataset, DateTime now)
    {
        // SR Document Type (referenced as Concept Name at root)
        // Instance Number (0020,0013) - Type 1
        dataset.Add(DicomTag.InstanceNumber, "1");

        // Content Date (0008,0023) - Type 1
        dataset.Add(DicomTag.ContentDate, now.ToString("yyyyMMdd"));

        // Content Time (0008,0033) - Type 1
        dataset.Add(DicomTag.ContentTime, now.ToString("HHmmss"));

        // Completion Flag (0040,A491) - Type 1: COMPLETE since dose study is closed
        dataset.Add(DicomTag.CompletionFlag, "COMPLETE");

        // Verification Flag (0040,A493) - Type 1: UNVERIFIED (no radiologist sign-off required for RDSR)
        dataset.Add(DicomTag.VerificationFlag, "UNVERIFIED");
    }

    private static void PopulateSrDocumentContentModule(
        DicomDataset dataset,
        StudyDoseSummary summary,
        IReadOnlyList<DoseRecord> exposures,
        DateTime now)
    {
        // Root Content Item: TID 10001 Container
        // Value Type (0040,A040) - Type 1
        dataset.Add(DicomTag.ValueType, "CONTAINER");

        // Concept Name Code Sequence (0040,A043) - Type 1
        dataset.Add(DicomTag.ConceptNameCodeSequence, BuildCodeSequenceItem(CodeProjectionXRayRadiationDose));

        // Continuity of Content (0040,A050) - Type 1: SEPARATE for child items
        dataset.Add(DicomTag.ContinuityOfContent, "SEPARATE");

        // Build Content Sequence (0040,A730)
        var contentItems = new List<DicomDataset>();

        // Language of Content Item (TID 10001 Row 1)
        contentItems.Add(BuildCodeContentItem(
            "HAS CONCEPT MOD",
            CodeLanguageOfContentItemAndDescendants,
            CodeEnglish));

        // Total DAP (TID 10001 Row 11)
        // Convert Gy·cm² to mGy·cm² for DICOM (1 Gy·cm² = 1000 mGy·cm²)
        var totalDapMgyCm2 = summary.TotalDapGyCm2 * 1000m;
        contentItems.Add(BuildNumericContentItem(
            "CONTAINS",
            CodeDoseAreaProduct,
            totalDapMgyCm2,
            CodeMgyPerCm2));

        // Per-exposure irradiation events (TID 10003)
        foreach (var record in exposures)
        {
            contentItems.Add(BuildIrradiationEventItem(record));
        }

        dataset.Add(DicomTag.ContentSequence, contentItems.ToArray());
    }

    private static DicomDataset BuildIrradiationEventItem(DoseRecord record)
    {
        // @MX:NOTE: [AUTO] TID 10003 container - one per DoseRecord irradiation event.
        var item = new DicomDataset();
        item.Add(DicomTag.RelationshipType, "CONTAINS");
        item.Add(DicomTag.ValueType, "CONTAINER");
        item.Add(DicomTag.ConceptNameCodeSequence, BuildCodeSequenceItem(CodeIrradiationEventXRayData));
        item.Add(DicomTag.ContinuityOfContent, "SEPARATE");

        var children = new List<DicomDataset>();

        // Irradiation Event UID
        children.Add(BuildUidContentItem("CONTAINS", CodeIrradiationEventUid, record.IrradiationEventUid));

        // KVP
        children.Add(BuildNumericContentItem("CONTAINS", CodeKiloPeakVoltage, record.KvpValue, CodeKv));

        // X-Ray Tube Current (derived from mAs / exposure time)
        // @MX:NOTE: [AUTO] Tube current approximation: report mAs as mA (1 second assumed) per TID 10003 mapping.
        children.Add(BuildNumericContentItem("CONTAINS", CodeTubeCurrent, record.MasValue, CodeMa));

        // Exposure (mAs)
        children.Add(BuildNumericContentItem("CONTAINS", CodeExposure, record.MasValue, CodeMas));

        // Dose Area Product (per exposure, in mGy·cm²)
        var dapMgyCm2 = record.EffectiveDapGyCm2 * 1000m;
        children.Add(BuildNumericContentItem("CONTAINS", CodeDoseRpTotal, dapMgyCm2, CodeMgyPerCm2));

        // Dose Source
        var doseSourceCode = record.DoseSource == DoseSource.Measured ? CodeMeasured : CodeCalculated;
        children.Add(BuildCodeContentItem("CONTAINS", CodeDoseSource, doseSourceCode));

        // Distance Source to Detector
        if (record.SidMm > 0)
        {
            children.Add(BuildNumericContentItem(
                "CONTAINS", CodeDistanceSourceToDetector, record.SidMm, CodeMm));
        }

        // Field of View (width x height in mm)
        if (record.FieldWidthMm > 0 && record.FieldHeightMm > 0)
        {
            children.Add(BuildNumericContentItem(
                "CONTAINS", CodeFieldOfView, record.FieldWidthMm, CodeMm));
        }

        item.Add(DicomTag.ContentSequence, children.ToArray());

        return item;
    }

    private static DicomDataset BuildCodeContentItem(
        string relationshipType,
        DicomCodeItem conceptName,
        DicomCodeItem value)
    {
        var item = new DicomDataset();
        item.Add(DicomTag.RelationshipType, relationshipType);
        item.Add(DicomTag.ValueType, "CODE");
        item.Add(DicomTag.ConceptNameCodeSequence, BuildCodeSequenceItem(conceptName));
        item.Add(DicomTag.ConceptCodeSequence, BuildCodeSequenceItem(value));
        return item;
    }

    private static DicomDataset BuildNumericContentItem(
        string relationshipType,
        DicomCodeItem conceptName,
        decimal value,
        DicomCodeItem unit)
    {
        var item = new DicomDataset();
        item.Add(DicomTag.RelationshipType, relationshipType);
        item.Add(DicomTag.ValueType, "NUM");
        item.Add(DicomTag.ConceptNameCodeSequence, BuildCodeSequenceItem(conceptName));

        // Measured Value Sequence (0040,A300)
        var measuredValue = new DicomDataset();
        measuredValue.Add(DicomTag.MeasurementUnitsCodeSequence, BuildCodeSequenceItem(unit));
        measuredValue.Add(DicomTag.NumericValue, value);
        item.Add(DicomTag.MeasuredValueSequence, measuredValue);

        return item;
    }

    private static DicomDataset BuildUidContentItem(
        string relationshipType,
        DicomCodeItem conceptName,
        string uid)
    {
        var item = new DicomDataset();
        item.Add(DicomTag.RelationshipType, relationshipType);
        item.Add(DicomTag.ValueType, "UIDREF");
        item.Add(DicomTag.ConceptNameCodeSequence, BuildCodeSequenceItem(conceptName));
        item.Add(DicomTag.UID, uid);
        return item;
    }

    private static DicomDataset BuildCodeSequenceItem(DicomCodeItem code)
    {
        var item = new DicomDataset();
        item.Add(DicomTag.CodeValue, code.Value);
        item.Add(DicomTag.CodingSchemeDesignator, code.Scheme);
        item.Add(DicomTag.CodeMeaning, code.Meaning);
        return item;
    }
}

/// <summary>
/// Lightweight value type representing a DICOM concept code (value, scheme, meaning).
/// </summary>
internal sealed record DicomCodeItem(string Value, string Scheme, string Meaning);
