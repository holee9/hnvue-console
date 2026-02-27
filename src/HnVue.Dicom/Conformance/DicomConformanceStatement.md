# DICOM Conformance Statement

## HnVue DICOM Communication Services

**Version:** 1.0
**Date:** 2026-02-27
**Manufacturer:** abyz-lab
**Device Name:** HnVue Console
**Software Version:** 1.0.0

---

## Section 1 - Implementation Model

### 1.1 Application Data Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          HnVue Console Application                          │
│                                                                               │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────┐  │
│  │ Image        │───▶│ DX/CR IOD    │───▶│ Storage SCU  │───▶│  PACS    │  │
│  │ Acquisition  │    │ Builder      │    │ (C-STORE)    │    │  SCP     │  │
│  └──────────────┘    └──────────────┘    └──────────────┘    └──────────┘  │
│         │                                                                  │
│         ▼                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                  │
│  │ Procedure    │───▶│ MPPS SCU     │───▶│   MPPS SCP   │                  │
│  │ Start        │    │ (N-CREATE)   │    │              │                  │
│  └──────────────┘    └──────────────┘    └──────────────┘                  │
│         │                                                                  │
│         ▼                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                  │
│  │ Procedure    │───▶│ MPPS SCU     │───▶│   MPPS SCP   │                  │
│  │ Complete     │    │ (N-SET)      │    │              │                  │
│  └──────────────┘    └──────────────┘    └──────────────┘                  │
│         │                                                                  │
│         ▼                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                  │
│  │ Storage      │───▶│ Storage      │───▶│   Storage    │                  │
│  │ Commit       │    │ Commit SCU   │    │ Commit SCP   │                  │
│  │ Ready        │    │ (N-ACTION)    │    │ (N-EVENT)    │                  │
│  └──────────────┘    └──────────────┘    └──────────────┘                  │
│                                                                               │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                  │
│  │ Operator     │───▶│ Worklist SCU │───▶│  Worklist    │                  │
│  │ Query        │    │ (C-FIND)     │    │  SCP         │                  │
│  └──────────────┘    └──────────────┘    └──────────────┘                  │
│                                                                               │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                  │
│  │ Prior Study  │───▶│ Query/Retr.  │───▶│  QR SCP      │                  │
│  │ Query        │    │ SCU (C-FIND) │    │              │                  │
│  └──────────────┘    └──────────────┘    └──────────────┘                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Functional Definition of Application Entities

HnVue Console implements a **single DICOM Application Entity (AE)** acting exclusively in the **SCU (Service Class User) role**.

| AE Title            | Role | Description                     |
|---------------------|------|---------------------------------|
| HNVUE_<configured>  | SCU  | Calling AE Title for all operations |

The AE Title is configurable at deployment time via `DicomServiceOptions.CallingAeTitle`.

### 1.3 Sequencing of Real-World Activities

**Typical Clinical Workflow:**

1. **Pre-Acquisition:** Operator queries Modality Worklist (C-FIND) and selects patient/study
2. **Acquisition Start:** System sends MPPS N-CREATE (IN PROGRESS)
3. **Image Acquisition:** Images acquired and processed
4. **Image Storage:** System transmits images via C-STORE to PACS
5. **Acquisition Complete:** System sends MPPS N-SET (COMPLETED)
6. **Storage Commitment:** System sends N-ACTION, receives N-EVENT-REPORT confirmation

**Optional Workflows:**

- **Prior Study Query:** C-FIND/C-MOVE for historical studies (Query/Retrieve SCU)
- **RDSR Transmission:** Radiation dose report sent via C-STORE after procedure completion

---

## Section 2 - AE Specifications

### 2.1 Storage SCU (SOP Class UID: 1.2.840.10008.5.1.4.1.1.1.1)

| Field                   | Value                                    |
|-------------------------|------------------------------------------|
| SOP Class               | Digital X-Ray Image - For Presentation  |
| SOP Class UID           | 1.2.840.10008.5.1.4.1.1.1.1              |
| SOP Class               | Digital X-Ray Image - For Processing    |
| SOP Class UID           | 1.2.840.10008.5.1.4.1.1.1.1.1            |
| Role                    | SCU                                      |
| DIMSE Command           | C-STORE                                  |
| Association Initiation  | Initiator                                |
| Concurrent Associations | Up to 4 per SCP destination              |

**Transfer Syntaxes (proposed in order of preference):**

| Transfer Syntax                    | UID                            |
|------------------------------------|--------------------------------|
| JPEG 2000 Lossless Only           | 1.2.840.10008.1.2.4.90         |
| JPEG Lossless, Non-Hierarchical   | 1.2.840.10008.1.2.4.70         |
| Explicit VR Little Endian         | 1.2.840.10008.1.2.1            |
| Implicit VR Little Endian         | 1.2.840.10008.1.2              |

**Status Handling:**

| Status Range | Category  | System Action                                   |
|--------------|-----------|-------------------------------------------------|
| 0x0000       | Success   | Mark complete, proceed to Storage Commitment    |
| 0xB000-B007  | Warning   | Log warning, mark complete with warning         |
| 0xA700-A7FF  | Failure   | Enqueue for retry                                |
| 0xA900-A9FF  | Failure   | Log error, do not retry                          |
| 0xC000-CFFF  | Failure   | Log error, escalate to operator                  |

### 2.2 Modality Worklist SCU (SOP Class UID: 1.2.840.10008.5.1.4.31)

| Field                   | Value                                    |
|-------------------------|------------------------------------------|
| SOP Class               | Modality Worklist Information - FIND    |
| SOP Class UID           | 1.2.840.10008.5.1.4.31                  |
| Role                    | SCU                                      |
| DIMSE Command           | C-FIND                                   |
| Query Model             | Study Root Modality Worklist             |
| Association Initiation  | Initiator                                |

**Transfer Syntaxes (proposed in order of preference):**

| Transfer Syntax                | UID                      |
|--------------------------------|--------------------------|
| Explicit VR Little Endian      | 1.2.840.10008.1.2.1     |
| Implicit VR Little Endian     | 1.2.840.10008.1.2        |

**Return Key Attributes:**

| Tag         | Attribute Name                  |
|-------------|----------------------------------|
| (0010,0020) | Patient ID                       |
| (0010,0010) | Patient Name                     |
| (0010,0030) | Patient Birth Date               |
| (0010,0040) | Patient Sex                      |
| (0020,000D) | Study Instance UID               |
| (0008,0050) | Accession Number                 |
| (0040,0101) | Requested Procedure ID           |
| (0040,0002) | Scheduled Procedure Step ID      |
| (0040,0006) | Scheduled Procedure Step Desc.   |
| (0040,0001) | Scheduled Station AE Title       |
| (0008,0060) | Modality                         |

### 2.3 MPPS SCU (SOP Class UID: 1.2.840.10008.3.1.2.3.3)

| Field                   | Value                                    |
|-------------------------|------------------------------------------|
| SOP Class               | Modality Performed Procedure Step        |
| SOP Class UID           | 1.2.840.10008.3.1.2.3.3                 |
| Role                    | SCU                                      |
| DIMSE Commands          | N-CREATE, N-SET                          |
| Association Initiation  | Initiator                                |

**Transfer Syntaxes (proposed in order of preference):**

| Transfer Syntax                | UID                      |
|--------------------------------|--------------------------|
| Explicit VR Little Endian      | 1.2.840.10008.1.2.1     |
| Implicit VR Little Endian     | 1.2.840.10008.1.2        |

### 2.4 Storage Commitment SCU (SOP Class UID: 1.2.840.10008.1.3.10)

| Field                   | Value                                    |
|-------------------------|------------------------------------------|
| SOP Class               | Storage Commitment Push Model            |
| SOP Class UID           | 1.2.840.10008.1.3.10                    |
| Role                    | SCU                                      |
| DIMSE Commands          | N-ACTION, N-EVENT-REPORT                 |
| Association Initiation  | Initiator                                |

### 2.5 Query/Retrieve SCU (Optional)

| Field                   | Value                                    |
|-------------------------|------------------------------------------|
| SOP Class               | Study Root Query/Retrieve - FIND        |
| SOP Class UID           | 1.2.840.10008.5.1.4.1.2.2.1             |
| SOP Class               | Study Root Query/Retrieve - MOVE        |
| SOP Class UID           | 1.2.840.10008.5.1.4.1.2.2.2             |
| Role                    | SCU                                      |
| DIMSE Commands          | C-FIND, C-MOVE                           |
| Query Level             | STUDY                                    |

---

## Section 3 - Network Communication Support

### 3.1 Minimum Network Requirements

| Parameter                  | Requirement                     |
|----------------------------|---------------------------------|
| Network Type               | TCP/IP                          |
| Minimum Bandwidth          | 100 Mbit/s (1 Gbit/s recommended) |
| Maximum Latency            | < 100 ms (local)                |

### 3.2 Association Parameters

| Parameter                  | Value                           |
|----------------------------|---------------------------------|
| Maximum PDU Size           | 128 KB (configurable up to 256 KB) |
| Timeout (Association)      | 30 seconds (configurable)       |
| Timeout (DIMSE)            | 60 seconds (configurable)       |
| Async Ops Invoked          | 1 (default)                     |
| Async Ops Performed        | 1 (default)                     |

### 3.3 TLS Support

When TLS is enabled:

| Parameter                  | Value                           |
|----------------------------|---------------------------------|
| Minimum TLS Version        | TLS 1.2                         |
| Preferred TLS Version      | TLS 1.3                         |
| Cipher Suites (TLS 1.3)    | TLS_AES_256_GCM_SHA384, TLS_AES_128_GCM_SHA256 |
| Cipher Suites (TLS 1.2)    | TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 |
| Certificate Validation     | Full chain validation           |
| Hostname Verification      | Enabled                         |
| Mutual TLS (mTLS)          | Optional (per destination)       |

**Security Profile Conformance:**
- DICOM Basic TLS Secure Transport Connection Profile (PS3.15 Annex B.1)

---

## Section 4 - Extensions / Specializations / Privatizations

### 4.1 Private Attributes

None. HnVue uses only standard DICOM attributes defined in the relevant DICOM IOD specifications.

### 4.2 Vendor-Specific Extensions

None planned at this time.

---

## Section 5 - Configuration

### 5.1 AE Title Configuration

| Parameter              | Description                              | Default  |
|------------------------|------------------------------------------|----------|
| CallingAeTitle         | AE Title of HnVue console               | HNVUE    |

### 5.2 SCP Destination Configuration

Each SCP destination is configured with:

| Parameter          | Description                          | Example              |
|--------------------|--------------------------------------|----------------------|
| AeTitle            | Called AE Title                      | PACS_SCP             |
| Host               | IP address or hostname                | 192.168.1.100        |
| Port               | TCP port number                      | 104                  |
| TlsEnabled         | Enable TLS for this destination       | false                |

### 5.3 UID Configuration

| Parameter          | Description                          | Default              |
|--------------------|--------------------------------------|----------------------|
| UidRoot            | Organization UID root prefix         | (deployment required) |

### 5.4 Retry Queue Configuration

| Parameter              | Description                          | Default              |
|------------------------|--------------------------------------|----------------------|
| MaxRetryCount          | Maximum retry attempts               | 5                    |
| InitialRetryInterval   | First retry delay (seconds)          | 30                   |
| BackoffMultiplier      | Exponential back-off factor          | 2.0                  |
| MaxRetryInterval       | Maximum retry delay (seconds)        | 3600                 |

### 5.5 Timeout Configuration

| Parameter          | Description                          | Default              |
|--------------------|--------------------------------------|----------------------|
| AssociationTimeout | Association establishment timeout    | 30 seconds           |
| DimseTimeout       | DIMSE operation timeout              | 60 seconds           |
| StorageCommitTimeout | Storage Commitment response timeout | 300 seconds          |

---

## Section 6 - Support of Character Sets

### 6.1 Character Repertoires

| Character Set                  | DICOM Tag        | Usage               |
|--------------------------------|------------------|---------------------|
| ISO 8859-1 (Latin-1)           | (0008,0005)      | Default repertoire  |
| ISO_IR 192 (UTF-8, Unicode)    | (0008,0005)      | Extended characters |

### 6.2 Specific Character Set Handling

- Default: ISO 8859-1 (Latin-1)
- Extended characters: UTF-8 encoded, with `Specific Character Set (0008,0005)` set to `ISO_IR 192`
- Patient names support multi-byte characters via UTF-8 encoding

---

## Appendix A: Supported SOP Classes Summary

| SOP Class Description            | SOP Class UID                      | Role | IHE Profile |
|----------------------------------|------------------------------------|------|-------------|
| Digital X-Ray Image - Presentation | 1.2.840.10008.5.1.4.1.1.1.1      | SCU  | SWF         |
| Digital X-Ray Image - Processing  | 1.2.840.10008.5.1.4.1.1.1.1.1    | SCU  | SWF         |
| Computed Radiography Image       | 1.2.840.10008.5.1.4.1.1.1        | SCU  | SWF         |
| Modality Worklist - FIND         | 1.2.840.10008.5.1.4.31           | SCU  | SWF         |
| MPPS N-CREATE/N-SET              | 1.2.840.10008.3.1.2.3.3           | SCU  | SWF         |
| Storage Commitment Push          | 1.2.840.10008.1.3.10              | SCU  | SWF         |
| Study Root QR - FIND             | 1.2.840.10008.5.1.4.1.2.2.1       | SCU  | PIR (Optional) |
| Study Root QR - MOVE             | 1.2.840.10008.5.1.4.1.2.2.2       | SCU  | PIR (Optional) |
| X-Ray Radiation Dose SR          | 1.2.840.10008.5.1.4.1.1.88.67     | SCU  | REM         |

---

## Appendix B: IHE Integration Profile Claims

### SWF (Scheduled Workflow)

| Actor                | Transaction                    | DIMSE      |
|----------------------|--------------------------------|------------|
| Acquisition Modality | Query Modality Worklist (RAD-5) | C-FIND    |
| Acquisition Modality | MPPS In Progress (RAD-6)       | N-CREATE  |
| Acquisition Modality | Modality Images Stored (RAD-8) | C-STORE   |
| Acquisition Modality | MPPS Completed (RAD-7)         | N-SET     |
| Acquisition Modality | Storage Commitment (RAD-10)    | N-ACTION  |

### PIR (Patient Information Reconciliation)

| Actor                | Transaction                    | DIMSE      |
|----------------------|--------------------------------|------------|
| Acquisition Modality | Query Prior Studies (RAD-49)   | C-FIND    |
| Acquisition Modality | Retrieve Prior Studies (RAD-50)| C-MOVE    |

### REM (Radiation Exposure Monitoring)

| Actor                | Transaction                    | DIMSE      |
|----------------------|--------------------------------|------------|
| Acquisition Modality | Report Dose (RAD-41)           | C-STORE   |

---

**Document Control**

| Version | Date       | Author   | Changes                           |
|---------|------------|----------|-----------------------------------|
| 1.0     | 2026-02-27 | MoAI     | Initial release for SPEC-DICOM-001 |
