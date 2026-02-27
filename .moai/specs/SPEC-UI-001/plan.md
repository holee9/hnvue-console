# SPEC-UI-001: Implementation Plan

## Metadata

| Field    | Value                                         |
|----------|-----------------------------------------------|
| SPEC ID  | SPEC-UI-001                                   |
| Title    | HnVue GUI Console User Interface              |
| Package  | src/HnVue.Console/                            |
| Language | C# 12 / .NET 8 LTS                           |
| Pattern  | MVVM (no System.Windows in ViewModels)        |

---

## 1. Milestones

### Primary Goal: Shell & Infrastructure (4A)

Deliver the WPF application foundation: project scaffold, DI container, service interfaces, localization, and design system. All subsequent View work depends on this foundation.

Components in scope:
- `HnVue.Console.csproj` — WPF application project targeting .NET 8
- `App.xaml` / `App.xaml.cs` — Application entry point, DI bootstrapping
- `MainWindow.xaml` — Shell window with NavigationBar, StatusBar, and ContentRegion
- `ShellViewModel` — Navigation state, global status badge
- 9 gRPC service interfaces (`IPatientService`, `IWorklistService`, `IExposureService`, `IProtocolService`, `IAECService`, `IDoseService`, `IImageService`, `IQCService`, `ISystemStatusService`, `ISystemConfigService`, `IUserService`, `INetworkService`, `IAuditLogService`)
- Localization resources (`Resources.ko-KR.resx`, `Resources.en-US.resx`)
- Design system (`Colors.xaml`, `Typography.xaml`, `Spacing.xaml`, `BaseTheme.xaml`)
- `RelayCommand` / `AsyncRelayCommand` base implementations
- `ViewModelBase` implementing `INotifyPropertyChanged`

Acceptance Gate: Application launches, navigation between empty view placeholders works, DI container resolves all registered types, localization switches between Korean and English at runtime.

### Secondary Goal: Primary Views — Patient, Worklist, Acquisition (4B Part 1)

Deliver the first three primary screens and their ViewModels covering the patient management and acquisition workflow.

Components in scope:
- `PatientView.xaml` + `PatientViewModel` — search, registration dialog, emergency registration, edit
- `PatientRegistrationDialog.xaml` / `PatientEditDialog.xaml` — modal dialogs
- `WorklistView.xaml` + `WorklistViewModel` — MWL display, procedure selection, refresh
- `AcquisitionView.xaml` + `AcquisitionViewModel` — real-time preview host, AEC toggle, dose display
- `ExposureParameterPanel.xaml` + `ExposureParameterViewModel` — kVp, mA, time, SID, FSS
- `ProtocolSelectionPanel.xaml` + `ProtocolViewModel` — body part / projection selection
- `AECTogglePanel.xaml` — AEC enable/disable with visual indicator
- `DoseDisplayPanel.xaml` + `DoseViewModel` — current dose, cumulative dose, alert threshold

Acceptance Gate: Patient search returns results from mocked `IPatientService`. Worklist populates from mocked `IWorklistService`. AcquisitionView renders protocol panel and exposure parameters. AEC toggle updates ExposureParameterPanel field editability.

### Tertiary Goal: Primary Views — Image Review, Status, Config, Audit Log (4B Part 2)

Deliver the remaining four primary screens.

Components in scope:
- `ImageReviewView.xaml` + `ImageReviewViewModel` — image viewport, measurement tool panel, QC actions
- `ImageViewerPanel.xaml` — 16-bit grayscale WriteableBitmap render host
- `MeasurementToolPanel.xaml` — tool selection (Distance, Angle, Cobb, Annotation)
- `QCActionPanel.xaml` — Accept / Reject (with reason prompt) / Reprocess
- `SystemStatusView.xaml` + `SystemStatusViewModel` — component status tiles, traffic light badge
- `ConfigurationView.xaml` + `ConfigurationViewModel` — tabbed config sections (Calibration, Network, Users, Logging)
- `AuditLogView.xaml` + `AuditLogViewModel` — paginated log table with filter controls
- `ConfirmationDialog.xaml` / `ErrorDialog.xaml` — shared dialogs

Acceptance Gate: ImageReviewView renders placeholder image. QC buttons trigger mocked `IQCService` calls. SystemStatusView shows component status from mocked stream. ConfigurationView enforces role-based section visibility. AuditLogView loads and filters mock entries.

### Final Goal: Image Viewer Core + Testing (4C + 4D)

Deliver the 16-bit grayscale rendering pipeline, W/L transformation, measurement overlay engine, and the full ViewModel unit test suite.

Components in scope:
- `GrayscaleRenderer` — `WriteableBitmap` (Gray16) pixel pipeline
- `WindowLevelTransform` — DICOM PS 3.14 GSDF-compliant W/L LUT computation
- `MeasurementOverlayService` — distance (mm), angle (°), Cobb angle (°), free-text annotation
- `MeasurementOverlay` model — session-scoped persistence
- `HnVue.Console.Tests` project — xUnit + Moq ViewModel unit tests
- MVVM compliance validator — reflection-based check for `System.Windows` references in ViewModel assemblies

Acceptance Gate: W/L slider updates Gray16 image within 200 ms. Distance measurement produces correct mm value given pixel spacing metadata. All ViewModel unit tests green. Reflection scan reports zero `System.Windows` references in ViewModel classes.

---

## 2. Technical Approach

### 2.1 Project Structure

```
src/HnVue.Console/
├── App.xaml
├── App.xaml.cs
├── HnVue.Console.csproj
├── Shell/
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   └── ShellViewModel.cs
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── PatientViewModel.cs
│   ├── WorklistViewModel.cs
│   ├── AcquisitionViewModel.cs
│   ├── ExposureParameterViewModel.cs
│   ├── ProtocolViewModel.cs
│   ├── DoseViewModel.cs
│   ├── ImageReviewViewModel.cs
│   ├── SystemStatusViewModel.cs
│   ├── ConfigurationViewModel.cs
│   └── AuditLogViewModel.cs
├── Views/
│   ├── PatientView.xaml
│   ├── WorklistView.xaml
│   ├── AcquisitionView.xaml
│   ├── ImageReviewView.xaml
│   ├── SystemStatusView.xaml
│   ├── ConfigurationView.xaml
│   └── AuditLogView.xaml
├── Panels/
│   ├── ExposureParameterPanel.xaml
│   ├── ProtocolSelectionPanel.xaml
│   ├── AECTogglePanel.xaml
│   ├── DoseDisplayPanel.xaml
│   ├── ImageViewerPanel.xaml
│   ├── MeasurementToolPanel.xaml
│   └── QCActionPanel.xaml
├── Dialogs/
│   ├── PatientRegistrationDialog.xaml
│   ├── PatientEditDialog.xaml
│   ├── ConfirmationDialog.xaml
│   └── ErrorDialog.xaml
├── Services/
│   ├── IPatientService.cs
│   ├── IWorklistService.cs
│   ├── IExposureService.cs
│   ├── IProtocolService.cs
│   ├── IAECService.cs
│   ├── IDoseService.cs
│   ├── IImageService.cs
│   ├── IQCService.cs
│   ├── ISystemStatusService.cs
│   ├── ISystemConfigService.cs
│   ├── IUserService.cs
│   ├── INetworkService.cs
│   └── IAuditLogService.cs
├── Rendering/
│   ├── GrayscaleRenderer.cs
│   ├── WindowLevelTransform.cs
│   └── MeasurementOverlayService.cs
├── Commands/
│   ├── RelayCommand.cs
│   └── AsyncRelayCommand.cs
├── Resources/
│   ├── Strings.ko-KR.resx
│   ├── Strings.en-US.resx
│   └── Styles/
│       ├── Colors.xaml
│       ├── Typography.xaml
│       ├── Spacing.xaml
│       └── BaseTheme.xaml
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs

tests/HnVue.Console.Tests/
├── HnVue.Console.Tests.csproj
├── ViewModels/
│   ├── PatientViewModelTests.cs
│   ├── WorklistViewModelTests.cs
│   ├── AcquisitionViewModelTests.cs
│   ├── ExposureParameterViewModelTests.cs
│   ├── ProtocolViewModelTests.cs
│   ├── DoseViewModelTests.cs
│   ├── ImageReviewViewModelTests.cs
│   ├── SystemStatusViewModelTests.cs
│   ├── ConfigurationViewModelTests.cs
│   └── AuditLogViewModelTests.cs
└── Compliance/
    └── MvvmComplianceTests.cs
```

### 2.2 MVVM Enforcement

**Hard constraint**: ViewModel assembly must contain zero references to `System.Windows.*`. This is validated by a reflection-based test in `MvvmComplianceTests.cs` that scans all types in the ViewModel assembly and asserts no field, property, parameter, or return type resolves from the `System.Windows` namespace.

All ViewModels inherit from `ViewModelBase`:
- Implements `INotifyPropertyChanged`
- Provides `SetProperty<T>` helper for change notification
- Provides `OnPropertyChanged` overload for computed properties

All commands are exposed as `ICommand` via `RelayCommand` (synchronous) or `AsyncRelayCommand` (async operations with `CancellationToken` support). No event handlers are wired from View code-behind to ViewModel methods.

### 2.3 Dependency Injection

All ViewModels and Services are registered at application startup in `ServiceCollectionExtensions.cs`:

```
services.AddHnVueConsole(configuration);
```

ViewModels are registered with `Transient` lifetime. Service implementations that wrap `HnVue.Ipc.Client` are registered with `Singleton` lifetime (gRPC channel is shared). The WPF `DataContext` for each View is resolved from the DI container — Views do not `new` their ViewModels.

### 2.4 gRPC Integration

The pre-existing `HnVue.Ipc.Client` library (8 files, ~1,584 LOC) provides the gRPC channel for 5 service groups: Command, Config, Health, Image, IPC. Each of the 13 service interfaces defined in this SPEC maps to one or more operations on these channels. Service implementations adapt gRPC responses to domain model types consumed by ViewModels.

### 2.5 Asynchronous Operation Pattern

All gRPC calls are dispatched via `AsyncRelayCommand`. `IsBusy` observable property is set on the ViewModel before the call and cleared in a `finally` block, enabling the View to bind a loading indicator and disable action buttons during in-flight operations. The UI thread is never blocked.

Server-streaming responses (preview frames, status updates, dose notifications) are consumed via `IAsyncEnumerable<T>` adapters on the service interfaces, subscribed using `await foreach` in `CancellationToken`-aware background tasks started from the ViewModel. Results are marshalled to the UI thread via `Application.Current.Dispatcher.InvokeAsync`.

### 2.6 16-bit Grayscale Rendering

`GrayscaleRenderer` creates a `WriteableBitmap` with `PixelFormats.Gray16`. Pixel data received from the Core Engine (16-bit unsigned shorts) is written directly to the `WriteableBitmap` back buffer using unsafe memory copy. `WindowLevelTransform` pre-computes a 65536-entry LUT from the W/L window center and width values, applied per-pixel before writing to the buffer. Hardware-accelerated WPF rendering via Direct3D is enabled by setting `RenderOptions.BitmapScalingMode` to `HighQuality`.

### 2.7 Measurement Overlay Rendering

Measurement overlays are drawn as WPF `Adorner` elements over the `ImageViewerPanel`. `MeasurementOverlayService` maintains a session-scoped list of `MeasurementOverlay` objects. Each overlay stores its geometry in image-coordinate space and is re-projected to screen space on zoom/pan/rotate transforms. The distance measurement applies pixel spacing metadata (received from Core Engine with the image) to convert pixel distances to millimeters.

### 2.8 Localization

All user-facing strings are stored in `.resx` files keyed by string ID. `LocalizationService` wraps `ResourceManager` and exposes an `IObservable<CultureInfo>` for runtime locale switching. ViewModels do not reference string literals; they bind to properties exposed by `LocalizationService` or use XAML markup extensions backed by it.

---

## 3. Risks and Mitigations

| Risk                                                          | Likelihood | Impact | Mitigation                                                                      |
|---------------------------------------------------------------|------------|--------|---------------------------------------------------------------------------------|
| gRPC proto service contracts not finalized (OI-01)           | Medium     | High   | Define service interfaces against placeholder proto stubs; replace adapters when protos are finalized |
| 16-bit grayscale WriteableBitmap performance below 200 ms    | Low        | High   | Profile Gray16 unsafe write path early; fall back to pre-computed Gray8 LUT display if needed |
| MVVM discipline drift (System.Windows leak into ViewModel)   | Low        | Medium | Enforce via reflection test in CI from day one; fail build on any violation     |
| Dose alert threshold not yet agreed (OI-04)                  | Medium     | Low    | Implement threshold as a configurable value injected via `IDoseService`; UI displays whatever the service provides |
| Multi-monitor support (FR-UI-14) hardware unavailability (OI-02) | Medium  | Low    | Implement as optional feature gated by `SystemParameters.PrimaryScreenWidth`; skip if second monitor absent |
| Korean localization review not scheduled (OI-05)             | Medium     | Medium | Use machine-translated Korean strings initially; mark all strings with `// REVIEW` comment; replace after clinical review |
| High-contrast theme contrast ratio not validated (OI-03)     | Low        | Low    | Apply WCAG 2.1 AA formula at design time; defer usability study validation to Phase 5 TEST |

---

## 4. Architecture Design Direction

### 4.1 Separation of Concerns

- `src/HnVue.Console/` — WPF shell, Views, ViewModels, service interfaces; no business logic
- `src/HnVue.Ipc.Client/` — gRPC channel management (pre-existing, complete)
- `tests/HnVue.Console.Tests/` — ViewModel unit tests and MVVM compliance tests
- No direct DICOM library references; all image and protocol data arrives via gRPC service interfaces

### 4.2 Interface-First Design

All 13 service interfaces are defined before their gRPC adapter implementations. This allows ViewModel unit tests to inject `Moq`-generated mocks without requiring a running Core Engine. The gRPC adapter implementations are registered in the DI container only in the production entry point (`App.xaml.cs`).

### 4.3 Observability

All gRPC-bound ViewModel commands log structured events using `Microsoft.Extensions.Logging`:
- Command invoked (Debug)
- gRPC call initiated / completed (Info)
- gRPC stream frame received (Verbose / Trace)
- Error response from Core Engine (Warning / Error)

No PHI (patient name, patient ID, birth date) appears in log output at Info level or above.

### 4.4 Safety-Critical Confirmation Pattern

All safety-critical operator actions (exposure initiation, image QC accept/reject, dose alert acknowledgment) require explicit confirmation via `ConfirmationDialog` before the corresponding gRPC command is dispatched. This is implemented as a guard step inside the `AsyncRelayCommand` execute body, calling `IDialogService.ConfirmAsync()` and cancelling the command if the operator dismisses without confirming.

---

## 5. Dependency Order

1. Define 13 gRPC service interfaces (blocks all ViewModel development)
2. Implement `ViewModelBase`, `RelayCommand`, `AsyncRelayCommand` (foundation for all ViewModels)
3. Implement Shell (`MainWindow`, `ShellViewModel`, DI bootstrapping)
4. Implement localization infrastructure and design system
5. Implement `PatientViewModel` + `PatientView` (simplest CRUD workflow)
6. Implement `WorklistViewModel` + `WorklistView` (read-only list with refresh)
7. Implement `ExposureParameterViewModel`, `ProtocolViewModel`, `DoseViewModel` (sub-ViewModels used by AcquisitionView)
8. Implement `AcquisitionViewModel` + `AcquisitionView` (composes sub-ViewModels from step 7)
9. Implement `GrayscaleRenderer` and `WindowLevelTransform` (rendering pipeline)
10. Implement `MeasurementOverlayService` (overlay engine)
11. Implement `ImageReviewViewModel` + `ImageReviewView` (depends on steps 9–10)
12. Implement `SystemStatusViewModel` + `SystemStatusView`
13. Implement `ConfigurationViewModel` + `ConfigurationView`
14. Implement `AuditLogViewModel` + `AuditLogView`
15. Write ViewModel unit tests for all ViewModels (85% coverage gate)
16. Write `MvvmComplianceTests` (reflection scan for `System.Windows` references)
17. Validate layout at 1920 × 1080, 96/120/144/192 DPI; validate Korean locale strings

---

## 6. Test Strategy

### 6.1 ViewModel Unit Tests (xUnit + Moq)

Each ViewModel class has a corresponding `*Tests.cs` file in `tests/HnVue.Console.Tests/ViewModels/`. Tests mock all service interfaces using Moq. No WPF application instance is required (`STA` thread attribute is applied only to tests that exercise `WriteableBitmap` directly).

Coverage target: **85% statement coverage** for all types in `src/HnVue.Console/ViewModels/` and `src/HnVue.Console/Rendering/`.

Key test categories per ViewModel:

| ViewModel               | Key Test Scenarios                                                                 |
|-------------------------|------------------------------------------------------------------------------------|
| PatientViewModel        | Search returns filtered list; empty mandatory field blocks submission; emergency registration skips demographics |
| WorklistViewModel       | View activation triggers MWL fetch; refresh button disabled during fetch; procedure selection navigates |
| AcquisitionViewModel    | Preview frame update within timing contract; AEC toggle propagates to ExposureParameterViewModel |
| ExposureParameterViewModel | Out-of-range value rejected with error message; AEC mode sets fields read-only |
| ProtocolViewModel       | Body part selection filters projection list; projection selection sends protocol to service |
| DoseViewModel           | Dose update propagates to observable properties; alert threshold breach sets alert flag |
| ImageReviewViewModel    | Accept dispatches gRPC command; Reject prompts for reason before dispatching; QC buttons disabled during in-flight |
| SystemStatusViewModel   | Component error state sets CanInitiateExposure to false; stream update reflected within timing contract |
| ConfigurationViewModel  | Non-admin role hides Users and Calibration sections; network field format validation |
| AuditLogViewModel       | Filter by date range requests filtered data; paginated load appends entries        |

### 6.2 MVVM Compliance Test

`MvvmComplianceTests.cs` uses reflection to enumerate all types in the `HnVue.Console` assembly whose names end in `ViewModel`. For each such type, it asserts that:
- No field, property, constructor parameter, or method parameter has a type from the `System.Windows` namespace
- No method return type is from the `System.Windows` namespace

This test runs in CI on every pull request and blocks merge on failure.

### 6.3 Manual Verification

| Verification Item                                     | Method                                                      |
|-------------------------------------------------------|-------------------------------------------------------------|
| Layout at 1920 × 1080, no scroll required             | Manual visual check at each DPI scaling level               |
| Korean locale rendering (Hangul, fonts)               | Launch with `ko-KR` culture; check all primary screens      |
| Runtime locale switch without restart                 | Toggle locale in ConfigurationView; observe all string updates |
| 200 ms W/L response time                             | Stopwatch instrumentation in `WindowLevelTransform`; log to Debug |
| Safety-critical confirmation dialogs present          | Walk through: exposure initiation, Accept, Reject workflows  |
