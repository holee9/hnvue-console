# SPEC-DOSE-001: Acceptance Criteria

## Metadata

| Field    | Value                                              |
|----------|----------------------------------------------------|
| SPEC ID  | SPEC-DOSE-001                                      |
| Title    | HnVue Radiation Dose Management                    |
| Format   | Given-When-Then (Gherkin-style)                    |

---

## Definition of Done

A requirement is considered complete when:
1. All acceptance scenarios for that requirement pass in the automated test suite
2. The implementation is traceable to this SPEC in the traceability matrix
3. IEC 62304 Class B unit test documentation exists for the affected component
4. No PHI (Patient ID, Patient Name) appears in plain text in any log line at INFO level
5. Dose calculation accuracy is within ±5% of the reference value across the calibration test matrix
6. Line coverage for `src/HnVue.Dose/` is at or above 90%

---

## AC-01: DAP Calculation and Recording Per Exposure (FR-DOSE-01)

### Scenario 1.1 - Calculated DAP Persisted Within 1 Second

```
Given a configured HnVue.Dose system with valid calibration coefficients (k_factor, n, C_cal)
  And an active study session with a Patient ID
When an X-ray exposure completes and HVG parameters (kVp=80, mAs=5, filtration=Al 2.5mm)
  And detector geometry (field 30x30 cm, SID=110 cm) are received
Then DapCalculator.Calculate() returns a DoseCalculationResult within 200 ms
  And DoseRecordRepository.PersistAsync() completes atomically within 1 second of exposure completion
  And the persisted DoseRecord contains: KvpValue, MasValue, FilterMaterial, FilterThicknessMm, SidMm, FieldWidthMm, FieldHeightMm, CalculatedDapGyCm2
  And DoseSource is set to Calculated
  And an audit trail entry with EventType=ExposureRecorded and Outcome=Success is written
```

### Scenario 1.2 - External DAP Meter Reading Takes Precedence

```
Given an external DAP meter connected and returning a valid reading of 1.25 Gy·cm²
  And the HVG-parameter-based calculation yields 1.20 Gy·cm²
When DapCalculator.Calculate() is called with meterDapGyCm2=1.25
Then DoseCalculationResult.MeasuredDapGyCm2 is 1.25
  And DoseCalculationResult.CalculatedDapGyCm2 is 1.20
  And DoseCalculationResult.DoseSource is Measured
  And the persisted DoseRecord contains both values
  And the RDSR Dose Source coded value is set to Measured (CID 10022)
```

### Scenario 1.3 - System Operates Without External DAP Meter

```
Given no external DAP meter is connected
  And IDapMeterInterface returns null for the meter reading
When an exposure event is processed
Then DapCalculator.Calculate() uses HVG parameters and detector geometry exclusively
  And DoseCalculationResult.MeasuredDapGyCm2 is null
  And DoseCalculationResult.DoseSource is Calculated
  And the system does not degrade or raise an error
```

### Scenario 1.4 - DAP Record Not Lost When Patient Context Unavailable

```
Given no active study session is open at the time of exposure
When an X-ray exposure completes and DAP calculation succeeds
Then the exposure record is stored to the holding buffer
  And the record is available for association with the next opened study
  And no DoseRecord is discarded
```

### Scenario 1.5 - Partial HVG Parameters Handled Gracefully

```
Given HVG delivers kVp=80 but mAs is not available (not received within timeout)
When an exposure event is processed
Then the DoseRecord is persisted with KvpValue=80 and MasValue marked with absence indicator
  And an audit trail entry with EventType=ExposureRecorded and Outcome=Success (partial) is written
  And the imaging workflow is not blocked
```

---

## AC-02: RDSR Generation (FR-DOSE-02)

### Scenario 2.1 - RDSR Produced Per Study With All Mandatory TID 10001 Items

```
Given a completed study session with 3 exposure events recorded
  And all exposures have valid DoseRecords with PatientId, StudyInstanceUid, kVp, mAs, DAP
When the study is closed and RdsrBuilder.Build() is called with the study summary and exposure list
Then the returned DicomDataset contains a TID 10001 root container
  And TID 10002 Accumulated X-Ray Dose Data is present with total DAP and exposure count = 3
  And 3 TID 10003 Irradiation Event containers are present, one per exposure
  And each TID 10003 contains: Irradiation Event UID, KVP, Exposure, DAP, SID, Field of View, Filter Type, Dose Source
  And DVTK validation of the dataset produces zero Critical violations and zero Error violations
```

### Scenario 2.2 - RDSR Build Fails on Missing Mandatory Items

```
Given a DoseRecord with StudyInstanceUid missing (null)
When RdsrBuilder.Build() is called with this record
Then RdsrBuildException is thrown with a descriptive message identifying the missing field
  And no partial DicomDataset is returned
  And an audit trail entry with EventType=RdsrGenerated and Outcome=Failure is written
```

### Scenario 2.3 - RDSR Includes IHE REM Mandatory Attributes

```
Given a complete study with Patient ID, Patient Name, Accession Number, and Study Instance UID
When the RDSR is generated for the study
Then the DicomDataset includes the General Study Module (Study Instance UID, Accession Number)
  And the Patient Module (Patient Name, Patient ID, Patient Birthdate, Patient Sex)
  And the SR Document Series Module (Series Instance UID, Series Number)
  And the Dose SR Document Module (Content Date, Content Time)
```

---

## AC-03: Cumulative Dose Tracking Per Patient Per Study (FR-DOSE-03)

### Scenario 3.1 - Cumulative DAP Updated After Each Exposure

```
Given an active study session for Patient A (PatientId=PA001)
  And 2 prior exposures with DAP values of 0.50 Gy·cm² and 0.30 Gy·cm²
When a third exposure with DAP=0.20 Gy·cm² is recorded
Then StudyDoseAccumulator.GetCumulativeDap() returns 1.00 Gy·cm²
  And the exposure count for the study is 3
```

### Scenario 3.2 - Study Session Associates All Exposures to One Patient

```
Given a study session opened with PatientId=PA001 and StudyInstanceUid=UID001
When 5 exposures are recorded during the session
Then all 5 DoseRecords have PatientId=PA001 and StudyInstanceUid=UID001
  And no exposure record is associated with a different patient
```

### Scenario 3.3 - Exposures From Different Patients Not Merged

```
Given a completed study for Patient A was closed
  And a new study is opened for Patient B
When Patient B receives 2 exposures
Then StudyDoseAccumulator for Patient B contains only 2 exposure records
  And the cumulative DAP for Patient B does not include any DAP values from Patient A's study
```

### Scenario 3.4 - Holding Buffer Retains Pre-Session Exposures

```
Given no study session is open
When an exposure event occurs and is processed to the holding buffer
  And a new study is subsequently opened for Patient A
Then the holding buffer record is available for manual association with Patient A's study
  And the holding buffer is not automatically discarded
```

---

## AC-04: Dose Display on GUI During and After Exposure (FR-DOSE-04)

### Scenario 4.1 - GUI Receives Dose Update Within 1 Second

```
Given the GUI layer has subscribed to IDoseDisplayNotifier.DoseUpdates (IObservable)
When an exposure completes and DAP calculation finishes
Then DoseDisplayNotifier.Publish() is called with a DoseDisplayUpdate
  And the DoseDisplayUpdate is received by the subscriber within 1 second of exposure completion
  And DoseDisplayUpdate.CurrentExposureDapGyCm2 matches the calculated DAP for the exposure
  And DoseDisplayUpdate.CumulativeStudyDapGyCm2 matches the updated study cumulative DAP
```

### Scenario 4.2 - Running Cumulative DAP Updated After Each Exposure in Sequence

```
Given a study session in progress with cumulative DAP = 0.80 Gy·cm²
When a new exposure with DAP=0.25 Gy·cm² completes
Then the GUI receives a DoseDisplayUpdate with CumulativeStudyDapGyCm2 = 1.05 Gy·cm²
```

### Scenario 4.3 - Dose Values Displayed in SI Units With Minimum 2 Decimal Places

```
Given the system configuration sets DisplayUnits=GySquareCm and DisplayDecimalPlaces=2
When a DoseDisplayUpdate is published with CalculatedDapGyCm2 = 1.23456
Then the display value is formatted as "1.23 Gy·cm²"
  And no fewer than 2 decimal places are shown
```

### Scenario 4.4 - No Active Study Clears Dose Panel

```
Given no active study session is open
When the GUI subscribes to DoseUpdates
Then the published update indicates no active patient dose is being tracked
  And the dose panel displays a cleared state
```

---

## AC-05: DRL Comparison and Alerting (FR-DOSE-05)

### Scenario 5.1 - Alert Raised When Cumulative DAP Exceeds Study DRL

```
Given DrlConfiguration contains a DRL of 2.00 Gy·cm² for examination type "Chest PA"
  And the current study cumulative DAP has reached 1.90 Gy·cm²
When a new exposure with DAP=0.15 Gy·cm² brings the cumulative total to 2.05 Gy·cm²
Then DrlComparer triggers a dose alert notification to the GUI layer
  And an audit trail entry with EventType=DrlExceeded is written
```

### Scenario 5.2 - Single-Exposure DRL Exceedance Logged

```
Given DrlConfiguration contains a single-exposure DRL threshold of 0.80 Gy·cm² for "Chest PA"
  And a single exposure results in a calculated DAP of 0.85 Gy·cm²
When the exposure is recorded
Then an audit trail entry with EventType=DrlExceeded is written for the single exposure
  And the exposure workflow is not blocked or delayed
```

### Scenario 5.3 - DRL Comparison Suppressed When Not Configured

```
Given no DRL is configured for examination type "Hand PA"
  And the current study examination type is "Hand PA"
When any exposure is recorded during the study
Then DrlComparer does not perform any comparison or trigger any alert
  And no error or warning is generated
```

### Scenario 5.4 - Exposure Workflow Never Blocked by DRL Alert

```
Given a cumulative study DAP that has already exceeded the DRL
When the operator initiates another X-ray exposure
Then the exposure proceeds immediately without delay from DRL alerting logic
  And the alert is advisory only and does not interrupt the exposure trigger
```

---

## AC-06: Exposure Parameter Logging (FR-DOSE-06)

### Scenario 6.1 - All Required Parameters Persisted Per Exposure

```
Given HVG delivers: kVp=100, mA=200, exposure time=25ms, filtration=Cu 0.1mm + Al 1.0mm
  And detector reports: SID=120cm, field width=35cm, field height=43cm
When the exposure event is processed
Then the persisted DoseRecord contains:
  KvpValue=100, MasValue=5.0 (200mA × 25ms / 1000), FilterMaterial="Cu+Al",
  FilterThicknessMm for each filter layer, SidMm=1200, FieldWidthMm=350, FieldHeightMm=430
  And all values include their units as defined in the DoseRecord schema
  And TimestampUtc is present with millisecond precision
```

### Scenario 6.2 - Unavailable Parameters Marked With Absence Indicator

```
Given HVG delivers mAs=4.0 but kVp is not received within the acquisition timeout
When the exposure event is processed
Then the persisted DoseRecord has MasValue=4.0
  And KvpValue is marked with the documented absence indicator (null or sentinel value)
  And an audit trail entry notes the partial parameter set
  And no exception propagates to the imaging workflow
```

---

## AC-07: RDSR Export to PACS and Dose Registry (FR-DOSE-07)

### Scenario 7.1 - RDSR Transmitted to PACS via DICOM C-STORE

```
Given a PACS destination configured with AE Title, host, and port
  And an RDSR DicomDataset built for a completed study
When RdsrExporter.ExportAsync() is called
Then a DICOM C-STORE request is sent to the configured PACS with SOP Class UID 1.2.840.10008.5.1.4.1.1.88.67
  And the PACS (Orthanc test SCP) receives and stores the RDSR
  And the C-STORE response status is 0x0000 (Success)
  And the RDSR is removed from the export queue after successful acknowledgement
  And an audit trail entry with EventType=ExportAttempted and Outcome=Success is written
```

### Scenario 7.2 - RDSR Retained in Queue on C-STORE Failure

```
Given a PACS destination that is temporarily unreachable
When RdsrExporter.ExportAsync() attempts C-STORE and the connection is refused
Then the RDSR is retained in the persistent export queue with status RETRYING
  And the RDSR DicomDataset is not deleted from local storage
  And an audit trail entry with EventType=ExportAttempted and Outcome=Failure is written
```

### Scenario 7.3 - C-STORE Retried With Exponential Back-off Up to 3 Times

```
Given a queued RDSR export item with ExportRetryBaseMs=1000
  And the PACS remains unreachable
When the first retry attempt fails
Then the next retry is scheduled at approximately now + 1000ms
When the second retry attempt fails
Then the next retry is scheduled at approximately now + 2000ms
When the third retry attempt fails
Then the item transitions to FAILED terminal state
  And the operator is notified
  And no further automatic retries occur for this item
```

### Scenario 7.4 - RDSR Not Deleted Until Success Acknowledgement Received

```
Given a queued RDSR export item
  And the PACS connection drops after sending C-STORE but before receiving C-STORE-RSP
When the application restarts
Then the queued RDSR item is recovered from persistent storage
  And it is re-scheduled for retry
  And no data loss occurs
```

---

## AC-08: Dose Report Generation (FR-DOSE-08)

### Scenario 8.1 - PDF Dose Report Generated for Completed Study

```
Given a completed study for Patient A with 4 exposure events recorded
  And site header configured as "General Hospital Radiology"
When the operator requests a dose report for the study
Then DoseReportGenerator produces a valid PDF document
  And the PDF contains: patient information section, study details, per-exposure parameter table (kVp, mAs, DAP per exposure), cumulative dose summary, and DRL comparison results
  And the site header "General Hospital Radiology" appears in the report header
  And an audit trail entry with EventType=ReportGenerated and Outcome=Success is written
```

### Scenario 8.2 - PDF Export Is Supported Format

```
Given a completed study
When the dose report is generated
Then the output file has a .pdf extension
  And the PDF is valid and openable by a standard PDF reader
```

---

## AC-09: Calculation Performance (NFR-DOSE-01)

### Scenario 9.1 - DAP Calculation and Persistence Within 1 Second

```
Given valid HVG parameters and detector geometry are available
When DapCalculator.Calculate() is called and the result is persisted by DoseRecordRepository.PersistAsync()
Then the total elapsed time from Calculate() invocation to PersistAsync() completion is under 1000 ms
  And this criterion holds across 100 consecutive exposures in a performance benchmark test
```

### Scenario 9.2 - Dose Processing Does Not Block Imaging Workflow Thread

```
Given the imaging workflow thread publishes an exposure event to the dose pipeline
When DAP calculation and persistence are in progress on the background consumer thread
Then the imaging workflow thread returns control immediately after publishing the event
  And no blocking wait occurs on the imaging thread
  And the imaging workflow can initiate the next exposure without waiting for dose processing to complete
```

---

## AC-10: Persistence Reliability (NFR-DOSE-02)

### Scenario 10.1 - Dose Record Not Lost on Application Crash

```
Given DAP calculation has completed and a dose record is being written to disk
When the application process is killed immediately (simulated crash) after DapCalculator.Calculate() returns
  But before DoseRecordRepository.PersistAsync() signals completion
When the application is restarted
Then the dose record is recovered from the NTFS write-ahead log mechanism
  And no corrupt partial record (.tmp file) remains after recovery
  And the recovered record is complete and valid
```

### Scenario 10.2 - Write-Ahead Log Guarantees Atomicity

```
Given a dose record is written to {ExposureEventId}.tmp
When the atomic NTFS rename to {ExposureEventId}.json succeeds
Then only the .json file is present; the .tmp file is removed
When the rename fails (simulated disk full)
Then only the .tmp file is present; no partial .json file is written
  And on restart, the incomplete .tmp file is handled without data corruption
```

---

## AC-11: Calculation Accuracy (NFR-DOSE-03)

### Scenario 11.1 - Calculated DAP Within 5% of Reference DAP

```
Given calibration coefficients configured for the test tube (k_factor=0.0120, n=2.5, C_cal=1.02)
  And a reference DAP value of 1.000 Gy·cm² as reported by MockDoseMonitor.GetDap()
  And exposure parameters: kVp=80, mAs=4.0, SID=110cm, field area=900cm²
When DapCalculator.Calculate() is called with these parameters
Then DoseCalculationResult.CalculatedDapGyCm2 is within ±5% of 1.000 Gy·cm²
  And this criterion is verified across a calibration sweep matrix covering:
    kVp in [60, 70, 80, 100, 120], mAs in [1, 5, 10, 25], SID in [100, 110, 120, 150] cm
```

---

## AC-12: Audit Trail (NFR-DOSE-04)

### Scenario 12.1 - Full Audit Trail Written for All Dose Events

```
Given a complete workflow execution (study open, 3 exposures, RDSR generated, export, report)
When all operations complete successfully
Then the audit trail contains entries for:
  ExposureRecorded (×3), RdsrGenerated (×1), ExportAttempted (×1), ReportGenerated (×1)
  And each entry contains: AuditId, EventType, TimestampUtc (millisecond precision), StudyInstanceUid, PatientId, Outcome
```

### Scenario 12.2 - Audit Trail Records Cannot Be Modified

```
Given an existing audit trail with 10 records
When an attempt is made to modify or delete any audit trail record
Then the modification is rejected (AuditTrailWriter provides no update or delete API)
  And the original record remains unchanged
```

### Scenario 12.3 - SHA-256 Hash Chain Is Unbroken After 100+ Records

```
Given AuditTrailWriter has written 100 consecutive audit records
When the audit trail verification utility runs
Then it reports hash chain intact for all 100 records
  And no breaks are detected
```

### Scenario 12.4 - Hash Chain Break Detected After Record Tampering

```
Given an audit trail with 20 records
When record number 10 is externally modified (simulated tampering)
  And the audit trail verification utility runs
Then the verification utility reports a chain break at record 10
  And it identifies record 10 as the first corrupted or missing record
  And records 1-9 are reported as intact
```

### Scenario 12.5 - No PHI in Log Output

```
Given a complete dose workflow execution (study open, exposure, RDSR, export)
  And application logging is set to INFO level
When the log output is captured and analyzed
Then no occurrences of Patient Name or Patient ID appear in plain text in any log line
  And DICOM tag values for (0010,0010) and (0010,0020) are not present in plain text in any log line
```

---

## Quality Gates

| Gate                         | Criterion                                                                   | Blocking |
|------------------------------|-----------------------------------------------------------------------------|----------|
| Unit Test Coverage           | >= 90% line coverage for src/HnVue.Dose/                                   | Yes      |
| Accuracy Validation          | Calculated DAP within ±5% of reference across calibration sweep matrix      | Yes      |
| DVTK Validation              | Zero Critical, zero Error violations for all RDSR datasets produced         | Yes      |
| Integration Tests            | All AC scenarios pass; Orthanc receives RDSR via C-STORE                    | Yes      |
| PHI Log Audit                | Zero PHI occurrences in INFO log output across all test scenarios           | Yes      |
| Calculation Performance      | DAP calculation + persistence <= 1 second across 100 consecutive exposures  | Yes      |
| Crash Recovery               | No dose record lost after simulated process kill during persistence          | Yes      |
| Hash Chain Integrity         | Zero undetected breaks in 100-record audit trail; tampering detectable       | Yes      |
| Atomic Persistence           | No corrupt partial records after crash recovery                              | Yes      |
| IEC 62304 Trace              | All FR-DOSE-01 through NFR-DOSE-04 traceable to test cases in RTM          | Yes      |
