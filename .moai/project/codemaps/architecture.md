# HnVue Console - Architecture Documentation

## Architecture Overview

HnVue Console is a medical X-ray GUI console software (IEC 62304 Class B/C) built with C# 12 / .NET 8, WPF MVVM, and gRPC IPC.

## Core Architectural Patterns

### 1. MVVM Pattern (Model-View-ViewModel)

```csharp
// ViewModelBase provides common functionality
public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
{
    public bool IsDemoMode { get; protected set; } // Offline mode indicator
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null);
}
```

**Components**:
- **Views**: XAML-based UI with data binding (PatientView, WorklistView, AcquisitionView)
- **ViewModels**: Business logic and state management (ShellViewModel, PatientViewModel, WorklistViewModel)
- **Models**: Domain entities using C# records (SystemOverallStatus, ComponentStatus, PatientInfo)
- **Commands**: RelayCommand and AsyncRelayCommand for user actions
- **Converters**: Value converters for UI data binding (StatusToBrushConverter, BoolToVisibilityConverter)

### 2. Adapter Pattern (gRPC Integration)

```csharp
public abstract class GrpcAdapterBase : IDisposable
{
    protected T CreateClient<T>() where T : Grpc.Core.ClientBase<T>;
    protected async Task<bool> TryConnectAsync(int timeoutMs = 2000);
    protected ConnectivityState GetChannelState();
}
```

**Features**:
- **Shared Channels**: Singleton lifetime for efficient connection management
- **Streaming Support**: IAsyncEnumerable for real-time updates
- **Error Handling**: Graceful degradation with RpcException handling
- **Demo Mode**: Safe defaults when services unavailable

### 3. Facade Pattern (DICOM Integration)

```csharp
public sealed class DicomServiceFacade : IDicomServiceFacade
{
    private readonly IStorageScu _storageScu;
    private readonly IWorklistScu _worklistScu;
    private readonly IMppsScu _mppsScu;
    private readonly IStorageCommitScu _storageCommitScu;
    private readonly IRdsrBuilder _rdsrBuilder;
}
```

**DICOM Features**:
- **IOD Construction**: DX/CR image builders with DICOM standard compliance
- **SCU Operations**: Storage, worklist, MPPS, and storage commitment
- **Retry Logic**: Automatic retry with configurable destinations
- **RDSR Export**: Radiation Dose Structured Report generation

### 4. Dependency Injection

```csharp
public static void AddHnVueConsole(this IServiceCollection services, IConfiguration configuration)
{
    // ViewModels - Transient lifetime (fresh state)
    services.AddTransient<ShellViewModel>();
    services.AddTransient<PatientViewModel>();

    // Service Adapters - Singleton lifetime (shared gRPC channels)
    services.AddSingleton<IPatientService, PatientServiceAdapter>();
    services.AddSingleton<IWorklistService, WorklistServiceAdapter>();

    // Rendering Services - Singleton lifetime
    services.AddSingleton<GrayscaleRenderer>();
    services.AddSingleton<WindowLevelTransform>();
}
```

**Lifetime Strategy**:
| Lifetime | Usage | Examples |
|----------|-------|----------|
| **Singleton** | gRPC channels, shared services | GrpcAdapterBase, GrayscaleRenderer |
| **Transient** | ViewModels (fresh state) | All ViewModels |
| **Scoped** | Per-request services | (future use) |

## Communication Layer

### gRPC IPC Communication

**Design Decisions**:
- **Binary Protocol**: Efficiency for medical imaging data
- **Unary Calls**: Simple request/response (exposure triggers)
- **Server Streaming**: Real-time updates (status notifications)
- **Connection Health**: Automatic connectivity monitoring
- **Fallback Values**: Default data when services unavailable

**Currently Used gRPC Services**:
| Proto Service | Methods Called | Used By |
|---------------|----------------|---------|
| `CommandService` | GetSystemState, StartExposure, AbortExposure, RunCalibration | SystemStatus, Exposure, SystemConfig |
| `HealthService` | SubscribeHealth | SystemStatus |
| `ConfigService` | GetConfiguration, SetConfiguration | SystemConfig, Network |

## Data Flow

```
User Action (View)
    ↓
Command (AsyncRelayCommand)
    ↓
ViewModel Method
    ↓
Service Interface (I*Service)
    ↓
Adapter (gRPC or Mock)
    ↓
Backend Service (gRPC Server) or Mock Data
    ↓
ViewModel Property Update
    ↓
UI Binding Update (View)
```

## Key Technical Decisions

| Decision | Rationale |
|----------|-----------|
| **gRPC over HTTP** | Binary protocol efficiency for medical imaging |
| **WPF over Modern UI** | Desktop application for clinical environment |
| **Records over Classes** | Immutability for data transfer objects |
| **Singleton Channels** | Shared gRPC connections for efficiency |
| **Graceful Degradation** | Safe defaults when services unavailable |
| **Demo Mode** | Visual indicators for offline operation |

## Risk Assessment

| Component | Risk Level | Notes |
|-----------|------------|-------|
| PatientService | **High** | Critical for patient care workflow |
| ImageService | **High** | Core viewer functionality |
| WorklistService | **High** | DICOM MWL integration |
| DoseService | **High** | IEC 62304 safety requirement |
| ProtocolService | **Medium** | Affects clinical workflow |
| AECService | **Medium** | Image quality control |
| SystemStatus | **Low** | Partially implemented |
| ExposureService | **Medium** | Partially implemented |
