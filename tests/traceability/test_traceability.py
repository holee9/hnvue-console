# test_traceability.py
"""
Requirements Traceability Matrix (RTM) Tests

Tests for FR-TEST-09 Requirements Traceability Matrix requirements:
- FR-TEST-09.1: RTM links each software requirement to one or more test cases
- FR-TEST-09.2: RTM provides bidirectional traceability
- FR-TEST-09.3: Test cases must reference at least one SPEC requirement ID
- FR-TEST-09.4: New requirements flagged as incomplete until linked
- FR-TEST-09.5: RTM is machine-readable (CSV or JSON)
- FR-TEST-09.6: Human-readable RTM report in HTML format
"""

import csv
from pathlib import Path

import pytest


# FR-TEST-09.5: RTM shall be machine-readable (CSV)
@pytest.mark.requirement("FR-TEST-09.5")
@pytest.mark.safety_class("A")
class TestRtmCsvExistence:
    """Tests for RTM CSV file existence and basic structure."""

    def test_rtm_csv_file_exists(self):
        """
        FR-TEST-09.5: The RTM shall be maintained as a machine-readable artifact (CSV).

        This test verifies that the RTM CSV file exists at the expected location.
        """
        rtm_csv_path = Path(__file__).parent / "rtm.csv"
        assert rtm_csv_path.exists(), (
            f"RTM CSV file not found at {rtm_csv_path}. "
            "Create rtm.csv with requirement-test mappings."
        )

    def test_rtm_csv_not_empty(self):
        """
        FR-TEST-09.5: The RTM CSV file shall contain data.

        This test verifies that the RTM CSV file is not empty.
        """
        rtm_csv_path = Path(__file__).parent / "rtm.csv"
        if not rtm_csv_path.exists():
            pytest.skip("RTM CSV file does not exist yet")

        content = rtm_csv_path.read_text(encoding="utf-8")
        assert content.strip(), "RTM CSV file is empty"

    def test_rtm_csv_has_required_columns(self):
        """
        FR-TEST-09.5: The RTM CSV shall have the required schema columns.

        Required columns: requirement_id, test_case_id, test_suite, safety_class, status
        """
        rtm_csv_path = Path(__file__).parent / "rtm.csv"
        if not rtm_csv_path.exists():
            pytest.skip("RTM CSV file does not exist yet")

        required_columns = {
            "requirement_id",
            "test_case_id",
            "test_suite",
            "safety_class",
            "status",
        }

        with open(rtm_csv_path, newline="", encoding="utf-8") as csvfile:
            reader = csv.DictReader(csvfile)
            actual_columns = set(reader.fieldnames) if reader.fieldnames else set()

        missing_columns = required_columns - actual_columns
        assert not missing_columns, (
            f"RTM CSV is missing required columns: {missing_columns}. "
            f"Found columns: {actual_columns}"
        )


@pytest.mark.requirement("FR-TEST-09.1")
@pytest.mark.requirement("FR-TEST-09.2")
@pytest.mark.safety_class("A")
class TestRtmTraceability:
    """Tests for RTM bidirectional traceability requirements."""

    def test_all_requirements_have_test_cases(self):
        """
        FR-TEST-09.1: RTM links each software requirement to one or more test cases.

        This test verifies that all SPEC-TEST-001 requirements (FR-TEST-01 through FR-TEST-10)
        have at least one linked test case. Requirements are tracked at the sub-requirement
        level (e.g., FR-TEST-01.1, FR-TEST-01.2), so we verify each parent requirement
        has at least one sub-requirement mapped.
        """
        rtm_csv_path = Path(__file__).parent / "rtm.csv"
        if not rtm_csv_path.exists():
            pytest.skip("RTM CSV file does not exist yet")

        expected_parent_requirements = [f"FR-TEST-0{i}" for i in range(1, 10)] + [
            "FR-TEST-10"
        ]

        with open(rtm_csv_path, newline="", encoding="utf-8") as csvfile:
            reader = csv.DictReader(csvfile)
            linked_requirements = [row["requirement_id"] for row in reader]

        # Check each parent requirement has at least one mapped sub-requirement
        unlinked_parents = []
        for parent in expected_parent_requirements:
            has_mapping = any(
                req_id.startswith(parent + ".") or req_id == parent
                for req_id in linked_requirements
            )
            if not has_mapping:
                unlinked_parents.append(parent)

        assert not unlinked_parents, (
            f"The following requirements have no linked test cases: {unlinked_parents}. "
            "Each requirement must have at least one test case per FR-TEST-09.1"
        )

    def test_all_test_cases_reference_requirements(self):
        """
        FR-TEST-09.3: When a test case is created, it must reference at least one SPEC requirement.

        This test verifies bidirectional traceability from test to requirement.
        """
        rtm_csv_path = Path(__file__).parent / "rtm.csv"
        if not rtm_csv_path.exists():
            pytest.skip("RTM CSV file does not exist yet")

        with open(rtm_csv_path, newline="", encoding="utf-8") as csvfile:
            reader = csv.DictReader(csvfile)
            for row in reader:
                requirement_id = row.get("requirement_id", "").strip()
                assert requirement_id, (
                    f"Test case '{row.get('test_case_id', 'UNKNOWN')}' has no requirement_id. "
                    "All test cases must reference at least one requirement per FR-TEST-09.3"
                )
                assert requirement_id.startswith("FR-TEST-"), (
                    f"Test case '{row.get('test_case_id', 'UNKNOWN')}' references "
                    f"invalid requirement '{requirement_id}'. Must be FR-TEST-XX format."
                )

    def test_valid_safety_class_values(self):
        """
        Verify that all safety_class values are valid (A, B, or C).
        """
        rtm_csv_path = Path(__file__).parent / "rtm.csv"
        if not rtm_csv_path.exists():
            pytest.skip("RTM CSV file does not exist yet")

        valid_safety_classes = {"A", "B", "C", "NA"}

        with open(rtm_csv_path, newline="", encoding="utf-8") as csvfile:
            reader = csv.DictReader(csvfile)
            for row in reader:
                safety_class = row.get("safety_class", "").strip()
                assert safety_class in valid_safety_classes, (
                    f"Invalid safety_class '{safety_class}' for test case "
                    f"'{row.get('test_case_id', 'UNKNOWN')}'. "
                    f"Valid values: {valid_safety_classes}"
                )

    def test_valid_status_values(self):
        """
        Verify that all status values are valid (pass, fail, skip, pending).
        """
        rtm_csv_path = Path(__file__).parent / "rtm.csv"
        if not rtm_csv_path.exists():
            pytest.skip("RTM CSV file does not exist yet")

        valid_statuses = {"pass", "fail", "skip", "pending"}

        with open(rtm_csv_path, newline="", encoding="utf-8") as csvfile:
            reader = csv.DictReader(csvfile)
            for row in reader:
                status = row.get("status", "").strip().lower()
                assert status in valid_statuses, (
                    f"Invalid status '{status}' for test case "
                    f"'{row.get('test_case_id', 'UNKNOWN')}'. "
                    f"Valid values: {valid_statuses}"
                )


@pytest.mark.requirement("FR-TEST-09.6")
@pytest.mark.safety_class("A")
class TestRtmHtmlReport:
    """Tests for RTM HTML report generation."""

    def test_rtm_html_report_exists(self):
        """
        FR-TEST-09.6: The system shall generate a human-readable RTM report in HTML format.

        This test verifies that the HTML report file exists.
        """
        rtm_html_path = Path(__file__).parent / "rtm_report.html"
        assert rtm_html_path.exists(), (
            f"RTM HTML report not found at {rtm_html_path}. "
            "Generate rtm_report.html from rtm.csv per FR-TEST-09.6"
        )

    def test_rtm_html_report_valid_structure(self):
        """
        FR-TEST-09.6: The HTML report shall have valid HTML structure.

        This test verifies basic HTML validity.
        """
        rtm_html_path = Path(__file__).parent / "rtm_report.html"
        if not rtm_html_path.exists():
            pytest.skip("RTM HTML report does not exist yet")

        content = rtm_html_path.read_text(encoding="utf-8")

        assert "<!DOCTYPE html>" in content or "<html" in content, (
            "RTM HTML report must have valid HTML structure with <html> tag"
        )
        assert "</html>" in content, "RTM HTML report must have closing </html> tag"

    def test_rtm_html_report_contains_requirements(self):
        """
        FR-TEST-09.6: The HTML report shall display all requirements.

        This test verifies that the HTML report contains requirement information.
        """
        rtm_html_path = Path(__file__).parent / "rtm_report.html"
        if not rtm_html_path.exists():
            pytest.skip("RTM HTML report does not exist yet")

        content = rtm_html_path.read_text(encoding="utf-8")

        # Check that at least some requirement IDs are present
        assert "FR-TEST" in content, (
            "RTM HTML report must contain requirement IDs (FR-TEST-XX)"
        )
