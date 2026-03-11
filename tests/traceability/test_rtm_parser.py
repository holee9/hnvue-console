# test_rtm_parser.py
"""
Tests for RTM Parser Module

FR-TEST-09.5: RTM is machine-readable (CSV)
"""

from pathlib import Path

import pytest

from tests.traceability.rtm_parser import RtmEntry, RtmParser


@pytest.fixture
def rtm_parser():
    """Create an RtmParser instance pointing to the test RTM CSV."""
    rtm_csv_path = Path(__file__).parent / "rtm.csv"
    return RtmParser(rtm_csv_path)


@pytest.fixture
def temp_rtm_csv(tmp_path):
    """Create a temporary RTM CSV file for testing."""
    csv_path = tmp_path / "test_rtm.csv"
    csv_content = """requirement_id,test_case_id,test_suite,safety_class,status
FR-TEST-01.1,test_one,unit/cpp,NA,pass
FR-TEST-01.2,test_two,unit/csharp,A,pass
FR-TEST-02.1,test_three,integration/ipc,B,fail
"""
    csv_path.write_text(csv_content, encoding="utf-8")
    return csv_path


class TestRtmParserInit:
    """Tests for RtmParser initialization."""

    def test_init_with_valid_csv(self, temp_rtm_csv):
        """RtmParser should initialize successfully with valid CSV."""
        parser = RtmParser(temp_rtm_csv)
        assert parser.csv_path == temp_rtm_csv

    def test_init_with_nonexistent_csv_raises_error(self, tmp_path):
        """RtmParser should raise FileNotFoundError for nonexistent file."""
        nonexistent_path = tmp_path / "nonexistent.csv"
        with pytest.raises(FileNotFoundError) as exc_info:
            RtmParser(nonexistent_path)
        assert "RTM CSV file not found" in str(exc_info.value)


class TestRtmParserEntries:
    """Tests for RtmParser entries loading."""

    def test_entries_loads_all_rows(self, rtm_parser):
        """Parser should load all entries from CSV."""
        entries = rtm_parser.entries
        assert len(entries) > 0

    def test_entries_are_rtm_entry_objects(self, rtm_parser):
        """Parser should return RtmEntry objects."""
        entries = rtm_parser.entries
        assert all(isinstance(e, RtmEntry) for e in entries)

    def test_entries_have_correct_attributes(self, temp_rtm_csv):
        """RtmEntry objects should have correct attributes."""
        parser = RtmParser(temp_rtm_csv)
        entries = parser.entries

        assert len(entries) == 3
        assert entries[0].requirement_id == "FR-TEST-01.1"
        assert entries[0].test_case_id == "test_one"
        assert entries[0].test_suite == "unit/cpp"
        assert entries[0].safety_class == "NA"
        assert entries[0].status == "pass"

    def test_entries_caching(self, rtm_parser):
        """Parser should cache entries after first load."""
        entries1 = rtm_parser.entries
        entries2 = rtm_parser.entries
        assert entries1 is entries2


class TestRtmParserGetEntriesByRequirement:
    """Tests for get_entries_by_requirement method."""

    def test_get_entries_by_exact_requirement_id(self, rtm_parser):
        """Should return entries matching exact requirement ID."""
        entries = rtm_parser.get_entries_by_requirement("FR-TEST-01.1")
        assert len(entries) >= 1
        assert all(e.requirement_id == "FR-TEST-01.1" for e in entries)

    def test_get_entries_by_parent_requirement(self, rtm_parser):
        """Should return all entries under parent requirement."""
        entries = rtm_parser.get_entries_by_requirement("FR-TEST-01")
        assert len(entries) >= 1
        assert all(e.requirement_id.startswith("FR-TEST-01") for e in entries)

    def test_get_entries_by_nonexistent_requirement(self, rtm_parser):
        """Should return empty list for nonexistent requirement."""
        entries = rtm_parser.get_entries_by_requirement("FR-TEST-99")
        assert entries == []


class TestRtmParserGetTestCases:
    """Tests for get_test_cases_for_requirement method."""

    def test_get_test_cases_returns_ids(self, rtm_parser):
        """Should return list of test case IDs."""
        test_cases = rtm_parser.get_test_cases_for_requirement("FR-TEST-01")
        assert len(test_cases) >= 1
        assert all(isinstance(tc, str) for tc in test_cases)

    def test_get_test_cases_for_nonexistent_requirement(self, rtm_parser):
        """Should return empty list for nonexistent requirement."""
        test_cases = rtm_parser.get_test_cases_for_requirement("FR-TEST-99")
        assert test_cases == []


class TestRtmParserGetEntriesByTestSuite:
    """Tests for get_entries_by_test_suite method."""

    def test_get_entries_by_test_suite(self, rtm_parser):
        """Should return entries in specified test suite."""
        entries = rtm_parser.get_entries_by_test_suite("traceability")
        assert len(entries) >= 1
        assert all(e.test_suite == "traceability" for e in entries)

    def test_get_entries_by_nonexistent_suite(self, rtm_parser):
        """Should return empty list for nonexistent suite."""
        entries = rtm_parser.get_entries_by_test_suite("nonexistent/suite")
        assert entries == []


class TestRtmParserGetRequirements:
    """Tests for requirement retrieval methods."""

    def test_get_all_requirements(self, rtm_parser):
        """Should return set of all unique requirement IDs."""
        requirements = rtm_parser.get_all_requirements()
        assert isinstance(requirements, set)
        assert len(requirements) >= 10  # At least 10 FR-TEST requirements

    def test_get_parent_requirements(self, rtm_parser):
        """Should return set of parent requirement IDs."""
        parents = rtm_parser.get_parent_requirements()
        assert isinstance(parents, set)
        # Should include FR-TEST-01 through FR-TEST-10
        for i in range(1, 11):
            expected = f"FR-TEST-0{i}" if i < 10 else "FR-TEST-10"
            assert expected in parents


class TestRtmParserSummary:
    """Tests for get_summary method."""

    def test_get_summary_returns_dict(self, rtm_parser):
        """Should return summary dictionary."""
        summary = rtm_parser.get_summary()
        assert isinstance(summary, dict)

    def test_get_summary_has_required_keys(self, rtm_parser):
        """Summary should have all required keys."""
        summary = rtm_parser.get_summary()
        required_keys = [
            "total_entries",
            "unique_requirements",
            "parent_requirements",
            "status_counts",
            "safety_class_counts",
        ]
        for key in required_keys:
            assert key in summary

    def test_get_summary_counts(self, temp_rtm_csv):
        """Summary counts should be accurate."""
        parser = RtmParser(temp_rtm_csv)
        summary = parser.get_summary()

        assert summary["total_entries"] == 3
        assert summary["unique_requirements"] == 3
        assert summary["status_counts"]["pass"] == 2
        assert summary["status_counts"]["fail"] == 1


class TestRtmParserValidation:
    """Tests for validate method."""

    def test_validate_returns_list(self, rtm_parser):
        """Should return list of errors."""
        errors = rtm_parser.validate()
        assert isinstance(errors, list)

    def test_validate_empty_for_valid_data(self, rtm_parser):
        """Should return empty list for valid data."""
        errors = rtm_parser.validate()
        assert errors == []

    def test_validate_invalid_safety_class(self, tmp_path):
        """Should detect invalid safety_class values."""
        csv_path = tmp_path / "invalid.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,cpp,pass",
            encoding="utf-8",
        )

        parser = RtmParser(csv_path)
        errors = parser.validate()
        assert len(errors) > 0
        assert any("Invalid safety_class" in e for e in errors)

    def test_validate_invalid_status(self, tmp_path):
        """Should detect invalid status values."""
        csv_path = tmp_path / "invalid.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "FR-TEST-01.1,test_one,unit,NA,invalid_status",
            encoding="utf-8",
        )

        parser = RtmParser(csv_path)
        errors = parser.validate()
        assert len(errors) > 0
        assert any("Invalid status" in e for e in errors)

    def test_validate_invalid_requirement_format(self, tmp_path):
        """Should detect invalid requirement ID format."""
        csv_path = tmp_path / "invalid.csv"
        csv_path.write_text(
            "requirement_id,test_case_id,test_suite,safety_class,status\n"
            "INVALID-01.1,test_one,unit,NA,pass",
            encoding="utf-8",
        )

        parser = RtmParser(csv_path)
        errors = parser.validate()
        assert len(errors) > 0
        assert any("Invalid requirement_id format" in e for e in errors)


class TestRtmParserColumnValidation:
    """Tests for CSV column validation."""

    def test_missing_columns_raises_error(self, tmp_path):
        """Should raise ValueError for missing required columns."""
        csv_path = tmp_path / "missing_columns.csv"
        csv_path.write_text(
            "requirement_id,test_case_id\nFR-TEST-01.1,test_one",
            encoding="utf-8",
        )

        with pytest.raises(ValueError) as exc_info:
            RtmParser(csv_path).entries
        assert "missing required columns" in str(exc_info.value)

    def test_empty_columns_raises_error(self, tmp_path):
        """Should raise ValueError for CSV with no columns."""
        csv_path = tmp_path / "empty.csv"
        csv_path.write_text("", encoding="utf-8")

        with pytest.raises(ValueError) as exc_info:
            RtmParser(csv_path).entries
        assert "no columns" in str(exc_info.value)
