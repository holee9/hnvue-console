"""
pytest configuration for HnVue Python HAL Simulator tests.

SPEC-TEST-001 FR-TEST-09: Requirements traceability via test markers
"""

import pytest


def pytest_configure(config):
    """Register custom markers for traceability."""
    config.addinivalue_line(
        "markers", "requirement(id): Link test to SPEC requirement ID (e.g., FR-TEST-06.2)"
    )
    config.addinivalue_line("markers", "safety_class(class): IEC 62304 safety class (A, B, C)")


@pytest.fixture
def safety_interlock_safe():
    """Fixture providing a safety interlock callback that always returns True (safe)."""
    return lambda: True


@pytest.fixture
def safety_interlock_unsafe():
    """Fixture providing a safety interlock callback that always returns False (unsafe)."""
    return lambda: False


@pytest.fixture
def safety_interlock_toggleable():
    """Fixture providing a toggleable safety interlock callback."""
    state = [True]

    def callback():
        return state[0]

    def set_state(new_state):
        state[0] = new_state

    callback.set_state = set_state
    return callback
