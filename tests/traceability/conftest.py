"""
pytest configuration for RTM tests.

SPEC-TEST-001 FR-TEST-09: Requirements traceability via test markers
"""


def pytest_configure(config):
    """Register custom markers for traceability."""
    config.addinivalue_line(
        "markers",
        "requirement(id): Link test to SPEC requirement ID (e.g., FR-TEST-09.1)",
    )
    config.addinivalue_line(
        "markers", "safety_class(class): IEC 62304 safety class (A, B, C, NA)"
    )
