# test_rtm_gate.py
"""
SPEC-TEST-001 Phase 3.3: RTM Gate Integration Tests

Tests for RTM validation gate in CI pipeline that ensures all requirements
have test coverage and all tests trace back to requirements.

Covers:
- FR-TEST-09.1: RTM links each requirement to one or more test cases
- FR-TEST-09.2: Bidirectional traceability
- FR-TEST-09.3: Test cases must reference at least one SPEC requirement ID
- FR-TEST-09.4: New requirements flagged as incomplete until linked
- NFR-TEST-08: Every test case traceable to at least one requirement
"""

import json
from pathlib import Path

import pytest


import sys

sys.path.insert(0, str(Path(__file__).parent))

from rtm_gate import (
    RtmGateValidator,
    RtmGateResult,
    validate_rtm_gate,
)


@pytest.fixture
def complete_rtm_csv() -> str:
    """Complete RTM CSV with all requirements linked."""
    return """requirement_id,test_case_id,test_suite,safety_class,status
FR-TEST-01.1,test_generator_init,unit/cpp/generator,C,pass
FR-TEST-01.1,test_generator_arm,unit/cpp/generator,C,pass
FR-TEST-01.2,test_xunit_ui_init,unit/csharp/ui,B,pass
FR-TEST-01.3,test_pytest_rtm_parser,unit/python/rtm,A,pass
FR-TEST-02.1,test_ipc_grpc,integration/ipc,B,pass
FR-TEST-03.1,test_e2e_workflow,system/workflows,B,pass
FR-TEST-04.1,test_dicom_cstore,dicom/conformance,B,pass
FR-TEST-05.1,test_usability_plan,usability/plans,NA,pending
FR-TEST-06.1,test_detector_sim,simulators/detector,C,pass
FR-TEST-07.1,test_interop_pacs,interop/pacs,B,pass
FR-TEST-08.1,test_junit_report,unit/python/ci,NA,pass
FR-TEST-09.1,test_rtm_parser,unit/python/rtm,A,pass
FR-TEST-10.1,test_data_generator,data/generators,NA,pass
"""


@pytest.fixture
def incomplete_rtm_csv() -> str:
    """Incomplete RTM missing FR-TEST-07 linkage."""
    return """requirement_id,test_case_id,test_suite,safety_class,status
FR-TEST-01.1,test_generator_init,unit/cpp/generator,C,pass
FR-TEST-02.1,test_ipc_grpc,integration/ipc,B,pass
FR-TEST-03.1,test_e2e_workflow,system/workflows,B,pass
FR-TEST-04.1,test_dicom_cstore,dicom/conformance,B,pass
FR-TEST-05.1,test_usability_plan,usability/plans,NA,pending
FR-TEST-06.1,test_detector_sim,simulators/detector,C,pass
FR-TEST-08.1,test_junit_report,unit/python/ci,NA,pass
FR-TEST-09.1,test_rtm_parser,unit/python/rtm,A,pass
FR-TEST-10.1,test_data_generator,data/generators,NA,pass
"""


@pytest.fixture
def temp_rtm_complete(tmp_path: Path, complete_rtm_csv: str) -> Path:
    """Create temp directory with complete RTM."""
    rtm_path = tmp_path / "rtm.csv"
    rtm_path.write_text(complete_rtm_csv)
    return tmp_path


@pytest.fixture
def temp_rtm_incomplete(tmp_path: Path, incomplete_rtm_csv: str) -> Path:
    """Create temp directory with incomplete RTM."""
    rtm_path = tmp_path / "rtm.csv"
    rtm_path.write_text(incomplete_rtm_csv)
    return tmp_path


class TestRtmGateValidator:
    """Tests for RtmGateValidator class."""

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_validator_exists(self):
        """FR-TEST-09.1: RtmGateValidator class should exist."""
        assert RtmGateValidator is not None, "RtmGateValidator class not implemented"

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_validator_initialization(self, temp_rtm_complete: Path):
        """FR-TEST-09.1: Validator should initialize with RTM path."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        assert validator.rtm_path == temp_rtm_complete / "rtm.csv"

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_validate_complete_rtm(self, temp_rtm_complete: Path):
        """FR-TEST-09.1: Complete RTM should pass validation."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        result = validator.validate()

        assert result.is_valid is True
        assert len(result.unlinked_requirements) == 0

    @pytest.mark.requirement("FR-TEST-09.4")
    def test_validate_incomplete_rtm(self, temp_rtm_incomplete: Path):
        """FR-TEST-09.4: Incomplete RTM should fail validation."""
        validator = RtmGateValidator(rtm_path=temp_rtm_incomplete / "rtm.csv")
        result = validator.validate()

        assert result.is_valid is False
        assert "FR-TEST-07" in result.unlinked_requirements

    @pytest.mark.requirement("FR-TEST-09.2")
    def test_bidirectional_traceability(self, temp_rtm_complete: Path):
        """FR-TEST-09.2: Should verify bidirectional traceability."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        result = validator.validate()

        assert result.bidirectional_traceability is True

    @pytest.mark.requirement("FR-TEST-09.3")
    def test_invalid_requirement_format(self, tmp_path: Path):
        """FR-TEST-09.3: Should detect invalid requirement format."""
        invalid_csv = """requirement_id,test_case_id,test_suite,safety_class,status
INVALID-01,test_something,unit/python,A,pass
"""
        rtm_path = tmp_path / "rtm.csv"
        rtm_path.write_text(invalid_csv)

        validator = RtmGateValidator(rtm_path=rtm_path)
        result = validator.validate()

        assert result.is_valid is False
        assert len(result.invalid_requirement_refs) > 0

    @pytest.mark.requirement("NFR-TEST-08")
    def test_untraceable_tests_detection(self, tmp_path: Path):
        """NFR-TEST-08: Should detect tests without requirement linkage."""
        csv_content = """requirement_id,test_case_id,test_suite,safety_class,status
FR-TEST-01.1,test_linked,unit/cpp,C,pass
,orphan_test,unit/python,A,pass
"""
        rtm_path = tmp_path / "rtm.csv"
        rtm_path.write_text(csv_content)

        validator = RtmGateValidator(rtm_path=rtm_path)
        result = validator.validate()

        assert result.is_valid is False
        assert "orphan_test" in result.untraceable_tests


class TestRtmGateResult:
    """Tests for RtmGateResult dataclass."""

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_result_dataclass_exists(self):
        """FR-TEST-09.1: RtmGateResult should exist."""
        assert RtmGateResult is not None

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_result_has_required_fields(self, temp_rtm_complete: Path):
        """FR-TEST-09.1: Result should have all required fields."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        result = validator.validate()

        assert hasattr(result, "is_valid")
        assert hasattr(result, "unlinked_requirements")
        assert hasattr(result, "untraceable_tests")
        assert hasattr(result, "invalid_requirement_refs")
        assert hasattr(result, "bidirectional_traceability")
        assert hasattr(result, "total_requirements")
        assert hasattr(result, "linked_requirements")

    @pytest.mark.requirement("FR-TEST-09.6")
    def test_result_json_serialization(self, temp_rtm_complete: Path):
        """FR-TEST-09.6: Result should be JSON serializable."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        result = validator.validate()

        json_str = result.to_json()
        assert json_str is not None

        # Should be valid JSON
        parsed = json.loads(json_str)
        assert "is_valid" in parsed

    @pytest.mark.requirement("FR-TEST-09.6")
    def test_result_markdown_report(self, temp_rtm_complete: Path):
        """FR-TEST-09.6: Result should generate Markdown report."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        result = validator.validate()

        markdown = result.to_markdown()
        assert "# RTM Gate Validation Report" in markdown
        assert "PASS" in markdown  # Status should show PASS


class TestConvenienceFunction:
    """Tests for convenience function."""

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_validate_rtm_gate_function_exists(self):
        """FR-TEST-09.1: validate_rtm_gate function should exist."""
        assert validate_rtm_gate is not None

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_validate_rtm_gate_returns_result(self, temp_rtm_complete: Path):
        """FR-TEST-09.1: Function should return RtmGateResult."""
        result = validate_rtm_gate(temp_rtm_complete / "rtm.csv")
        assert isinstance(result, RtmGateResult)

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_validate_rtm_gate_with_output(self, temp_rtm_complete: Path):
        """FR-TEST-09.1: Function should write output file if specified."""
        output_file = temp_rtm_complete / "rtm-gate-result.json"
        validate_rtm_gate(temp_rtm_complete / "rtm.csv", output_file=output_file)

        assert output_file.exists()
        data = json.loads(output_file.read_text())
        assert data["is_valid"] is True


class TestCiIntegration:
    """Tests for CI integration scenarios."""

    @pytest.mark.requirement("FR-TEST-09.4")
    def test_exit_code_on_failure(self, temp_rtm_incomplete: Path):
        """FR-TEST-09.4: Should return non-zero exit code on failure."""
        validator = RtmGateValidator(rtm_path=temp_rtm_incomplete / "rtm.csv")
        result = validator.validate()

        assert result.exit_code != 0

    @pytest.mark.requirement("FR-TEST-09.4")
    def test_exit_code_on_success(self, temp_rtm_complete: Path):
        """FR-TEST-09.4: Should return zero exit code on success."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        result = validator.validate()

        assert result.exit_code == 0

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_expected_requirements_check(self, temp_rtm_complete: Path):
        """FR-TEST-09.1: Should check all expected requirements."""
        validator = RtmGateValidator(
            rtm_path=temp_rtm_complete / "rtm.csv",
            expected_requirements=[
                "FR-TEST-01",
                "FR-TEST-02",
                "FR-TEST-03",
                "FR-TEST-04",
                "FR-TEST-05",
                "FR-TEST-06",
                "FR-TEST-07",
                "FR-TEST-08",
                "FR-TEST-09",
                "FR-TEST-10",
            ],
        )
        result = validator.validate()

        assert result.total_requirements == 10


class TestEdgeCases:
    """Tests for edge cases and error handling."""

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_missing_rtm_file(self, tmp_path: Path):
        """FR-TEST-09.1: Missing RTM file should fail validation."""
        validator = RtmGateValidator(rtm_path=tmp_path / "nonexistent.csv")
        result = validator.validate()

        assert result.is_valid is False
        assert "not found" in result.error_message.lower()

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_empty_rtm_file(self, tmp_path: Path):
        """FR-TEST-09.1: Empty RTM file should fail validation."""
        rtm_path = tmp_path / "rtm.csv"
        rtm_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status"
        )

        validator = RtmGateValidator(rtm_path=rtm_path)
        result = validator.validate()

        assert result.is_valid is False

    @pytest.mark.requirement("FR-TEST-09.1")
    def test_malformed_csv(self, tmp_path: Path):
        """FR-TEST-09.1: Malformed CSV should be handled gracefully."""
        rtm_path = tmp_path / "rtm.csv"
        rtm_path.write_text("this is not valid csv\nwith,bad,format")

        validator = RtmGateValidator(rtm_path=rtm_path)
        # Should not raise exception
        result = validator.validate()

        assert result.is_valid is False

    @pytest.mark.requirement("FR-TEST-09.3")
    def test_duplicate_test_case_ids(self, tmp_path: Path):
        """FR-TEST-09.3: Should detect duplicate test case IDs."""
        csv_content = """requirement_id,test_case_id,test_suite,safety_class,status
FR-TEST-01.1,test_dup,unit/cpp,C,pass
FR-TEST-02.1,test_dup,unit/cpp,C,pass
"""
        rtm_path = tmp_path / "rtm.csv"
        rtm_path.write_text(csv_content)

        validator = RtmGateValidator(rtm_path=rtm_path)
        result = validator.validate()

        # Should detect duplicate
        assert result.is_valid is False or len(result.warnings) > 0


class TestReporting:
    """Tests for reporting functionality."""

    @pytest.mark.requirement("FR-TEST-09.6")
    def test_coverage_percentage(self, temp_rtm_complete: Path):
        """FR-TEST-09.6: Should calculate requirement coverage percentage."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        result = validator.validate()

        assert result.coverage_percentage == 100.0

    @pytest.mark.requirement("FR-TEST-09.6")
    def test_partial_coverage_report(self, temp_rtm_incomplete: Path):
        """FR-TEST-09.6: Should report partial coverage accurately."""
        validator = RtmGateValidator(
            rtm_path=temp_rtm_incomplete / "rtm.csv",
            expected_requirements=[
                "FR-TEST-01",
                "FR-TEST-02",
                "FR-TEST-03",
                "FR-TEST-04",
                "FR-TEST-05",
                "FR-TEST-06",
                "FR-TEST-07",
                "FR-TEST-08",
                "FR-TEST-09",
                "FR-TEST-10",
            ],
        )
        result = validator.validate()

        # 9 out of 10 linked = 90%
        assert result.coverage_percentage == 90.0

    @pytest.mark.requirement("FR-TEST-09.6")
    def test_safety_class_breakdown(self, temp_rtm_complete: Path):
        """FR-TEST-09.6: Should provide safety class breakdown."""
        validator = RtmGateValidator(rtm_path=temp_rtm_complete / "rtm.csv")
        result = validator.validate()

        assert hasattr(result, "safety_class_breakdown")
        assert "A" in result.safety_class_breakdown
        assert "B" in result.safety_class_breakdown
        assert "C" in result.safety_class_breakdown
