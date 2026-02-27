# Changelog

All notable changes to HnVue Console will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-02-28

### Added - SPEC-DICOM-001: DICOM Communication Services

Complete implementation of DICOM SCU (Service Class User) communication services for medical imaging integration.

#### Storage SCU (FR-DICOM-01)
- C-STORE image transmission to PACS destinations
- Support for Digital X-Ray (DX) and Computed Radiography (CR) IODs
- Transfer syntax negotiation: JPEG 2000 Lossless, JPEG Lossless, Explicit/Implicit VR Little Endian
- Automatic retry queue with exponential back-off
- Association pooling for efficient connection management

#### Modality Worklist SCU (FR-DICOM-03)
- C-FIND queries to retrieve scheduled patient and procedure information
- Study Root Modality Worklist query model
- Returns: Patient ID, Name, Birth Date, Sex, Accession Number, Study Instance UID, Procedure IDs, Descriptions

#### MPPS SCU (FR-DICOM-04)
- N-CREATE for procedure step IN PROGRESS notification
- N-SET for procedure step COMPLETED/DISCONTINUED status
- Performed Procedure Step tracking with series and image references

#### Storage Commitment SCU (FR-DICOM-05)
- N-ACTION for commitment request after successful C-STORE
- N-EVENT-REPORT for confirmation from PACS
- Ensures image archival confirmation before marking complete

#### Query/Retrieve SCU (FR-DICOM-06)
- C-FIND for prior study metadata queries (Study, Series, Image levels)
- C-MOVE for retrieving prior images to configured storage location
- Study Root Query/Retrieve model

#### Security (FR-DICOM-10)
- TLS 1.2 and TLS 1.3 support for encrypted DICOM associations
- Certificate validation and hostname verification
- Optional mutual TLS (mTLS) per destination
- DICOM Basic TLS Secure Transport Connection Profile conformance

#### UID Generation (FR-DICOM-11)
- Globally unique DICOM UID generation
- Configurable organization UID root prefix
- Study, Series, SOP Instance, and MPPS Instance UID support

#### DICOM Conformance Statement (FR-DICOM-12)
- Complete DICOM PS 3.2 conformant Conformance Statement
- Section 1: Implementation Model with data flow diagrams
- Section 2: AE Specifications for all SOP classes
- Section 3: Network Communication Support with TLS parameters
- Section 4: Extensions/Privatizations (none - standard only)
- Section 5: Configuration parameters
- Section 6: Character Set support (ISO 8859-1, UTF-8)
- Appendix A: Supported SOP Classes Summary
- Appendix B: IHE Integration Profile Claims (SWF, PIR, REM)

#### IHE Integration Profile Support
- **SWF (Scheduled Workflow)**: RAD-5, RAD-6, RAD-7, RAD-8, RAD-10 transactions
- **PIR (Patient Information Reconciliation)**: RAD-49, RAD-50 transactions (optional)
- **REM (Radiation Exposure Monitoring)**: RAD-41 transaction for RDSR

#### Additional Components
- `DicomServiceFacade`: Single entry point for all DICOM operations
- `AssociationManager`: Efficient association lifecycle and pooling management
- `TransmissionQueue`: Durable retry queue for failed transmissions
- `UidGenerator`: Globally unique UID generation
- `DicomTlsFactory`: TLS context factory for secure associations
- `DxImageBuilder`, `CrImageBuilder`: DX/CR IOD construction and validation
- `RdsrBuilder`: X-Ray Radiation Dose SR IOD builder

#### Testing
- Comprehensive unit tests for all SCU components
- 16 test cases for QueryRetrieveScu
- 12 test cases for MppsScu
- Integration test infrastructure with Orthanc DICOM SCP

#### Package Structure
- `src/HnVue.Dicom/` - Main DICOM service package
- `src/HnVue.Dicom/Conformance/` - Conformance Statement document
- `src/HnVue.Dicom/Scu/` - All SCU implementations
- `src/HnVue.Dicom/Iod/` - IOD builders
- `src/HnVue.Dicom/Security/` - TLS implementation
- `src/HnVue.Dicom/Queue/` - Transmission retry queue

### Technical Details
- **Library**: fo-dicom 5.x
- **Platform**: .NET 8 LTS
- **Language**: C# 12
- **Safety Class**: IEC 62304 Class B (Data Integrity)

---

[1.0.0]: https://github.com/abyz-lab/hnvue-console/releases/tag/v1.0.0
