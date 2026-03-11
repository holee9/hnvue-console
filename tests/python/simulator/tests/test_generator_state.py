"""
Unit tests for Generator State Machine

SPEC-TEST-001 FR-TEST-06.3: Generator simulator behavioral verification

pytest markers:
    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")  # IEC 62304 Class C - Safety-critical

@MX:WARN: IEC 62304 Class C - These tests verify safety-critical behavior
"""

import os
import sys
import threading
import time

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from models.generator_state import (
    ExposureParams,
    GeneratorState,
    GeneratorStateMachine,
    HvgCapabilities,
)


class TestGeneratorStateMachine:
    """Unit tests for GeneratorStateMachine."""

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_initialization(self):
        """Test generator initializes to IDLE state."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()

        assert state_machine.state == GeneratorState.IDLE
        assert state_machine.is_ready is False
        assert state_machine.fault_code is None
        assert state_machine.exposure_count == 0

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_prepare_transition(self):
        """Test IDLE -> PREPARING -> READY state transition."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()

        result = state_machine.prepare()
        assert result is True
        assert state_machine.state == GeneratorState.READY
        assert state_machine.is_ready is True

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_exposure_params_validation(self):
        """Test exposure parameter validation against IEC 60601-2-44 limits."""
        state_machine = GeneratorStateMachine()

        # Valid parameters
        valid_params = ExposureParams(kvp=80.0, ma=200.0, ms=100.0)
        success, error = state_machine.set_exposure_params(valid_params)
        assert success is True
        assert error == ""

        # Invalid kV (too low)
        invalid_kv_low = ExposureParams(kvp=30.0, ma=200.0, ms=100.0)
        success, error = state_machine.set_exposure_params(invalid_kv_low)
        assert success is False
        assert "40" in error

        # Invalid kV (too high)
        invalid_kv_high = ExposureParams(kvp=160.0, ma=200.0, ms=100.0)
        success, error = state_machine.set_exposure_params(invalid_kv_high)
        assert success is False
        assert "150" in error

        # Invalid mA (too high)
        invalid_ma = ExposureParams(kvp=80.0, ma=1500.0, ms=100.0)
        success, error = state_machine.set_exposure_params(invalid_ma)
        assert success is False
        assert "1000" in error

        # Invalid ms (too low)
        invalid_ms = ExposureParams(kvp=80.0, ma=200.0, ms=0.5)
        success, error = state_machine.set_exposure_params(invalid_ms)
        assert success is False
        assert "1" in error

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_trigger_exposure(self):
        """Test exposure trigger and completion."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()
        state_machine.prepare()

        params = ExposureParams(kvp=80.0, ma=200.0, ms=50.0)  # 50ms exposure
        success, error, actual_kvp, actual_ma, actual_ms = state_machine.trigger_exposure(params)

        assert success is True
        assert error == ""
        assert actual_kvp == 80.0
        assert actual_ma == 200.0
        assert actual_ms == 50.0
        assert state_machine.exposure_count == 1

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_abort_exposure(self):
        """Test exposure abort functionality."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()
        state_machine.prepare()

        # Trigger exposure in a thread
        params = ExposureParams(kvp=80.0, ma=200.0, ms=5000.0)  # 5 second exposure

        result = [None]

        def exposure_thread():
            success, error, kvp, ma, ms = state_machine.trigger_exposure(params)
            result[0] = (success, error)

        thread = threading.Thread(target=exposure_thread)
        thread.start()

        # Wait briefly then abort
        time.sleep(0.1)
        state_machine.abort_exposure()

        thread.join(timeout=1.0)

        # Exposure should have been aborted
        assert result[0] is not None
        success, error = result[0]
        assert success is False
        assert "cancel" in error.lower() or "abort" in error.lower()

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_safety_interlock_blocks_exposure(self):
        """Test that safety interlock blocks exposure."""
        # Safety interlock returns False (unsafe)
        state_machine = GeneratorStateMachine(safety_interlock_callback=lambda: False)
        state_machine.initialize()
        state_machine.prepare()

        params = ExposureParams(kvp=80.0, ma=200.0, ms=100.0)
        success, error, kvp, ma, ms = state_machine.trigger_exposure(params)

        assert success is False
        assert "interlock" in error.lower()
        assert state_machine.exposure_count == 0

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_safety_interlock_aborts_mid_exposure(self):
        """Test that safety interlock aborts exposure mid-flight."""
        interlock_safe = [True]

        def interlock_callback():
            return interlock_safe[0]

        state_machine = GeneratorStateMachine(safety_interlock_callback=interlock_callback)
        state_machine.initialize()
        state_machine.prepare()

        params = ExposureParams(kvp=80.0, ma=200.0, ms=5000.0)  # 5 second exposure

        result = [None]

        def exposure_thread():
            success, error, kvp, ma, ms = state_machine.trigger_exposure(params)
            result[0] = (success, error)

        thread = threading.Thread(target=exposure_thread)
        thread.start()

        # Wait briefly then trigger interlock
        time.sleep(0.1)
        interlock_safe[0] = False  # Interlock becomes unsafe

        thread.join(timeout=1.0)

        # Exposure should have been aborted
        assert result[0] is not None
        success, error = result[0]
        assert success is False
        assert "interlock" in error.lower()

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_fault_injection(self):
        """Test fault injection mode causes exposure failure."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()

        state_machine.set_fault_mode(True)

        result = state_machine.prepare()
        assert result is False
        assert state_machine.state == GeneratorState.ERROR
        assert state_machine.fault_code is not None

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_clear_fault(self):
        """Test clearing fault condition."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()

        state_machine.set_fault_mode(True)
        state_machine.prepare()

        assert state_machine.state == GeneratorState.ERROR

        state_machine.clear_fault()
        assert state_machine.state == GeneratorState.IDLE
        assert state_machine.fault_code is None

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_exposure_count(self):
        """Test exposure counter increments correctly."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()

        assert state_machine.exposure_count == 0

        # First exposure
        state_machine.prepare()
        params = ExposureParams(kvp=80.0, ma=200.0, ms=10.0)
        state_machine.trigger_exposure(params)

        assert state_machine.exposure_count == 1

        # Second exposure
        state_machine.prepare()
        state_machine.trigger_exposure(params)

        assert state_machine.exposure_count == 2

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_get_status(self):
        """Test status retrieval."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()

        status = state_machine.get_status()
        assert status.state == GeneratorState.IDLE
        assert status.is_ready is False

        state_machine.prepare()
        status = state_machine.get_status()
        assert status.state == GeneratorState.READY
        assert status.is_ready is True

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_capabilities(self):
        """Test capabilities query."""
        custom_caps = HvgCapabilities(
            min_kvp=50.0,
            max_kvp=125.0,
            has_aec=False,
            vendor_name="Test Generator",
        )

        state_machine = GeneratorStateMachine(capabilities=custom_caps)
        caps = state_machine.capabilities

        assert caps.min_kvp == 50.0
        assert caps.max_kvp == 125.0
        assert caps.has_aec is False
        assert caps.vendor_name == "Test Generator"

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_reset(self):
        """Test reset returns to initial state."""
        state_machine = GeneratorStateMachine()
        state_machine.initialize()
        state_machine.prepare()

        params = ExposureParams(kvp=80.0, ma=200.0, ms=10.0)
        state_machine.trigger_exposure(params)

        state_machine.reset()

        assert state_machine.state == GeneratorState.IDLE
        assert state_machine.is_ready is False
        assert state_machine.exposure_count == 0

    @pytest.mark.requirement("FR-TEST-06.3")
    @pytest.mark.safety_class("C")
    def test_alarm_recording(self):
        """Test alarm recording during exposure abort."""
        interlock_safe = [True]

        def interlock_callback():
            return interlock_safe[0]

        state_machine = GeneratorStateMachine(safety_interlock_callback=interlock_callback)
        state_machine.initialize()
        state_machine.prepare()

        params = ExposureParams(kvp=80.0, ma=200.0, ms=5000.0)

        result = [None]

        def exposure_thread():
            success, error, kvp, ma, ms = state_machine.trigger_exposure(params)
            result[0] = (success, error)

        thread = threading.Thread(target=exposure_thread)
        thread.start()

        time.sleep(0.1)
        interlock_safe[0] = False  # Trigger interlock

        thread.join(timeout=1.0)

        # Check for alarm
        alarms = state_machine.get_alarms()
        assert len(alarms) > 0
        assert any("interlock" in a.description.lower() for a in alarms)


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
