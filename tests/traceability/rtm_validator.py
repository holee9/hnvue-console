# rtm_validator.py
"""
RTM Validator Module

Provides validation utilities for the Requirements Traceability Matrix.
Ensures bidirectional traceability and completeness per FR-TEST-09 requirements.

FR-TEST-09.1: RTM links each software requirement to one or more test cases
FR-TEST-09.2: RTM provides bidirectional traceability
FR-TEST-09.3: Test cases must reference at least one SPEC requirement ID
FR-TEST-09.4: New requirements flagged as incomplete until linked
"""

from dataclasses import dataclass, field
from pathlib import Path

from .rtm_parser import RtmParser


@dataclass
class ValidationResult:
    """Result of RTM validation."""

    is_valid: bool
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    unlinked_requirements: list[str] = field(default_factory=list)
    untraceable_tests: list[str] = field(default_factory=list)


class RtmValidator:
    """
    Validator for Requirements Traceability Matrix completeness and integrity.

    Validates:
    - All expected requirements have at least one test case (FR-TEST-09.1)
    - Bidirectional traceability exists (FR-TEST-09.2)
    - All test cases reference valid requirements (FR-TEST-09.3)
    """

    # SPEC-TEST-001 expected parent requirements
    EXPECTED_REQUIREMENTS = [f"FR-TEST-0{i}" for i in range(1, 10)] + ["FR-TEST-10"]

    def __init__(self, rtm_csv_path: str | Path):
        """
        Initialize the RTM validator.

        Args:
            rtm_csv_path: Path to the RTM CSV file.
        """
        self.parser = RtmParser(rtm_csv_path)

    def validate(self) -> ValidationResult:
        """
        Perform complete RTM validation.

        Returns:
            ValidationResult with validation status and details.
        """
        result = ValidationResult(is_valid=True)

        # Check data integrity
        data_errors = self.parser.validate()
        result.errors.extend(data_errors)

        # Check requirement coverage (FR-TEST-09.1, FR-TEST-09.4)
        result.unlinked_requirements = self._find_unlinked_requirements()
        if result.unlinked_requirements:
            result.is_valid = False
            result.errors.append(
                f"Requirements without test cases: {result.unlinked_requirements}"
            )

        # Check bidirectional traceability (FR-TEST-09.2, FR-TEST-09.3)
        result.untraceable_tests = self._find_untraceable_tests()
        if result.untraceable_tests:
            result.warnings.append(
                f"Test cases with potentially invalid requirement references: "
                f"{result.untraceable_tests}"
            )

        return result

    def _find_unlinked_requirements(self) -> list[str]:
        """
        Find requirements that have no linked test cases.

        Returns:
            List of requirement IDs without test cases.
        """
        linked_parents = self.parser.get_parent_requirements()
        unlinked = [
            req for req in self.EXPECTED_REQUIREMENTS if req not in linked_parents
        ]
        return unlinked

    def _find_untraceable_tests(self) -> list[str]:
        """
        Find test cases that reference invalid or non-existent requirements.

        Returns:
            List of test case IDs with invalid requirement references.
        """
        untraceable = []
        for entry in self.parser.entries:
            # Check if requirement ID follows expected format
            req_id = entry.requirement_id
            is_valid_format = req_id.startswith("FR-TEST-")

            # Check if parent requirement is in expected list
            if "." in req_id:
                parent = req_id.rsplit(".", 1)[0]
            else:
                parent = req_id

            is_expected_parent = parent in self.EXPECTED_REQUIREMENTS

            if not is_valid_format or not is_expected_parent:
                untraceable.append(entry.test_case_id)

        return untraceable

    def get_coverage_report(self) -> dict:
        """
        Generate a coverage report for all expected requirements.

        Returns:
            Dictionary with coverage status for each expected requirement.
        """
        coverage = {}
        for req in self.EXPECTED_REQUIREMENTS:
            test_cases = self.parser.get_test_cases_for_requirement(req)
            entries = self.parser.get_entries_by_requirement(req)

            coverage[req] = {
                "has_tests": len(test_cases) > 0,
                "test_count": len(test_cases),
                "test_cases": test_cases,
                "safety_classes": list({e.safety_class for e in entries}),
                "status": self._determine_status(entries),
            }

        return coverage

    def _determine_status(self, entries: list) -> str:
        """
        Determine overall status for a set of entries.

        Args:
            entries: List of RtmEntry objects.

        Returns:
            Overall status string.
        """
        if not entries:
            return "unlinked"

        statuses = {e.status.lower() for e in entries}

        if "fail" in statuses:
            return "fail"
        if "pending" in statuses:
            return "pending"
        if "skip" in statuses and "pass" not in statuses:
            return "skip"
        if statuses == {"pass"}:
            return "pass"
        if "pass" in statuses:
            return "partial"

        return "unknown"
