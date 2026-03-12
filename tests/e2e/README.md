# HnVue Console E2E Tests

FlaUI-based End-to-End UI Tests for HnVue Console WPF Application.

## Overview

E2E tests use **AutomationId** as the primary element location strategy for stability. All key UI elements have `AutomationProperties.AutomationId` attributes assigned, making tests resilient to text changes and localization updates.

## Prerequisites

- Windows OS (required for WPF and FlaUI)
- .NET 8 SDK
- HnVue.Console application built in Debug or Release configuration

## Build Application Before Running Tests

```bash
# From solution root
dotnet build src/HnVue.Console/HnVue.Console.csproj -c Debug
```

## Running Tests

### Run All E2E Tests

```bash
dotnet test tests/e2e/HnVue.Console.E2E.Tests/HnVue.Console.E2E.Tests.csproj
```

### Run Specific Test Category

```bash
# Application Startup Tests
dotnet test tests/e2e/HnVue.Console.E2E.Tests/HnVue.Console.E2E.Tests.csproj --filter "Category=E2E"

# Navigation Tests Only
dotnet test tests/e2e/HnVue.Console.E2E.Tests/HnVue.Console.E2E.Tests.csproj --filter "UserJourney=Navigation"

# Patient Management Tests
dotnet test tests/e2e/HnVue.Console.E2E.Tests/HnVue.Console.E2E.Tests.csproj --filter "UserJourney=PatientManagement"

# Worklist Tests
dotnet test tests/e2e/HnVue.Console.E2E.Tests/HnVue.Console.E2E.Tests.csproj --filter "UserJourney=Worklist"
```

### Run with Verbose Output

```bash
dotnet test tests/e2e/HnVue.Console.E2E.Tests/HnVue.Console.E2E.Tests.csproj --logger "console;verbosity=detailed"
```

## Test Categories

| Category | Description | Test File |
|----------|-------------|-----------|
| ApplicationLaunch | Application startup and main window verification | ApplicationStartupTests.cs |
| Navigation | View navigation between Patient, Worklist, etc. | NavigationTests.cs |
| PatientManagement | Patient search, registration, selection | PatientManagementTests.cs |
| Worklist | Worklist refresh and procedure selection | WorklistTests.cs |
| LocaleSelection | Language switching (Korean/English) | LocaleSelectionTests.cs |

## User Journeys Covered

### 1. Application Launch Journey
- Application starts and displays main window
- Navigation bar is visible with all buttons
- Status bar is present
- Default view is Patient Management

### 2. Navigation Journey
- Click Patient button -> Patient Management view
- Click Worklist button -> Modality Worklist view
- Click Status button -> System Status view
- Click Config button -> Configuration view
- Click Audit Log button -> Audit Log view
- Sequential navigation through multiple views

### 3. Patient Management Journey
- View search controls (Search, Register, Emergency buttons)
- View patient list DataGrid
- Enter search text in search box
- Emergency registration creates patient and navigates to Worklist
- Status bar shows patient count

### 4. Worklist Journey
- View Refresh button
- View procedure DataGrid with expected columns
- Refresh button is clickable
- Status bar shows procedure count and last refreshed time

### 5. Locale Selection Journey
- Locale ComboBox exists in status bar
- Korean (default) and English options available
- Can change locale selection

## Architecture

```
tests/e2e/HnVue.Console.E2E.Tests/
├── HnVue.Console.E2E.Tests.csproj   # Project file with FlaUI dependencies
├── TestBase.cs                       # Base class with app lifecycle management
├── ApplicationStartupTests.cs        # Application launch tests
├── NavigationTests.cs                # View navigation tests
├── PatientManagementTests.cs         # Patient management tests
├── WorklistTests.cs                  # Worklist tests
├── LocaleSelectionTests.cs           # Locale switching tests
└── xunit.runner.json                 # xUnit configuration (sequential execution)
```

## TestBase Features

- **Application Lifecycle**: Launches and disposes WPF application
- **Window Management**: Finds and attaches to main window
- **Element Finding**: Helper methods for finding UI elements
  - `FindButtonByAutomationId(id, fallbackText)`: Primary method with text fallback
  - `FindElementByAutomationId(id)`: General element finding
  - `FindButtonByText(text)`: Legacy text-based search (fallback)
- **Wait Support**: Async waiting for elements to appear
- **Screenshot Capture**: Debugging support (placeholder)

## AutomationId Strategy

### Why AutomationId?

1. **Stability**: Unchanged by localization, UI text updates
2. **Performance**: Direct lookup faster than text search
3. **Maintainability**: Clear element identification

### Assigned AutomationIds

| UI Element | AutomationId | Location |
|------------|--------------|----------|
| Patient Button | NavigatePatientButton | MainWindow |
| Worklist Button | NavigateWorklistButton | MainWindow |
| Status Button | NavigateStatusButton | MainWindow |
| Config Button | NavigateConfigButton | MainWindow |
| Audit Log Button | NavigateAuditLogButton | MainWindow |
| Patient Search Box | PatientSearchTextBox | PatientView |
| Patient Search Button | PatientSearchButton | PatientView |
| Patient Register Button | PatientRegisterButton | PatientView |
| Patient Emergency Button | PatientEmergencyButton | PatientView |
| Worklist Refresh Button | WorklistRefreshButton | WorklistView |
| Worklist Status Bar | WorklistStatusBar | WorklistView |
| Locale Selector | LocaleSelectorComboBox | MainWindow |

### Adding New AutomationIds

When adding new UI elements that need E2E testing:

```xml
<!-- XAML Example -->
<Button Content="New Button"
        Command="{Binding NewCommand}"
        AutomationProperties.AutomationId="DescriptiveAutomationId"/>
```

```csharp
// Test Example
var button = await WaitForElementAsync(
    () => FindButtonByAutomationId("DescriptiveAutomationId"),
    TimeSpan.FromSeconds(5));
```

## Configuration

E2E tests run sequentially (parallelization disabled) to prevent UI automation conflicts.

```json
// xunit.runner.json
{
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1
}
```

## Troubleshooting

### Application Not Found Error

```
Application not found at .../HnVue.Console.exe
```

**Solution**: Build the application first:
```bash
dotnet build src/HnVue.Console/HnVue.Console.csproj -c Debug
```

### Main Window Not Found

```
Failed to find main window after launching application
```

**Solution**: Ensure application starts correctly. Check for:
- Missing dependencies
- Configuration errors
- Application crashes on startup

### Element Not Found

```
Expected element not found
```

**Solution**:
- Increase wait time for slow operations
- Verify element automation properties
- Check if element is in a different view

## Dependencies

- **FlaUI.UIA3**: UI Automation framework for Windows
- **xUnit**: Testing framework
- **FluentAssertions**: Readable assertion library

## Notes

- E2E tests require a Windows environment with UI access
- Tests interact with actual WPF application (not mocked)
- Screenshots directory is created automatically for debugging
- Tests clean up application process after each test class
