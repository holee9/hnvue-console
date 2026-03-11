# rtm_parser.py
"""
RTM CSV Parser Module

Provides utilities for parsing and querying the Requirements Traceability Matrix CSV file.

FR-TEST-09.5: RTM is machine-readable (CSV)
FR-TEST-09.1: RTM links each requirement to one or more test cases
FR-TEST-09.2: RTM provides bidirectional traceability
"""

import csv
from dataclasses import dataclass
from pathlib import Path


@dataclass
class RtmEntry:
    """Represents a single RTM entry linking a requirement to a test case."""

    requirement_id: str
    test_case_id: str
    test_suite: str
    safety_class: str
    status: str


class RtmParser:
    """
    Parser for Requirements Traceability Matrix CSV files.

    Usage:
        parser = RtmParser("tests/traceability/rtm.csv")
        entries = parser.get_entries_by_requirement("FR-TEST-09")
        tests = parser.get_test_cases_for_requirement("FR-TEST-09.1")
    """

    REQUIRED_COLUMNS = {
        "requirement_id",
        "test_case_id",
        "test_suite",
        "safety_class",
        "status",
    }
    VALID_SAFETY_CLASSES = {"A", "B", "C", "NA"}
    VALID_STATUSES = {"pass", "fail", "skip", "pending"}

    def __init__(self, csv_path: str | Path):
        """
        Initialize the RTM parser.

        Args:
            csv_path: Path to the RTM CSV file.

        Raises:
            FileNotFoundError: If the CSV file does not exist.
            ValueError: If the CSV file is missing required columns.
        """
        self.csv_path = Path(csv_path)
        self._entries: list[RtmEntry] | None = None

        if not self.csv_path.exists():
            raise FileNotFoundError(f"RTM CSV file not found: {self.csv_path}")

    @property
    def entries(self) -> list[RtmEntry]:
        """
        Load and cache all RTM entries from the CSV file.

        Returns:
            List of RtmEntry objects.
        """
        if self._entries is None:
            self._entries = self._load_entries()
        return self._entries

    def _load_entries(self) -> list[RtmEntry]:
        """Load entries from CSV file with validation."""
        with open(self.csv_path, newline="", encoding="utf-8") as csvfile:
            reader = csv.DictReader(csvfile)
            self._validate_columns(reader.fieldnames)
            return [RtmEntry(**row) for row in reader]

    def _validate_columns(self, fieldnames: list[str] | None) -> None:
        """Validate that all required columns are present."""
        if not fieldnames:
            raise ValueError("RTM CSV file has no columns")

        actual_columns = set(fieldnames)
        missing = self.REQUIRED_COLUMNS - actual_columns
        if missing:
            raise ValueError(f"RTM CSV is missing required columns: {missing}")

    def get_entries_by_requirement(self, requirement_id: str) -> list[RtmEntry]:
        """
        Get all RTM entries for a specific requirement.

        Args:
            requirement_id: The requirement ID to filter by (e.g., "FR-TEST-09").
                           Can be a parent requirement (FR-TEST-09) or sub-requirement (FR-TEST-09.1).

        Returns:
            List of RtmEntry objects matching the requirement.
        """
        return [
            entry
            for entry in self.entries
            if entry.requirement_id == requirement_id
            or entry.requirement_id.startswith(requirement_id + ".")
        ]

    def get_test_cases_for_requirement(self, requirement_id: str) -> list[str]:
        """
        Get all test case IDs linked to a requirement.

        Args:
            requirement_id: The requirement ID to query.

        Returns:
            List of test case IDs.
        """
        return [
            entry.test_case_id
            for entry in self.get_entries_by_requirement(requirement_id)
        ]

    def get_entries_by_test_suite(self, test_suite: str) -> list[RtmEntry]:
        """
        Get all RTM entries for a specific test suite.

        Args:
            test_suite: The test suite to filter by (e.g., "unit/cpp").

        Returns:
            List of RtmEntry objects in the test suite.
        """
        return [entry for entry in self.entries if entry.test_suite == test_suite]

    def get_requirements_for_test_case(self, test_case_id: str) -> list[str]:
        """
        Get all requirement IDs linked to a test case (bidirectional traceability).

        Args:
            test_case_id: The test case ID to query.

        Returns:
            List of requirement IDs linked to the test case.
        """
        return [
            entry.requirement_id
            for entry in self.entries
            if entry.test_case_id == test_case_id
        ]

    def get_all_requirements(self) -> set[str]:
        """
        Get all unique requirement IDs in the RTM.

        Returns:
            Set of all requirement IDs.
        """
        return {entry.requirement_id for entry in self.entries}

    def get_parent_requirements(self) -> set[str]:
        """
        Get all unique parent requirement IDs (e.g., FR-TEST-01 from FR-TEST-01.1).

        Returns:
            Set of parent requirement IDs.
        """
        parents = set()
        for entry in self.entries:
            # Extract parent from sub-requirement (FR-TEST-01.1 -> FR-TEST-01)
            if "." in entry.requirement_id:
                parent = entry.requirement_id.rsplit(".", 1)[0]
                parents.add(parent)
            else:
                parents.add(entry.requirement_id)
        return parents

    def get_summary(self) -> dict:
        """
        Get a summary of the RTM.

        Returns:
            Dictionary with summary statistics.
        """
        status_counts: dict[str, int] = {}
        safety_class_counts: dict[str, int] = {}

        for entry in self.entries:
            status_counts[entry.status] = status_counts.get(entry.status, 0) + 1
            safety_class_counts[entry.safety_class] = (
                safety_class_counts.get(entry.safety_class, 0) + 1
            )

        return {
            "total_entries": len(self.entries),
            "unique_requirements": len(self.get_all_requirements()),
            "parent_requirements": len(self.get_parent_requirements()),
            "status_counts": status_counts,
            "safety_class_counts": safety_class_counts,
        }

    def validate(self) -> list[str]:
        """
        Validate the RTM for data integrity issues.

        Returns:
            List of validation error messages. Empty list if valid.
        """
        errors = []

        for entry in self.entries:
            # Validate safety class
            if entry.safety_class not in self.VALID_SAFETY_CLASSES:
                errors.append(
                    f"Invalid safety_class '{entry.safety_class}' for test case "
                    f"'{entry.test_case_id}'"
                )

            # Validate status
            if entry.status.lower() not in self.VALID_STATUSES:
                errors.append(
                    f"Invalid status '{entry.status}' for test case '{entry.test_case_id}'"
                )

            # Validate requirement ID format
            if not entry.requirement_id.startswith("FR-TEST-"):
                errors.append(
                    f"Invalid requirement_id format '{entry.requirement_id}' for test case "
                    f"'{entry.test_case_id}'"
                )

        return errors
