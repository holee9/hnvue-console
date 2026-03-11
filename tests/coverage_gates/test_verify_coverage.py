# -*- coding: utf-8 -*-
"""
SPEC-TEST-001 Phase 3.2: Coverage Verification Module Tests

Tests for verify_coverage.py module functionality.

Covers:
- NFR-TEST-01: 85% coverage minimum
- NFR-TEST-01.1: 100% decision coverage for Class C components
- NFR-TEST-04: Class C merge gate enforcement
"""

import json
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

from tests.coverage_gates.verify_coverage import (
    CoberturaParser,
    CoverageGatesConfig,
    CoverageResult,
    CoverageVerifier,
    ComponentThreshold,
    VerificationReport,
    load_coverage_gates,
)


# Path to coverage gates configuration
COVERAGE_GATES_PATH = Path(__file__).parent / "coverage_gates.json"


class TestCoverageGatesConfig:
    """Tests for CoverageGatesConfig class."""

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_load_config(self):
        """NFR-TEST-01: Config loads successfully from coverage_gates.json."""
        config = CoverageGatesConfig(COVERAGE_GATES_PATH)

        assert config.version == "1.0.0"
        assert len(config.components) == 7

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_class_c_components(self):
        """NFR-TEST-01.1: Class C components are correctly identified."""
        config = CoverageGatesConfig(COVERAGE_GATES_PATH)

        class_c = config.class_c_components

        assert len(class_c) == 2
        assert all(c.is_class_c() for c in class_c)
        assert all(c.coverage_type == "decision" for c in class_c)
        assert all(c.threshold == 100.0 for c in class_c)

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_file_not_found(self):
        """NFR-TEST-01: FileNotFoundError when config file missing."""
        with pytest.raises(FileNotFoundError):
            CoverageGatesConfig(Path("/nonexistent/coverage_gates.json"))


class TestComponentThreshold:
    """Tests for ComponentThreshold dataclass."""

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_is_class_c(self):
        """NFR-TEST-01.1: is_class_c() returns correct value."""
        class_c = ComponentThreshold(
            name="Test",
            safety_class="C",
            coverage_type="decision",
            threshold=100.0,
            path_pattern="src/**/test/**",
        )
        class_b = ComponentThreshold(
            name="Test",
            safety_class="B",
            coverage_type="statement",
            threshold=85.0,
            path_pattern="src/**/test/**",
        )

        assert class_c.is_class_c() is True
        assert class_b.is_class_c() is False


class TestCoberturaParser:
    """Tests for CoberturaParser class."""

    def _create_cobertura_xml(self, coverage_data: dict[str, float]) -> Path:
        """Create a temporary Cobertura XML file for testing."""
        root = ET.Element("coverage")
        root.set("line-rate", "0.85")

        packages = ET.SubElement(root, "packages")
        pkg = ET.SubElement(packages, "package")
        pkg.set("name", "test.package")
        pkg.set("line-rate", "0.85")

        classes = ET.SubElement(pkg, "classes")

        for filename, rate in coverage_data.items():
            cls = ET.SubElement(classes, "class")
            cls.set("filename", filename)
            cls.set("line-rate", str(rate / 100))

        tree = ET.ElementTree(root)

        temp_file = tempfile.NamedTemporaryFile(mode="w", suffix=".xml", delete=False)
        temp_path = Path(temp_file.name)
        temp_file.close()

        tree.write(temp_path, encoding="unicode", xml_declaration=True)

        return temp_path

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_parse_coverage(self):
        """NFR-TEST-01: Cobertura XML is parsed correctly."""
        coverage_data = {
            "src/generator/control.cpp": 100.0,
            "src/imaging/processor.cpp": 85.0,
        }

        xml_path = self._create_cobertura_xml(coverage_data)
        try:
            parser = CoberturaParser(xml_path)
            result = parser.parse()

            assert "src/generator/control.cpp" in result
            assert result["src/generator/control.cpp"] == 100.0
            assert "src/imaging/processor.cpp" in result
            assert result["src/imaging/processor.cpp"] == 85.0
        finally:
            xml_path.unlink()

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_get_overall_coverage(self):
        """NFR-TEST-01: Overall coverage is extracted correctly."""
        coverage_data = {"src/test.cpp": 90.0}

        xml_path = self._create_cobertura_xml(coverage_data)
        try:
            parser = CoberturaParser(xml_path)
            overall = parser.get_overall_coverage()

            assert overall == 85.0  # Root line-rate is 0.85
        finally:
            xml_path.unlink()

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_file_not_found(self):
        """NFR-TEST-01: FileNotFoundError when coverage file missing."""
        with pytest.raises(FileNotFoundError):
            CoberturaParser(Path("/nonexistent/coverage.xml")).parse()


class TestCoverageVerifier:
    """Tests for CoverageVerifier class."""

    def _create_test_config(self) -> Path:
        """Create a temporary coverage_gates.json for testing."""
        config_data = {
            "version": "1.0.0-test",
            "components": [
                {
                    "name": "GeneratorControl",
                    "safety_class": "C",
                    "coverage_type": "decision",
                    "threshold": 100.0,
                    "path_pattern": "src/**/generator/**",
                },
                {
                    "name": "ImageProcessing",
                    "safety_class": "B",
                    "coverage_type": "statement",
                    "threshold": 85.0,
                    "path_pattern": "src/**/imaging/**",
                },
            ],
        }

        temp_file = tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False)
        temp_path = Path(temp_file.name)
        temp_file.write(json.dumps(config_data))
        temp_file.close()

        return temp_path

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_verify_all_passing(self):
        """NFR-TEST-01: All components pass when thresholds met."""
        config_path = self._create_test_config()
        try:
            config = CoverageGatesConfig(config_path)
            verifier = CoverageVerifier(config)

            coverage_data = {
                "src/generator/control.cpp": 100.0,
                "src/imaging/processor.cpp": 90.0,
            }

            report = verifier.verify(coverage_data)

            assert report.is_passing() is True
            assert report.total_components == 2
            assert report.passed_components == 2
            assert report.failed_components == 0
            assert report.class_c_passed is True
        finally:
            config_path.unlink()

    @pytest.mark.requirement("NFR-TEST-01.1")
    @pytest.mark.safety_class("C")
    def test_verify_class_c_failing(self):
        """NFR-TEST-01.1: Class C failure blocks overall pass."""
        config_path = self._create_test_config()
        try:
            config = CoverageGatesConfig(config_path)
            verifier = CoverageVerifier(config)

            coverage_data = {
                "src/generator/control.cpp": 95.0,  # Below 100%
                "src/imaging/processor.cpp": 90.0,
            }

            report = verifier.verify(coverage_data)

            assert report.is_passing() is False
            assert report.class_c_passed is False
            assert len(report.get_failed_results()) == 1
        finally:
            config_path.unlink()

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_verify_pattern_matching(self):
        """NFR-TEST-01: Path patterns match correctly."""
        config_path = self._create_test_config()
        try:
            config = CoverageGatesConfig(config_path)
            verifier = CoverageVerifier(config)

            coverage_data = {
                "src/generator/subdir/control.cpp": 100.0,
                "src/imaging/deep/nested/processor.cpp": 90.0,
            }

            report = verifier.verify(coverage_data)

            assert report.is_passing() is True
            # Check files matched
            for result in report.results:
                assert len(result.files_matched) > 0
        finally:
            config_path.unlink()

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_verify_no_matching_files(self):
        """NFR-TEST-01: Components with no matching files get 0% coverage."""
        config_path = self._create_test_config()
        try:
            config = CoverageGatesConfig(config_path)
            verifier = CoverageVerifier(config)

            coverage_data = {
                "src/other/file.cpp": 90.0,
            }

            report = verifier.verify(coverage_data)

            assert report.is_passing() is False
            assert report.passed_components == 0
            for result in report.results:
                assert result.actual_coverage == 0.0
        finally:
            config_path.unlink()


class TestVerificationReport:
    """Tests for VerificationReport dataclass."""

    def _create_report(self, passed: bool) -> VerificationReport:
        """Create a test VerificationReport."""
        component = ComponentThreshold(
            name="Test",
            safety_class="B",
            coverage_type="statement",
            threshold=85.0,
            path_pattern="src/**",
        )

        return VerificationReport(
            total_components=2,
            passed_components=2 if passed else 1,
            failed_components=0 if passed else 1,
            class_c_passed=passed,
            results=[
                CoverageResult(
                    component=component,
                    actual_coverage=90.0,
                    passed=True,
                ),
                CoverageResult(
                    component=component,
                    actual_coverage=80.0,
                    passed=passed,
                ),
            ],
            overall_coverage=85.0,
        )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_is_passing(self):
        """NFR-TEST-01: is_passing() returns correct value."""
        passing_report = self._create_report(True)
        failing_report = self._create_report(False)

        assert passing_report.is_passing() is True
        assert failing_report.is_passing() is False

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_get_failed_results(self):
        """NFR-TEST-01: get_failed_results() returns failing results."""
        report = self._create_report(False)

        failed = report.get_failed_results()

        assert len(failed) == 1
        assert all(not r.passed for r in failed)


class TestConvenienceFunctions:
    """Tests for convenience functions."""

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_load_coverage_gates(self):
        """NFR-TEST-01: load_coverage_gates() loads config correctly."""
        config = load_coverage_gates(COVERAGE_GATES_PATH)

        assert isinstance(config, CoverageGatesConfig)
        assert len(config.components) == 7


class TestCoverageResultString:
    """Tests for CoverageResult string representation."""

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_str_passing(self):
        """NFR-TEST-01: Passing result string contains PASS."""
        component = ComponentThreshold(
            name="Test",
            safety_class="B",
            coverage_type="statement",
            threshold=85.0,
            path_pattern="src/**",
        )
        result = CoverageResult(
            component=component,
            actual_coverage=90.0,
            passed=True,
        )

        s = str(result)

        assert "PASS" in s
        assert "Test" in s
        assert "90.00%" in s
        assert "85.0%" in s  # Threshold displayed as 85.0%

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_str_failing(self):
        """NFR-TEST-01: Failing result string contains FAIL."""
        component = ComponentThreshold(
            name="Test",
            safety_class="C",
            coverage_type="decision",
            threshold=100.0,
            path_pattern="src/**",
        )
        result = CoverageResult(
            component=component,
            actual_coverage=95.0,
            passed=False,
        )

        s = str(result)

        assert "FAIL" in s
        assert "Test" in s
        assert "95.00%" in s
        assert "100.0%" in s  # Threshold displayed as 100.0%
