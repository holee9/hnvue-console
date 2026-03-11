# test_rtm_report_generator.py
"""
Tests for RTM Report Generator Module

FR-TEST-09.6: The system shall generate a human-readable RTM report in HTML format
"""

from pathlib import Path

import pytest

from tests.traceability.rtm_report_generator import RtmReportGenerator


@pytest.fixture
def rtm_report_generator():
    """Create an RtmReportGenerator for the test RTM CSV."""
    rtm_csv_path = Path(__file__).parent / "rtm.csv"
    return RtmReportGenerator(rtm_csv_path)


@pytest.fixture
def temp_rtm_csv(tmp_path):
    """Create a temporary RTM CSV file for testing."""
    csv_path = tmp_path / "test_rtm.csv"
    csv_content = """requirement_id,test_case_id,test_suite,safety_class,status
FR-TEST-01.1,test_one,unit/cpp,NA,pass
FR-TEST-02.1,test_two,integration/ipc,B,fail
FR-TEST-03.1,test_three,system/workflows,A,pending
"""
    csv_path.write_text(csv_content, encoding="utf-8")
    return csv_path


@pytest.fixture
def temp_output_dir(tmp_path):
    """Create a temporary output directory."""
    output_dir = tmp_path / "output"
    output_dir.mkdir()
    return output_dir


class TestRtmReportGeneratorInit:
    """Tests for RtmReportGenerator initialization."""

    def test_init_with_valid_csv(self, temp_rtm_csv):
        """Should initialize successfully with valid CSV."""
        generator = RtmReportGenerator(temp_rtm_csv)
        assert generator.parser is not None
        assert generator.validator is not None


class TestRtmReportGeneratorGenerateReport:
    """Tests for report generation."""

    def test_generate_report_creates_file(self, temp_rtm_csv, temp_output_dir):
        """Should create HTML file at specified path."""
        output_path = temp_output_dir / "report.html"
        generator = RtmReportGenerator(temp_rtm_csv)
        generator.generate_report(output_path)

        assert output_path.exists()

    def test_generate_report_creates_parent_dirs(self, temp_rtm_csv, tmp_path):
        """Should create parent directories if they don't exist."""
        output_path = tmp_path / "nested" / "dirs" / "report.html"
        generator = RtmReportGenerator(temp_rtm_csv)
        generator.generate_report(output_path)

        assert output_path.exists()
        assert output_path.parent.exists()

    def test_generate_report_utf8_encoding(self, temp_rtm_csv, temp_output_dir):
        """Should write file with UTF-8 encoding."""
        output_path = temp_output_dir / "report.html"
        generator = RtmReportGenerator(temp_rtm_csv)
        generator.generate_report(output_path)

        # Should be readable with UTF-8
        content = output_path.read_text(encoding="utf-8")
        assert len(content) > 0


class TestRtmReportGeneratorHtmlStructure:
    """Tests for HTML structure validation."""

    def test_html_has_doctype(self, rtm_report_generator, temp_output_dir):
        """HTML should have DOCTYPE declaration."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "<!DOCTYPE html>" in content

    def test_html_has_html_tag(self, rtm_report_generator, temp_output_dir):
        """HTML should have html tag with lang attribute."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "<html" in content
        assert "</html>" in content

    def test_html_has_head_section(self, rtm_report_generator, temp_output_dir):
        """HTML should have head section with title."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "<head>" in content
        assert "</head>" in content
        assert "<title>" in content

    def test_html_has_body_section(self, rtm_report_generator, temp_output_dir):
        """HTML should have body section."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "<body>" in content
        assert "</body>" in content

    def test_html_has_css_styles(self, rtm_report_generator, temp_output_dir):
        """HTML should have CSS styles."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "<style>" in content
        assert "</style>" in content


class TestRtmReportGeneratorContent:
    """Tests for HTML content validation."""

    def test_html_has_header(self, rtm_report_generator, temp_output_dir):
        """HTML should have header with title."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "Requirements Traceability Matrix" in content
        assert "SPEC-TEST-001" in content

    def test_html_has_summary_section(self, rtm_report_generator, temp_output_dir):
        """HTML should have summary section."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "Total Requirements:" in content or "Total Test Cases:" in content
        assert "summary" in content.lower()

    def test_html_has_coverage_section(self, rtm_report_generator, temp_output_dir):
        """HTML should have coverage section."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "Coverage" in content or "FR-TEST" in content

    def test_html_has_details_table(self, rtm_report_generator, temp_output_dir):
        """HTML should have details table with entries."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "<table>" in content
        assert "</table>" in content
        assert "<thead>" in content
        assert "<tbody>" in content

    def test_html_has_footer(self, rtm_report_generator, temp_output_dir):
        """HTML should have footer with generation info."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "Generated:" in content
        assert "HnVue" in content

    def test_html_contains_requirement_ids(self, rtm_report_generator, temp_output_dir):
        """HTML should contain all requirement IDs from CSV."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "FR-TEST-01" in content

    def test_html_contains_test_case_ids(self, rtm_report_generator, temp_output_dir):
        """HTML should contain test case IDs."""
        output_path = temp_output_dir / "report.html"
        rtm_report_generator.generate_report(output_path)

        content = output_path.read_text(encoding="utf-8")
        assert "test_" in content


class TestRtmReportGeneratorCssMethods:
    """Tests for CSS generation methods."""

    def test_get_css_returns_string(self, rtm_report_generator):
        """_get_css should return a CSS string."""
        css = rtm_report_generator._get_css()
        assert isinstance(css, str)
        assert len(css) > 0
        assert "body" in css
        assert ".summary" in css

    def test_css_has_body_styles(self, rtm_report_generator):
        """CSS should have body element styles."""
        css = rtm_report_generator._get_css()
        assert "font-family" in css
        assert "background-color" in css

    def test_css_has_status_classes(self, rtm_report_generator):
        """CSS should have status classes."""
        css = rtm_report_generator._get_css()
        assert ".status-pass" in css
        assert ".status-fail" in css
        assert ".status-pending" in css

    def test_css_has_safety_class_styles(self, rtm_report_generator):
        """CSS should have safety class styles."""
        css = rtm_report_generator._get_css()
        assert ".safety-class-c" in css
        assert ".safety-class-b" in css
        assert ".safety-class-a" in css


class TestRtmReportGeneratorSectionMethods:
    """Tests for HTML section generation methods."""

    def test_generate_header_returns_html(self, rtm_report_generator):
        """_generate_header should return HTML string."""
        header = rtm_report_generator._generate_header()
        assert "<h1>" in header
        assert "Requirements Traceability Matrix" in header

    def test_generate_summary_section_returns_html(self, rtm_report_generator):
        """_generate_summary_section should return HTML string."""
        summary = self._get_summary(rtm_report_generator)
        html = rtm_report_generator._generate_summary_section(summary)
        assert "summary" in html.lower()

    def test_generate_coverage_section_returns_html(self, rtm_report_generator):
        """_generate_coverage_section should return HTML string."""
        coverage = rtm_report_generator.validator.get_coverage_report()
        html = rtm_report_generator._generate_coverage_section(coverage)
        assert "coverage" in html.lower() or "FR-TEST" in html

    def test_generate_details_table_returns_html(self, rtm_report_generator):
        """_generate_details_table should return HTML string."""
        html = rtm_report_generator._generate_details_table()
        assert "<table>" in html
        assert "<thead>" in html
        assert "<tbody>" in html

    def test_generate_footer_returns_html(self, rtm_report_generator):
        """_generate_footer should return HTML string."""
        footer = rtm_report_generator._generate_footer()
        assert "Generated:" in footer
        assert "HnVue" in footer

    def _get_summary(self, generator):
        """Helper to get summary dict."""
        return generator.parser.get_summary()
