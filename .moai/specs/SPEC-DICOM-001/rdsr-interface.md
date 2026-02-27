# RDSR Interface Design: DICOM-Dose Module Integration

## Overview

This document specifies the interface contract between the HnVue.Dose module (SPEC-DOSE-001) and the HnVue.Dicom module (SPEC-DICOM-001) for generating and transmitting X-Ray Radiation Dose Structured Reports (RDSR) conforming to DICOM PS 3.16 TID 10001.

---

## 1. Interface Specification

### IRdsrDataProvider

**Namespace**: `HnVue.Dose`

**Purpose**: Provider interface that the DOSE module implements and DICOM module consumes. This allows DICOM to request RDSR data and metadata from completed dose studies without coupling to specific dose calculation internals.

```csharp
/// <summary>
/// Provides RDSR (X-Ray Radiation Dose SR) data to DICOM consumers.
/// Implemented by HnVue.Dose; consumed by HnVue.Dicom for RDSR generation and export.
///
/// Thread Safety: All methods are thread-safe for concurrent calls.
/// </summary>
public interface IRdsrDataProvider
{
    /// <summary>
    /// Retrieves a summary of accumulated dose data for a completed study.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>StudyDoseSummary with cumulative metrics, or null if study not found</returns>
    /// <remarks>
    /// Returned data is immutable and safe for concurrent access.
    /// Returns null if studyInstanceUid has no recorded dose events.
    /// </remarks>
    Task<StudyDoseSummary?> GetStudyDoseSummaryAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all exposure records for a study, in chronological order.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read-only list of DoseRecord for each exposure, empty if none</returns>
    /// <remarks>
    /// Results are sorted by TimestampUtc ascending.
    /// Records include both calculated and measured DAP values (if meter was used).
    /// </remarks>
    Task<IReadOnlyList<DoseRecord>> GetStudyExposureRecordsAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that a study dose session is complete and may be queried for export.
    /// Called by DOSE module when study is closed; DICOM module can then request RDSR generation.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="patientId">Patient ID for this study</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the completion notification</returns>
    /// <remarks>
    /// This method allows DICOM module to be notified without polling.
    /// Implementations may use IObservable&lt;StudyCompletedEvent&gt; as an alternative.
    /// </remarks>
    Task NotifyStudyClosedAsync(
        string studyInstanceUid,
        string patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Observes study closure events for reactive RDSR generation workflow.
    /// </summary>
    /// <remarks>
    /// Returns an observable stream of StudyCompletedEvent objects.
    /// DICOM consumers subscribe to receive notifications when dose studies close.
    /// Alternative to polling GetStudyDoseSummaryAsync().
    /// </remarks>
    IObservable<StudyCompletedEvent> StudyClosed { get; }
}
```

---

## 2. Data Transfer Objects (DTOs)

### StudyDoseSummary

```csharp
/// <summary>
/// Immutable summary of accumulated radiation dose for a completed study.
/// Designed for RDSR SR Document and IHE REM Actor "Dose Reporter" compliance.
/// </summary>
public record StudyDoseSummary
{
    /// <summary>DICOM Study Instance UID (primary key)</summary>
    public required string StudyInstanceUid { get; init; }

    /// <summary>Patient ID associated with this study</summary>
    public required string PatientId { get; init; }

    /// <summary>Patient Name (required for RDSR Patient Module)</summary>
    public string? PatientName { get; init; }

    /// <summary>Patient Birth Date (optional for RDSR)</summary>
    public DateTime? PatientBirthDate { get; init; }

    /// <summary>Patient Sex (M/F/O for RDSR)</summary>
    public string? PatientSex { get; init; }

    /// <summary>Modality code (DX, CR, etc.)</summary>
    public required string Modality { get; init; }

    /// <summary>Examination description or protocol name</summary>
    public string? ExaminationDescription { get; init; }

    /// <summary>Anatomic region code (SNOMED-CT)</summary>
    public string? BodyRegionCode { get; init; }

    /// <summary>Total cumulative DAP in Gy·cm² (sum of all exposures)</summary>
    public decimal TotalDapGyCm2 { get; init; }

    /// <summary>Total number of X-ray exposures in this study</summary>
    public int ExposureCount { get; init; }

    /// <summary>Study start timestamp (UTC)</summary>
    public DateTime StudyStartTimeUtc { get; init; }

    /// <summary>Study end/close timestamp (UTC)</summary>
    public DateTime StudyEndTimeUtc { get; init; }

    /// <summary>Accession Number (optional, from HIS/RIS)</summary>
    public string? AccessionNumber { get; init; }

    /// <summary>Whether cumulative dose exceeded DRL for this examination type</summary>
    public bool DrlExceeded { get; init; }

    /// <summary>Name of configured AE Title that performed this study</summary>
    public string? PerformedStationAeTitle { get; init; }
}
```

### DoseRecord (excerpt from SPEC-DOSE-001)

Key fields for RDSR mapping:

```csharp
public record DoseRecord
{
    public Guid ExposureEventId { get; init; }
    public string IrradiationEventUid { get; init; }  // DICOM UID for RDSR
    public string StudyInstanceUid { get; init; }
    public string PatientId { get; init; }
    public DateTime TimestampUtc { get; init; }
    public decimal KvpValue { get; init; }
    public decimal MasValue { get; init; }
    public string FilterMaterial { get; init; }
    public decimal FilterThicknessMm { get; init; }
    public decimal SidMm { get; init; }
    public decimal FieldWidthMm { get; init; }
    public decimal FieldHeightMm { get; init; }
    public decimal CalculatedDapGyCm2 { get; init; }
    public decimal? MeasuredDapGyCm2 { get; init; }
    public DoseSource DoseSource { get; init; }  // Calculated | Measured
    public string? AcquisitionProtocol { get; init; }
    public string? BodyRegionCode { get; init; }
    public bool DrlExceedance { get; init; }
}

public enum DoseSource
{
    Calculated,
    Measured
}
```

### StudyCompletedEvent

```csharp
/// <summary>
/// Event notification when a dose study is closed and ready for RDSR export.
/// </summary>
public record StudyCompletedEvent
{
    /// <summary>DICOM Study Instance UID</summary>
    public required string StudyInstanceUid { get; init; }

    /// <summary>Patient ID for this study</summary>
    public required string PatientId { get; init; }

    /// <summary>Timestamp when study was closed (UTC)</summary>
    public DateTime ClosedAtUtc { get; init; }

    /// <summary>Number of exposures in this study</summary>
    public int ExposureCount { get; init; }

    /// <summary>Total accumulated DAP for this study</summary>
    public decimal TotalDapGyCm2 { get; init; }
}
```

---

## 3. RDSR Generation Workflow (DICOM Module)

### IRdsrBuilder Integration

The HnVue.Dicom module already defines `IRdsrBuilder` (per SPEC-DICOM-001 Section 4.7):

```csharp
public interface IRdsrBuilder
{
    /// <summary>
    /// Builds a complete DICOM RDSR (Structured Report) dataset.
    /// </summary>
    /// <param name="studySummary">Accumulated dose summary from DOSE module</param>
    /// <param name="exposures">Per-exposure records from DOSE module</param>
    /// <returns>Complete DicomDataset ready for C-STORE transmission</returns>
    DicomDataset Build(StudyDoseSummary studySummary, IReadOnlyList<DoseRecord> exposures);
}
```

### DicomServiceFacade Extension

The DICOM module's `DicomServiceFacade` shall support RDSR export:

```csharp
public class DicomServiceFacade
{
    private readonly IRdsrBuilder _rdsrBuilder;
    private readonly IRdsrDataProvider _doseProvider;

    /// <summary>
    /// Generate and queue an RDSR for export to PACS.
    /// Called when a dose study closes or operator triggers manual export.
    /// </summary>
    public async Task<RdsrExportResult> ExportStudyDoseAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var doseSummary = await _doseProvider.GetStudyDoseSummaryAsync(
                studyInstanceUid, cancellationToken);

            if (doseSummary == null)
            {
                return new RdsrExportResult
                {
                    Success = false,
                    ErrorMessage = "No dose data found for study"
                };
            }

            var exposures = await _doseProvider.GetStudyExposureRecordsAsync(
                studyInstanceUid, cancellationToken);

            var rdsrDataset = _rdsrBuilder.Build(doseSummary, exposures);
            var storeResult = await this.StoreAsync(rdsrDataset, cancellationToken);

            return new RdsrExportResult
            {
                Success = storeResult.IsSuccess,
                RdsrSopInstanceUid = rdsrDataset.GetString(DicomTag.SOPInstanceUID),
                ErrorMessage = storeResult.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RDSR export failed for study {StudyUid}", studyInstanceUid);
            return new RdsrExportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

public record RdsrExportResult
{
    public bool Success { get; init; }
    public string? RdsrSopInstanceUid { get; init; }
    public string? ErrorMessage { get; init; }
}
```

---

## 4. Dependency Injection Configuration

```csharp
// In Program.cs or DI configuration
services.AddHnVueDoseServices();  // Registers IDoseCalculator, IRdsrDataProvider, etc.
services.AddHnVueDicomServices(); // Registers DicomServiceFacade, IRdsrBuilder, etc.

// Dependency injection ensures:
// - DOSE module is initialized before DICOM module
// - IRdsrDataProvider is available for injection into DicomServiceFacade
// - Both modules use shared configuration for PACS destinations
```

---

## 5. RDSR TID 10001 / 10003 Mapping Reference

### TID 10001 (Projection X-Ray Radiation Dose) Mapping

| DICOM Concept Name | DICOM Tag | Source from StudyDoseSummary | Mapped Value |
|---|---|---|---|
| Language of Content | (121070, DCM) | System config | EN |
| Dose Area Product | (113705, DCM) | TotalDapGyCm2 | Gy·cm² |
| Number of Exposures | Derived | ExposureCount | Count value |
| Study Date | (0008,0020) | StudyStartTimeUtc | YYYYMMDD |
| Study Time | (0008,0030) | StudyStartTimeUtc | HHMMSS |

### TID 10003 (Irradiation Event X-Ray Data) Mapping per DoseRecord

| DICOM Concept | DICOM Tag | Source from DoseRecord | Unit |
|---|---|---|---|
| Irradiation Event UID | (0008,0018) | IrradiationEventUid | UI |
| KVP | (0018,0060) | KvpValue | kV |
| X-Ray Tube Current | (0018,1405) | MasValue / ms | mA |
| Exposure Time | (0018,1150) | Derived from MasValue | ms |
| Exposure | (0018,1152) | MasValue | mAs |
| Dose (RP) Total | (113805, DCM) | CalculatedDapGyCm2 or MeasuredDapGyCm2 | Gy·cm² |
| Dose Source | (121049, DCM) | DoseSource enum | Measured or Calculated |
| Distance Source to Detector | (0018,1110) | SidMm | mm |
| Field of View | Derived | FieldWidthMm × FieldHeightMm | mm × mm |
| Filter Material | Coded | FilterMaterial | CID 10006 |

---

## 6. DICOM UID Root Status

### Current Implementation

**File**: `/mnt/work/workspace-github/hnvue-console/src/HnVue.Dicom/Configuration/DicomServiceOptions.cs` (Line 21)

```csharp
public string UidRoot { get; set; } = "2.25";
```

**File**: `/mnt/work/workspace-github/hnvue-console/src/HnVue.Dicom/Uid/UidGenerator.cs` (Lines 42, 44, 55-57)

```csharp
private const string DefaultTestRoot = "2.25";  // UUID-based root for testing
private readonly string _orgUidRoot;

public UidGenerator(string? orgUidRoot = null, string? deviceSerial = null)
{
    _orgUidRoot = string.IsNullOrWhiteSpace(orgUidRoot)
        ? DefaultTestRoot
        : orgUidRoot.Trim();
}
```

### Status Assessment

| Aspect | Current State | SPEC Requirement | Action Required |
|---|---|---|---|
| UID Root Configuration | Hardcoded to "2.25" (UUID-based test root) | "Configurable at deployment time; not hardcoded" (FR-DICOM-11) | CRITICAL: Make configurable before production |
| Default Test Root | "2.25" (standard UUID-derived OID) | Not specified | ACCEPTABLE: Good default for development |
| Runtime Configurability | DicomServiceOptions.UidRoot property exists | Required per FR-DICOM-11 | IN PLACE: IOptions pattern works correctly |
| Production OID | Not assigned yet | "Registered UID root prefix assigned to the organization" (A-04) | PENDING: Obtain from project lead before Go-Live |

### SPEC References

- **FR-DICOM-11** (Section 3.1): UID root must be configurable at deployment time, not hardcoded
- **Assumption A-04** (Section 2): "DICOM UID generation for RDSR SOP Instance UID follows the organization root OID"
- **Open Question OQ-01** (Section 6): "What is the registered DICOM UID root for the organization? Target Resolution: Before implementation start"

### Recommendation

1. Obtain organizational registered DICOM UID root from project lead / system architect
2. Update DicomServiceOptions default to organizational OID (e.g., "1.2.840.113619.4...")
3. Document in DICOM Conformance Statement (Section 5 Configuration)
4. Testing: Unit tests use "2.25"; integration tests use test-marked OID

---

## 7. Implementation Checklist

- [ ] IRdsrDataProvider interface defined in HnVue.Dose
- [ ] StudyDoseSummary, DoseRecord, StudyCompletedEvent DTOs implemented
- [ ] DicomServiceFacade.ExportStudyDoseAsync() implemented
- [ ] IRdsrBuilder.Build() tested with sample dose data
- [ ] RDSR dataset passes DVTK validation (NFR-QUAL-01)
- [ ] TID 10001 / 10003 mapping verified against DICOM PS 3.16
- [ ] IHE REM "Dose Reporter" attribute mapping verified
- [ ] Dependency injection configured in HnVue.Core
- [ ] Organizational UID root obtained and documented
- [ ] RDSR C-STORE integration test passes with Orthanc SCP
- [ ] Error handling contract tested (missing study, build failures)

---

**Document Version**: 1.0
**Date**: 2026-02-27
**Created by**: Research Team
**Status**: Ready for review and interface implementation
