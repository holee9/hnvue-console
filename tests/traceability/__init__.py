# tests/traceability/__init__.py
"""
Requirements Traceability Matrix (RTM) Infrastructure

This package provides tools for managing and validating the RTM as required by FR-TEST-09.

Modules:
    - rtm_parser: Parse and query RTM CSV files
    - rtm_validator: Validate RTM completeness and bidirectional traceability
    - rtm_report_generator: Generate HTML reports from RTM data

FR-TEST-09 Requirements Addressed:
    - FR-TEST-09.1: RTM links each software requirement to one or more test cases
    - FR-TEST-09.2: RTM provides bidirectional traceability
    - FR-TEST-09.3: Test cases must reference at least one SPEC requirement ID
    - FR-TEST-09.4: New requirements flagged as incomplete until linked
    - FR-TEST-09.5: RTM is machine-readable (CSV)
    - FR-TEST-09.6: Human-readable RTM report in HTML format
"""

from .rtm_parser import RtmEntry, RtmParser
from .rtm_report_generator import RtmReportGenerator
from .rtm_validator import RtmValidator, ValidationResult

__all__ = [
    "RtmEntry",
    "RtmParser",
    "RtmValidator",
    "ValidationResult",
    "RtmReportGenerator",
]
