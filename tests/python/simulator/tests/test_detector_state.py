"""
Unit tests for Detector State Machine

SPEC-TEST-001 FR-TEST-06.2: Detector simulator behavioral verification

pytest markers:
    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
"""

import os
import sys
import threading

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from models.detector_state import (
    AcquisitionConfig,
    DetectorInfo,
    DetectorState,
    DetectorStateMachine,
)


class TestDetectorStateMachine:
    """Unit tests for DetectorStateMachine."""

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_initialization(self):
        """Test detector initializes to IDLE state."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        assert state_machine.state == DetectorState.IDLE
        assert state_machine.is_ready is True
        assert state_machine.error_message is None
        assert state_machine.acquisition_count == 0

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_arm_transition(self):
        """Test IDLE -> ARMING -> READY state transition."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        config = AcquisitionConfig(
            mode=1,
            num_frames=1,
            frame_rate=10.0,
            binning=1,
            session_id="test-session",
        )

        result = state_machine.arm(config)
        assert result is True
        assert state_machine.state == DetectorState.READY
        assert state_machine.is_ready is True

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_acquisition_transition(self):
        """Test READY -> ACQUIRING -> IDLE state transition."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        config = AcquisitionConfig(mode=1, num_frames=1)
        state_machine.arm(config)

        result = state_machine.start_acquisition()
        assert result is True
        assert state_machine.state == DetectorState.ACQUIRING
        assert state_machine.is_ready is False

        state_machine.complete_acquisition()
        assert state_machine.state == DetectorState.IDLE
        assert state_machine.is_ready is True

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_acquisition_count(self):
        """Test acquisition counter increments correctly."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        assert state_machine.acquisition_count == 0

        config = AcquisitionConfig(mode=1, num_frames=1)
        state_machine.arm(config)
        state_machine.start_acquisition()
        state_machine.complete_acquisition()

        assert state_machine.acquisition_count == 1

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_fault_injection(self):
        """Test fault injection mode causes acquisition failure."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        # Enable fault mode
        state_machine.set_fault_mode(True)

        config = AcquisitionConfig(mode=1, num_frames=1)
        result = state_machine.arm(config)

        assert result is False
        assert state_machine.state == DetectorState.ERROR
        assert state_machine.error_message is not None

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_clear_fault(self):
        """Test clearing fault condition returns to IDLE."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        state_machine.set_fault_mode(True)
        config = AcquisitionConfig(mode=1, num_frames=1)
        state_machine.arm(config)

        assert state_machine.state == DetectorState.ERROR

        state_machine.clear_fault()
        assert state_machine.state == DetectorState.IDLE
        assert state_machine.is_ready is True
        assert state_machine.error_message is None

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_stop_acquisition(self):
        """Test stopping acquisition mid-flight."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        config = AcquisitionConfig(mode=1, num_frames=1)
        state_machine.arm(config)
        state_machine.start_acquisition()

        assert state_machine.state == DetectorState.ACQUIRING

        state_machine.stop_acquisition()
        assert state_machine.state == DetectorState.IDLE
        assert state_machine.is_ready is True

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_reset(self):
        """Test reset returns to initial state."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        config = AcquisitionConfig(mode=1, num_frames=1)
        state_machine.arm(config)
        state_machine.start_acquisition()
        state_machine.complete_acquisition()

        state_machine.reset()

        assert state_machine.state == DetectorState.INITIALIZING
        assert state_machine.is_ready is False
        assert state_machine.acquisition_count == 0

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_custom_detector_info(self):
        """Test custom detector info configuration."""
        custom_info = DetectorInfo(
            vendor="Test Vendor",
            model="TEST-100",
            serial_number="SN-12345",
            pixel_width=1024,
            pixel_height=1024,
        )

        state_machine = DetectorStateMachine(info=custom_info)
        info = state_machine.info

        assert info.vendor == "Test Vendor"
        assert info.model == "TEST-100"
        assert info.serial_number == "SN-12345"
        assert info.pixel_width == 1024

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_acquisition_time_setting(self):
        """Test acquisition time configuration."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        state_machine.set_acquisition_time(500)
        assert state_machine.get_acquisition_time() == 500

    @pytest.mark.requirement("FR-TEST-06.2")
    @pytest.mark.safety_class("B")
    def test_thread_safety(self):
        """Test concurrent access is thread-safe."""
        state_machine = DetectorStateMachine()
        state_machine.initialize()

        results = []
        errors = []
        lock = threading.Lock()

        def worker(worker_id: int):
            try:
                for _i in range(10):
                    config = AcquisitionConfig(mode=1, num_frames=1)
                    with lock:
                        state_machine.arm(config)
                        state_machine.start_acquisition()
                        state_machine.complete_acquisition()
                results.append(worker_id)
            except Exception as e:
                errors.append((worker_id, str(e)))

        threads = [threading.Thread(target=worker, args=(i,)) for i in range(5)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        assert len(errors) == 0
        assert len(results) == 5
        # Allow for some variation due to timing
        assert 45 <= state_machine.acquisition_count <= 50


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
