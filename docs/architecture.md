# HnVue Console Architecture

**Last Updated: 2026-03-12**

## System Overview

HnVue Console is a medical X-ray diagnostic device GUI console software implementing a hybrid architecture combining C++ core engine with C# WPF presentation layer, following IEC 62304 Class B/C safety standards.

### Architecture Principles

- **Safety First**: IEC 62304 Class B/C compliance throughout
- **Separation of Concerns**: Clear layer boundaries with defined interfaces
- **Technology Hybrid**: C++ for performance-critical, C# for business logic
- **Standards-Based**: DICOM, IHE, gRPC for interoperability
- **Testability**: Simulator-first development with comprehensive test coverage

## Layer Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation Layer (WPF GUI)                                │
│  - ViewModels (MVVM pattern)                                 │
│  - XAML Views                                                │
│  - User Interaction                                         │
├─────────────────────────────────────────────────────────────┤
│  Application Layer (C# .NET 8)                              │
│  - Workflow Engine (State Machine)                          │
│  - Dose Management                                           │
│  - DICOM Services                                            │
│  - Business Logic                                            │
├─────────────────────────────────────────────────────────────┤
│  Integration Layer (gRPC IPC)                               │
│  - gRPC Client/Server                                       │
│  - Protocol Buffer Definitions                              │
│  - Inter-Process Communication                             │
├─────────────────────────────────────────────────────────────┤
│  Core Engine Layer (C++)                                    │
│  - Image Processing Pipeline                                 │
│  - Hardware Abstraction Layer (HAL)                         │
│  - Device Drivers (HVG, Detector)                          │
├─────────────────────────────────────────────────────────────┤
│  Hardware Layer                                             │
│  - High-Voltage Generator                                   │
│  - Flat Panel Detector                                      │
│  - Safety Interlocks                                         │
│  - Collimator, Table                                        │
└─────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

### Presentation Layer (WPF GUI)

**Technology**: C# 12, .NET 8, WPF

**Responsibilities**:
- User interface rendering and interaction
- MVVM pattern implementation (16 ViewModels)
- Real-time status display
- Image viewing and manipulation
- User input validation

**Key Components**:
- `HnVue.Console` - Main WPF application
- ViewModels: Patient, Worklist, Acquisition, ImageReview, SystemStatus, etc.
- Views: XAML-based UI components
- Commands: RelayCommand, AsyncRelayCommand

**Safety Classification**: IEC 62304 Class B

### Application Layer (C#)

**Technology**: C# 12, .NET 8

**Responsibilities**:
- Clinical workflow orchestration
- Radiation dose tracking and management
- DICOM communication (Worklist, MPPS, Storage)
- Protocol management and validation
- Business rule enforcement

**Key Components**:
- `HnVue.Workflow` - Clinical workflow engine (10-state state machine)
- `HnVue.Dose` - Radiation dose management (DAP, cumulative tracking, RDSR)
- `HnVue.Dicom` - DICOM services (C-FIND, N-CREATE, C-STORE)

**Safety Classification**:
- Workflow: IEC 62304 Class C (exposure control)
- Dose: IEC 62304 Class B (monitoring and display)
- DICOM: IEC 62304 Class B

### Integration Layer (gRPC IPC)

**Technology**: gRPC 1.68.x, Protocol Buffers

**Responsibilities**:
- Inter-process communication
- Service boundary definition
- Type-safe serialization
- Network transparency

**Key Components**:
- `HnVue.Ipc.Client` - gRPC client implementation
- `HnVue.Ipc.Server` - gRPC server implementation (C++)
- Protocol definitions: *.proto files

**Supported Services**:
- Patient management
- Worklist queries
- Exposure control
- Protocol management
- Dose tracking
- Image transfer
- System status

**Safety Classification**: IEC 62304 Class C (IPC for exposure control)

### Core Engine Layer (C++)

**Technology**: C++17/20, CMake

**Responsibilities**:
- High-performance image processing
- Hardware device abstraction
- Real-time control loops
- Signal processing

**Key Components**:
- `hnvue-imaging` - Image processing pipeline (OpenCV, FFTW)
- `hnvue-hal` - Hardware Abstraction Layer
- `hnvue-ipc` - gRPC server implementation
- Device drivers: HVG, Detector, Safety Interlocks

**Safety Classification**: IEC 62304 Class C (direct hardware control)

## Data Flow

### Clinical Workflow (Acquisition)

```
1. Patient Selection
   ├─> Worklist Query (DICOM C-FIND)
   │  └─> HnVue.Dicom.WorklistClient
   │     └─> Remote MWL SCP

2. Protocol Selection
   ├─> Protocol Repository
   │  └─> HnVue.Workflow.ProtocolRepository
   │     └─> SQLite Database

3. Positioning & Preview
   ├─> Detector Arm Control
   │  └─> HnVue.Workflow.Hal.IDetector
   │     └─> gRPC → HVG Core Engine
   │        └─> Physical Detector

4. Exposure Preparation
   ├─> Safety Interlock Check (9 interlocks)
   │  └─> HnVue.Workflow.Safety.InterlockChecker
   ├─> Dose Limit Verification
   │  └─> HnVue.Workflow.Dose.DoseTrackingCoordinator
   └─> AEC Configuration
      └─> HnVue.Workflow.Hal.IAecController

5. Exposure Trigger
   ├─> HVG Control
   │  └─> HnVue.Workflow.Hal.IHvgDriver
   │     └─> gRPC → HVG Core Engine
   │        └─> Physical HVG
   ├─> Image Acquisition
   │  └─> IDetector.AcquireAsync()
   └─> Dose Recording
      └─> HnVue.Dose.Recording.DoseRecorder

6. Image Review & QC
   ├─> Image Processing Pipeline
   │  └─> hnvue-imaging (C++)
   │     └─> 16-bit grayscale processing
   └─> QC Review Workflow
      └─> HnVue.Workflow.States.QcReview

7. PACS Export
   ├─> MPPS Completion
   │  └─> HnVue.Dicom.MppsClient
   ├─> DICOM C-STORE
   │  └─> HnVue.Dicom.StoreClient
   │     └─> Remote PACS SCP
   └─> RDSR Generation
      └─> HnVue.Dose.Rdsr.RdsrGenerator
```

## Safety Classification Distribution

### Class C (High Risk)
- **SPEC-WORKFLOW-001**: X-ray exposure control
- **SPEC-HAL-001**: Hardware abstraction layer
- **SPEC-IPC-001**: IPC for exposure control

**Requirements**:
- Formal design verification
- Unit testing with 100% coverage
- Integration testing
- Risk analysis and mitigation

### Class B (Moderate Risk)
- **SPEC-DOSE-001**: Dose monitoring and display
- **SPEC-DICOM-001**: DICOM communication
- **SPEC-UI-001**: User interface

**Requirements**:
- Unit testing with 85%+ coverage
- Integration testing
- Traceability to requirements

### Class A (Low Risk)
- **SPEC-INFRA-001**: Build infrastructure
- **SPEC-IMAGING-001**: Image processing (non-safety-critical paths)

**Requirements**:
- Basic testing
- Code review

## Interface Definitions

### gRPC Service Interfaces

**Proto Definitions**: `proto/*.proto`

**Key Services**:
- `HnvuePatient` - Patient CRUD operations
- `HnvueWorklist` - DICOM MWL queries
- `HnvueProtocol` - Protocol management
- `HnvueExposure` - Exposure control
- `HnvueDose` - Dose tracking
- `HnvueImage` - Image transfer
- `HnvueSystemStatus` - System monitoring
- `HnvueAec` - Automatic exposure control
- `HnvueAuditLog` - Audit trail

### HAL Interfaces

**C# HAL Interfaces** (`src/HnVue.Workflow/Hal/`):
- `IHvgDriver` - High-voltage generator control
- `IDetector` - Detector control and image acquisition
- `ISafetyInterlock` - Safety interlock monitoring (9 interlocks)
- `IAecController` - Automatic exposure control
- `IDoseTracker` - Dose tracking integration

**C++ HAL Implementation** (`libs/hnvue-hal/`):
- Device driver wrappers
- Hardware communication protocols
- Real-time control loops

## Technology Stack

### Core Technologies
- **C++**: C++17/20, CMake 3.25+, vcpkg
- **C#**: C# 12, .NET 8 LTS
- **UI**: WPF (Windows-only), MVVM pattern
- **IPC**: gRPC 1.68.x, Protocol Buffers
- **DICOM**: fo-dicom 5.x
- **Database**: SQLite 3.x (WAL mode)
- **Image Processing**: OpenCV 4.x, FFTW 3.x

### Development Tools
- **Build**: CMake, MSBuild, dotnet CLI
- **Testing**: xUnit, Google Test
- **CI/CD**: Gitea Actions (self-hosted)
- **Quality**: MoAI-ADK (TRUST 5 framework)

## Design Patterns

### MVVM (Model-View-ViewModel)
- **Purpose**: Separation of UI from business logic
- **Implementation**: 16 ViewModels, WPF data binding
- **Benefits**: Testability, maintainability, designer-developer workflow

### State Machine
- **Purpose**: Clinical workflow orchestration
- **Implementation**: 10-state workflow with guard clauses
- **Benefits**: Predictable state transitions, safety verification

### Repository Pattern
- **Purpose**: Data access abstraction
- **Implementation**: ProtocolRepository, DoseRecordRepository
- **Benefits**: Testability, persistence layer isolation

### Observer Pattern
- **Purpose**: Real-time status updates
- **Implementation**: IObservable<T>, IWorkflowEventPublisher
- **Benefits**: Loose coupling, reactive UI updates

### Dependency Injection
- **Purpose**: Inversion of control
- **Implementation**: Microsoft.Extensions.DependencyInjection
- **Benefits**: Testability, modularity, configuration management

## Security Considerations

### Audit Trail
- **Standard**: FDA 21 CFR Part 11
- **Implementation**: SHA-256 hash chain
- **Coverage**: All dose-related operations, system configuration changes

### Authentication & Authorization
- **User Management**: Role-based access control
- **Session Management**: Secure token handling
- **Audit Logging**: All user actions logged

### Data Protection
- **Encryption**: TLS 1.3 for gRPC communication
- **Patient Data**: HIPAA compliance considerations
- **Dose Data**: Tamper-evident storage

## Performance Characteristics

### Real-Time Requirements
- **Interlock Check**: < 10ms (SPEC requirement)
- **State Transition**: < 50ms
- **Event Delivery**: < 50ms (verified with Stopwatch)
- **Image Acquisition**: < 5 seconds (typical)

### Throughput
- **Protocol Lookup**: < 50ms (500 protocols)
- **DICOM Association**: < 5 seconds
- **PACS Export**: Retry queue with exponential backoff

### Scalability
- **Multi-User**: Concurrent workflow support
- **Multi-Study**: Simultaneous patient processing
- **Multi-Device**: Scalable HAL architecture

## Deployment Architecture

### Development Environment
- **Primary**: Windows 10/11 (전체 기능 통합 개발)
- **Target Production**: Windows 10/11 Embedded

### Production Environment
- **Target**: Windows 10/11
- **Deployment**: XCOPY deployment (no GAC required)
- **Configuration**: JSON-based configuration files

### Hardware Requirements
- **Minimum**: 8GB RAM, quad-core CPU, 500MB disk
- **Recommended**: 16GB RAM, 8-core CPU, 2GB disk
- **Network**: Gigabit Ethernet for DICOM/gRPC

## Related Documentation

- [Project Structure](../README.md#project-structure)
- [Test Reports](test-reports/)
- [SPEC Documents](../.moai/specs/)
- [Regulatory Compliance](../README.md#regulatory-compliance)
