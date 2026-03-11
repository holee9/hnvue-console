# Tests Scripts Module

This module provides CI pipeline utilities for SPEC-TEST-001 Phase 3.3 & 3.4.

## Overview

The scripts in this directory support:

- **RTM Gate Integration**: Validates RTM completeness in CI pipeline
- **Test Report Consolidation**: Consolidates JUnit XML reports from multiple test suites
- **Build Summary Generation**: Generates comprehensive build summary with coverage and RTM status

## Requirements Coverage

| Requirement | Module | Description |
|-------------|-------|-------------|
| FR-TEST-08.1 | consolidate_reports.py | JUnit XML format test reports |
| FR-TEST-08.2 | consolidate_reports.py | Consolidated JUnit XML report |
| FR-TEST-08.3 | consolidate_reports.py | Test case details preservation |
| FR-TEST-08.4 | generate_summary.py | Cobertura XML coverage parsing |
| FR-TEST-08.5 | generate_summary.py | Report retention for audit |
| FR-TEST-09.1 | rtm_gate.py | RTM requirement-to-test linking |
| FR-TEST-09.2 | rtm_gate.py | Bidirectional traceability |
| FR-TEST-09.3 | rtm_gate.py | Test case requirement reference |
| FR-TEST-09.4 | rtm_gate.py | Incomplete requirement flagging |
| NFR-TEST-01 | generate_summary.py | Coverage threshold checking |
| NFR-TEST-04 | generate_summary.py | Class C merge gate |
| NFR-TEST-08 | rtm_gate.py | Traceability validation |

## Usage

### RTM Gate Validation

```bash
python tests/scripts/rtm_gate.py tests/traceability/rtm.csv --output rtm-gate-result.json
```

### Report Consolidation

```bash
python tests/scripts/consolidate_reports.py TestResults --output consolidated-junit.xml
```

### Build Summary Generation
```bash
python tests/scripts/generate_summary.py TestResults --output build-summary.md
```

## CI Integration

These scripts are integrated into `.gitea/workflows/ci.yml`:

- **rtm-gate job**: Runs after coverage-gate to validate RTM completeness
- **generate-summary job**: Generates final build summary with all artifacts

## Test Coverage

All modules have 100% test coverage:

| Module | Coverage |
|--------|---------|
| test_consolidate_reports.py | 100% |
| test_generate_summary.py | 100% |
| test_rtm_gate.py | 100% |
