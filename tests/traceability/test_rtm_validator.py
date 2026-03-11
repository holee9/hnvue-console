# test_rtm_validator.py
"""
Tests for RTM Validator Module

FR-TEST-09.1: RTM links each requirement to one or more test cases
FR-TEST-09.2: RTM provides bidirectional traceability
FR-TEST-09.3: Test cases must reference valid requirements
"""

from pathlib import Path

import pytest

from tests.traceability.rtm_validator import RtmValidator, ValidationResult


@pytest.fixture
def rtm_validator():
    """Create an RtmValidator instance for the test RTM CSV."""
    rtm_csv_path = Path(__file__).parent / "rtm.csv"
    return RtmValidator(rtm_csv_path)


@pytest.fixture
def temp_rtm_csv(tmp_path):
    """Create a temporary RTM CSV file for testing."""
    csv_path = tmp_path / "test_rtm.csv"
    csv_content = """requirement_id,test_case_id,test_suite,safety_class,status
FR-TEST-01.1,test_one,unit/cpp,NA,pass
FR-TEST-02.1,test_two,integration/ipc,B,fail
"""
    csv_path.write_text(csv_content, encoding="utf-8")
    return csv_path


class TestValidationResult:
    """Tests for ValidationResult dataclass."""

    def test_default_values(self):
        """ValidationResult should have default empty lists."""
        result = ValidationResult(is_valid=True)
        assert result.errors == []
        assert result.warnings == []
        assert result.unlinked_requirements == []
        assert result.untraceable_tests == []

    def test_with_errors(self):
        """ValidationResult should store errors."""
        result = ValidationResult(
            is_valid=False,
            errors=["Error 1", "Error 2"],
        )
        assert result.is_valid is False
        assert len(result.errors) == 2


class TestRtmValidatorInit:
    """Tests for RtmValidator initialization."""

    def test_init_with_valid_csv(self, temp_rtm_csv):
        """RtmValidator should initialize with valid CSV."""
        validator = RtmValidator(temp_rtm_csv)
        assert validator.parser is not None


class TestRtmValidatorValidate:
    """Tests for RtmValidator.validate method."""

    def test_validate_complete_rtm(self, rtm_validator):
        """Complete RTM should pass validation."""
        result = rtm_validator.validate()
        # Our test RTM should have all requirements linked
        assert result.unlinked_requirements == []

    def test_validate_returns_validation_result(self, rtm_validator):
        """validate() should return ValidationResult."""
        result = rtm_validator.validate()
        assert isinstance(result, ValidationResult)

    def test_validate_incomplete_rtm(self, tmp_path):
        """Incomplete RTM should fail validation."""
        # Create CSV with only one requirement
        csv_path = tmp_path / "incomplete.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,NA,pass",
            encoding="utf-8",
        )

        validator = RtmValidator(csv_path)
        result = validator.validate()

        # Should have unlinked requirements (FR-TEST-02 through FR-TEST-10)
        assert len(result.unlinked_requirements) > 0
        assert result.is_valid is False


class TestRtmValidatorFindUnlinkedRequirements:
    """Tests for finding unlinked requirements."""

    def test_all_linked(self, rtm_validator):
        """Should return empty list when all requirements are linked."""
        unlinked = rtm_validator._find_unlinked_requirements()
        assert unlinked == []

    def test_some_unlinked(self, tmp_path):
        """Should return list of unlinked requirements."""
        csv_path = tmp_path / "partial.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,NA,pass\n"
            "FR-TEST-02.1,test_two,unit,NA,pass\n",
            encoding="utf-8",
        )

        validator = RtmValidator(csv_path)
        unlinked = validator._find_unlinked_requirements()

        # FR-TEST-03 through FR-TEST-10 should be unlinked
        assert "FR-TEST-03" in unlinked
        assert "FR-TEST-10" in unlinked
        assert "FR-TEST-01" not in unlinked
        assert "FR-TEST-02" not in unlinked


class TestRtmValidatorFindUntraceableTests:
    """Tests for finding tests with invalid requirement references."""

    def test_all_traceable(self, rtm_validator):
        """Should return empty list when all tests are traceable."""
        untraceable = rtm_validator._find_untraceable_tests()
        assert untraceable == []

    def test_invalid_requirement_format(self, tmp_path):
        """Should detect tests with invalid requirement ID format."""
        csv_path = tmp_path / "invalid_req.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "INVALID-01.1,test_one,unit,NA,pass\n",
            encoding="utf-8",
        )

        validator = RtmValidator(csv_path)
        untraceable = validator._find_untraceable_tests()

        assert "test_one" in untraceable


class TestRtmValidatorCoverageReport:
    """Tests for coverage report generation."""

    def test_get_coverage_report_returns_dict(self, rtm_validator):
        """get_coverage_report should return a dictionary."""
        coverage = rtm_validator.get_coverage_report()
        assert isinstance(coverage, dict)

    def test_coverage_report_has_all_requirements(self, rtm_validator):
        """Coverage report should include all expected requirements."""
        coverage = rtm_validator.get_coverage_report()

        expected = [f"FR-TEST-0{i}" for i in range(1, 10)] + ["FR-TEST-10"]
        for req in expected:
            assert req in coverage

    def test_coverage_report_has_required_fields(self, rtm_validator):
        """Each requirement in coverage report should have required fields."""
        coverage = rtm_validator.get_coverage_report()

        for req_id, data in coverage.items():
            assert "has_tests" in data
            assert "test_count" in data
            assert "test_cases" in data
            assert "safety_classes" in data
            assert "status" in data


class TestRtmValidatorDetermineStatus:
    """Tests for status determination logic."""

    def test_determine_status_unlinked(self, rtm_validator):
        """Should return 'unlinked' for empty entries."""
        status = rtm_validator._determine_status([])
        assert status == "unlinked"

    def test_determine_status_fail(self, tmp_path):
        """Should return 'fail' when any entry has fail status."""
        csv_path = tmp_path / "status.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,NA,pass\n"
            "FR-TEST-01.2,test_two,unit,NA,fail\n",
            encoding="utf-8",
        )

        validator = RtmValidator(csv_path)
        from tests.traceability.rtm_parser import RtmEntry

        entries = [
            RtmEntry("FR-TEST-01.1", "test_one", "unit", "NA", "pass"),
            RtmEntry("FR-TEST-01.2", "test_two", "unit", "NA", "fail"),
        ]
        status = validator._determine_status(entries)
        assert status == "fail"

    def test_determine_status_pending(self, tmp_path):
        """Should return 'pending' when any entry has pending status."""
        csv_path = tmp_path / "status.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,NA,pending\n",
            encoding="utf-8",
        )

        validator = RtmValidator(csv_path)
        from tests.traceability.rtm_parser import RtmEntry

        entries = [RtmEntry("FR-TEST-01.1", "test_one", "unit", "NA", "pending")]
        status = validator._determine_status(entries)
        assert status == "pending"

    def test_determine_status_pass(self, tmp_path):
        """Should return 'pass' when all entries pass."""
        csv_path = tmp_path / "status.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,NA,pass\n"
            "FR-TEST-01.2,test_two,unit,NA,pass\n",
            encoding="utf-8",
        )

        validator = RtmValidator(csv_path)
        from tests.traceability.rtm_parser import RtmEntry

        entries = [
            RtmEntry("FR-TEST-01.1", "test_one", "unit", "NA", "pass"),
            RtmEntry("FR-TEST-01.2", "test_two", "unit", "NA", "pass"),
        ]
        status = validator._determine_status(entries)
        assert status == "pass"

    def test_determine_status_skip(self, tmp_path):
        """Should return 'skip' when all entries are skipped."""
        csv_path = tmp_path / "status.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,NA,skip\n",
            encoding="utf-8",
        )

        validator = RtmValidator(csv_path)
        from tests.traceability.rtm_parser import RtmEntry

        entries = [RtmEntry("FR-TEST-01.1", "test_one", "unit", "NA", "skip")]
        status = validator._determine_status(entries)
        assert status == "skip"

    def test_determine_status_partial(self, tmp_path):
        """Should return 'partial' when mixed pass and skip."""
        csv_path = tmp_path / "status.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,NA,pass\n"
            "FR-TEST-01.2,test_two,unit,NA,skip\n",
            encoding="utf-8",
        )

        validator = RtmValidator(csv_path)
        from tests.traceability.rtm_parser import RtmEntry

        entries = [
            RtmEntry("FR-TEST-01.1", "test_one", "unit", "NA", "pass"),
            RtmEntry("FR-TEST-01.2", "test_two", "unit", "NA", "skip"),
        ]
        status = validator._determine_status(entries)
        assert status == "partial"
