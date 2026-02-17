# SPEC-DICOM-001: Acceptance Criteria

## Metadata

| Field    | Value                                  |
|----------|----------------------------------------|
| SPEC ID  | SPEC-DICOM-001                         |
| Title    | HnVue DICOM Communication Services    |
| Format   | Given-When-Then (Gherkin-style)        |

---

## Definition of Done

A requirement is considered complete when:
1. All acceptance scenarios for that requirement pass in the automated test suite
2. DVTK validation produces zero Critical and zero Error violations for all IODs produced by the scenario
3. The implementation is traceable to this SPEC in the traceability matrix
4. IEC 62304 Class B unit test documentation exists for the affected component
5. No PHI appears in log output when the scenario executes at INFO level

---

## AC-01: Storage SCU - Image Transmission (FR-DICOM-01)

### Scenario 1.1 - Successful C-STORE to PACS

```
Given a configured PACS destination (AE Title, host, port)
  And a completed DX image acquisition with valid pixel data
When the DICOM service transmits the image via C-STORE
Then the remote SCP (Orthanc) receives and stores the DICOM object
  And the C-STORE response status is 0x0000 (Success)
  And the SOP Instance UID in the stored object matches the transmitted object
  And the transmission is marked COMPLETE in the transmission queue
  And DVTK validation of the stored object produces zero Critical violations
```

### Scenario 1.2 - C-STORE Failure Enqueues for Retry

```
Given a configured PACS destination that is temporarily unreachable
  And a completed DX image acquisition
When the DICOM service attempts C-STORE and the network connection is refused
Then the operation is enqueued in the persistent retry queue with status RETRYING
  And no image data is lost
  And an error log entry is written (without PHI)
  And the retry queue item has a calculated next-retry timestamp using exponential back-off
```

### Scenario 1.3 - Retry Queue Survives Application Restart

```
Given a RETRYING item exists in the persistent retry queue
When the application is terminated (simulated crash) and restarted
Then the queued item is recovered from persistent storage on startup
  And it is re-scheduled for retry according to its back-off state
  And no duplicate SOP Instance UIDs are generated for the retry attempt
```

### Scenario 1.4 - Max Retries Reached Transitions to FAILED

```
Given a queued C-STORE item that has been retried the maximum configured number of times
When the next retry attempt also fails
Then the item transitions to FAILED terminal state
  And the item is retained in persistent storage (not deleted)
  And an operator-facing notification is raised
  And subsequent automatic retries do not occur for this item
```

---

## AC-02: Transfer Syntax Negotiation (FR-DICOM-02)

### Scenario 2.1 - JPEG 2000 Lossless Accepted

```
Given an SCP that accepts JPEG 2000 Lossless transfer syntax
When the system initiates an association for C-STORE
Then JPEG 2000 Lossless (1.2.840.10008.1.2.4.90) is proposed first in the presentation context
  And the association is accepted with JPEG 2000 Lossless
  And the transmitted pixel data is JPEG 2000 Lossless encoded
  And DVTK validates the pixel data encoding is correct
```

### Scenario 2.2 - Fallback to Implicit VR Little Endian

```
Given an SCP that accepts only Implicit VR Little Endian transfer syntax
When the system initiates an association for C-STORE
Then the system transcodes the pixel data to the accepted transfer syntax
  And the transmitted object uses Implicit VR Little Endian encoding
  And pixel data integrity is preserved (lossless conversion)
  And the C-STORE completes with Success status
```

### Scenario 2.3 - No Lossy Transfer for Diagnostic Images

```
Given a DX or CR image marked as diagnostic (for archival)
When transfer syntax negotiation results in only lossy JPEG being accepted
Then the system refuses to transmit using lossy compression
  And an error is logged indicating the transfer syntax conflict
  And the item is placed in FAILED state with a descriptive reason
```

---

## AC-03: Modality Worklist (FR-DICOM-03)

### Scenario 3.1 - Successful Worklist Query Returns Scheduled Procedures

```
Given a Worklist SCP (Orthanc with worklist plugin) containing 3 scheduled procedures for today
  And the device AE Title matches the Scheduled Station AE Title in the worklist items
When the operator requests a worklist query
Then a C-FIND request is sent with the device AE Title and today's date as query keys
  And 3 response datasets are received with status 0xFF00 for each
  And the final response has status 0x0000 (Success)
  And all responses are available within 3 seconds
  And each response includes: Patient ID, Patient Name, Accession Number, Scheduled Procedure Step ID
```

### Scenario 3.2 - Worklist SCP Failure Is Surfaced to Caller

```
Given a Worklist SCP that returns a Failure status (0xA700)
When the operator requests a worklist query
Then the system does not populate any procedure context with partial data
  And an error result is returned to the caller with the SCP status code
  And an error log entry is written
```

### Scenario 3.3 - Empty Worklist Returns Successfully

```
Given a Worklist SCP that returns 0 matching items (Success with no pending data)
When the operator requests a worklist query for today
Then the system returns an empty result set without error
  And the query completes within 3 seconds
```

---

## AC-04: MPPS Reporting (FR-DICOM-04)

### Scenario 4.1 - MPPS IN PROGRESS on Procedure Start

```
Given a Worklist item has been selected for a procedure
When the procedure is started (first exposure initiated)
Then an N-CREATE request is sent to the MPPS SCP within 2 seconds
  And the MPPS dataset contains: MPPS Instance UID, Performed Procedure Step Start Date/Time, Modality, Performed Station AE Title
  And the Scheduled Step Attributes Sequence references the selected Worklist item
  And the N-CREATE response status is 0x0000 (Success)
```

### Scenario 4.2 - MPPS COMPLETED on Procedure End

```
Given an MPPS instance was created (IN PROGRESS) for a procedure
  And images have been acquired and the procedure is finalized
When the operator marks the procedure as complete
Then an N-SET request is sent to the MPPS SCP
  And the dataset contains: Performed Procedure Step Status = COMPLETED, Performed Series Sequence referencing acquired images
  And the N-SET response status is 0x0000 (Success)
```

### Scenario 4.3 - MPPS DISCONTINUED on Procedure Abort

```
Given an MPPS instance was created (IN PROGRESS) for a procedure
When the operator discontinues the procedure before completion
Then an N-SET request is sent with Performed Procedure Step Status = DISCONTINUED
  And the N-SET response status is 0x0000 (Success)
  And the discontinuation reason is recorded if provided by the operator
```

### Scenario 4.4 - MPPS N-CREATE Failure Is Logged and Surfaced

```
Given an MPPS SCP that is unreachable
When the procedure is started and N-CREATE is attempted
Then the failure is logged with a specific error code
  And the failure is surfaced to the operator as a warning
  And the system continues to allow image acquisition (non-blocking behavior)
```

---

## AC-05: Storage Commitment (FR-DICOM-05)

### Scenario 5.1 - All Images Committed Successfully

```
Given all images for a procedure have been C-STOREd to the PACS with Success status
When the system sends an N-ACTION Storage Commitment Push request
Then the request contains references to all SOP Instance UIDs transmitted for the procedure
  And the system enters a pending state awaiting N-EVENT-REPORT
  And when the N-EVENT-REPORT is received with all instances confirmed
  And all referenced SOP instances appear in the Referenced SOP Sequence (success list)
  Then the procedure is marked as safely archived
  And no further transmission is required
```

### Scenario 5.2 - Partial Commitment Failure Triggers Re-transmission

```
Given a Storage Commitment N-EVENT-REPORT is received
  And one or more SOP instances appear in the Failed SOP Sequence
When the system processes the N-EVENT-REPORT
Then the failed SOP instances are flagged for re-transmission
  And they are enqueued in the retry queue
  And the operator is notified of the partial commitment failure
  And the procedure is not marked as safely archived until all instances are committed
```

### Scenario 5.3 - Commitment Timeout Notifies Operator

```
Given an N-ACTION Storage Commitment Push request has been sent
When no N-EVENT-REPORT is received within the configured timeout (default 300 seconds)
Then the pending commitment record transitions to a TIMEOUT state
  And the operator is notified
  And the system continues listening (the association remains open if applicable)
  And the timeout duration is configurable without recompilation
```

---

## AC-06: Retry Queue Resilience (FR-DICOM-08)

### Scenario 6.1 - Exponential Back-off Is Applied

```
Given a queued C-STORE item with initial retry interval 30 s and back-off multiplier 2.0
When the first retry attempt fails
Then the next-retry timestamp is set to now + 30 seconds
When the second retry attempt fails
Then the next-retry timestamp is set to now + 60 seconds
When the third retry attempt fails
Then the next-retry timestamp is set to now + 120 seconds
```

### Scenario 6.2 - Max Retry Interval Is Respected

```
Given a queued item with max retry interval 3600 seconds
When back-off calculation exceeds 3600 seconds
Then the next-retry timestamp is capped at now + 3600 seconds
```

---

## AC-07: TLS Security (FR-DICOM-10)

### Scenario 7.1 - TLS Association Established Successfully

```
Given a PACS destination configured with TLS enabled
  And a valid CA certificate is configured for server verification
When the system initiates an association to the TLS-enabled SCP
Then a TLS 1.2 or 1.3 handshake completes successfully
  And the DICOM association is established over the encrypted channel
  And the C-STORE operation proceeds normally
```

### Scenario 7.2 - Invalid Certificate Aborts Connection

```
Given a PACS destination configured with TLS enabled
  And the SCP presents a certificate that does not validate against the configured CA bundle
When the system initiates a TLS connection
Then the TLS handshake fails with a certificate validation error
  And the connection is aborted; no DICOM data is transmitted
  And a security event is logged
  And the system does not fall back to unencrypted transport
```

### Scenario 7.3 - Mutual TLS (mTLS) Authentication

```
Given a PACS destination configured with mTLS (client certificate required)
  And a valid client certificate and private key are configured
When the system initiates a TLS connection
Then the TLS handshake includes client certificate presentation
  And the SCP accepts the client certificate
  And the association is established successfully
```

---

## AC-08: DICOM UID Uniqueness (FR-DICOM-11)

### Scenario 8.1 - No Duplicate SOP Instance UIDs Across Sessions

```
Given a test run that acquires 100 images across 10 restarts of the application
When all generated SOP Instance UIDs are collected
Then no two UIDs are identical
  And all UIDs conform to the DICOM UID format (2.25.{UUID} or registered root)
```

### Scenario 8.2 - UID Root Is Configurable

```
Given a deployment configuration specifying UID root "1.2.3.4.5"
When the system generates a SOP Instance UID
Then the generated UID begins with "1.2.3.4.5."
  And the UID does not exceed 64 characters in total length
```

---

## AC-09: PHI Log Prohibition (NFR-SEC-01)

### Scenario 9.1 - No PHI in INFO-Level Logs

```
Given a complete workflow execution (Worklist query, acquisition, MPPS, C-STORE, Storage Commitment)
  And application logging is set to INFO level
When the log output is captured and analyzed
Then no occurrences of Patient Name, Patient ID, or Birth Date appear in plain text in any log line
  And DICOM tag values for (0010,0010), (0010,0020), (0010,0030), (0010,0040) are not present in plain text
```

---

## AC-10: Performance Thresholds (NFR-PERF-01, NFR-PERF-02)

### Scenario 10.1 - C-STORE 50 MB Image Within 10 Seconds

```
Given a local network connection with 100 Mbit/s bandwidth and round-trip latency under 5 ms
  And a DX image of 50 MB uncompressed
When the C-STORE operation is initiated
Then the Success response is received within 10 seconds from request initiation
```

### Scenario 10.2 - Worklist Query Within 3 Seconds

```
Given a Worklist SCP with 50 scheduled procedures for today
  And network round-trip latency under 10 ms
When a Modality Worklist C-FIND query is executed
Then all matching responses are received and parsed within 3 seconds
```

---

## AC-11: DVTK Validation (NFR-QUAL-01)

### Scenario 11.1 - DX IOD Passes DVTK Validation

```
Given a DX image produced by the HnVue DICOM service
When the DICOM object is validated using DVTK DicomValidator
Then the validation result contains zero Critical violations
  And the validation result contains zero Error violations
  And all Type 1 (mandatory) attributes are present and non-zero length
```

### Scenario 11.2 - RDSR Passes DVTK Validation

```
Given an RDSR (X-Ray Radiation Dose SR) object produced after a procedure
When the DICOM object is validated using DVTK
Then the validation result contains zero Critical violations
  And IHE REM profile compliance is verified
```

---

## Quality Gates

| Gate                | Criterion                                                         | Blocking |
|---------------------|-------------------------------------------------------------------|----------|
| Unit Test Coverage  | >= 85% line coverage for src/HnVue.Dicom/                        | Yes      |
| DVTK Validation     | Zero Critical, zero Error violations for all produced IODs        | Yes      |
| Integration Tests   | All AC scenarios pass against Orthanc Docker SCP                  | Yes      |
| PHI Log Audit       | Zero PHI occurrences in INFO log output across all test scenarios | Yes      |
| Performance         | C-STORE 50 MB within 10 s, Worklist within 3 s                   | Yes      |
| DVTK UID Check      | Zero duplicate SOP Instance UIDs in 100-image test run           | Yes      |
| Retry Recovery      | All queued items recovered after simulated crash restart          | Yes      |
| TLS Validation      | Certificate rejection correctly aborts connection                 | Yes      |
| IEC 62304 Trace     | All requirements traceable to test cases in traceability matrix   | Yes      |
