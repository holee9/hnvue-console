# generate_summary.py
"""
SPEC-TEST-001 Phase 3.4: Build Summary Generation

Generates comprehensive build summary with test results, coverage reports,
and RTM status for CI pipeline integration.

Covers:
- FR-TEST-08.4: Coverage report in Cobertura XML format
- FR-TEST-08.5: Test report retention for regulatory audit
- NFR-TEST-01: 85% coverage minimum per component
- NFR-TEST-04: Class C merge gate enforcement
"""

import json
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Optional


@dataclass
class ComponentCoverage:
    """Coverage data for a single component."""

    name: str
    line_rate: float
    branch_rate: float
    safety_class: str = "B"
    threshold: float = 85.0

    def meets_threshold(self) -> bool:
        """Check if coverage meets the threshold."""
        return self.line_rate * 100 >= self.threshold


@dataclass
class CoverageSummary:
    """Summary of coverage from Cobertura report."""

    overall_line_rate: float
    overall_branch_rate: float
    components: list[ComponentCoverage] = field(default_factory=list)

    def get_class_c_components(self) -> list[ComponentCoverage]:
        """Get all Class C (safety-critical) components."""
        return [c for c in self.components if c.safety_class == "C"]

    def all_class_c_passing(self) -> bool:
        """Check if all Class C components meet 100% threshold."""
        class_c = self.get_class_c_components()
        return all(c.meets_threshold() for c in class_c) if class_c else True


@dataclass
class TestResultsSummary:
    """Summary of test execution results."""

    total_tests: int
    passed: int
    failed: int
    skipped: int
    total_time: float
    suites: list[dict] = field(default_factory=list)

    @property
    def pass_rate(self) -> float:
        """Calculate pass rate percentage."""
        if self.total_tests == 0:
            return 100.0
        return (self.passed / self.total_tests) * 100


@dataclass
class RtmSummary:
    """Summary of RTM validation status."""

    is_valid: bool
    total_requirements: int
    linked_requirements: int
    unlinked_requirements: list[str] = field(default_factory=list)
    coverage_by_requirement: dict = field(default_factory=dict)


@dataclass
class BuildSummary:
    """Complete build summary combining all artifacts."""

    test_results: Optional[TestResultsSummary] = None
    coverage: Optional[CoverageSummary] = None
    rtm: Optional[RtmSummary] = None
    commit_sha: str = "unknown"
    branch: str = "unknown"
    timestamp: str = ""

    def __post_init__(self):
        if not self.timestamp:
            self.timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    @property
    def coverage_passed(self) -> bool:
        """Check if all coverage thresholds are met."""
        if self.coverage is None:
            return False
        return all(c.meets_threshold() for c in self.coverage.components)

    @property
    def class_c_passed(self) -> bool:
        """Check if all Class C components pass."""
        if self.coverage is None:
            return False
        return self.coverage.all_class_c_passing()

    @property
    def tests_passed(self) -> bool:
        """Check if all tests passed."""
        if self.test_results is None:
            return False
        return self.test_results.failed == 0

    @property
    def rtm_passed(self) -> bool:
        """Check if RTM validation passed."""
        if self.rtm is None:
            return False
        return self.rtm.is_valid

    @property
    def overall_passed(self) -> bool:
        """Check if all gates passed."""
        return (
            self.tests_passed
            and self.coverage_passed
            and self.class_c_passed
            and self.rtm_passed
        )

    def to_markdown(self) -> str:
        """Generate Markdown summary report."""
        lines = [
            "# HnVue Build Summary",
            "",
            f"**Generated**: {self.timestamp}",
            f"**Commit**: {self.commit_sha}",
            f"**Branch**: {self.branch}",
            "",
            f"## Overall Status: {'PASS' if self.overall_passed else 'FAIL'}",
            "",
        ]

        # Test Results Section
        if self.test_results:
            lines.extend(
                [
                    "## Test Results",
                    "",
                    "| Metric | Value |",
                    "|--------|-------|",
                    f"| Total Tests | {self.test_results.total_tests} |",
                    f"| Passed | {self.test_results.passed} |",
                    f"| Failed | {self.test_results.failed} |",
                    f"| Skipped | {self.test_results.skipped} |",
                    f"| Pass Rate | {self.test_results.pass_rate:.2f}% |",
                    f"| Total Time | {self.test_results.total_time:.3f}s |",
                    "",
                ]
            )

            if self.test_results.suites:
                lines.extend(
                    [
                        "### Suites",
                        "",
                        "| Suite | Tests | Failures | Time |",
                        "|-------|-------|----------|------|",
                    ]
                )
                for suite in self.test_results.suites:
                    lines.append(
                        f"| {suite.get('name', 'unknown')} | {suite.get('tests', 0)} | "
                        f"{suite.get('failures', 0)} | {suite.get('time', 0):.3f}s |"
                    )
                lines.append("")

        # Coverage Section
        if self.coverage:
            lines.extend(
                [
                    "## Coverage Report",
                    "",
                    f"**Overall Line Coverage**: {self.coverage.overall_line_rate * 100:.2f}%",
                    f"**Overall Branch Coverage**: {self.coverage.overall_branch_rate * 100:.2f}%",
                    "",
                    "### Component Coverage",
                    "",
                    "| Component | Safety Class | Line Rate | Threshold | Status |",
                    "|-----------|--------------|-----------|-----------|--------|",
                ]
            )

            for comp in self.coverage.components:
                status = "PASS" if comp.meets_threshold() else "FAIL"
                lines.append(
                    f"| {comp.name} | {comp.safety_class} | "
                    f"{comp.line_rate * 100:.2f}% | {comp.threshold}% | {status} |"
                )

            lines.extend(
                [
                    "",
                    f"**Class C Gate**: {'PASS' if self.class_c_passed else 'FAIL'}",
                    "",
                ]
            )

        # RTM Section
        if self.rtm:
            lines.extend(
                [
                    "## RTM Status",
                    "",
                    f"**Validation**: {'PASS' if self.rtm.is_valid else 'FAIL'}",
                    f"**Coverage**: {self.rtm.linked_requirements}/{self.rtm.total_requirements} requirements linked",
                    "",
                ]
            )

            if self.rtm.unlinked_requirements:
                lines.extend(
                    [
                        "### Unlinked Requirements",
                        "",
                    ]
                )
                for req in self.rtm.unlinked_requirements:
                    lines.append(f"- {req}")
                lines.append("")

        # Regulatory Compliance Note
        lines.extend(
            [
                "---",
                "",
                "*Report generated per SPEC-TEST-001 FR-TEST-08.5 (90-day retention for regulatory audit)*",
            ]
        )

        return "\n".join(lines)


class BuildSummaryGenerator:
    """
    Generates build summary from CI artifacts.

    Usage:
        generator = BuildSummaryGenerator(
            artifacts_dir=Path("TestResults"),
            commit_sha="abc123",
            branch="main"
        )
        summary = generator.generate()
        summary.to_markdown()  # Markdown report
    """

    # Component thresholds per SPEC-TEST-001 Section 1.3
    COMPONENT_THRESHOLDS = {
        "generator": {"safety_class": "C", "threshold": 100.0},
        "aec": {"safety_class": "C", "threshold": 100.0},
        "imaging": {"safety_class": "B", "threshold": 85.0},
        "dicom": {"safety_class": "B", "threshold": 85.0},
        "dose": {"safety_class": "B", "threshold": 90.0},
        "ui": {"safety_class": "B", "threshold": 80.0},
        "patient": {"safety_class": "A", "threshold": 85.0},
    }

    def __init__(
        self, artifacts_dir: Path, commit_sha: str = "unknown", branch: str = "unknown"
    ):
        """
        Initialize the generator.

        Args:
            artifacts_dir: Directory containing CI artifacts.
            commit_sha: Git commit SHA.
            branch: Git branch name.
        """
        self.artifacts_dir = Path(artifacts_dir)
        self.commit_sha = commit_sha
        self.branch = branch

    def parse_coverage(self) -> Optional[CoverageSummary]:
        """Parse Cobertura XML coverage report."""
        coverage_file = self.artifacts_dir / "coverage.xml"

        if not coverage_file.exists():
            return None

        try:
            tree = ET.parse(coverage_file)
            root = tree.getroot()

            overall_line_rate = float(root.get("line-rate", "0"))
            overall_branch_rate = float(root.get("branch-rate", "0"))

            components = []

            # Parse packages
            for pkg in root.iter("package"):
                name = pkg.get("name", "")
                line_rate = float(pkg.get("line-rate", "0"))
                branch_rate = float(pkg.get("branch-rate", "0"))

                # Get component config
                config = self.COMPONENT_THRESHOLDS.get(
                    name, {"safety_class": "B", "threshold": 85.0}
                )

                components.append(
                    ComponentCoverage(
                        name=name,
                        line_rate=line_rate,
                        branch_rate=branch_rate,
                        safety_class=config["safety_class"],
                        threshold=config["threshold"],
                    )
                )

            return CoverageSummary(
                overall_line_rate=overall_line_rate,
                overall_branch_rate=overall_branch_rate,
                components=components,
            )

        except Exception:
            return None

    def parse_rtm(self) -> Optional[RtmSummary]:
        """Parse RTM status JSON file."""
        rtm_file = self.artifacts_dir / "rtm_status.json"

        if not rtm_file.exists():
            return None

        try:
            data = json.loads(rtm_file.read_text(encoding="utf-8"))

            return RtmSummary(
                is_valid=data.get("is_valid", False),
                total_requirements=data.get("total_requirements", 0),
                linked_requirements=data.get("linked_requirements", 0),
                unlinked_requirements=data.get("unlinked_requirements", []),
                coverage_by_requirement=data.get("coverage_by_requirement", {}),
            )

        except Exception:
            return None

    def parse_test_results(self) -> Optional[TestResultsSummary]:
        """Parse test results JSON file."""
        results_file = self.artifacts_dir / "test_results.json"

        if not results_file.exists():
            return None

        try:
            data = json.loads(results_file.read_text(encoding="utf-8"))

            return TestResultsSummary(
                total_tests=data.get("total_tests", 0),
                passed=data.get("passed", 0),
                failed=data.get("failed", 0),
                skipped=data.get("skipped", 0),
                total_time=data.get("total_time", 0.0),
                suites=data.get("suites", []),
            )

        except Exception:
            return None

    def generate(self) -> BuildSummary:
        """
        Generate complete build summary.

        Returns:
            BuildSummary with all parsed artifacts.
        """
        return BuildSummary(
            test_results=self.parse_test_results(),
            coverage=self.parse_coverage(),
            rtm=self.parse_rtm(),
            commit_sha=self.commit_sha,
            branch=self.branch,
        )


def generate_build_summary(
    artifacts_dir: Path,
    output_file: Optional[Path] = None,
    commit_sha: str = "unknown",
    branch: str = "unknown",
) -> BuildSummary:
    """
    Convenience function to generate build summary.

    Args:
        artifacts_dir: Directory containing CI artifacts.
        output_file: Optional path to write Markdown report.
        commit_sha: Git commit SHA.
        branch: Git branch name.

    Returns:
        BuildSummary with all parsed artifacts.
    """
    generator = BuildSummaryGenerator(
        artifacts_dir=artifacts_dir, commit_sha=commit_sha, branch=branch
    )
    summary = generator.generate()

    if output_file is not None:
        output_file = Path(output_file)
        output_file.parent.mkdir(parents=True, exist_ok=True)
        output_file.write_text(summary.to_markdown(), encoding="utf-8")

    return summary


def main():
    """CLI entry point for build summary generation."""
    import argparse
    import sys

    parser = argparse.ArgumentParser(
        description="Generate build summary from CI artifacts"
    )
    parser.add_argument("artifacts_dir", help="Directory containing CI artifacts")
    parser.add_argument(
        "--output",
        "-o",
        default="build-summary.md",
        help="Output Markdown file path (default: build-summary.md)",
    )
    parser.add_argument("--commit", "-c", default="unknown", help="Git commit SHA")
    parser.add_argument("--branch", "-b", default="unknown", help="Git branch name")

    args = parser.parse_args()

    artifacts_dir = Path(args.artifacts_dir)
    output_file = Path(args.output)

    if not artifacts_dir.exists():
        print(f"Error: Artifacts directory not found: {artifacts_dir}", file=sys.stderr)
        sys.exit(1)

    summary = generate_build_summary(
        artifacts_dir=artifacts_dir,
        output_file=output_file,
        commit_sha=args.commit,
        branch=args.branch,
    )

    print(f"Build summary generated: {output_file}")
    print(f"Overall status: {'PASS' if summary.overall_passed else 'FAIL'}")

    if not summary.overall_passed:
        if not summary.tests_passed:
            print("  - Tests: FAIL")
        if not summary.coverage_passed:
            print("  - Coverage: FAIL")
        if not summary.class_c_passed:
            print("  - Class C Gate: FAIL")
        if not summary.rtm_passed:
            print("  - RTM: FAIL")
        sys.exit(1)


if __name__ == "__main__":
    main()
