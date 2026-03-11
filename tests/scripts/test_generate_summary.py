# test_generate_summary.py
"""
SPEC-TEST-001 Phase 3.4: Build Summary Generation Tests

Tests for generating comprehensive build summary with component coverage
breakdown and RTM status integration.

Covers:
- FR-TEST-08.4: Coverage report in Cobertura XML format
- FR-TEST-08.5: Test report retention for regulatory audit
- NFR-TEST-01: 85% coverage minimum per component
"""

import json
from pathlib import Path

import pytest


import sys

sys.path.insert(0, str(Path(__file__).parent))

from generate_summary import (
    BuildSummaryGenerator,
    CoverageSummary,
    RtmSummary,
    generate_build_summary,
)


@pytest.fixture
def sample_cobertura_xml() -> str:
    """Sample Cobertura XML coverage report."""
    return """<?xml version="1.0"?>
<coverage line-rate="0.92" branch-rate="0.88" version="2.0">
    <packages>
        <package name="generator" line-rate="1.0" branch-rate="1.0">
            <classes>
                <class name="GeneratorControl" filename="src/generator/control.cpp" line-rate="1.0"/>
            </classes>
        </package>
        <package name="aec" line-rate="1.0" branch-rate="1.0">
            <classes>
                <class name="AecController" filename="src/aec/controller.cpp" line-rate="1.0"/>
            </classes>
        </package>
        <package name="imaging" line-rate="0.87" branch-rate="0.82">
            <classes>
                <class name="ImageProcessor" filename="src/imaging/processor.cpp" line-rate="0.87"/>
            </classes>
        </package>
        <package name="dicom" line-rate="0.89" branch-rate="0.85">
            <classes>
                <class name="DicomService" filename="src/dicom/service.cpp" line-rate="0.89"/>
            </classes>
        </package>
    </packages>
</coverage>
"""


@pytest.fixture
def sample_rtm_status() -> dict:
    """Sample RTM validation status."""
    return {
        "is_valid": True,
        "total_requirements": 10,
        "linked_requirements": 10,
        "unlinked_requirements": [],
        "total_test_cases": 45,
        "coverage_by_requirement": {
            "FR-TEST-01": {"has_tests": True, "test_count": 8, "status": "pass"},
            "FR-TEST-02": {"has_tests": True, "test_count": 5, "status": "pass"},
            "FR-TEST-03": {"has_tests": True, "test_count": 6, "status": "pass"},
            "FR-TEST-04": {"has_tests": True, "test_count": 4, "status": "pass"},
            "FR-TEST-05": {"has_tests": True, "test_count": 3, "status": "pending"},
            "FR-TEST-06": {"has_tests": True, "test_count": 7, "status": "pass"},
            "FR-TEST-07": {"has_tests": True, "test_count": 3, "status": "pass"},
            "FR-TEST-08": {"has_tests": True, "test_count": 4, "status": "pass"},
            "FR-TEST-09": {"has_tests": True, "test_count": 3, "status": "pass"},
            "FR-TEST-10": {"has_tests": True, "test_count": 2, "status": "pass"},
        },
    }


@pytest.fixture
def sample_test_results() -> dict:
    """Sample consolidated test results."""
    return {
        "total_tests": 26,
        "passed": 25,
        "failed": 1,
        "skipped": 0,
        "total_time": 4.935,
        "suites": [
            {"name": "cpp.generator.ClassC", "tests": 5, "failures": 0, "time": 1.234},
            {"name": "cpp.aec.ClassC", "tests": 3, "failures": 1, "time": 0.567},
            {"name": "csharp.ui.ViewModels", "tests": 10, "failures": 0, "time": 2.345},
            {"name": "python.rtm.validation", "tests": 8, "failures": 0, "time": 0.789},
        ],
    }


@pytest.fixture
def temp_artifacts_dir(
    tmp_path: Path,
    sample_cobertura_xml: str,
    sample_rtm_status: dict,
    sample_test_results: dict,
) -> Path:
    """Create temp directory with sample artifacts."""
    # Coverage report
    (tmp_path / "coverage.xml").write_text(sample_cobertura_xml)

    # RTM status
    (tmp_path / "rtm_status.json").write_text(json.dumps(sample_rtm_status))

    # Test results
    (tmp_path / "test_results.json").write_text(json.dumps(sample_test_results))

    return tmp_path


class TestBuildSummaryGenerator:
    """Tests for BuildSummaryGenerator class."""

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_generator_exists(self):
        """FR-TEST-08.5: BuildSummaryGenerator class should exist."""
        assert BuildSummaryGenerator is not None, (
            "BuildSummaryGenerator class not implemented"
        )

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_generator_initialization(self):
        """FR-TEST-08.5: Generator should initialize with artifacts directory."""
        generator = BuildSummaryGenerator(artifacts_dir=Path("/tmp/artifacts"))
        assert generator.artifacts_dir == Path("/tmp/artifacts")

    @pytest.mark.requirement("FR-TEST-08.4")
    def test_parse_coverage_report(self, temp_artifacts_dir: Path):
        """FR-TEST-08.4: Should parse Cobertura XML coverage report."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        coverage = generator.parse_coverage()

        assert coverage is not None
        assert coverage.overall_line_rate == 0.92
        assert coverage.overall_branch_rate == 0.88

    @pytest.mark.requirement("NFR-TEST-01")
    def test_coverage_component_breakdown(self, temp_artifacts_dir: Path):
        """NFR-TEST-01: Should provide coverage breakdown by component."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        coverage = generator.parse_coverage()

        assert len(coverage.components) == 4

        # Check Class C components (100% required)
        generator_pkg = next(c for c in coverage.components if c.name == "generator")
        assert generator_pkg.line_rate == 1.0
        assert generator_pkg.safety_class == "C"

        aec_pkg = next(c for c in coverage.components if c.name == "aec")
        assert aec_pkg.line_rate == 1.0
        assert aec_pkg.safety_class == "C"

    @pytest.mark.requirement("FR-TEST-09.6")
    def test_parse_rtm_status(self, temp_artifacts_dir: Path):
        """FR-TEST-09.6: Should parse RTM status JSON."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        rtm = generator.parse_rtm()

        assert rtm is not None
        assert rtm.is_valid is True
        assert rtm.total_requirements == 10
        assert rtm.linked_requirements == 10
        assert len(rtm.unlinked_requirements) == 0

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_parse_test_results(self, temp_artifacts_dir: Path):
        """FR-TEST-08.5: Should parse test results JSON."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        results = generator.parse_test_results()

        assert results is not None
        assert results.total_tests == 26
        assert results.passed == 25
        assert results.failed == 1
        assert results.pass_rate == pytest.approx(96.15, rel=0.01)

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_generate_summary(self, temp_artifacts_dir: Path):
        """FR-TEST-08.5: Should generate complete build summary."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        summary = generator.generate()

        assert summary is not None
        assert hasattr(summary, "test_results")
        assert hasattr(summary, "coverage")
        assert hasattr(summary, "rtm")

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_generate_markdown_report(self, temp_artifacts_dir: Path):
        """FR-TEST-08.5: Should generate Markdown report."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        summary = generator.generate()
        markdown = summary.to_markdown()

        assert "# HnVue Build Summary" in markdown
        assert "## Test Results" in markdown
        assert "## Coverage Report" in markdown
        assert "## RTM Status" in markdown

    @pytest.mark.requirement("NFR-TEST-01")
    def test_coverage_threshold_check(self, temp_artifacts_dir: Path):
        """NFR-TEST-01: Should check coverage against thresholds."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        summary = generator.generate()

        # All components should pass thresholds
        assert summary.coverage_passed is True

    @pytest.mark.requirement("NFR-TEST-04")
    def test_class_c_strict_gate(self, temp_artifacts_dir: Path):
        """NFR-TEST-04: Class C components must pass 100% threshold."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        summary = generator.generate()

        assert summary.class_c_passed is True


class TestCoverageSummary:
    """Tests for CoverageSummary dataclass."""

    @pytest.mark.requirement("FR-TEST-08.4")
    def test_coverage_summary_exists(self):
        """FR-TEST-08.4: CoverageSummary should exist."""
        assert CoverageSummary is not None

    @pytest.mark.requirement("NFR-TEST-01")
    def test_component_level_coverage(self):
        """NFR-TEST-01: Should track component-level coverage."""
        # This will be implemented in GREEN phase
        pass


class TestRtmSummary:
    """Tests for RtmSummary dataclass."""

    @pytest.mark.requirement("FR-TEST-09.6")
    def test_rtm_summary_exists(self):
        """FR-TEST-09.6: RtmSummary should exist."""
        assert RtmSummary is not None

    @pytest.mark.requirement("FR-TEST-09.2")
    def test_bidirectional_traceability_check(self):
        """FR-TEST-09.2: Should verify bidirectional traceability."""
        # This will be implemented in GREEN phase
        pass


class TestConvenienceFunction:
    """Tests for convenience function."""

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_generate_build_summary_function_exists(self):
        """FR-TEST-08.5: generate_build_summary function should exist."""
        assert generate_build_summary is not None

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_generate_build_summary_returns_summary(self, temp_artifacts_dir: Path):
        """FR-TEST-08.5: Function should return summary object."""
        result = generate_build_summary(temp_artifacts_dir)
        assert result is not None

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_generate_build_summary_with_output(self, temp_artifacts_dir: Path):
        """FR-TEST-08.5: Function should write output file if specified."""
        output_file = temp_artifacts_dir / "build-summary.md"
        generate_build_summary(temp_artifacts_dir, output_file=output_file)

        assert output_file.exists()
        content = output_file.read_text()
        assert "# HnVue Build Summary" in content


class TestEdgeCases:
    """Tests for edge cases and error handling."""

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_missing_coverage_file(self, tmp_path: Path):
        """FR-TEST-08.5: Missing coverage file should be handled gracefully."""
        generator = BuildSummaryGenerator(artifacts_dir=tmp_path)
        coverage = generator.parse_coverage()

        # Should return None or empty summary
        assert coverage is None or coverage.overall_line_rate == 0

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_missing_rtm_file(self, tmp_path: Path):
        """FR-TEST-08.5: Missing RTM file should be handled gracefully."""
        generator = BuildSummaryGenerator(artifacts_dir=tmp_path)
        rtm = generator.parse_rtm()

        # Should return None or empty summary
        assert rtm is None or rtm.is_valid is False

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_partial_artifacts(self, tmp_path: Path, sample_test_results: dict):
        """FR-TEST-08.5: Should work with partial artifacts."""
        # Only test results, no coverage or RTM
        (tmp_path / "test_results.json").write_text(json.dumps(sample_test_results))

        generator = BuildSummaryGenerator(artifacts_dir=tmp_path)
        summary = generator.generate()

        assert summary is not None
        assert summary.test_results is not None


class TestReportFormat:
    """Tests for report formatting."""

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_markdown_includes_timestamp(self, temp_artifacts_dir: Path):
        """FR-TEST-08.5: Report should include timestamp."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        summary = generator.generate()
        markdown = summary.to_markdown()

        assert "Generated" in markdown or "Timestamp" in markdown

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_markdown_includes_commit_info(self, temp_artifacts_dir: Path):
        """FR-TEST-08.5: Report should include commit/branch info."""
        generator = BuildSummaryGenerator(
            artifacts_dir=temp_artifacts_dir, commit_sha="abc123", branch="main"
        )
        summary = generator.generate()
        markdown = summary.to_markdown()

        assert "abc123" in markdown
        assert "main" in markdown

    @pytest.mark.requirement("FR-TEST-08.5")
    def test_component_table_format(self, temp_artifacts_dir: Path):
        """FR-TEST-08.5: Component coverage should be in table format."""
        generator = BuildSummaryGenerator(artifacts_dir=temp_artifacts_dir)
        summary = generator.generate()
        markdown = summary.to_markdown()

        # Check for markdown table format
        assert "| Component |" in markdown or "| Name |" in markdown
        assert "|---" in markdown or "|-------|" in markdown
