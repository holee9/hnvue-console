# SPEC-DICOM-001: HnVue DICOM Communication Services

## Metadata

| Field        | Value                                      |
|--------------|--------------------------------------------|
| SPEC ID      | SPEC-DICOM-001                             |
| Title        | HnVue DICOM Communication Services        |
| Product      | HnVue - Diagnostic Medical Device X-ray GUI Console SW |
| Status       | Planned                                    |
| Priority     | High                                       |
| Safety Class | IEC 62304 Class B (Data Integrity)         |
| Created      | 2026-02-17                                 |
| IHE Profiles | SWF, PIR, REM                              |
| Library      | fo-dicom 5.x (sole DICOM engine)           |
| Package      | src/HnVue.Dicom/                           |

---

## 1. Environment

### 1.1 Operating Context

HnVue is a GUI console application for diagnostic medical-grade X-ray imaging devices. The DICOM communication subsystem operates within a clinical network environment connecting the imaging device to downstream clinical information systems including PACS, HIS/RIS, and DICOM print servers.

The system operates under IEC 62304 Software Safety Class B requirements, where incorrect data transmission or loss of image data may affect diagnostic quality. The DICOM subsystem is responsible for the reliable, standards-compliant exchange of image data, patient demographics, procedure information, and dose reports.

### 1.2 Network Topology

- HnVue Console (DICOM SCU) connects to one or more remote DICOM SCP entities
- PACS Server: Receives images via C-STORE, provides Storage Commitment via N-ACTION
- HIS/RIS (Worklist SCP): Provides Modality Worklist via C-FIND
- MPPS SCP: Receives procedure step status via N-CREATE / N-SET
- Query/Retrieve SCP: Provides prior studies via C-FIND / C-MOVE (optional)
- DICOM Print SCP: Receives print jobs via N-CREATE (optional)
- All connections may use direct TCP or TLS-secured transport

### 1.3 Supported IODs

| IOD                                         | DICOM Standard  | SOP Class UID                          |
|---------------------------------------------|-----------------|----------------------------------------|
| Digital X-Ray Image (DX) - For Presentation | IHE SWF         | 1.2.840.10008.5.1.4.1.1.1.1            |
| Digital X-Ray Image (DX) - For Processing   | IHE SWF         | 1.2.840.10008.5.1.4.1.1.1.1.1          |
| Computed Radiography Image (CR)             | IHE SWF         | 1.2.840.10008.5.1.4.1.1.1              |
| X-Ray Radiation Dose SR (RDSR)              | IHE REM         | 1.2.840.10008.5.1.4.1.1.88.67          |
| Grayscale Softcopy Presentation State (GSPS)| DICOM PS3.3     | 1.2.840.10008.5.1.4.1.1.11.1           |
| Modality Performed Procedure Step           | IHE SWF         | 1.2.840.10008.3.1.2.3.3                |

### 1.4 Supported SOP Classes (SCU Role)

| SOP Class                                | Role | DIMSE Commands         | IHE Profile |
|------------------------------------------|------|------------------------|-------------|
| Storage SCU                              | SCU  | C-STORE                | SWF         |
| Modality Worklist Management SCU         | SCU  | C-FIND                 | SWF         |
| Modality Performed Procedure Step SCU    | SCU  | N-CREATE, N-SET        | SWF         |
| Storage Commitment Push Model SCU        | SCU  | N-ACTION, N-EVENT-REPORT | SWF       |
| Study Root Query/Retrieve (Find) SCU     | SCU  | C-FIND                 | PIR         |
| Study Root Query/Retrieve (Move) SCU     | SCU  | C-MOVE                 | PIR         |
| Basic Grayscale Print Management SCU     | SCU  | N-CREATE               | Optional    |

### 1.5 Supported Transfer Syntaxes

| Transfer Syntax                        | UID                            | Usage                         |
|----------------------------------------|--------------------------------|-------------------------------|
| Implicit VR Little Endian (Default)    | 1.2.840.10008.1.2              | Mandatory fallback            |
| Explicit VR Little Endian              | 1.2.840.10008.1.2.1            | Preferred uncompressed        |
| JPEG 2000 Lossless Only                | 1.2.840.10008.1.2.4.90         | Primary lossless compression  |
| JPEG Lossless, Non-hierarchical, FOP   | 1.2.840.10008.1.2.4.70         | Secondary lossless compression|
| JPEG Baseline Process 1 (lossy, optional) | 1.2.840.10008.1.2.4.50      | Print preview only            |

### 1.6 Technical Dependencies

| Component              | Version / Specification      | Purpose                              |
|------------------------|------------------------------|--------------------------------------|
| fo-dicom               | 5.x (latest stable)          | Sole DICOM networking and codec library |
| .NET Runtime           | 8.0 LTS or later             | Application host platform            |
| C#                     | 12.0 or later                | Implementation language              |
| Orthanc                | Docker, latest               | DICOM SCP for integration testing    |
| DVTK                   | Latest                       | DICOM validation toolkit             |
| OpenSSL / SChannel     | OS-provided                  | TLS transport security               |

---

## 2. Assumptions

| ID   | Assumption                                                                                          | Confidence | Risk if Wrong                                          |
|------|-----------------------------------------------------------------------------------------------------|------------|--------------------------------------------------------|
| A-01 | PACS and HIS systems at deployment sites are DICOM PS 3 conformant and respond within defined NFR timeouts | Medium | Transmission failures and SLA violations             |
| A-02 | The clinical network provides reliable TCP connectivity with latency under 100 ms for local connections | High    | Retry logic will compensate; performance NFRs may not be met |
| A-03 | All image pixel data produced by the device is already in a complete, device-corrected form before handoff to DICOM services | High | Image integrity violation if raw sensor data is passed |
| A-04 | fo-dicom 5.x provides complete codec support for all required transfer syntaxes and SOP classes, eliminating the need for a DCMTK fallback in the C# DICOM layer | High | If a codec gap is discovered, a fo-dicom codec plugin or managed wrapper must be developed |
| A-05 | The device operates as SCU only; no SCP server capability is required for this SPEC                | High       | Architecture change required if SCP role is needed    |
| A-06 | TLS 1.2 or 1.3 is available on all target deployment environments                                 | Medium     | Non-TLS deployments must be explicitly configured and documented |
| A-07 | A single DICOM Application Entity (AE) Title is configured per device instance                    | High       | Multi-AE support requires additional configuration layer |
| A-08 | Patient data reconciliation via PIR is limited to demographic updates only, not image reassignment | High       | Image reassignment requires separate SPEC               |

---

## 3. Requirements

### 3.1 Functional Requirements

#### FR-DICOM-01: Storage SCU - Image Transmission

**[UBIQUITOUS]** The HnVue DICOM service shall maintain a configurable list of Storage SCP destinations (PACS targets) identified by AE Title, host, and port.

**[EVENT-DRIVEN]** When an image acquisition is completed, the system shall encode the image as a conformant DICOM object (DX or CR IOD) and transmit it to all configured Storage SCP destinations using C-STORE.

**[STATE-DRIVEN]** While a C-STORE operation is in progress, the system shall maintain an association with the remote SCP and report transmission progress to the calling component.

**[UNWANTED]** If a C-STORE operation fails after all configured retry attempts are exhausted, the system shall not silently discard the image; it shall log the failure and retain the image in the persistent retry queue.

**[EVENT-DRIVEN]** When a C-STORE response status is not Success (0x0000), the system shall classify the status (Warning, Failure) and take the appropriate retry or escalation action.

#### FR-DICOM-02: Transfer Syntax Negotiation

**[UBIQUITOUS]** The system shall propose transfer syntaxes in presentation contexts during association negotiation in the following priority order: JPEG 2000 Lossless, JPEG Lossless (FOP), Explicit VR Little Endian, Implicit VR Little Endian.

**[EVENT-DRIVEN]** When the remote SCP accepts only Implicit VR Little Endian, the system shall transcode the image pixel data to the accepted transfer syntax before transmission.

**[UNWANTED]** The system shall not transmit lossy-compressed pixel data for diagnostic DX or CR images intended for archival; lossy transfer syntaxes are permitted only for print preview purposes.

#### FR-DICOM-03: Modality Worklist - Patient and Procedure Fetch

**[EVENT-DRIVEN]** When the operator requests a worklist query, the system shall send a C-FIND request to the configured Modality Worklist SCP using the Study Root Modality Worklist query model.

**[UBIQUITOUS]** The system shall populate C-FIND query keys at minimum with: Scheduled Station AE Title, Modality, and Scheduled Procedure Step Start Date.

**[EVENT-DRIVEN]** When C-FIND responses are received, the system shall parse and expose the following mandatory attributes: Patient ID, Patient Name, Patient Birth Date, Patient Sex, Accession Number, Study Instance UID (if present), Requested Procedure ID, Scheduled Procedure Step ID, Scheduled Procedure Step Description, Scheduled Station AE Title, and Modality.

**[UNWANTED]** If the Modality Worklist SCP returns a Failure status, the system shall not populate the procedure context with incomplete data; it shall report the error to the user interface layer.

#### FR-DICOM-04: MPPS - Procedure Step Reporting

**[EVENT-DRIVEN]** When a procedure is started on the device (exposure initiated), the system shall send an N-CREATE request to the MPPS SCP to create a Modality Performed Procedure Step in IN PROGRESS status.

**[EVENT-DRIVEN]** When a procedure is completed or discontinued, the system shall send an N-SET request to the MPPS SCP to set the Modality Performed Procedure Step status to COMPLETED or DISCONTINUED respectively.

**[UBIQUITOUS]** The MPPS dataset shall include: MPPS Instance UID (generated by the device), Performed Procedure Step Start Date and Time, Modality, Performed Station AE Title, Scheduled Step Attributes Sequence (linked from Worklist item if available), and Series and Image references for COMPLETED status.

**[UNWANTED]** If the N-CREATE request fails, the system shall not proceed with image transmission without logging the MPPS failure; the failure shall be surfaced to the operator and recorded in the event log.

#### FR-DICOM-05: Storage Commitment

**[EVENT-DRIVEN]** When all images for a procedure have been successfully C-STOREd to the PACS, the system shall send an N-ACTION Storage Commitment Push request containing references to all transmitted SOP instances.

**[EVENT-DRIVEN]** When an N-EVENT-REPORT Storage Commitment response is received, the system shall validate that all referenced SOP instances are confirmed as committed and update the transmission status accordingly.

**[STATE-DRIVEN]** While awaiting a Storage Commitment N-EVENT-REPORT, the system shall maintain the commitment request in a persistent pending state and continue listening for the response for a configurable timeout period (default: 300 seconds).

**[UNWANTED]** If Storage Commitment returns a failure for any SOP instance, the system shall not mark those images as safely archived; it shall flag them for re-transmission and notify the operator.

#### FR-DICOM-06: Query/Retrieve - Prior Studies (Optional)

**[OPTIONAL]** Where Query/Retrieve is configured, the system shall support Study Root C-FIND queries to retrieve prior study metadata (Study, Series, Image levels).

**[OPTIONAL]** Where Query/Retrieve is configured, the system shall support C-MOVE requests to retrieve prior image objects to a configured local or intermediate storage location.

**[UNWANTED]** If a C-MOVE operation fails, the system shall not block the current examination workflow; the failure shall be reported as a non-critical warning.

#### FR-DICOM-07: DICOM Print (Optional)

**[OPTIONAL]** Where DICOM Print is configured, the system shall support Basic Grayscale Print Management using N-CREATE to create Film Session, Film Box, and Image Box SOP instances on the configured Print SCP.

**[OPTIONAL]** Where DICOM Print is configured, the system shall construct print layouts from operator-selected images and configured print parameters (film size, orientation, magnification type).

#### FR-DICOM-08: Retry Queue and Transmission Resilience

**[UBIQUITOUS]** The system shall maintain a persistent transmission retry queue backed by durable storage (not in-memory only) to survive application restarts.

**[EVENT-DRIVEN]** When a C-STORE, N-ACTION, N-CREATE, or N-SET operation fails due to a network or SCP error, the system shall enqueue the operation for automatic retry using an exponential back-off strategy.

**[UBIQUITOUS]** The retry queue shall be configurable with: maximum retry count (default: 5), initial retry interval (default: 30 seconds), back-off multiplier (default: 2.0), and maximum retry interval (default: 3600 seconds).

**[STATE-DRIVEN]** While the retry queue contains items, the system shall periodically attempt retransmission without requiring operator intervention.

**[UNWANTED]** If the maximum retry count is reached for a queued item, the system shall not automatically delete the item; it shall transition it to a FAILED terminal state and notify the operator, preserving the data for manual recovery.

#### FR-DICOM-09: Association Management

**[UBIQUITOUS]** The system shall manage DICOM associations (TCP connections with negotiated presentation contexts) efficiently, supporting association pooling or reuse where the remote SCP supports it.

**[EVENT-DRIVEN]** When an association is requested, the system shall perform A-ASSOCIATE negotiation including: Called AE Title, Calling AE Title, Application Context (DICOM standard), and presentation contexts for all required SOP classes.

**[UNWANTED]** If A-ASSOCIATE negotiation is rejected by the SCP, the system shall not retry with identical parameters indefinitely; it shall log the rejection reason and surface a configuration error to the operator.

#### FR-DICOM-10: Security - TLS Transport

**[UBIQUITOUS]** The system shall support TLS 1.2 and TLS 1.3 encrypted transport for all DICOM associations when TLS is configured for a given destination.

**[UBIQUITOUS]** TLS configuration shall support: CA certificate for server verification, optional client certificate for mutual TLS (mTLS), and cipher suite selection aligned with the DICOM TLS Security Profile (DICOM PS 3.15 Annex B).

**[STATE-DRIVEN]** While establishing a TLS connection, if certificate validation fails, the system shall abort the connection and log a security event; it shall not fall back to unencrypted transport automatically.

**[UNWANTED]** The system shall not store private key material in plaintext configuration files; private keys shall be managed through the OS certificate store or an encrypted key container.

#### FR-DICOM-11: DICOM UIDs - Generation and Uniqueness

**[UBIQUITOUS]** The system shall generate globally unique DICOM UIDs (Study Instance UID, Series Instance UID, SOP Instance UID, MPPS Instance UID) using a registered UID root prefix assigned to the organization.

**[UBIQUITOUS]** The UID root shall be configurable at deployment time and shall not be hardcoded in the compiled assembly.

**[UNWANTED]** The system shall not reuse a previously generated SOP Instance UID for a new image object, even across device restarts.

#### FR-DICOM-12: DICOM Conformance Statement

**[UBIQUITOUS]** The system shall be accompanied by a DICOM Conformance Statement document that conforms to the structure defined in DICOM PS 3.2.

**[UBIQUITOUS]** The Conformance Statement shall document: all supported SOP classes, roles (SCU/SCP), transfer syntaxes per SOP class, optional negotiation parameters, Security Profiles, and IHE Integration Profile claims.

**[EVENT-DRIVEN]** When the software version is incremented, the Conformance Statement version shall be updated to reflect the new capabilities.

---

### 3.2 Non-Functional Requirements

#### NFR-PERF-01: C-STORE Throughput

**[UBIQUITOUS]** The system shall transmit a single DX image of up to 50 MB over a 100 Mbit/s local area network within 10 seconds from C-STORE request initiation to receipt of a Success response.

#### NFR-PERF-02: Worklist Query Latency

**[UBIQUITOUS]** The system shall complete a Modality Worklist C-FIND query and return all matching responses within 3 seconds under normal network conditions (round-trip latency under 10 ms).

#### NFR-PERF-03: MPPS Timeliness

**[UBIQUITOUS]** The system shall send the N-CREATE MPPS IN PROGRESS request within 2 seconds of procedure start notification.

#### NFR-REL-01: Retry Queue Durability

**[UBIQUITOUS]** The retry queue shall survive application crash and controlled shutdown without data loss; queued items shall be recoverable after restart.

#### NFR-QUAL-01: DVTK Validation

**[UBIQUITOUS]** All transmitted DICOM objects (DX, CR, RDSR, GSPS) shall pass DVTK validation with zero critical violations before being considered conformant.

#### NFR-SEC-01: PHI Logging Prohibition

**[UNWANTED]** The system shall not write Patient Health Information (PHI) such as Patient Name, Patient ID, or birth date to application log files in plain text; PHI shall be pseudonymized or omitted from logs.

#### NFR-POOL-01: Association Pool Limits

**[UBIQUITOUS]** The system shall limit the number of concurrent DICOM associations per SCP destination to a configurable maximum (default: 4) to prevent connection exhaustion.

**[UNWANTED]** If all association pool slots for a destination are occupied and a new association is requested, the system shall not open unbounded additional connections; it shall wait up to a configurable acquisition timeout (default: 30 seconds) before falling back to the retry queue.

#### NFR-SAFE-01: IEC 62304 Class B Compliance

**[UBIQUITOUS]** The DICOM communication library (src/HnVue.Dicom/) shall be developed and maintained in accordance with IEC 62304 Class B requirements including: requirements traceability, unit testing, integration testing, risk management documentation, and change control.

---

## 4. Specifications

### 4.1 IHE Integration Profile Claims

#### 4.1.1 Scheduled Workflow (SWF)

The system claims conformance to the IHE Radiology SWF Integration Profile as the following actor:

| Actor                | Transaction                    | DIMSE      | Required |
|----------------------|--------------------------------|------------|----------|
| Acquisition Modality | Query Modality Worklist (RAD-5) | C-FIND    | Mandatory |
| Acquisition Modality | Modality Procedure Step In Progress (RAD-6) | N-CREATE | Mandatory |
| Acquisition Modality | Modality Images Stored (RAD-8) | C-STORE   | Mandatory |
| Acquisition Modality | Modality Procedure Step Completed (RAD-7) | N-SET | Mandatory |
| Acquisition Modality | Storage Commitment (RAD-10)    | N-ACTION  | Mandatory |

#### 4.1.2 Patient Information Reconciliation (PIR)

The system claims conformance to the IHE Radiology PIR Integration Profile as the Acquisition Modality actor supporting demographic reconciliation of worklist-sourced patient data prior to image storage.

#### 4.1.3 Radiation Exposure Monitoring (REM)

The system claims conformance to the IHE Radiology REM Integration Profile by generating and transmitting X-Ray Radiation Dose SR (RDSR) objects after each procedure.

---

### 4.2 DICOM Conformance Statement Outline

The following sections shall constitute the DICOM Conformance Statement for HnVue DICOM Communication Services:

**Section 1 - Implementation Model**
- Application Data Flow Diagram
- Functional Definition of AEs (single SCU AE)
- Sequencing of Real-World Activities (worklist -> acquisition -> MPPS -> storage -> commitment)

**Section 2 - AE Specifications**

For each supported SOP class, the Conformance Statement shall declare:

- SOP Class UID and name
- SCU / SCP role
- Proposed presentation context(s) with ordered transfer syntax list
- Association establishment policy (initiator only)
- Number of associations (simultaneous connections)
- DIMSE commands used
- Status handling (how each DIMSE status code is interpreted)
- Attribute mapping (which DICOM attributes are populated and from what source)
- Conditional behavior (if optional SOP classes are present)

**Section 3 - Network Communication Support**

- Minimum network requirements
- Maximum PDU size
- Maximum number of associations
- TLS version and cipher suite support
- IP addressing requirements

**Section 4 - Extensions / Specializations / Privatizations**

- Private attributes (if any)
- Vendor-specific extensions (none planned)

**Section 5 - Configuration**

- AE Title configuration
- IP address and port per SCP destination
- TLS certificate configuration
- UID root configuration
- Retry queue parameters
- Timeout values

**Section 6 - Support of Character Sets**

- Default character repertoire: ISO 8859-1 (Latin-1)
- Extended character sets: UTF-8 (Unicode) via Specific Character Set attribute 0008,0005

---

### 4.3 Architecture Specification

#### 4.3.1 Package Structure

```
src/HnVue.Dicom/
  Associations/          - Association management, pooling, A-ASSOCIATE negotiation
  Scu/
    StorageScu.cs        - C-STORE operations, transfer syntax transcoding
    WorklistScu.cs       - Modality Worklist C-FIND operations
    MppsScu.cs           - MPPS N-CREATE / N-SET operations
    StorageCommitScu.cs  - Storage Commitment N-ACTION / N-EVENT-REPORT
    QueryRetrieveScu.cs  - Optional C-FIND / C-MOVE for prior studies
    PrintScu.cs          - Optional Basic Grayscale Print Management
  Iod/
    DxImage.cs           - DX IOD builder and validator
    CrImage.cs           - CR IOD builder and validator
    RdsrBuilder.cs       - RDSR IOD builder
    GspsBuilder.cs       - GSPS IOD builder
  Security/
    DicomTlsFactory.cs   - TLS context factory, certificate management
  Queue/
    TransmissionQueue.cs - Persistent retry queue implementation
    QueueItem.cs         - Queue item model with retry state
  Uid/
    UidGenerator.cs      - Globally unique UID generation
  Configuration/
    DicomServiceOptions.cs - Configuration model (IOptions<T>)
  Conformance/
    ConformanceStatement.md - DICOM Conformance Statement document (generated)
```

#### 4.3.2 Component Interaction Model

```
HnVue.Core (caller)
       |
       v
DicomServiceFacade          - Single entry point for all DICOM operations
       |
  +----|----+
  |         |
  v         v
StorageScu  WorklistScu ... (other SCUs)
  |
  v
AssociationManager          - Manages association lifecycle, TLS, pooling
  |
  v
fo-dicom 5.x DicomService   - Underlying DICOM networking
  |
  v
TLS Transport (optional)    - OS TLS / OpenSSL
  |
  v
Remote SCP (PACS / HIS / MPPS SCP / Print SCP)
```

---

### 4.4 Attribute Mapping Specification

#### 4.4.1 DX Image IOD - Mandatory Attribute Sources

| DICOM Tag   | Attribute Name                  | Source                                     |
|-------------|----------------------------------|--------------------------------------------|
| (0008,0060) | Modality                        | Device configuration (DX)                  |
| (0008,0022) | Acquisition Date                | System clock at acquisition time           |
| (0008,0032) | Acquisition Time                | System clock at acquisition time           |
| (0010,0020) | Patient ID                      | Worklist response or operator input        |
| (0010,0010) | Patient Name                    | Worklist response or operator input        |
| (0010,0030) | Patient Birth Date              | Worklist response or operator input        |
| (0010,0040) | Patient Sex                     | Worklist response or operator input        |
| (0020,000D) | Study Instance UID              | Worklist response or device-generated      |
| (0020,000E) | Series Instance UID             | Device-generated                           |
| (0008,0018) | SOP Instance UID                | Device-generated                           |
| (0040,0275) | Request Attributes Sequence     | From Worklist response                     |
| (0018,1164) | Imager Pixel Spacing            | Device calibration data                    |
| (0028,0030) | Pixel Spacing                   | Device calibration data                    |
| (7FE0,0010) | Pixel Data                      | Acquired image data (device-corrected)     |

#### 4.4.2 Modality Worklist C-FIND Query Keys

| DICOM Tag   | Attribute Name                      | Query Value                        |
|-------------|--------------------------------------|------------------------------------|
| (0040,0100) | Scheduled Procedure Step Sequence    | Sequence container                 |
| (0040,0001) | Scheduled Station AE Title          | Configured device AE Title         |
| (0008,0060) | Modality                            | DX (or DR, CR as applicable)       |
| (0040,0002) | Scheduled Procedure Step Start Date | Today (or operator-specified range)|
| (0040,0003) | Scheduled Procedure Step Start Time | Wildcard (*) unless filtered       |
| (0010,0020) | Patient ID                          | Operator input or wildcard (*)     |
| (0010,0010) | Patient Name                        | Operator input or wildcard (*)     |

---

### 4.5 Error Handling and Status Code Mapping

#### 4.5.1 C-STORE Status Codes

| Status Code Range | Category  | System Action                                                 |
|-------------------|-----------|---------------------------------------------------------------|
| 0x0000            | Success   | Mark transmission complete; proceed to Storage Commitment     |
| 0xB000, 0xB006, 0xB007 | Warning | Log warning; mark as complete with warning; notify operator |
| 0xA700 - 0xA7FF   | Failure (Resources) | Enqueue for retry with back-off                     |
| 0xA900 - 0xA9FF   | Failure (Data Set) | Log error; do not retry; escalate to operator           |
| 0xC000 - 0xCFFF   | Failure (Cannot Understand) | Log error; do not retry; escalate              |
| 0x0110            | Processing Failure | Enqueue for retry                                      |
| Network timeout   | N/A       | Enqueue for retry with back-off                               |

#### 4.5.2 C-FIND (Worklist) Status Codes

| Status Code | Category  | System Action                                      |
|-------------|-----------|----------------------------------------------------|
| 0x0000      | Success   | Return all accumulated responses to caller         |
| 0xFF00      | Pending   | Accumulate response dataset; continue receiving    |
| 0xFF01      | Pending (Attribute Warning) | Accumulate; log attribute warning   |
| 0xA700      | Failure (Refused) | Log error; surface to operator              |
| 0xA900      | Failure (Identifier) | Log error; surface to operator           |
| 0xC000      | Failure   | Log error; surface to operator                     |

---

### 4.6 Security Specification

#### 4.6.1 TLS Configuration

- Minimum TLS version: TLS 1.2
- Preferred TLS version: TLS 1.3
- Cipher suites (TLS 1.3): TLS_AES_256_GCM_SHA384, TLS_AES_128_GCM_SHA256
- Cipher suites (TLS 1.2): TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256
- Certificate validation: Full chain validation against configured CA bundle; hostname verification enabled
- Mutual TLS: Optional; enabled per-destination if client certificate is configured
- Session resumption: Supported if enabled by both sides

#### 4.6.2 DICOM Security Profile

The system claims conformance to the DICOM Basic TLS Secure Transport Connection Profile (DICOM PS 3.15 Annex B.1) when TLS is enabled.

#### 4.6.3 PHI Protection

- Application logs shall use Patient ID hash (SHA-256 truncated) where patient identity reference is necessary
- No PHI shall appear in log levels INFO, WARNING, ERROR in production configuration
- PHI-containing data structures in memory shall use SecureString or equivalent where the platform supports it

---

### 4.7 Testing and Validation Specification

#### 4.7.1 Test Infrastructure

| Component          | Purpose                              | Configuration                     |
|--------------------|--------------------------------------|-----------------------------------|
| Orthanc DICOM SCP  | Storage and retrieval integration test | Docker: orthancteam/orthanc       |
| DVTK               | DICOM object conformance validation   | Integrated into test pipeline      |
| dcm4chee (optional)| Extended PACS compatibility test      | Docker: dcm4che/dcm4chee-arc-5    |
| fo-dicom test SCP  | In-process SCP for unit test isolation| fo-dicom DicomServer in test project|

#### 4.7.2 Validation Gates

All transmitted IODs must satisfy:

1. DVTK validation: zero Critical violations, zero Error violations
2. fo-dicom DicomValidation: no required attributes absent
3. Transfer syntax encoding: verified byte-level correctness for JPEG 2000 Lossless
4. UID uniqueness: verified by test SCP that no SOP Instance UID is duplicated across a test run

#### 4.7.3 Performance Test Targets

| Test Scenario                       | Acceptance Threshold      |
|-------------------------------------|---------------------------|
| C-STORE 50 MB DX image (100 Mbit/s) | Completed within 10 s     |
| Worklist C-FIND (10 items return)   | Response within 3 s       |
| MPPS N-CREATE roundtrip             | Completed within 2 s      |
| Retry queue recovery after restart  | All queued items recovered within 5 s of startup |
| 10 concurrent C-STORE operations    | All complete without deadlock; throughput linear |

---

## 5. Traceability

### 5.1 Requirements to IHE Profiles

| Requirement ID | EARS Type      | IHE Profile / Transaction              |
|----------------|----------------|----------------------------------------|
| FR-DICOM-01    | Event-Driven   | SWF RAD-8 (Modality Images Stored)    |
| FR-DICOM-02    | Ubiquitous     | SWF (Transfer Syntax negotiation)     |
| FR-DICOM-03    | Event-Driven   | SWF RAD-5 (Query Modality Worklist)   |
| FR-DICOM-04    | Event-Driven   | SWF RAD-6, RAD-7 (MPPS)              |
| FR-DICOM-05    | Event-Driven   | SWF RAD-10 (Storage Commitment)       |
| FR-DICOM-06    | Optional       | PIR (Query/Retrieve prior studies)    |
| FR-DICOM-07    | Optional       | Standalone (DICOM Print)              |
| FR-DICOM-08    | Ubiquitous     | SWF (reliability requirement)         |
| FR-DICOM-09    | Ubiquitous     | All DICOM associations                |
| FR-DICOM-10    | Ubiquitous     | DICOM TLS Security Profile (PS3.15)   |
| FR-DICOM-11    | Ubiquitous     | DICOM PS 3.5 UID assignment           |
| FR-DICOM-12    | Ubiquitous     | DICOM PS 3.2 Conformance Statement    |

### 5.2 Requirements to IODs

| Requirement ID | IOD                        | SOP Class UID Prefix            |
|----------------|----------------------------|---------------------------------|
| FR-DICOM-01    | DX For Presentation / CR  | 1.2.840.10008.5.1.4.1.1.1.1    |
| FR-DICOM-03    | Modality Worklist          | 1.2.840.10008.5.1.4.31          |
| FR-DICOM-04    | MPPS                       | 1.2.840.10008.3.1.2.3.3         |
| FR-DICOM-05    | Storage Commitment         | 1.2.840.10008.1.3.10            |
| REM (RDSR)     | X-Ray Radiation Dose SR    | 1.2.840.10008.5.1.4.1.1.88.67  |

### 5.3 Safety Traceability (IEC 62304 Class B)

| Requirement ID | Safety Concern                           | Mitigation                                  |
|----------------|------------------------------------------|---------------------------------------------|
| FR-DICOM-01 (UNWANTED) | Silent image loss                | Persistent retry queue; FAILED state retention |
| FR-DICOM-05 (UNWANTED) | Premature archive confirmation   | Storage Commitment mandatory before archival confirmation |
| FR-DICOM-08 (UNWANTED) | Retry queue data loss            | Durable backing store; restart recovery      |
| FR-DICOM-10 (STATE)    | Unencrypted PHI on network       | TLS mandatory when configured; no fallback   |
| NFR-SEC-01             | PHI in logs                      | Log sanitization; PHI pseudonymization       |

---

## 6. Open Questions

| ID   | Question                                                                                          | Owner            | Target Resolution |
|------|---------------------------------------------------------------------------------------------------|------------------|-------------------|
| OQ-01 | What is the registered DICOM UID root for the organization? Required before UID generation can be finalized. | Project Lead | Before implementation start |
| OQ-02 | Is Storage Commitment required to be synchronous (block image complete signal) or asynchronous (background confirmation only)? | Clinical Workflow Team | Before FR-DICOM-05 implementation |
| OQ-03 | What is the maximum number of simultaneous PACS destinations required (affects association pool sizing)? | System Architecture | Before NFR-PERF-01 final acceptance |
| OQ-04 | Is GSPS persistence (storage to PACS) required or is GSPS only used locally? | Clinical Team | Before FR-DICOM-01 implementation |
| OQ-05 | Does the deployment environment use a Windows certificate store, file-based PEM/PFX, or an HSM for TLS private key management? | IT / Deployment  | Before FR-DICOM-10 implementation |
