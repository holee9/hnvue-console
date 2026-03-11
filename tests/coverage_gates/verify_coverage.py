# -*- coding: utf-8 -*-
"""
SPEC-TEST-001 Phase 3.2: Coverage Verification Module

Verifies coverage against component-level thresholds defined in coverage_gates.json.

Covers:
- NFR-TEST-01: 85% coverage minimum
- NFR-TEST-01.1: 100% decision coverage for Class C components
- NFR-TEST-04: Class C merge gate enforcement
"""

import json
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from fnmatch import fnmatch
from pathlib import Path
from typing import Optional


@dataclass
class ComponentThreshold:
    """Coverage threshold for a component."""

    name: str
    safety_class: str
    coverage_type: str
    threshold: float
    path_pattern: str
    description: str = ""
    iec_62304_reference: str = ""

    def is_class_c(self) -> bool:
        """Check if this is a Class C (safety-critical) component."""
        return self.safety_class == "C"


@dataclass
class CoverageResult:
    """Coverage result for a component."""

    component: ComponentThreshold
    actual_coverage: float
    passed: bool
    files_matched: list[str] = field(default_factory=list)

    def __str__(self) -> str:
        status = "PASS" if self.passed else "FAIL"
        return (
            f"[{status}] {self.component.name} "
            f"({self.component.safety_class}): "
            f"{self.actual_coverage:.2f}% / {self.component.threshold}% "
            f"({self.component.coverage_type})"
        )


@dataclass
class VerificationReport:
    """Complete coverage verification report."""

    total_components: int
    passed_components: int
    failed_components: int
    class_c_passed: bool
    results: list[CoverageResult]
    overall_coverage: float

    def is_passing(self) -> bool:
        """Check if all coverage gates pass."""
        return self.failed_components == 0 and self.class_c_passed

    def get_failed_results(self) -> list[CoverageResult]:
        """Get all failing coverage results."""
        return [r for r in self.results if not r.passed]

    def get_class_c_results(self) -> list[CoverageResult]:
        """Get all Class C component results."""
        return [r for r in self.results if r.component.is_class_c()]


class CoverageGatesConfig:
    """Configuration loader for coverage gates."""

    def __init__(self, config_path: Path):
        self.config_path = config_path
        self._components: list[ComponentThreshold] = []
        self._version: str = ""
        self._load()

    def _load(self) -> None:
        """Load configuration from JSON file."""
        if not self.config_path.exists():
            raise FileNotFoundError(
                f"Coverage gates config not found: {self.config_path}"
            )

        with open(self.config_path, encoding="utf-8") as f:
            data = json.load(f)

        self._version = data.get("version", "unknown")

        for comp_data in data.get("components", []):
            self._components.append(
                ComponentThreshold(
                    name=comp_data["name"],
                    safety_class=comp_data["safety_class"],
                    coverage_type=comp_data["coverage_type"],
                    threshold=float(comp_data["threshold"]),
                    path_pattern=comp_data["path_pattern"],
                    description=comp_data.get("description", ""),
                    iec_62304_reference=comp_data.get("iec_62304_reference", ""),
                )
            )

    @property
    def components(self) -> list[ComponentThreshold]:
        """Get all component thresholds."""
        return self._components

    @property
    def class_c_components(self) -> list[ComponentThreshold]:
        """Get Class C (safety-critical) components."""
        return [c for c in self._components if c.is_class_c()]

    @property
    def version(self) -> str:
        """Get configuration version."""
        return self._version


class CoberturaParser:
    """Parser for Cobertura XML coverage reports."""

    def __init__(self, coverage_path: Path):
        self.coverage_path = coverage_path

    def parse(self) -> dict[str, float]:
        """
        Parse Cobertura XML and extract coverage by file.

        Returns:
            Dictionary mapping file paths to line coverage percentages.
        """
        if not self.coverage_path.exists():
            raise FileNotFoundError(f"Coverage report not found: {self.coverage_path}")

        tree = ET.parse(self.coverage_path)
        root = tree.getroot()

        coverage_by_file: dict[str, float] = {}

        # Find all class elements with filename and line-rate
        for cls in root.iter("class"):
            filename = cls.get("filename", "")
            line_rate = float(cls.get("line-rate", "0"))

            if filename:
                coverage_by_file[filename] = line_rate * 100

        # Also check package-level coverage
        for pkg in root.iter("package"):
            name = pkg.get("name", "")
            line_rate = float(pkg.get("line-rate", "0"))

            if name:
                coverage_by_file[name] = line_rate * 100

        return coverage_by_file

    def get_overall_coverage(self) -> float:
        """Get overall line coverage from the report."""
        tree = ET.parse(self.coverage_path)
        root = tree.getroot()

        coverage = root.find("coverage")
        if coverage is not None:
            return float(coverage.get("line-rate", "0")) * 100

        # Fallback: calculate from root
        line_rate = root.get("line-rate", "0")
        return float(line_rate) * 100


class CoverageVerifier:
    """Verifies coverage against component-level thresholds."""

    def __init__(self, config: CoverageGatesConfig):
        self.config = config

    def _matches_pattern(self, file_path: str, pattern: str) -> bool:
        """Check if file path matches the pattern."""
        # Normalize separators
        normalized_path = file_path.replace("\\", "/")
        normalized_pattern = pattern.replace("\\", "/")

        # Handle glob-style patterns with **
        if "**" in normalized_pattern:
            # Split pattern into segments for more flexible matching
            # Pattern: src/**/generator/** means:
            # - Must start with src/
            # - Must contain generator/ somewhere in the path
            # - Can have any number of directories between segments

            # Remove leading **/ or trailing /**
            pattern_parts = [
                p for p in normalized_pattern.split("/") if p and p != "**"
            ]

            # Check if all pattern parts appear in the path
            path_lower = normalized_path.lower()
            for part in pattern_parts:
                if part.lower() not in path_lower:
                    return False
            return True

        return fnmatch(normalized_path, normalized_pattern)

    def verify(
        self, coverage_data: dict[str, float], overall_coverage: float = 0.0
    ) -> VerificationReport:
        """
        Verify coverage against all component thresholds.

        Args:
            coverage_data: Dictionary mapping file paths to coverage percentages.
            overall_coverage: Overall coverage percentage for the project.

        Returns:
            VerificationReport with detailed results.
        """
        results: list[CoverageResult] = []

        for component in self.config.components:
            # Find matching files for this component
            matched_files = [
                f
                for f in coverage_data.keys()
                if self._matches_pattern(f, component.path_pattern)
            ]

            # Calculate actual coverage for matched files
            if matched_files:
                total_coverage = sum(coverage_data[f] for f in matched_files)
                actual_coverage = total_coverage / len(matched_files)
            else:
                actual_coverage = 0.0

            # Check if threshold is met
            passed = actual_coverage >= component.threshold

            results.append(
                CoverageResult(
                    component=component,
                    actual_coverage=actual_coverage,
                    passed=passed,
                    files_matched=matched_files,
                )
            )

        # Check if all Class C components pass
        class_c_results = [r for r in results if r.component.is_class_c()]
        class_c_passed = all(r.passed for r in class_c_results)

        return VerificationReport(
            total_components=len(results),
            passed_components=sum(1 for r in results if r.passed),
            failed_components=sum(1 for r in results if not r.passed),
            class_c_passed=class_c_passed,
            results=results,
            overall_coverage=overall_coverage,
        )

    def verify_from_cobertura(self, cobertura_path: Path) -> VerificationReport:
        """
        Verify coverage from a Cobertura XML file.

        Args:
            cobertura_path: Path to Cobertura XML coverage report.

        Returns:
            VerificationReport with detailed results.
        """
        parser = CoberturaParser(cobertura_path)
        coverage_data = parser.parse()
        overall_coverage = parser.get_overall_coverage()

        return self.verify(coverage_data, overall_coverage)


def load_coverage_gates(config_path: Optional[Path] = None) -> CoverageGatesConfig:
    """
    Load coverage gates configuration.

    Args:
        config_path: Path to coverage_gates.json. Defaults to
                    tests/coverage/coverage_gates.json.

    Returns:
        CoverageGatesConfig instance.
    """
    if config_path is None:
        config_path = Path(__file__).parent / "coverage_gates.json"

    return CoverageGatesConfig(config_path)


def verify_coverage(
    cobertura_path: Path, config_path: Optional[Path] = None
) -> VerificationReport:
    """
    Convenience function to verify coverage.

    Args:
        cobertura_path: Path to Cobertura XML coverage report.
        config_path: Path to coverage_gates.json.

    Returns:
        VerificationReport with detailed results.
    """
    config = load_coverage_gates(config_path)
    verifier = CoverageVerifier(config)
    return verifier.verify_from_cobertura(cobertura_path)


def main():
    """CLI entry point for coverage verification."""
    import argparse
    import sys

    parser = argparse.ArgumentParser(
        description="Verify coverage against component-level thresholds"
    )
    parser.add_argument(
        "--cobertura", "-c", required=True, help="Path to Cobertura XML coverage report"
    )
    parser.add_argument(
        "--config",
        "-g",
        default=None,
        help="Path to coverage_gates.json (default: tests/coverage/coverage_gates.json)",
    )
    parser.add_argument(
        "--fail-on-error",
        action="store_true",
        help="Exit with error code if any threshold not met",
    )
    parser.add_argument(
        "--json-output",
        "-j",
        default=None,
        help="Output results as JSON to specified file",
    )

    args = parser.parse_args()

    cobertura_path = Path(args.cobertura)
    config_path = Path(args.config) if args.config else None

    try:
        report = verify_coverage(cobertura_path, config_path)

        # Print results
        print("\n" + "=" * 60)
        print("SPEC-TEST-001 Component-Level Coverage Verification Report")
        print("=" * 60)
        print(f"\nConfiguration version: {load_coverage_gates(config_path).version}")
        print(f"Overall coverage: {report.overall_coverage:.2f}%")
        print(
            f"\nComponent Results ({report.passed_components}/{report.total_components} passed):"
        )
        print("-" * 60)

        for result in report.results:
            print(f"  {result}")

        print("-" * 60)

        if report.class_c_passed:
            print(
                "Class C Gate: PASS (all Class C components meet 100% decision coverage)"
            )
        else:
            print("Class C Gate: FAIL (some Class C components below threshold)")
            for result in report.get_class_c_results():
                if not result.passed:
                    print(f"  - {result.component.name}: {result.actual_coverage:.2f}%")

        print("=" * 60)

        if report.is_passing():
            print("\n[SUCCESS] All coverage gates passed!")
        else:
            print(f"\n[FAILURE] {report.failed_components} coverage gate(s) failed")
            for result in report.get_failed_results():
                print(
                    f"  - {result.component.name}: {result.actual_coverage:.2f}% < {result.component.threshold}%"
                )

        # JSON output
        if args.json_output:
            output_data = {
                "passed": report.is_passing(),
                "overall_coverage": report.overall_coverage,
                "total_components": report.total_components,
                "passed_components": report.passed_components,
                "failed_components": report.failed_components,
                "class_c_passed": report.class_c_passed,
                "results": [
                    {
                        "component": r.component.name,
                        "safety_class": r.component.safety_class,
                        "coverage_type": r.component.coverage_type,
                        "threshold": r.component.threshold,
                        "actual_coverage": r.actual_coverage,
                        "passed": r.passed,
                    }
                    for r in report.results
                ],
            }
            with open(args.json_output, "w", encoding="utf-8") as f:
                json.dump(output_data, f, indent=2)

        if args.fail_on_error and not report.is_passing():
            sys.exit(1)

    except FileNotFoundError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(2)
    except Exception as e:
        print(f"Unexpected error: {e}", file=sys.stderr)
        sys.exit(3)


if __name__ == "__main__":
    main()
