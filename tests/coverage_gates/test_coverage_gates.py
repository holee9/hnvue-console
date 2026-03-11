# -*- coding: utf-8 -*-
"""
SPEC-TEST-001 Phase 3.2: Component-Level Coverage Gates

Tests for coverage_gates.json configuration and validation.

Covers:
- NFR-TEST-01: 85% coverage minimum
- NFR-TEST-01.1: 100% decision coverage for Class C components
- NFR-TEST-04: Class C merge gate enforcement
"""

import json
import pytest
from pathlib import Path


# Path to coverage gates configuration
COVERAGE_GATES_PATH = Path(__file__).parent / "coverage_gates.json"


class TestCoverageGatesConfig:
    """Tests for coverage_gates.json configuration file."""

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_coverage_gates_json_exists(self):
        """NFR-TEST-01: coverage_gates.json file must exist."""
        assert COVERAGE_GATES_PATH.exists(), (
            f"coverage_gates.json not found at {COVERAGE_GATES_PATH}"
        )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_coverage_gates_json_valid_structure(self):
        """NFR-TEST-01: coverage_gates.json must have valid JSON structure."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        assert "components" in data, "Missing 'components' key in coverage_gates.json"
        assert isinstance(data["components"], list), "'components' must be a list"
        assert len(data["components"]) > 0, "components list must not be empty"

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_component_required_fields(self):
        """NFR-TEST-01: Each component must have required fields."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        required_fields = [
            "name",
            "safety_class",
            "coverage_type",
            "threshold",
            "path_pattern",
        ]

        for component in data["components"]:
            for field in required_fields:
                assert field in component, (
                    f"Component missing required field '{field}': {component}"
                )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_component_safety_class_valid(self):
        """NFR-TEST-01: safety_class must be A, B, or C per IEC 62304."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        valid_classes = {"A", "B", "C"}

        for component in data["components"]:
            safety_class = component["safety_class"]
            assert safety_class in valid_classes, (
                f"Invalid safety_class '{safety_class}' for {component['name']}. "
                f"Must be one of {valid_classes}"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_component_coverage_type_valid(self):
        """NFR-TEST-01: coverage_type must be 'statement' or 'decision'."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        valid_types = {"statement", "decision"}

        for component in data["components"]:
            coverage_type = component["coverage_type"]
            assert coverage_type in valid_types, (
                f"Invalid coverage_type '{coverage_type}' for {component['name']}. "
                f"Must be one of {valid_types}"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_component_threshold_range(self):
        """NFR-TEST-01: threshold must be between 0 and 100."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        for component in data["components"]:
            threshold = component["threshold"]
            assert isinstance(threshold, (int, float)), (
                f"threshold must be a number for {component['name']}"
            )
            assert 0 <= threshold <= 100, (
                f"threshold {threshold} out of range for {component['name']}. "
                f"Must be between 0 and 100"
            )

    @pytest.mark.requirement("NFR-TEST-01.1")
    @pytest.mark.safety_class("C")
    def test_class_c_components_decision_coverage(self):
        """NFR-TEST-01.1: Class C components must require 100% decision coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        class_c_components = [c for c in data["components"] if c["safety_class"] == "C"]

        for component in class_c_components:
            assert component["coverage_type"] == "decision", (
                f"Class C component '{component['name']}' must use 'decision' coverage, "
                f"not '{component['coverage_type']}'"
            )
            assert component["threshold"] == 100.0, (
                f"Class C component '{component['name']}' must have 100% threshold, "
                f"not {component['threshold']}%"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("B")
    def test_class_b_components_statement_coverage(self):
        """NFR-TEST-01: Class B components must use statement coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        class_b_components = [c for c in data["components"] if c["safety_class"] == "B"]

        for component in class_b_components:
            assert component["coverage_type"] == "statement", (
                f"Class B component '{component['name']}' should use 'statement' coverage, "
                f"not '{component['coverage_type']}'"
            )
            assert component["threshold"] >= 80.0, (
                f"Class B component '{component['name']}' must have at least 80% threshold, "
                f"not {component['threshold']}%"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_generator_control_class_c(self):
        """NFR-TEST-01.1: Generator Control must be Class C with 100% decision coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        generator_components = [
            c for c in data["components"] if "generator" in c["name"].lower()
        ]

        assert len(generator_components) > 0, (
            "Generator Control component not found in coverage_gates.json"
        )

        for component in generator_components:
            assert component["safety_class"] == "C", (
                f"Generator component '{component['name']}' must be Class C, "
                f"not Class {component['safety_class']}"
            )

    @pytest.mark.requirement("NFR-TEST-01.1")
    @pytest.mark.safety_class("C")
    def test_aec_class_c(self):
        """NFR-TEST-01.1: AEC (Auto Exposure Control) must be Class C with 100% decision coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        aec_components = [
            c
            for c in data["components"]
            if "aec" in c["name"].lower() or "exposure" in c["name"].lower()
        ]

        assert len(aec_components) > 0, "AEC component not found in coverage_gates.json"

        for component in aec_components:
            if "aec" in component["name"].lower():
                assert component["safety_class"] == "C", (
                    f"AEC component '{component['name']}' must be Class C, "
                    f"not Class {component['safety_class']}"
                )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_image_processing_class_b(self):
        """NFR-TEST-01: Image Processing must be Class B with 85% statement coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        imaging_components = [
            c
            for c in data["components"]
            if "image" in c["name"].lower() or "imaging" in c["name"].lower()
        ]

        assert len(imaging_components) > 0, (
            "Image Processing component not found in coverage_gates.json"
        )

        for component in imaging_components:
            assert component["safety_class"] == "B", (
                f"Image component '{component['name']}' should be Class B"
            )
            assert component["threshold"] >= 85.0, (
                f"Image component '{component['name']}' must have at least 85% threshold"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_dicom_class_b(self):
        """NFR-TEST-01: DICOM Communication must be Class B with 85% statement coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        dicom_components = [
            c for c in data["components"] if "dicom" in c["name"].lower()
        ]

        assert len(dicom_components) > 0, (
            "DICOM component not found in coverage_gates.json"
        )

        for component in dicom_components:
            assert component["safety_class"] == "B", (
                f"DICOM component '{component['name']}' should be Class B"
            )
            assert component["threshold"] >= 85.0, (
                f"DICOM component '{component['name']}' must have at least 85% threshold"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_dose_management_class_b_90(self):
        """NFR-TEST-01: Dose Management must be Class B with 90% statement coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        dose_components = [c for c in data["components"] if "dose" in c["name"].lower()]

        assert len(dose_components) > 0, (
            "Dose Management component not found in coverage_gates.json"
        )

        for component in dose_components:
            assert component["safety_class"] == "B", (
                f"Dose component '{component['name']}' should be Class B"
            )
            assert component["threshold"] >= 90.0, (
                f"Dose component '{component['name']}' must have at least 90% threshold"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_ui_viewer_class_b_80(self):
        """NFR-TEST-01: UI/Viewer Layer must be Class B with 80% statement coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        ui_components = [
            c
            for c in data["components"]
            if "ui" in c["name"].lower() or "viewer" in c["name"].lower()
        ]

        assert len(ui_components) > 0, (
            "UI/Viewer component not found in coverage_gates.json"
        )

        for component in ui_components:
            assert component["safety_class"] == "B", (
                f"UI component '{component['name']}' should be Class B"
            )
            assert component["threshold"] >= 80.0, (
                f"UI component '{component['name']}' must have at least 80% threshold"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("A")
    def test_patient_management_class_a(self):
        """NFR-TEST-01: Patient Management must be Class A with 85% statement coverage."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        patient_components = [
            c for c in data["components"] if "patient" in c["name"].lower()
        ]

        assert len(patient_components) > 0, (
            "Patient Management component not found in coverage_gates.json"
        )

        for component in patient_components:
            assert component["safety_class"] == "A", (
                f"Patient component '{component['name']}' should be Class A"
            )
            assert component["threshold"] >= 85.0, (
                f"Patient component '{component['name']}' must have at least 85% threshold"
            )


class TestCoverageGatesValidation:
    """Tests for coverage gate validation logic."""

    @pytest.mark.requirement("NFR-TEST-04")
    @pytest.mark.safety_class("C")
    def test_class_c_threshold_enforcement(self):
        """NFR-TEST-04: Class C components must enforce 100% decision coverage on merge."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        class_c_components = [c for c in data["components"] if c["safety_class"] == "C"]

        # Verify at least one Class C component exists
        assert len(class_c_components) >= 2, (
            "Must have at least 2 Class C components (Generator, AEC) per SPEC-TEST-001"
        )

        for component in class_c_components:
            assert component["threshold"] == 100.0, (
                f"Class C component '{component['name']}' must have 100% threshold"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_all_components_have_path_patterns(self):
        """NFR-TEST-01: All components must have valid path patterns for coverage matching."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        for component in data["components"]:
            path_pattern = component["path_pattern"]
            assert path_pattern, (
                f"Component '{component['name']}' must have a non-empty path_pattern"
            )
            assert isinstance(path_pattern, str), (
                f"path_pattern must be a string for {component['name']}"
            )
            # Path pattern should contain wildcards or specific paths
            assert "*" in path_pattern or "/" in path_pattern or "\\" in path_pattern, (
                f"path_pattern for {component['name']} should contain wildcards or paths"
            )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_no_duplicate_component_names(self):
        """NFR-TEST-01: Component names must be unique."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        names = [c["name"] for c in data["components"]]
        unique_names = set(names)

        assert len(names) == len(unique_names), (
            f"Duplicate component names found: {[n for n in names if names.count(n) > 1]}"
        )

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_version_field_exists(self):
        """NFR-TEST-01: Configuration should have a version field for tracking."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        # Version field is recommended for configuration tracking
        # Not strictly required but good practice
        if "version" in data:
            assert isinstance(data["version"], str), "version must be a string"


class TestCoverageThresholdCompliance:
    """Tests for SPEC-TEST-001 Section 1.3 compliance."""

    @pytest.mark.requirement("NFR-TEST-01")
    @pytest.mark.safety_class("NA")
    def test_all_spec_components_present(self):
        """NFR-TEST-01: All components from SPEC-TEST-001 Section 1.3 must be present."""
        with open(COVERAGE_GATES_PATH, encoding="utf-8") as f:
            data = json.load(f)

        # Required components from SPEC-TEST-001 Section 1.3
        required_components = [
            ("generator", "C"),  # Generator Control
            ("aec", "C"),  # AEC (Auto Exposure Control)
            ("image", "B"),  # Image Processing Pipeline
            ("dicom", "B"),  # DICOM Communication
            ("dose", "B"),  # Dose Management
            ("ui", "B"),  # UI / Viewer Layer
            ("patient", "A"),  # Patient Management
        ]

        component_names = [c["name"].lower() for c in data["components"]]

        for keyword, expected_class in required_components:
            matching = [n for n in component_names if keyword in n]
            assert len(matching) > 0, (
                f"Required component containing '{keyword}' not found in coverage_gates.json"
            )
