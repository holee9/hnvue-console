# SPEC-TEST-001 Phase 3.2: Component-Level Coverage Gates

This directory implements component-level coverage gates as specified in
SPEC-TEST-001 Section 1.3 (Component Safety Classification).

## Overview

The coverage gate system enforces different coverage thresholds based on
IEC 62304 safety classifications:

| Component              | Safety Class | Coverage Type | Threshold |
|------------------------|-------------|---------------|-----------|
| Generator Control      | C           | Decision      | 100%      |
| AEC                    | C           | Decision      | 100%      |
| Image Processing       | B           | Statement     | 85%       |
| DICOM Communication    | B           | Statement     | 85%       |
| Dose Management        | B           | Statement     | 90%       |
| UI / Viewer Layer      | B           | Statement     | 80%       |
| Patient Management     | A           | Statement     | 85%       |

## Files

- `coverage_gates.json` - Component threshold configuration
- `verify_coverage.py` - Coverage verification module
- `test_coverage_gates.py` - Tests for configuration validation
- `test_verify_coverage.py` - Tests for verification module
- `conftest.py` - pytest configuration with markers

## Usage

### Command Line

```bash
# Verify coverage against component thresholds
python -m verify_coverage --cobertura path/to/coverage.cobertura.xml

# With custom config
python -m verify_coverage --cobertura coverage.xml --config coverage_gates.json

# Fail on threshold violation
python -m verify_coverage --cobertura coverage.xml --fail-on-error

# JSON output
python -m verify_coverage --cobertura coverage.xml --json-output results.json
```

### Programmatic API

```python
from verify_coverage import load_coverage_gates, verify_coverage
from pathlib import Path

# Load configuration
config = load_coverage_gates(Path("coverage_gates.json"))

# Verify coverage from Cobertura XML
report = verify_coverage(Path("coverage.cobertura.xml"))

# Check results
if report.is_passing():
    print("All coverage gates passed!")
else:
    for result in report.get_failed_results():
        print(f"FAILED: {result.component.name} - {result.actual_coverage:.2f}%")
```

## CI Integration

The coverage gate is integrated into the CI pipeline as a separate job
that runs after all test jobs complete. It:

1. Downloads all coverage artifacts (C#, Python)
2. Verifies each coverage report against component thresholds
3. Fails the pipeline if any Class C component has < 100% coverage
4. Generates a consolidated coverage summary

### GitHub Actions / Gitea Actions

```yaml
coverage-gate:
  name: Component-Level Coverage Gates
  runs-on: [self-hosted, windows]
  needs: [test-csharp, test-python]
  steps:
    - name: Download coverage artifacts
      uses: actions/download-artifact@v3
      with:
        path: coverage-artifacts

    - name: Verify component-level coverage
      run: |
        python tests/coverage/verify_coverage.py \
          --cobertura coverage-artifacts/csharp-coverage-reports/coverage.cobertura.xml \
          --config tests/coverage/coverage_gates.json \
          --fail-on-error \
          --json-output coverage-results.json
```

## Adding New Components

To add a new component to the coverage gates:

1. Edit `coverage_gates.json`
2. Add a new component entry:

```json
{
  "name": "NewComponent",
  "description": "Description of the component",
  "safety_class": "B",
  "coverage_type": "statement",
  "threshold": 85.0,
  "path_pattern": "src/**/newcomponent/**",
  "iec_62304_reference": "IEC 62304 Class B - Non-serious injury possible"
}
```

3. Run tests to verify configuration:

```bash
pytest tests/coverage/test_coverage_gates.py -v
```

## Regulatory Compliance

This module implements the following SPEC-TEST-001 requirements:

- **NFR-TEST-01**: 85% minimum statement coverage per module
- **NFR-TEST-01.1**: 100% decision coverage for Class C components
- **NFR-TEST-04**: Class C merge gate enforcement (block merge on coverage regression)

Coverage reports are retained for 90 days per FR-TEST-08.5.

## Test Coverage

Run coverage for this module:

```bash
cd tests/coverage
pytest --cov=. --cov-report=html --cov-report=term
```

Target: 85%+ coverage (Class B infrastructure component).
