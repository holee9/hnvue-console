# consolidate_reports.py
"""
SPEC-TEST-001 Phase 3.4: JUnit Report Consolidation

Consolidates JUnit XML reports from multiple test suites into a single
unified report for CI pipeline integration.

Covers:
- FR-TEST-08.1: JUnit XML format test reports for all test suites
- FR-TEST-08.2: Consolidated JUnit XML report aggregating all results
- FR-TEST-08.3: JUnit XML with test suite name, test case name, execution time, result
"""

import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Optional


@dataclass
class TestSuiteSummary:
    """Summary of a single test suite."""

    name: str
    tests: int
    failures: int
    errors: int
    skipped: int
    time: float
    testcases: list[dict] = field(default_factory=list)


@dataclass
class ConsolidatedReport:
    """Consolidated test report from multiple JUnit XML files."""

    xml_content: str
    total_tests: int
    total_failures: int
    total_errors: int
    total_skipped: int
    total_time: float
    suites: list[TestSuiteSummary]

    @property
    def pass_rate(self) -> float:
        """Calculate overall pass rate as percentage."""
        if self.total_tests == 0:
            return 100.0
        passed = (
            self.total_tests
            - self.total_failures
            - self.total_errors
            - self.total_skipped
        )
        return (passed / self.total_tests) * 100

    def to_markdown(self) -> str:
        """Generate Markdown summary of the report."""
        lines = [
            "# Test Report Summary",
            "",
            f"**Generated**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "",
            "## Overview",
            "",
            "| Metric | Value |",
            "|--------|-------|",
            f"| Total Tests | {self.total_tests} |",
            f"| Passed | {self.total_tests - self.total_failures - self.total_errors - self.total_skipped} |",
            f"| Failed | {self.total_failures} |",
            f"| Errors | {self.total_errors} |",
            f"| Skipped | {self.total_skipped} |",
            f"| Pass Rate | {self.pass_rate:.2f}% |",
            f"| Total Time | {self.total_time:.3f}s |",
            "",
            "## Suite Breakdown",
            "",
            "| Suite | Tests | Failures | Errors | Time |",
            "|-------|-------|----------|--------|------|",
        ]

        for suite in self.suites:
            lines.append(
                f"| {suite.name} | {suite.tests} | {suite.failures} | "
                f"{suite.errors} | {suite.time:.3f}s |"
            )

        # Add failed test details if any
        if self.total_failures > 0 or self.total_errors > 0:
            lines.extend(
                [
                    "",
                    "## Failed Tests",
                    "",
                ]
            )
            for suite in self.suites:
                for tc in suite.testcases:
                    if tc.get("failure") or tc.get("error"):
                        failure_msg = tc.get("failure_message", "No message")
                        lines.append(f"- **{suite.name}::{tc['name']}**: {failure_msg}")

        return "\n".join(lines)


class JUnitConsolidator:
    """
    Consolidates multiple JUnit XML reports into a single unified report.

    Usage:
        consolidator = JUnitConsolidator(source_dir=Path("TestResults"))
        report = consolidator.consolidate()
        report.xml_content  # Full XML content
        report.to_markdown()  # Markdown summary
    """

    def __init__(self, source_dir: Path, pattern: str = "**/junit.xml"):
        """
        Initialize the consolidator.

        Args:
            source_dir: Directory to search for JUnit XML files.
            pattern: Glob pattern for finding XML files.
        """
        self.source_dir = Path(source_dir)
        self.pattern = pattern

    def find_junit_files(self) -> list[Path]:
        """Find all JUnit XML files matching the pattern."""
        return list(self.source_dir.glob(self.pattern))

    def _parse_junit_file(self, file_path: Path) -> list[TestSuiteSummary]:
        """Parse a single JUnit XML file and return test suites."""
        suites = []

        try:
            tree = ET.parse(file_path)
            root = tree.getroot()

            # Handle both <testsuites> and <testsuite> as root
            if root.tag == "testsuites":
                testsuite_elements = root.findall("testsuite")
            elif root.tag == "testsuite":
                testsuite_elements = [root]
            else:
                return suites

            for ts in testsuite_elements:
                name = ts.get("name", "unknown")
                tests = int(ts.get("tests", 0))
                failures = int(ts.get("failures", 0))
                errors = int(ts.get("errors", 0))
                skipped = int(ts.get("skipped", 0))
                time = float(ts.get("time", 0))

                testcases = []
                for tc in ts.findall("testcase"):
                    tc_data = {
                        "name": tc.get("name", "unknown"),
                        "classname": tc.get("classname", ""),
                        "time": float(tc.get("time", 0)),
                        "failure": False,
                        "error": False,
                        "skipped": False,
                        "failure_message": "",
                    }

                    if tc.find("failure") is not None:
                        tc_data["failure"] = True
                        tc_data["failure_message"] = tc.find("failure").get(
                            "message", ""
                        )
                    if tc.find("error") is not None:
                        tc_data["error"] = True
                        tc_data["failure_message"] = tc.find("error").get("message", "")
                    if tc.find("skipped") is not None:
                        tc_data["skipped"] = True

                    testcases.append(tc_data)

                suites.append(
                    TestSuiteSummary(
                        name=name,
                        tests=tests,
                        failures=failures,
                        errors=errors,
                        skipped=skipped,
                        time=time,
                        testcases=testcases,
                    )
                )

        except ET.ParseError:
            # Skip malformed XML files
            pass
        except Exception:
            # Skip files that can't be processed
            pass

        return suites

    def consolidate(self) -> ConsolidatedReport:
        """
        Consolidate all JUnit XML files into a single report.

        Returns:
            ConsolidatedReport with aggregated results.
        """
        all_suites: list[TestSuiteSummary] = []
        junit_files = self.find_junit_files()

        for junit_file in junit_files:
            suites = self._parse_junit_file(junit_file)
            all_suites.extend(suites)

        # Aggregate statistics
        total_tests = sum(s.tests for s in all_suites)
        total_failures = sum(s.failures for s in all_suites)
        total_errors = sum(s.errors for s in all_suites)
        total_skipped = sum(s.skipped for s in all_suites)
        total_time = sum(s.time for s in all_suites)

        # Build consolidated XML
        root = ET.Element("testsuites")
        root.set("name", "consolidated")
        root.set("tests", str(total_tests))
        root.set("failures", str(total_failures))
        root.set("errors", str(total_errors))
        root.set("skipped", str(total_skipped))
        root.set("time", f"{total_time:.3f}")

        for suite in all_suites:
            ts = ET.SubElement(root, "testsuite")
            ts.set("name", suite.name)
            ts.set("tests", str(suite.tests))
            ts.set("failures", str(suite.failures))
            ts.set("errors", str(suite.errors))
            ts.set("skipped", str(suite.skipped))
            ts.set("time", f"{suite.time:.3f}")

            for tc in suite.testcases:
                tc_elem = ET.SubElement(ts, "testcase")
                tc_elem.set("name", tc["name"])
                tc_elem.set("classname", tc["classname"])
                tc_elem.set("time", f"{tc['time']:.3f}")

                if tc["failure"]:
                    failure = ET.SubElement(tc_elem, "failure")
                    failure.set("message", tc["failure_message"])
                if tc["error"]:
                    error = ET.SubElement(tc_elem, "error")
                    error.set("message", tc["failure_message"])
                if tc["skipped"]:
                    ET.SubElement(tc_elem, "skipped")

        xml_content = ET.tostring(root, encoding="unicode", xml_declaration=True)

        return ConsolidatedReport(
            xml_content=xml_content,
            total_tests=total_tests,
            total_failures=total_failures,
            total_errors=total_errors,
            total_skipped=total_skipped,
            total_time=total_time,
            suites=all_suites,
        )


def consolidate_junit_reports(
    source_dir: Path, output_file: Optional[Path] = None, pattern: str = "**/junit.xml"
) -> ConsolidatedReport:
    """
    Convenience function to consolidate JUnit XML reports.

    Args:
        source_dir: Directory containing JUnit XML files.
        output_file: Optional path to write consolidated XML.
        pattern: Glob pattern for finding XML files.

    Returns:
        ConsolidatedReport with aggregated results.
    """
    consolidator = JUnitConsolidator(source_dir=source_dir, pattern=pattern)
    report = consolidator.consolidate()

    if output_file is not None:
        output_file = Path(output_file)
        output_file.parent.mkdir(parents=True, exist_ok=True)
        output_file.write_text(report.xml_content, encoding="utf-8")

    return report


def main():
    """CLI entry point for report consolidation."""
    import argparse
    import sys

    parser = argparse.ArgumentParser(description="Consolidate JUnit XML reports")
    parser.add_argument("source_dir", help="Directory containing JUnit XML files")
    parser.add_argument(
        "--output",
        "-o",
        default="consolidated-junit.xml",
        help="Output file path (default: consolidated-junit.xml)",
    )
    parser.add_argument(
        "--pattern",
        "-p",
        default="**/junit.xml",
        help="Glob pattern for finding XML files (default: **/junit.xml)",
    )
    parser.add_argument(
        "--summary", "-s", action="store_true", help="Also generate Markdown summary"
    )

    args = parser.parse_args()

    source_dir = Path(args.source_dir)
    output_file = Path(args.output)

    if not source_dir.exists():
        print(f"Error: Source directory not found: {source_dir}", file=sys.stderr)
        sys.exit(1)

    report = consolidate_junit_reports(
        source_dir=source_dir, output_file=output_file, pattern=args.pattern
    )

    print(f"Consolidated {len(report.suites)} test suites")
    print(f"Total tests: {report.total_tests}")
    print(
        f"Passed: {report.total_tests - report.total_failures - report.total_errors - report.total_skipped}"
    )
    print(f"Failed: {report.total_failures}")
    print(f"Errors: {report.total_errors}")
    print(f"Pass rate: {report.pass_rate:.2f}%")
    print(f"Output written to: {output_file}")

    if args.summary:
        summary_file = output_file.with_suffix(".md")
        summary_file.write_text(report.to_markdown(), encoding="utf-8")
        print(f"Summary written to: {summary_file}")

    if report.total_failures > 0 or report.total_errors > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
