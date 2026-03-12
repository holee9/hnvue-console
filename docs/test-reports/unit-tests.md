# Unit Test Summary

**Last Updated: 2026-03-12**

## Overall Test Statistics

- **Total Tests**: 1,048
- **Pass Rate**: 100% ✅
- **Coverage**: ~85%+

## Test Suite Breakdown

### HnVue.Workflow.Tests: 351 tests ✅

**Clinical Workflow Engine**
- State machine transitions (10 states)
- Guard clause evaluation
- Study context management
- Protocol validation
- Dose tracking coordination
- Multi-exposure handling
- HAL integration points

**Key Test Areas**:
- `StateMachine/` - Workflow state transitions
- `States/` - Study context and state handlers
- `Safety/` - Interlock validation (9 interlocks)
- `Study/` - Multi-exposure coordination
- `Protocol/` - Protocol repository and validation
- `Dose/` - Dose tracking and limits
- `Hal/Simulators/` - HAL simulator implementations

### HnVue.Dose.Tests: 222 tests ✅

**Radiation Dose Management**
- DAP calculation accuracy
- Cumulative dose tracking
- Real-time dose display
- DRL comparison and alerts
- RDSR data generation
- Audit trail integrity (SHA-256)

**Key Test Areas**:
- `Calculation/` - DAP calculator, calibration
- `Recording/` - Dose record repository, audit trail
- `Display/` - Dose display notifications
- `Alerting/` - DRL comparison
- `RDSR/` - RDSR data provider

### HnVue.Dicom.Tests: 256 tests ✅

**DICOM Communication Services**
- C-FIND Worklist queries
- MPPS N-CREATE/N-SET operations
- C-STORE PACS export
- Association pooling
- Error handling and graceful degradation

**Key Test Areas**:
- `Worklist/` - DICOM MWL client
- `Mpps/` - MPPS creation and updates
- `Store/` - PACS export and retry queue
- `Association/` - Connection pooling
- `Common/` - Error handling

### HnVue.Console.Tests: 219 tests ✅

**WPF MVVM ViewModels**
- MVVM pattern compliance
- ViewModel lifecycle management
- Command binding validation
- Property change notifications
- Dependency injection integration

**Key Test Areas**:
- `ViewModels/` - All 16 ViewModels
- `Commands/` - RelayCommand, AsyncRelayCommand
- `MvvmCompliance/` - MVVM pattern validation

## Running Tests

### All Tests
```bash
dotnet test
```

### Specific Test Suite
```bash
# Workflow Engine
dotnet test tests/csharp/HnVue.Workflow.Tests/

# Dose Management
dotnet test tests/csharp/HnVue.Dose.Tests/

# DICOM Services
dotnet test tests/csharp/HnVue.Dicom.Tests/

# Console UI (ViewModels)
dotnet test tests/csharp/HnVue.Console.Tests/
```

### With Coverage
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults/Coverage
```

### Filter by Test Name
```bash
# Only ViewModel tests
dotnet test --filter "FullyQualifiedName~ViewModels"

# Only safety-critical tests
dotnet test --filter "FullyQualifiedName~Safety"
```

## Test Quality Metrics

### Code Coverage
- **Overall**: ~85%+
- **Critical Components**: >90%
- **Business Logic**: >95%

### MX Tag Coverage
- **267 MX tags** across **43 files** in HnVue.Workflow
- Tags: `@MX:NOTE`, `@MX:ANCHOR`, `@MX:WARN`, `@MX:TODO`, `@MX:REASON`
- High fan_in functions annotated for AI context

### TRUST 5 Score
- **Current**: 3.8/5.0
- **Target**: 5.0/5.0
- **Focus Areas**: Stub adapter implementation, test coverage expansion

## Platform Support

### Linux-Compatible Tests (Primary Development)
- ✅ HnVue.Workflow.Tests (351 tests)
- ✅ HnVue.Dose.Tests (222 tests)
- ✅ HnVue.Dicom.Tests (256 tests)
- ✅ HnVue.Console.Tests ViewModel tests (219 tests)

**Total Linux-Compatible**: 1,048 tests

### Windows-Only Tests
- WPF XAML view rendering tests
- Hardware driver integration tests
- Visual Studio design-time tests

## Test Categories

### Safety-Critical Tests
- Interlock validation (9 interlocks)
- Dose limit enforcement
- Exposure abort mechanisms
- Audit trail integrity

### Integration Tests
- End-to-end workflows
- Hardware failure scenarios
- DICOM network failures
- See [integration-tests.md](integration-tests.md) for details

### Performance Tests
- Composite key lookup (< 50ms)
- Dose calculation accuracy
- State transition latency
- DICOM association pooling

### Compliance Tests
- IEC 62304 requirements
- FDA 21 CFR Part 11 audit trail
- DICOM conformance
- DRL comparison accuracy

## Related Documentation

- [Integration Test Results](integration-tests.md)
- [Quality Validation Report](../../quality_validation_report.md)
- [SPEC-TEST-001](../../.moai/specs/SPEC-TEST-001/spec.md)
