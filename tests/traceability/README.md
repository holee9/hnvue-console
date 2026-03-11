# Requirements Traceability Matrix (RTM) Infrastructure

This directory contains the Requirements Traceability Matrix implementation for SPEC-TEST-001.

## Purpose

The RTM provides bidirectional traceability between software requirements and test cases, as required by:

- **FR-TEST-09.1**: RTM links each software requirement to one or more test cases
- **FR-TEST-09.2**: RTM provides bidirectional traceability (requirement to test, test to requirement)
- **FR-TEST-09.3**: Test cases must reference at least one SPEC requirement ID
- **FR-TEST-09.4**: New requirements flagged as incomplete until linked
- **FR-TEST-09.5**: RTM is machine-readable (CSV format)
- **FR-TEST-09.6**: Human-readable RTM report in HTML format

## Files

| File | Description |
|------|-------------|
| `rtm.csv` | Machine-readable RTM in CSV format |
| `rtm_report.html` | Human-readable HTML report |
| `rtm_parser.py` | Parser module for RTM CSV files |
| `rtm_validator.py` | Validation module for RTM completeness |
| `rtm_report_generator.py` | HTML report generator |
| `test_traceability.py` | Specification tests for RTM infrastructure |

## CSV Schema

```csv
requirement_id,test_case_id,test_suite,safety_class,status
FR-TEST-01.1,test_unit_cpp_framework_exists,unit/cpp,NA,pass
```

### Column Definitions

| Column | Description | Valid Values |
|--------|-------------|--------------|
| `requirement_id` | SPEC requirement identifier | `FR-TEST-XX.Y` format |
| `test_case_id` | Unique test case identifier | Any string |
| `test_suite` | Test suite location | Directory path (e.g., `unit/cpp`) |
| `safety_class` | IEC 62304 safety classification | `A`, `B`, `C`, `NA` |
| `status` | Current test status | `pass`, `fail`, `skip`, `pending` |

## Usage

### Parse RTM CSV

```python
from tests.traceability import RtmParser

parser = RtmParser("tests/traceability/rtm.csv")

# Get all entries for a requirement
entries = parser.get_entries_by_requirement("FR-TEST-09")

# Get test cases for a requirement
tests = parser.get_test_cases_for_requirement("FR-TEST-09.1")

# Get summary statistics
summary = parser.get_summary()
```

### Validate RTM Completeness

```python
from tests.traceability import RtmValidator

validator = RtmValidator("tests/traceability/rtm.csv")
result = validator.validate()

if result.is_valid:
    print("RTM is complete and valid")
else:
    print("Errors:", result.errors)
    print("Unlinked requirements:", result.unlinked_requirements)
```

### Generate HTML Report

```python
from tests.traceability import RtmReportGenerator

generator = RtmReportGenerator("tests/traceability/rtm.csv")
generator.generate_report("tests/traceability/rtm_report.html")
```

Or from command line:

```bash
python -m tests.traceability.rtm_report_generator tests/traceability/rtm.csv -o rtm_report.html
```

## CI Integration

The RTM Gate CI stage validates:

1. All requirements (FR-TEST-01 through FR-TEST-10) have at least one test case
2. All test cases reference valid requirement IDs
3. Safety class and status values are valid
4. HTML report is generated and up-to-date

## Regulatory Compliance

The RTM supports IEC 62304 compliance by providing:

- **Traceability**: Bidirectional links between requirements and tests
- **Audit Trail**: CSV file version controlled in git
- **Evidence**: HTML report for design verification documentation

---

Reference: SPEC-TEST-001 Section 4.2 (V&V Matrix) and Section 4.5.1 (CI Pipeline Stages - RTM Gate)
