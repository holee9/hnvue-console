# HnVue Console - Project Structure

## Overall Architecture

```
hnvue-console/
├── src/
│   ├── HnVue.Console/              # WPF Application Layer (Presentation)
│   │   ├── Commands/               # ICommand implementations (AsyncRelayCommand, RelayCommand)
│   │   ├── Converters/             # Value converters for data binding
│   │   ├── DependencyInjection/    # ServiceCollectionExtensions
│   │   ├── Dialogs/                # Modal dialogs
│   │   ├── Models/                 # Data models (Patient, Worklist, Dose, etc.)
│   │   ├── Rendering/              # Image rendering services
│   │   ├── Services/
│   │   │   ├── Adapters/           # gRPC adapters (13 total: 4 partial, 9 stub)
│   │   │   ├── Mock*/              # Mock service implementations
│   │   │   └── I*Service.cs        # Service interfaces
│   │   ├── Shell/                  # MainWindow shell
│   │   ├── ViewModels/             # MVVM ViewModels
│   │   └── Views/                  # XAML views and panels
│   ├── HnVue.Dicom/                # DICOM Service Layer
│   ├── HnVue.Dose/                 # Dose Management Layer
│   ├── HnVue.Ipc.Client/           # gRPC IPC Client Layer
│   └── HnVue.Workflow/             # Workflow Engine Layer
└── tests/
    ├── HnVue.Console.Tests/        # ViewModels tests
    ├── HnVue.Dicom.Tests/          # DICOM tests
    ├── HnVue.Dose.Tests/           # Dose management tests
    └── HnVue.Workflow.Tests/       # Workflow tests
```

## Layer Organization

| Layer | Purpose | Key Components |
|-------|---------|----------------|
| **Presentation** | WPF UI with MVVM | Views, ViewModels, Commands, Converters |
| **Service Interface** | Clean service contracts | I*Service interfaces (13 total) |
| **Adapter Implementation** | gRPC integration | Service/Adapters/*Adapter.cs (13 adapters) |
| **Business Logic** | Domain services | Dose management, Workflow engine |
| **Infrastructure** | External integrations | DICOM, gRPC IPC |
| **Data Access** | Medical imaging | DICOM storage, MPPS, MWL |

## Architecture Patterns

1. **Clean Architecture**: Layered separation with clear boundaries
2. **MVVM Pattern**: Presentation logic separation with data binding
3. **Adapter Pattern**: gRPC integration with graceful degradation
4. **Facade Pattern**: DICOM operations unified entry point
5. **Dependency Injection**: Container-based service management
6. **Async-First**: Full async support for responsiveness
7. **Command Pattern**: User action handling through MVVM commands

## Dependency Lifetime Strategy

| Lifetime | Usage | Examples |
|----------|-------|----------|
| **Singleton** | gRPC channels, shared services | GrpcAdapterBase, GrayscaleRenderer |
| **Transient** | ViewModels (fresh state) | ShellViewModel, PatientViewModel |
| **Scoped** | Per-request services | (future use) |
