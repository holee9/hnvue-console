# rtm_gate.py
"""
SPEC-TEST-001 Phase 3.3: RTM Gate Validation

Validates the Requirements Traceability Matrix in CI pipeline to ensure
all requirements have test coverage and all tests trace to requirements.

Covers:
- FR-TEST-09.1: RTM links each requirement to one or more test cases
- FR-TEST-09.2: Bidirectional traceability
- FR-TEST-09.3: Test cases must reference at least one SPEC requirement ID
- FR-TEST-09.4: New requirements flagged as incomplete until linked
- NFR-TEST-08: Every test case traceable to at least one requirement
"""

import csv
import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional


# SPEC-TEST-001 expected parent requirements
DEFAULT_EXPECTED_REQUIREMENTS = [
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
]


@dataclass
class RtmGateResult:
    """Result of RTM gate validation."""

    is_valid: bool
    total_requirements: int
    linked_requirements: int
    unlinked_requirements: list[str] = field(default_factory=list)
    untraceable_tests: list[str] = field(default_factory=list)
    invalid_requirement_refs: list[str] = field(default_factory=list)
    bidirectional_traceability: bool = True
    exit_code: int = 0
    error_message: str = ""
    warnings: list[str] = field(default_factory=list)
    safety_class_breakdown: dict[str, int] = field(default_factory=dict)
    coverage_percentage: float = 0.0

    def to_json(self) -> str:
        """Serialize result to JSON string."""
        return json.dumps(
            {
                "is_valid": self.is_valid,
                "total_requirements": self.total_requirements,
                "linked_requirements": self.linked_requirements,
                "unlinked_requirements": self.unlinked_requirements,
                "untraceable_tests": self.untraceable_tests,
                "invalid_requirement_refs": self.invalid_requirement_refs,
                "bidirectional_traceability": self.bidirectional_traceability,
                "exit_code": self.exit_code,
                "error_message": self.error_message,
                "warnings": self.warnings,
                "safety_class_breakdown": self.safety_class_breakdown,
                "coverage_percentage": self.coverage_percentage,
            },
            indent=2,
        )

    def to_markdown(self) -> str:
        """Generate Markdown report of the validation result."""
        status = "PASS" if self.is_valid else "FAIL"
        lines = [
            "# RTM Gate Validation Report",
            "",
            f"**Status**: {status}",
            f"**Coverage**: {self.coverage_percentage:.1f}% ({self.linked_requirements}/{self.total_requirements})",
            "",
            "## Summary",
            "",
            f"- Total Requirements: {self.total_requirements}",
            f"- Linked Requirements: {self.linked_requirements}",
            f"- Bidirectional Traceability: {'Yes' if self.bidirectional_traceability else 'No'}",
            "",
        ]

        if self.unlinked_requirements:
            lines.extend(
                [
                    "## Unlinked Requirements",
                    "",
                ]
            )
            for req in self.unlinked_requirements:
                lines.append(f"- {req}")
            lines.append("")

        if self.untraceable_tests:
            lines.extend(
                [
                    "## Untraceable Tests",
                    "",
                ]
            )
            for test in self.untraceable_tests:
                lines.append(f"- {test}")
            lines.append("")

        if self.invalid_requirement_refs:
            lines.extend(
                [
                    "## Invalid Requirement References",
                    "",
                ]
            )
            for ref in self.invalid_requirement_refs:
                lines.append(f"- {ref}")
            lines.append("")

        if self.safety_class_breakdown:
            lines.extend(
                [
                    "## Safety Class Breakdown",
                    "",
                    "| Class | Count |",
                    "|-------|-------|",
                ]
            )
            for cls, count in sorted(self.safety_class_breakdown.items()):
                lines.append(f"| {cls} | {count} |")

        return "\n".join(lines)


class RtmGateValidator:
    """
    Validates RTM completeness for CI gate enforcement.

    Usage:
        validator = RtmGateValidator(rtm_path=Path("tests/traceability/rtm.csv"))
        result = validator.validate()
        if not result.is_valid:
            print(result.to_markdown())
            sys.exit(result.exit_code)
    """

    VALID_REQUIREMENT_PREFIX = "FR-TEST-"
    VALID_SAFETY_CLASSES = {"A", "B", "C", "NA"}

    def __init__(
        self, rtm_path: Path, expected_requirements: Optional[list[str]] = None
    ):
        """
        Initialize the validator.

        Args:
            rtm_path: Path to the RTM CSV file.
            expected_requirements: List of expected requirement IDs to check.
        """
        self.rtm_path = Path(rtm_path)
        self.expected_requirements = (
            expected_requirements or DEFAULT_EXPECTED_REQUIREMENTS
        )
        self._entries: list[dict] = []

    def _load_rtm(self) -> list[dict]:
        """Load and parse the RTM CSV file."""
        entries = []

        if not self.rtm_path.exists():
            return entries

        try:
            with open(self.rtm_path, newline="", encoding="utf-8") as csvfile:
                reader = csv.DictReader(csvfile)
                for row in reader:
                    entries.append(
                        {
                            "requirement_id": row.get("requirement_id", "").strip(),
                            "test_case_id": row.get("test_case_id", "").strip(),
                            "test_suite": row.get("test_suite", "").strip(),
                            "safety_class": row.get("safety_class", "NA")
                            .strip()
                            .upper(),
                            "status": row.get("status", "pending").strip().lower(),
                        }
                    )
        except Exception:
            pass

        return entries

    def _extract_parent_requirement(self, req_id: str) -> str:
        """Extract parent requirement ID from sub-requirement."""
        if "." in req_id:
            return req_id.rsplit(".", 1)[0]
        return req_id

    def validate(self) -> RtmGateResult:
        """
        Perform RTM gate validation.

        Returns:
            RtmGateResult with validation status and details.
        """
        # Check if file exists
        if not self.rtm_path.exists():
            return RtmGateResult(
                is_valid=False,
                total_requirements=len(self.expected_requirements),
                linked_requirements=0,
                error_message=f"RTM file not found: {self.rtm_path}",
                exit_code=2,
            )

        self._entries = self._load_rtm()

        # Check for empty RTM
        if not self._entries:
            return RtmGateResult(
                is_valid=False,
                total_requirements=len(self.expected_requirements),
                linked_requirements=0,
                error_message="RTM file is empty or has no valid entries",
                exit_code=1,
            )

        # Collect linked parent requirements
        linked_parents: set[str] = set()
        untraceable_tests: list[str] = []
        invalid_refs: list[str] = []
        safety_class_counts: dict[str, int] = {}
        seen_test_ids: set[str] = set()

        for entry in self._entries:
            req_id = entry["requirement_id"]
            test_id = entry["test_case_id"]
            safety_class = entry["safety_class"]

            # Track safety class distribution
            safety_class_counts[safety_class] = (
                safety_class_counts.get(safety_class, 0) + 1
            )

            # Check for duplicate test IDs
            if test_id and test_id in seen_test_ids:
                continue
            if test_id:
                seen_test_ids.add(test_id)

            # Validate requirement format
            if not req_id.startswith(self.VALID_REQUIREMENT_PREFIX):
                invalid_refs.append(f"{test_id}: {req_id}")
                if test_id:
                    untraceable_tests.append(test_id)
                continue

            # Check empty requirement (orphan test)
            if not req_id:
                if test_id:
                    untraceable_tests.append(test_id)
                continue

            # Extract and track parent requirement
            parent = self._extract_parent_requirement(req_id)
            linked_parents.add(parent)

        # Find unlinked requirements
        unlinked = [
            req for req in self.expected_requirements if req not in linked_parents
        ]

        # Calculate coverage percentage
        linked_count = len(self.expected_requirements) - len(unlinked)
        coverage_pct = (
            (linked_count / len(self.expected_requirements)) * 100
            if self.expected_requirements
            else 0
        )

        # Determine validity
        is_valid = len(unlinked) == 0 and len(untraceable_tests) == 0

        # Determine exit code
        exit_code = 0 if is_valid else 1

        # Check bidirectional traceability
        bidirectional = len(untraceable_tests) == 0 and len(invalid_refs) == 0

        return RtmGateResult(
            is_valid=is_valid,
            total_requirements=len(self.expected_requirements),
            linked_requirements=linked_count,
            unlinked_requirements=unlinked,
            untraceable_tests=untraceable_tests,
            invalid_requirement_refs=invalid_refs,
            bidirectional_traceability=bidirectional,
            exit_code=exit_code,
            safety_class_breakdown=safety_class_counts,
            coverage_percentage=coverage_pct,
        )


def validate_rtm_gate(
    rtm_path: Path,
    expected_requirements: Optional[list[str]] = None,
    output_file: Optional[Path] = None,
) -> RtmGateResult:
    """
    Convenience function to validate RTM gate.

    Args:
        rtm_path: Path to the RTM CSV file.
        expected_requirements: List of expected requirement IDs.
        output_file: Optional path to write JSON result.

    Returns:
        RtmGateResult with validation status.
    """
    validator = RtmGateValidator(
        rtm_path=rtm_path, expected_requirements=expected_requirements
    )
    result = validator.validate()

    if output_file is not None:
        output_file = Path(output_file)
        output_file.parent.mkdir(parents=True, exist_ok=True)
        output_file.write_text(result.to_json(), encoding="utf-8")

    return result


def main():
    """CLI entry point for RTM gate validation."""
    import argparse
    import sys

    parser = argparse.ArgumentParser(description="Validate RTM gate for CI pipeline")
    parser.add_argument("rtm_path", help="Path to RTM CSV file")
    parser.add_argument(
        "--expected",
        "-e",
        nargs="+",
        default=DEFAULT_EXPECTED_REQUIREMENTS,
        help="Expected requirement IDs",
    )
    parser.add_argument("--output", "-o", default=None, help="Output JSON file path")
    parser.add_argument(
        "--markdown", "-m", default=None, help="Output Markdown report path"
    )
    parser.add_argument(
        "--fail-on-error",
        action="store_true",
        default=True,
        help="Exit with error code if validation fails (default: True)",
    )

    args = parser.parse_args()

    rtm_path = Path(args.rtm_path)
    result = validate_rtm_gate(
        rtm_path=rtm_path,
        expected_requirements=args.expected,
        output_file=Path(args.output) if args.output else None,
    )

    # Print summary
    print("\n" + "=" * 60)
    print("RTM Gate Validation Report")
    print("=" * 60)
    print(f"\nStatus: {'PASS' if result.is_valid else 'FAIL'}")
    print(
        f"Coverage: {result.coverage_percentage:.1f}% ({result.linked_requirements}/{result.total_requirements})"
    )
    print(
        f"Bidirectional Traceability: {'Yes' if result.bidirectional_traceability else 'No'}"
    )

    if result.unlinked_requirements:
        print(f"\nUnlinked Requirements ({len(result.unlinked_requirements)}):")
        for req in result.unlinked_requirements:
            print(f"  - {req}")

    if result.untraceable_tests:
        print(f"\nUntraceable Tests ({len(result.untraceable_tests)}):")
        for test in result.untraceable_tests:
            print(f"  - {test}")

    if result.error_message:
        print(f"\nError: {result.error_message}")

    print("=" * 60)

    # Write markdown if requested
    if args.markdown:
        md_path = Path(args.markdown)
        md_path.write_text(result.to_markdown(), encoding="utf-8")
        print(f"\nMarkdown report written to: {md_path}")

    if args.fail_on_error and not result.is_valid:
        sys.exit(result.exit_code)


if __name__ == "__main__":
    main()
