# rtm_report_generator.py
"""
RTM Report Generator Module

Generates human-readable HTML reports from the Requirements Traceability Matrix.

FR-TEST-09.6: The system shall generate a human-readable RTM report in HTML format
"""

from datetime import datetime
from pathlib import Path

from .rtm_parser import RtmParser
from .rtm_validator import RtmValidator


class RtmReportGenerator:
    """
    Generates HTML reports from RTM CSV data.

    Usage:
        generator = RtmReportGenerator("tests/traceability/rtm.csv")
        generator.generate_report("tests/traceability/rtm_report.html")
    """

    def __init__(self, rtm_csv_path: str | Path):
        """
        Initialize the report generator.

        Args:
            rtm_csv_path: Path to the RTM CSV file.
        """
        self.parser = RtmParser(rtm_csv_path)
        self.validator = RtmValidator(rtm_csv_path)

    def generate_report(self, output_path: str | Path) -> None:
        """
        Generate an HTML report from the RTM CSV.

        Args:
            output_path: Path to write the HTML report.
        """
        output_path = Path(output_path)
        summary = self.parser.get_summary()
        coverage = self.validator.get_coverage_report()

        html_content = self._generate_html(summary, coverage)

        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(html_content, encoding="utf-8")

    def _generate_html(self, summary: dict, coverage: dict) -> str:
        """Generate the full HTML document."""
        return f"""<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Requirements Traceability Matrix - SPEC-TEST-001</title>
    <style>
        {self._get_css()}
    </style>
</head>
<body>
    {self._generate_header()}
    {self._generate_summary_section(summary)}
    {self._generate_coverage_section(coverage)}
    {self._generate_details_table()}
    {self._generate_footer()}
</body>
</html>"""

    def _get_css(self) -> str:
        """Return the CSS styles for the report."""
        return """
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f5;
        }
        h1 {
            color: #333;
            border-bottom: 2px solid #4a90d9;
            padding-bottom: 10px;
        }
        h2 {
            color: #4a90d9;
            margin-top: 30px;
        }
        .summary {
            background-color: #fff;
            padding: 15px;
            border-radius: 5px;
            margin-bottom: 20px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }
        .summary-item {
            display: inline-block;
            margin-right: 30px;
        }
        .summary-label {
            font-weight: bold;
            color: #666;
        }
        .summary-value {
            color: #333;
            font-size: 1.2em;
        }
        .coverage-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
            gap: 15px;
            margin-bottom: 30px;
        }
        .coverage-card {
            background-color: #fff;
            padding: 15px;
            border-radius: 5px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }
        .coverage-card h3 {
            margin-top: 0;
            margin-bottom: 10px;
            color: #333;
        }
        .coverage-card .status {
            display: inline-block;
            padding: 3px 8px;
            border-radius: 3px;
            font-size: 0.9em;
            font-weight: bold;
        }
        .status-pass { background-color: #d4edda; color: #155724; }
        .status-fail { background-color: #f8d7da; color: #721c24; }
        .status-pending { background-color: #fff3cd; color: #856404; }
        .status-skip { background-color: #e2e3e5; color: #383d41; }
        .status-unlinked { background-color: #f8d7da; color: #721c24; }
        .status-partial { background-color: #d1ecf1; color: #0c5460; }
        table {
            width: 100%;
            border-collapse: collapse;
            background-color: #fff;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }
        th, td {
            border: 1px solid #ddd;
            padding: 10px;
            text-align: left;
        }
        th {
            background-color: #4a90d9;
            color: white;
        }
        tr:nth-child(even) {
            background-color: #f9f9f9;
        }
        .safety-class-c {
            background-color: #ffebee;
            font-weight: bold;
        }
        .safety-class-b {
            background-color: #fff8e1;
        }
        .safety-class-a {
            background-color: #e8f5e9;
        }
        .generated-at {
            color: #666;
            font-size: 0.9em;
            margin-top: 20px;
        }
        """

    def _generate_header(self) -> str:
        """Generate the HTML header section."""
        return """
    <h1>Requirements Traceability Matrix</h1>
    <h2>SPEC-TEST-001: HnVue Testing Framework and V&V Strategy</h2>
    """

    def _generate_summary_section(self, summary: dict) -> str:
        """Generate the summary statistics section."""
        status_html = " | ".join(
            f"{status.upper()}: {count}"
            for status, count in summary.get("status_counts", {}).items()
        )

        return f"""
    <div class="summary">
        <div class="summary-item">
            <span class="summary-label">Total Requirements:</span>
            <span class="summary-value">{summary.get("parent_requirements", 0)}</span>
        </div>
        <div class="summary-item">
            <span class="summary-label">Total Test Cases:</span>
            <span class="summary-value">{summary.get("total_entries", 0)}</span>
        </div>
        <div class="summary-item">
            <span class="summary-label">Status:</span>
            <span class="summary-value">{status_html}</span>
        </div>
    </div>
    """

    def _generate_coverage_section(self, coverage: dict) -> str:
        """Generate the coverage cards section."""
        cards = []
        for req_id, data in coverage.items():
            status_class = f"status-{data['status']}"
            tests_info = (
                f"{data['test_count']} test(s)" if data["has_tests"] else "No tests"
            )
            safety_info = (
                ", ".join(data["safety_classes"]) if data["safety_classes"] else "N/A"
            )

            cards.append(f"""
        <div class="coverage-card">
            <h3>{req_id}</h3>
            <p><strong>Status:</strong> <span class="status {status_class}">{data["status"].upper()}</span></p>
            <p><strong>Tests:</strong> {tests_info}</p>
            <p><strong>Safety:</strong> {safety_info}</p>
        </div>
            """)

        return f"""
    <h2>Requirement Coverage</h2>
    <div class="coverage-grid">
        {"".join(cards)}
    </div>
    """

    def _generate_details_table(self) -> str:
        """Generate the detailed entries table."""
        rows = []
        for entry in self.parser.entries:
            safety_class = (
                f"safety-class-{entry.safety_class.lower()}"
                if entry.safety_class != "NA"
                else ""
            )
            rows.append(f"""
            <tr class="{safety_class}">
                <td>{entry.requirement_id}</td>
                <td>{entry.test_case_id}</td>
                <td>{entry.test_suite}</td>
                <td>{entry.safety_class}</td>
                <td class="status-{entry.status.lower()}">{entry.status.upper()}</td>
            </tr>
            """)

        return f"""
    <h2>Detailed Traceability</h2>
    <table>
        <thead>
            <tr>
                <th>Requirement ID</th>
                <th>Test Case ID</th>
                <th>Test Suite</th>
                <th>Safety Class</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
            {"".join(rows)}
        </tbody>
    </table>
    """

    def _generate_footer(self) -> str:
        """Generate the footer section."""
        generated_at = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        return f"""
    <p class="generated-at">
        Generated: {generated_at} |
        SPEC: SPEC-TEST-001 |
        Product: HnVue - Diagnostic Medical Device X-ray GUI Console SW
    </p>
    """


def main():
    """Command-line entry point for report generation."""
    import argparse

    parser = argparse.ArgumentParser(description="Generate RTM HTML report from CSV")
    parser.add_argument(
        "csv_path",
        help="Path to the RTM CSV file",
    )
    parser.add_argument(
        "-o",
        "--output",
        default="rtm_report.html",
        help="Output HTML file path (default: rtm_report.html)",
    )

    args = parser.parse_args()

    generator = RtmReportGenerator(args.csv_path)
    generator.generate_report(args.output)
    print(f"RTM report generated: {args.output}")


if __name__ == "__main__":
    main()
