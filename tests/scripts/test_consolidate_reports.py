# test_consolidate_reports.py
"""
SPEC-TEST-001 Phase 3.4: Test Report Consolidation Tests

Tests for consolidating JUnit XML reports and coverage reports from multiple
test suites into a single unified report.

Covers:
- FR-TEST-08.1: JUnit XML format test reports for all test suites
- FR-TEST-08.2: Consolidated JUnit XML report aggregating all results
- FR-TEST-08.3: JUnit XML with test suite name, test case name, execution time, result
"""

import xml.etree.ElementTree as ET
from pathlib import Path

import pytest


import sys

sys.path.insert(0, str(Path(__file__).parent))

from consolidate_reports import (
    JUnitConsolidator,
    ConsolidatedReport,
    consolidate_junit_reports,
)


@pytest.fixture
def sample_junit_cpp() -> str:
    """Sample JUnit XML for C++ tests."""
    return """<?xml version="1.0" encoding="UTF-8"?>
<testsuites>
    <testsuite name="cpp.generator.ClassC" tests="5" failures="0" errors="0" time="1.234">
        <testcase name="FR-TEST-01.1_TestGeneratorInit" classname="cpp.generator.ClassC" time="0.123"/>
        <testcase name="FR-TEST-01.2_TestGeneratorArm" classname="cpp.generator.ClassC" time="0.234"/>
        <testcase name="FR-TEST-01.3_TestGeneratorExpose" classname="cpp.generator.ClassC" time="0.345"/>
        <testcase name="FR-TEST-01.4_TestGeneratorAbort" classname="cpp.generator.ClassC" time="0.456"/>
        <testcase name="FR-TEST-01.5_TestGeneratorStatus" classname="cpp.generator.ClassC" time="0.076"/>
    </testsuite>
    <testsuite name="cpp.aec.ClassC" tests="3" failures="1" errors="0" time="0.567">
        <testcase name="FR-TEST-01.1_TestAecInit" classname="cpp.aec.ClassC" time="0.123"/>
        <testcase name="FR-TEST-01.2_TestAecCalibrate" classname="cpp.aec.ClassC" time="0.234">
            <failure message="Expected 100.0, got 99.5"/>
        </testcase>
        <testcase name="FR-TEST-01.3_TestAecExposure" classname="cpp.aec.ClassC" time="0.210"/>
    </testsuite>
</testsuites>
"""


@pytest.fixture
def sample_junit_csharp() -> str:
    """Sample JUnit XML for C# tests."""
    return """<?xml version="1.0" encoding="UTF-8"?>
<testsuites>
    <testsuite name="csharp.ui.ViewModels" tests="10" failures="0" errors="0" time="2.345">
        <testcase name="FR-UI-01.1_TestPatientRegistration" classname="csharp.ui.ViewModels" time="0.234"/>
        <testcase name="FR-UI-01.2_TestImageAcquisition" classname="csharp.ui.ViewModels" time="0.345"/>
    </testsuite>
</testsuites>
"""


@pytest.fixture
def sample_junit_python() -> str:
    """Sample JUnit XML for Python tests."""
    return """<?xml version="1.0" encoding="UTF-8"?>
<testsuites>
    <testsuite name="python.rtm.validation" tests="8" failures="0" errors="0" time="0.789">
        <testcase name="FR-TEST-09.1_TestRtmParser" classname="python.rtm.validation" time="0.123"/>
        <testcase name="FR-TEST-09.2_TestRtmValidator" classname="python.rtm.validation" time="0.234"/>
    </testsuite>
</testsuites>
"""


@pytest.fixture
def temp_junit_dir(
    tmp_path: Path,
    sample_junit_cpp: str,
    sample_junit_csharp: str,
    sample_junit_python: str,
) -> Path:
    """Create temp directory with sample JUnit XML files."""
    # Create subdirectories for each test type
    cpp_dir = tmp_path / "cpp"
    cpp_dir.mkdir()
    (cpp_dir / "junit.xml").write_text(sample_junit_cpp)

    csharp_dir = tmp_path / "csharp"
    csharp_dir.mkdir()
    (csharp_dir / "junit.xml").write_text(sample_junit_csharp)

    python_dir = tmp_path / "python"
    python_dir.mkdir()
    (python_dir / "junit.xml").write_text(sample_junit_python)

    return tmp_path


class TestJUnitConsolidator:
    """Tests for JUnitConsolidator class."""

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_consolidator_exists(self):
        """FR-TEST-08.2: JUnitConsolidator class should exist."""
        assert JUnitConsolidator is not None, "JUnitConsolidator class not implemented"

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_consolidator_initialization(self):
        """FR-TEST-08.2: JUnitConsolidator should initialize with source directory."""
        consolidator = JUnitConsolidator(source_dir=Path("/tmp/reports"))
        assert consolidator.source_dir == Path("/tmp/reports")

    @pytest.mark.requirement("FR-TEST-08.1")
    def test_find_junit_files(self, temp_junit_dir: Path):
        """FR-TEST-08.1: Should find all JUnit XML files recursively."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        junit_files = consolidator.find_junit_files()

        assert len(junit_files) == 3
        file_names = [f.name for f in junit_files]
        assert all(name == "junit.xml" for name in file_names)

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_consolidate_creates_valid_xml(self, temp_junit_dir: Path):
        """FR-TEST-08.2: Consolidated report should be valid XML."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        assert result is not None
        assert isinstance(result, ConsolidatedReport)

        # Verify XML is valid
        root = ET.fromstring(result.xml_content)
        assert root.tag == "testsuites"

    @pytest.mark.requirement("FR-TEST-08.3")
    def test_consolidate_includes_all_suites(self, temp_junit_dir: Path):
        """FR-TEST-08.3: Consolidated report should include all test suites."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        root = ET.fromstring(result.xml_content)
        suites = root.findall("testsuite")

        assert len(suites) == 4  # 2 cpp suites + 1 csharp + 1 python

    @pytest.mark.requirement("FR-TEST-08.3")
    def test_consolidate_aggregates_statistics(self, temp_junit_dir: Path):
        """FR-TEST-08.3: Consolidated report should aggregate test statistics."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        assert result.total_tests == 26  # 5 + 3 + 10 + 8
        assert result.total_failures == 1  # One failure in cpp.aec.ClassC
        assert result.total_errors == 0

    @pytest.mark.requirement("FR-TEST-08.3")
    def test_consolidate_preserves_testcase_details(self, temp_junit_dir: Path):
        """FR-TEST-08.3: Test case details should be preserved."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        root = ET.fromstring(result.xml_content)
        testcases = root.findall(".//testcase")

        assert len(testcases) > 0

        # Check first testcase has required attributes
        first_tc = testcases[0]
        assert first_tc.get("name") is not None
        assert first_tc.get("classname") is not None
        assert first_tc.get("time") is not None

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_consolidate_preserves_failures(self, temp_junit_dir: Path):
        """FR-TEST-08.2: Failure information should be preserved."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        root = ET.fromstring(result.xml_content)
        failures = root.findall(".//failure")

        assert len(failures) == 1
        assert "Expected 100.0, got 99.5" in failures[0].get("message", "")


class TestConsolidatedReport:
    """Tests for ConsolidatedReport dataclass."""

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_report_dataclass_exists(self):
        """FR-TEST-08.2: ConsolidatedReport should be a dataclass."""
        assert ConsolidatedReport is not None

    @pytest.mark.requirement("FR-TEST-08.3")
    def test_report_has_required_fields(self, temp_junit_dir: Path):
        """FR-TEST-08.3: Report should have all required fields."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        assert hasattr(result, "xml_content")
        assert hasattr(result, "total_tests")
        assert hasattr(result, "total_failures")
        assert hasattr(result, "total_errors")
        assert hasattr(result, "total_time")
        assert hasattr(result, "suites")

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_report_calculates_pass_rate(self, temp_junit_dir: Path):
        """FR-TEST-08.2: Report should calculate pass rate."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        # 25 passed out of 26 total = ~96.15%
        expected_rate = 25 / 26 * 100
        assert abs(result.pass_rate - expected_rate) < 0.01


class TestConvenienceFunction:
    """Tests for convenience function."""

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_consolidate_junit_reports_function_exists(self):
        """FR-TEST-08.2: consolidate_junit_reports function should exist."""
        assert consolidate_junit_reports is not None

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_consolidate_junit_reports_returns_report(self, temp_junit_dir: Path):
        """FR-TEST-08.2: Function should return ConsolidatedReport."""
        result = consolidate_junit_reports(temp_junit_dir)
        assert isinstance(result, ConsolidatedReport)

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_consolidate_junit_reports_with_output_file(self, temp_junit_dir: Path):
        """FR-TEST-08.2: Function should write output file if specified."""
        output_file = temp_junit_dir / "consolidated.xml"
        result = consolidate_junit_reports(temp_junit_dir, output_file=output_file)

        assert output_file.exists()
        assert output_file.read_text() == result.xml_content


class TestEdgeCases:
    """Tests for edge cases and error handling."""

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_empty_directory(self, tmp_path: Path):
        """FR-TEST-08.2: Empty directory should return empty report."""
        consolidator = JUnitConsolidator(source_dir=tmp_path)
        result = consolidator.consolidate()

        assert result.total_tests == 0
        assert result.total_failures == 0
        assert result.total_errors == 0

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_malformed_xml_skipped(self, tmp_path: Path):
        """FR-TEST-08.2: Malformed XML files should be skipped gracefully."""
        # Create a valid file
        valid_dir = tmp_path / "valid"
        valid_dir.mkdir()
        (valid_dir / "junit.xml").write_text(
            '<?xml version="1.0"?><testsuites><testsuite name="test" tests="1"/></testsuites>'
        )

        # Create a malformed file
        invalid_dir = tmp_path / "invalid"
        invalid_dir.mkdir()
        (invalid_dir / "junit.xml").write_text("not valid xml <")

        consolidator = JUnitConsolidator(source_dir=tmp_path)
        # Should not raise exception
        result = consolidator.consolidate()

        # Should only include valid file
        assert result.total_tests == 1

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_custom_pattern(self, tmp_path: Path):
        """FR-TEST-08.2: Should support custom file patterns."""
        # Create files with different names
        (tmp_path / "test-results.xml").write_text(
            '<?xml version="1.0"?><testsuites><testsuite name="test" tests="5"/></testsuites>'
        )
        (tmp_path / "junit.xml").write_text(
            '<?xml version="1.0"?><testsuites><testsuite name="test" tests="3"/></testsuites>'
        )

        consolidator = JUnitConsolidator(source_dir=tmp_path, pattern="test-*.xml")
        result = consolidator.consolidate()

        # Should only match custom pattern
        assert result.total_tests == 5


class TestSummaryGeneration:
    """Tests for summary generation."""

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_generate_summary_markdown(self, temp_junit_dir: Path):
        """FR-TEST-08.2: Should generate Markdown summary."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        summary = result.to_markdown()

        assert "# Test Report Summary" in summary
        assert "Total Tests" in summary
        assert "Pass Rate" in summary
        assert "Failed" in summary

    @pytest.mark.requirement("FR-TEST-08.2")
    def test_summary_includes_suite_breakdown(self, temp_junit_dir: Path):
        """FR-TEST-08.2: Summary should include breakdown by suite."""
        consolidator = JUnitConsolidator(source_dir=temp_junit_dir)
        result = consolidator.consolidate()

        summary = result.to_markdown()

        # Should list each suite
        assert "cpp.generator.ClassC" in summary
        assert "cpp.aec.ClassC" in summary
        assert "csharp.ui.ViewModels" in summary
        assert "python.rtm.validation" in summary
