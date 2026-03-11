"""
pytest configuration for coverage gate tests.

SPEC-TEST-001 FR-TEST-09: Requirements traceability via test markers
"""


def pytest_configure(config):
    """Register custom markers for coverage gate tests."""
    config.addinivalue_line(
        "markers",
        "requirement(id): Link test to SPEC requirement ID (e.g., NFR-TEST-01.1)",
    )
    config.addinivalue_line(
        "markers", "safety_class(class): IEC 62304 safety class (A, B, C, NA)"
    )
    config.addinivalue_line(
        "markers", "component(name): Component name for coverage gate"
    )
