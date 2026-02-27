# SPEC-DOSE-001: Implementation Plan

## Metadata

| Field    | Value                                              |
|----------|----------------------------------------------------|
| SPEC ID  | SPEC-DOSE-001                                      |
| Title    | HnVue Radiation Dose Management                    |
| Package  | src/HnVue.Dose/                                    |
| Language | C# 12 / .NET 8 LTS                                |
| Safety   | IEC 62304 Class B                                  |

---

## 1. Milestones

### Primary Goal: Core Dose Acquisition and Calculation

Deliver the mandatory dose acquisition and calculation components sufficient for per-exposure DAP recording with atomic persistence.

Components in scope:
- `DoseModelParameters` - HVG tube model parameters (k_factor, n exponent)
- `DapCalculator` - DAP calculation engine (K_air × A_field formula)
- `CalibrationManager` - Calibration coefficient management (signed, tamper-evident)
- `ExposureParameterReceiver` - HVG parameter acquisition via HAL `IDoseMonitor`
- `DetectorGeometryProvider` - Field area and SID data via HAL `IDetector`
- `DapMeterInterface` - External DAP meter adapter (optional, null-safe)
- `DoseRecord` domain model - Per-exposure dose record schema
- `DoseRecordRepository` - Atomic persistence with WAL (write-ahead log)
- `StudyDoseAccumulator` - Cumulative dose per study session
- `AuditTrailWriter` - Immutable SHA-256 hash-chain audit trail

Acceptance Gate: DAP calculation within 1 second of exposure completion, dose record atomically persisted, audit trail hash chain verified, all unit tests green at 90%+ coverage.

### Secondary Goal: RDSR Generation and DICOM Export

Deliver the RDSR builder and PACS export pipeline, interfacing with SPEC-DICOM-001 D-08 (RdsrBuilder) and D-02 (StorageScu).

Components in scope:
- `RdsrTemplateMapper` - TID 10001 / TID 10003 DICOM SR template mapping logic
- `RdsrBuilder` - DICOM X-Ray Radiation Dose SR document builder (IRdsrBuilder interface)
- `RdsrExporter` - DICOM C-STORE transmission with export queue and retry
- `DoseDisplayNotifier` - GUI notification publisher (IObservable stream)
- `DrlConfiguration` - DRL thresholds configuration per examination type
- `DrlComparer` - DRL comparison logic and alert trigger

Acceptance Gate: RDSR DICOM dataset passes DVTK validation (zero Critical violations), C-STORE succeeds to Orthanc test SCP, GUI receives dose update within 1 second, DRL alert raised when threshold exceeded.

### Final Goal: Reporting, Quality, and Safety Documentation

Deliver the printable dose report generator and IEC 62304 Class B documentation artifacts.

Components in scope:
- `DoseReportGenerator` - Printable PDF dose report builder
- Traceability matrix (FR-DOSE-01 through NFR-DOSE-04 to code to tests)
- Accuracy validation results (±5% against calibrated DAP meter reference)
- Full unit test suite with 90%+ line coverage for `src/HnVue.Dose/`

Acceptance Gate: All FR-DOSE-01 through FR-DOSE-08 and NFR items verified. PDF report renders correctly with site header. Accuracy within ±5% confirmed by calibration test fixture. IEC 62304 traceability complete.

### Optional Goal: Enhancements

Address open questions and extended capabilities:
- IHE REM dose registry endpoint integration (when registry URL configured)
- MFDS-specific dose report format extensions
- Multi-tube calibration profiles

---

## 2. Technical Approach

### 2.1 Framework and Existing HAL Assets

The implementation is a **greenfield C# class library** (`HnVue.Dose.dll`) with no dependency on the HnVue UI layer.

**Available HAL interfaces (C++ interop via IPC):**
- `IDoseMonitor.h` (105 LOC, complete) — `GetCurrentDose()`, `GetDap()`, `Reset()`, callback registration
- `MockDoseMonitor.h` (complete) — Test double for unit tests without hardware
- `IDetector.h` (complete) — Provides field area dimensions and SID
- `MockDetector.h` (complete) — Test double for detector geometry

`ExposureParameterReceiver` wraps `IDoseMonitor` via the existing IPC bridge, eliminating any direct P/Invoke dependency. `DetectorGeometryProvider` similarly wraps `IDetector`.

### 2.2 Dependency Injection Integration

All dose services are registered via `IServiceCollection` extension methods:

```
services.AddHnVueDose(configuration.GetSection("Dose"));
```

Configuration is bound to `DoseServiceOptions` via the IOptions pattern. All major components implement interfaces (`IDoseCalculator`, `IDoseRecordRepository`, `IRdsrBuilder`, `IDoseDisplayNotifier`) enabling Moq-based test doubles.

### 2.3 DAP Calculation Engine

The `DapCalculator` implements the two-path algorithm:

**Path 1 (Primary — no external meter):**
```
K_air_base = k_factor × (kVp^n) × mAs / SID²
K_air = K_air_base × C_cal
A_field = width_cm × height_cm
DAP = K_air × A_field
```

**Path 2 (External DAP meter present):**
- Meter-measured DAP takes precedence for display and RDSR
- Calculated DAP retained for audit
- DoseSource set to `Measured` (CID 10022)

Calibration coefficients (`k_factor`, `n`, `C_cal`) are loaded from a signed configuration file by `CalibrationManager`. All updates are audit-logged with before/after values.

### 2.4 Atomic Persistence Strategy

`DoseRecordRepository` uses NTFS atomic rename (write-ahead log pattern):
1. Write dose record to `{ExposureEventId}.tmp`
2. Atomic rename to `{ExposureEventId}.json`
3. Signal completion only after rename succeeds

This guarantees no partial records on application crash immediately post-exposure. On startup, any `.tmp` files are either completed or discarded.

### 2.5 SHA-256 Hash Chain Audit Trail

`AuditTrailWriter` maintains an append-only audit trail where each entry includes `PreviousRecordHash`:

- **First record**: `PreviousRecordHash` = well-known initialization vector (SHA-256 of empty string)
- **Subsequent records**: `PreviousRecordHash` = SHA-256 of the serialized previous record
- **Verification**: On-demand chain walk detects any record modification, insertion, or deletion

Audit records are never modified or deleted. The verification utility reports the first broken record in the chain.

### 2.6 RDSR Construction (TID 10001 / TID 10003)

`RdsrTemplateMapper` maps each `DoseRecord` to DICOM SR content items per DICOM PS 3.16:

- **TID 10001** (root): Study-level container, language, cumulative DAP total
- **TID 10002**: Accumulated X-Ray Dose Data — total DAP, exposure count
- **TID 10003** (per exposure): Irradiation Event UID, kVp, mAs, filter, SID, field dimensions, DAP, DoseSource code

`RdsrBuilder` assembles a `DicomDataset` (fo-dicom 5.x) ready for C-STORE. Mandatory items are validated before the dataset is returned; `RdsrBuildException` is thrown if validation fails.

### 2.7 RDSR Export Queue

`RdsrExporter` maintains a persistent export queue (file-system backed) ensuring no RDSR is deleted until a successful C-STORE-RSP (Status 0x0000H) is received. Retry policy:
- Maximum 3 retries
- Exponential back-off: `nextRetryAt = lastAttemptAt + (baseMs × 2^attemptCount)`
- Configurable via `Dose.ExportRetryCount` and `Dose.ExportRetryBaseMs`

### 2.8 Background Thread Isolation

Per NFR-DOSE-01-B, all dose processing executes on a dedicated `Channel<ExposureEvent>`-based background consumer. The primary imaging workflow thread returns immediately after publishing the exposure event. The background consumer:
1. Calls `DapCalculator.Calculate()`
2. Calls `DoseRecordRepository.PersistAsync()`
3. Updates `StudyDoseAccumulator`
4. Evaluates `DrlComparer`
5. Publishes to `DoseDisplayNotifier`
6. Writes to `AuditTrailWriter`

---

## 3. Risks and Mitigations

| Risk                                                          | Likelihood | Impact | Mitigation                                                                 |
|---------------------------------------------------------------|------------|--------|----------------------------------------------------------------------------|
| k_factor calibration data not available before implementation | Medium     | High   | Use placeholder coefficients from IEC 60601-1-3 reference tables; replace before release |
| IPC bridge latency causes HVG parameter delivery > 200 ms    | Low        | High   | Validate with HAL mock timing test; if exceeded, add parameter pre-fetch buffer |
| fo-dicom 5.x DICOM SR content item API underdocumented       | Medium     | Medium | Prototype TID 10001 mapping against DVTK early; resolve gaps before full build |
| NTFS atomic rename fails on network drive deployment          | Low        | Medium | Detect non-NTFS target at startup; warn operator; fallback to SQLite WAL   |
| SHA-256 hash chain performance under high exposure rate       | Low        | Low    | Benchmark at 10 exposures/min; chain write is append-only and off critical path |
| PACS does not accept RDSR SOP Class UID 1.2.840.10008.5.1.4.1.1.88.67 | Medium | Medium | Verify PACS conformance statement before integration; document as deployment prerequisite |
| DRL configuration absent at first deployment                  | Medium     | Low    | Suppress DRL comparison without error; alert operator to configure DRL table |

---

## 4. Architecture Design Direction

### 4.1 Separation of Concerns

- `src/HnVue.Dose/` — Dose domain logic only; no direct UI dependency
- `src/HnVue.Dicom/` — RDSR C-STORE transport (SPEC-DICOM-001 D-02, D-08)
- `tests/HnVue.Dose.Tests/` — Unit tests with HAL mocks (`MockDoseMonitor`, `MockDetector`)
- `tests/HnVue.Dose.IntegrationTests/` — End-to-end: exposure event → RDSR → Orthanc C-STORE

### 4.2 Interface-First Design

All major components implement interfaces for Moq-based injection:

| Interface               | Implementation              | Mock Usage                        |
|-------------------------|-----------------------------|-----------------------------------|
| `IDoseCalculator`       | `DapCalculator`             | Unit tests for accumulator, DRL   |
| `IDoseRecordRepository` | `DoseRecordRepository`      | Unit tests for calculator, notifier |
| `IRdsrBuilder`          | `RdsrBuilder`               | Unit tests for exporter           |
| `IDoseDisplayNotifier`  | `DoseDisplayNotifier`       | Unit tests for accumulator output |

### 4.3 Interface with SPEC-DICOM-001

The `RdsrExporter` depends on `IStorageScu` (SPEC-DICOM-001 D-02) for C-STORE transmission. The RDSR `DicomDataset` is built by `IRdsrBuilder` and handed to `IStorageScu.StoreAsync()`. This interface boundary is defined as a shared contract between DOSE and DICOM teams.

**RDSR-DICOM integration gap (from antigravity-plan.md):** DO-10/DO-11 must be co-designed with DICOM D-08. The `IRdsrBuilder` interface in `HnVue.Dose/Interfaces/` is the authoritative contract; DICOM-side implementation (D-08 `RdsrBuilder`) is out of scope for SPEC-DOSE-001.

### 4.4 Observability

All dose operations emit structured log events via `Microsoft.Extensions.Logging`:
- Exposure received / DAP calculated (Info)
- Persistence success / failure (Info / Error)
- RDSR generated / exported (Info)
- DRL threshold exceeded (Warning)
- Export retry enqueued / exhausted (Warning / Error)
- Audit trail hash chain break detected (Critical)
- PHI excluded from all log levels (Patient ID and Name never logged in plain text)

---

## 5. Dependency Order

1. Confirm HAL `IDoseMonitor` IPC bridge is operational (prerequisite: SPEC-IPC-001 complete — confirmed)
2. Confirm HAL `IDetector` interface availability for `DetectorGeometryProvider`
3. Implement `DoseModelParameters`, `CalibrationManager` (calibration foundation)
4. Implement `DapCalculator`, `DapMeterInterface` (calculation engine)
5. Implement `ExposureParameterReceiver`, `DetectorGeometryProvider` (HAL adapters)
6. Implement `DoseRecord`, `DoseRecordRepository` (atomic persistence)
7. Implement `AuditTrailWriter` (hash chain)
8. Implement `StudyDoseAccumulator`, `DrlConfiguration`, `DrlComparer` (study tracking)
9. Implement `DoseDisplayNotifier` (GUI observable stream)
10. Implement `RdsrTemplateMapper`, `RdsrBuilder` (DICOM SR construction)
11. Implement `RdsrExporter` (C-STORE integration — depends on SPEC-DICOM-001 D-02)
12. Integration test: exposure event → dose record → RDSR → Orthanc C-STORE
13. Implement `DoseReportGenerator` (PDF report)
14. Accuracy validation test: calculated DAP ±5% of `MockDoseMonitor` reference DAP
15. Complete IEC 62304 traceability matrix

---

## 6. File Structure

```
src/HnVue.Dose/
├── Calculation/
│   ├── DapCalculator.cs               # IDoseCalculator implementation
│   ├── CalibrationManager.cs          # Calibration coefficient management
│   └── DoseModelParameters.cs         # k_factor, n exponent, C_cal
├── Acquisition/
│   ├── ExposureParameterReceiver.cs   # IDoseMonitor HAL adapter
│   ├── DapMeterInterface.cs           # External DAP meter adapter (optional)
│   └── DetectorGeometryProvider.cs    # IDetector HAL adapter (field area, SID)
├── Recording/
│   ├── DoseRecord.cs                  # Domain model: per-exposure dose record
│   ├── StudyDoseAccumulator.cs        # Cumulative DAP per study
│   ├── DoseRecordRepository.cs        # IDoseRecordRepository (NTFS WAL)
│   └── AuditTrailWriter.cs            # SHA-256 hash chain audit trail
├── RDSR/
│   ├── RdsrBuilder.cs                 # IRdsrBuilder: DICOM SR dataset builder
│   ├── RdsrTemplateMapper.cs          # TID 10001/10003 content item mapping
│   └── RdsrExporter.cs                # C-STORE with export queue and retry
├── Alerting/
│   ├── DrlConfiguration.cs            # DRL thresholds per examination type
│   └── DrlComparer.cs                 # DRL comparison and alert trigger
├── Display/
│   └── DoseDisplayNotifier.cs         # IObservable<DoseDisplayUpdate> publisher
├── Reporting/
│   └── DoseReportGenerator.cs         # PDF dose report builder
└── Interfaces/
    ├── IDoseCalculator.cs
    ├── IDoseRecordRepository.cs
    ├── IRdsrBuilder.cs
    └── IDoseDisplayNotifier.cs

tests/HnVue.Dose.Tests/
├── Calculation/
│   ├── DapCalculatorTests.cs          # Formula correctness, boundary conditions
│   └── CalibrationManagerTests.cs     # Tamper detection, update audit
├── Recording/
│   ├── DoseRecordRepositoryTests.cs   # Atomic write, crash recovery
│   ├── StudyDoseAccumulatorTests.cs   # Cumulative total, patient isolation
│   └── AuditTrailWriterTests.cs       # Hash chain integrity, immutability
├── RDSR/
│   ├── RdsrBuilderTests.cs            # TID mandatory items, coded sequences
│   ├── RdsrTemplateMapperTests.cs     # Field mapping correctness
│   └── RdsrExporterTests.cs           # Export queue, retry, C-STORE mock
├── Alerting/
│   └── DrlComparerTests.cs            # Threshold triggers, no DRL suppression
└── Integration/
    └── DoseEndToEndTests.cs           # Exposure → RDSR → Orthanc C-STORE
```

---

## 7. Test Strategy

### 7.1 Coverage Target

Minimum **90% line coverage** for `src/HnVue.Dose/` (elevated above standard 85% due to IEC 62304 Class B dose accuracy requirements and patient safety implications).

### 7.2 Unit Test Approach

All unit tests use HAL mocks (`MockDoseMonitor`, `MockDetector`) and Moq-based interface doubles. No hardware dependency in unit tests.

Key test areas:
- **DapCalculator**: Formula correctness with reference inputs, SID² inverse-square law validation, calibration coefficient application, meter vs. calculated path selection
- **DoseRecordRepository**: Atomic write verification (partial `.tmp` file left after kill — verify recovery on next startup), concurrent write safety
- **AuditTrailWriter**: Hash chain continuity after 100+ records, chain break detection when record modified, first-record initialization vector validation
- **StudyDoseAccumulator**: Patient isolation (records from different patients never merged), holding buffer when no study open, cumulative DAP arithmetic precision
- **DrlComparer**: Alert raised when DAP exceeds threshold, no alert when DRL not configured, advisory-only (does not block exposure workflow)
- **RdsrBuilder**: All TID 10001 mandatory content items present, TID 10003 per-exposure mapping correctness, `RdsrBuildException` on missing mandatory fields

### 7.3 Accuracy Validation Test

A dedicated accuracy test fixture uses `MockDoseMonitor.GetDap()` as the reference DAP and verifies:

```
|CalculatedDap - ReferenceDap| / ReferenceDap <= 0.05
```

across a sweep of (kVp, mAs, SID, field size) combinations defined in the calibration test matrix.

### 7.4 Integration Tests

End-to-end test flow against Orthanc Docker SCP:
1. Publish a synthetic exposure event with known parameters
2. Verify dose record persisted atomically
3. Trigger study close
4. Verify RDSR generated with correct TID 10001/10003 mapping
5. Verify RDSR received and stored by Orthanc via C-STORE
6. Verify audit trail contains all expected event entries with unbroken hash chain

### 7.5 Crash Recovery Test

Chaos test: kill the application process immediately after `DapCalculator.Calculate()` returns but before `DoseRecordRepository.PersistAsync()` signals completion. On restart, verify no dose record is lost and no corrupt `.tmp` file remains.
