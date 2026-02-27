# Changelog

All notable changes to HnVue Console will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-02-28

### Added - SPEC-HAL-001: Hardware Abstraction Layer

Complete implementation of Hardware Abstraction Layer (HAL) for medical X-ray device control, providing unified interfaces for generator, detector, and safety systems.

#### Core HAL Interfaces (FR-HAL-01 ~ FR-HAL-09)
- **IDetector**: Vendor detector SDK integration interface with plugin ABI
- **IGenerator**: HVG (High Voltage Generator) control interface
- **ICollimator**: Collimator position feedback and control
- **IPatientTable**: Patient table 3-axis position monitoring
- **IAEC**: Automatic Exposure Control interface (Manual/AEC/Servo modes)
- **IDoseMonitor**: Real-time and accumulated dose tracking
- **ISafetyInterlock**: Unified safety interlock management (9 interlocks IL-01~IL-09)

#### HVG Control Implementation (FR-HAL-02, FR-HAL-04)
- **CommandQueue**: Thread-safe command FIFO with timeout and retry
- **GeneratorBase**: Base class for HVG driver implementations
- **GeneratorSimulator**: Software simulator for testing without hardware
- Serial/Ethernet communication protocols (RS-232, TCP/IP)
- Real-time status callback and alarm event subsystem

#### Detector Integration (FR-HAL-01, FR-HAL-09)
- **DmaRingBuffer**: High-performance SPSC ring buffer for frame acquisition
- Overwrite policies: DROP_OLDEST, BLOCK_PRODUCER
- Thread-safe concurrent producer/consumer pattern
- Frame-to-callback latency <100ms

#### Plugin Architecture (FR-HAL-03)
- **DetectorPluginLoader**: Runtime plugin DLL loading
- Plugin ABI version validation
- Exception isolation (plugin crash does not crash host)
- CreateDetector factory function for binary compatibility

#### Safety Features (FR-HAL-06, FR-HAL-07)
- **AecController**: AEC mode switching and termination signal handling
- Real-time alarm callbacks (<50ms latency)
- Status callbacks during exposure (≥10 Hz rate)
- Abort command priority (pre-empts queued commands)

#### Device Management
- **DeviceManager**: Unified lifecycle management for all devices
- Device initialization, shutdown, and status monitoring
- Temperature monitoring (HVG, Detector)

#### Protocol Buffer Definitions
- `hvg_control.proto`: HVG parameter and command definitions
- `detector_acquisition.proto`: Detector frame acquisition interface
- Standard interface for HVG and Detector integration

#### Testing (TDD Applied)
- 81 unit tests with Google Test framework
- 7 Google Mock classes (MockDetector, MockGenerator, etc.)
- Thread-sanitizer verified (zero data races)
- Address-sanitizer verified (zero memory errors)

#### Non-Functional Requirements
- NFR-HAL-03: Full mockability for SOUP-isolated unit testing
- NFR-HAL-05: Thread-safe concurrent access
- NFR-HAL-06: Error isolation (plugin crashes isolated)
- IEC 62304 Class C (generator), Class B (detector)

### Technical Details
- **Platform**: C++17, Windows 10/11
- **Build**: CMake 3.25+, vcpkg
- **Safety**: IEC 62304 Class C (generator), Class B (detector)
- **Files**: 47 files, 11587+ lines

---

### Added - SPEC-IPC-001: gRPC Inter-Process Communication

Complete implementation of gRPC-based IPC communication between C++ Core Engine and C# WPF GUI processes.

#### Protocol Buffer Definitions (FR-IPC-03)
- `hnvue_common.proto` - Shared types (InterfaceVersion, IpcError, ErrorCode, Timestamp)
- `hnvue_command.proto` - CommandService (StartExposure, AbortExposure, SetCollimator, RunCalibration, GetSystemState)
- `hnvue_image.proto` - ImageService (SubscribeImageStream with chunked streaming)
- `hnvue_health.proto` - HealthService (SubscribeHealth with heartbeat, hardware status, fault events)
- `hnvue_config.proto` - ConfigService (GetConfiguration, SetConfiguration, SubscribeConfigChanges)

#### C++ Core Engine Server Implementation
- **IpcServer**: gRPC server lifecycle management (localhost:50051, service registration)
- **CommandServiceImpl**: GUI-to-Core command channel with validation
- **ImageServiceImpl**: Server-streaming image transfer with chunking (PREVIEW/FULL_QUALITY modes)
- **HealthServiceImpl**: Continuous health monitoring (1Hz heartbeat, hardware status, fault reporting)
- **ConfigServiceImpl**: Configuration synchronization with parameter validation

#### C++ Test Coverage (TDD Applied)
- IpcServer: 8 tests (lifecycle, version reporting, start/stop cycles)
- CommandService: 25+ tests (all RPCs, validation, state management)
- ImageService: 12 tests (chunking, streaming, error handling)
- HealthService: 18 tests (heartbeat, hardware events, fault reporting)
- ConfigService: 15 tests (get/set, validation, change notifications)
- Integration Tests: 40+ test cases covering end-to-end scenarios

#### Connection Management (FR-IPC-04, NFR-IPC-04)
- Connection lifecycle: DISCONNECTED → CONNECTING → CONNECTED → RECONNECTING → FAULT
- Automatic reconnection with exponential backoff (500ms to 30s max)
- Heartbeat-based disconnect detection (3000ms timeout)
- Graceful shutdown with SYSTEM_STATE_SHUTTING_DOWN event

#### Image Data Transfer (FR-IPC-05)
- Chunked streaming for large images (9MP support)
- Transfer modes: PREVIEW (downsampled), FULL_QUALITY (16-bit diagnostic)
- Progressive rendering support
- Error chunk handling for failed transfers

#### Configuration Synchronization (FR-IPC-07)
- Initial config sync on connection (FR-IPC-07a)
- Bidirectional parameter access (Get/Set)
- Server-streaming change notifications
- Extensible parameter validation system

#### Protocol Buffer Schema Versioning (NFR-IPC-07)
- InterfaceVersion message for compatibility verification
- Semantic versioning: minor additions backward compatible
- Major version bump required for breaking changes
- Version negotiation on connection (GetSystemState)

#### Non-Functional Requirements
- NFR-IPC-01: Image transfer latency target <50ms
- NFR-IPC-02: Command round-trip latency target <10ms
- NFR-IPC-03: Crash isolation (process boundary enforced)
- NFR-IPC-05: Concurrent independent streams (no head-of-line blocking)
- NFR-IPC-06: IEC 62304 auditability (structured logging)

#### Build System Integration
- CMakeLists.txt for C++ server library
- protoc integration for C++ stub generation
- MSBuild .csproj for C# client stub generation
- gRPC++ v1.68+, Protocol Buffers v3 dependencies

#### Known Limitations
- C# client implementation pending (next phase)
- HAL integration mock (requires SPEC-HAL-001)
- Image downsampling placeholder
- Proto-generated types require protoc build step

### Technical Details
- **Transport**: gRPC over HTTP/2 (localhost)
- **Serialization**: Protocol Buffers v3
- **C++ Runtime**: C++17, gRPC++ v1.68+
- **C# Runtime**: .NET 8 LTS, Grpc.Core v2.67+
- **Port**: 50051 (default, configurable)
- **Test Framework**: Google Test (GTest) for C++
- **Safety Class**: IEC 62304 Class B (Process Isolation)

---

### Added - SPEC-INFRA-001: Project Infrastructure

Complete implementation of HnVue project build infrastructure, CI/CD pipeline, and development environment setup.

#### Dual-Language Build System (FR-INFRA-01)
- CMake 3.25+ based C++ build system with CMakePresets.json
- MSBuild 17 + .NET 8 LTS C# build system
- Unified build entry point via `scripts/build-all.ps1`
- Independent module compilation support
- Incremental build optimization

#### Package Management (FR-INFRA-02)
- vcpkg manifest mode with pinned baseline commit
- NuGet central package management via `Directory.Packages.props`
- SOUP register for IEC 62304 compliance
- Reproducible dependency resolution

#### CI/CD Pipeline (FR-INFRA-03)
- Gitea Actions workflow with 7 stages
- Automated C++ (CMake) and C# (MSBuild) builds
- Automated test execution (GTest, xUnit)
- Integration test environment with Orthanc DICOM server
- Artifact retention (90 days)
- Build time target: ≤15 minutes

#### Version Control (FR-INFRA-04)
- Self-hosted Gitea VCS configuration
- Branch protection rules for `main` and `develop`
- Conventional Commits format enforcement
- Feature/hotfix/release branch conventions

#### Automated Testing (FR-INFRA-05)
- C++ unit tests with Google Test (GTest)
- C# unit tests with xUnit
- Code coverage collection (LLVM/gcov, Coverlet)
- Coverage gate: ≥80% for new code
- Local test runner script

#### DICOM Test Environment (FR-INFRA-06)
- Orthanc DICOM server on Docker
- Pinned image version (jodogne/orthanc:24.1.2)
- Health check and automated lifecycle management
- C-STORE, C-FIND, C-MOVE support
- Test fixture management

#### Repository Structure
- Canonical layout with 39 files created
- C++ libraries: hnvue-infra, hnvue-hal, hnvue-ipc, hnvue-imaging
- C# projects: Ipc.Client, Dicom, Dose, Workflow, Console
- Protobuf definitions in `proto/`
- Test structure for both language stacks

#### Regulatory Compliance
- IEC 62304 alignment (Class B)
- ISO 13485 document control
- SOUP register with risk assessments
- Deterministic build requirements

### Technical Details
- **C++**: CMake 3.25+, vcpkg, MSVC 2022
- **C#**: .NET 8 LTS, MSBuild 17, C# 12
- **CI/CD**: Gitea Actions (Forgejo)
- **Containers**: Docker Desktop (Orthanc)
- **Safety Class**: IEC 62304 Class B

---

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
