"""
Detector State Machine Model

SPEC-TEST-001 FR-TEST-06.2: Detector simulator implements USB 3.x/PCIe
communication protocol simulation including state transitions.

State Machine: IDLE -> ARMING -> READY -> ACQUIRING -> TRANSFERRING -> IDLE

@MX:SPEC: SPEC-TEST-001 FR-TEST-06.2
"""

from enum import Enum
from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional
import threading
import time


class DetectorState(Enum):
    """Detector state enumeration matching C# DetectorState."""

    INITIALIZING = 0
    IDLE = 1
    ARMING = 2
    READY = 3
    ACQUIRING = 4
    TRANSFERRING = 5
    ERROR = 6


@dataclass
class DetectorInfo:
    """Detector information matching detector_acquisition.proto DetectorInfo."""

    vendor: str = "Simulated Detector Corp"
    model: str = "SD-2000"
    serial_number: str = "SIM-DET-001"
    pixel_width: int = 2048
    pixel_height: int = 2048
    pixel_pitch_um: float = 200.0
    max_bit_depth: int = 16
    max_frame_rate: float = 30.0
    firmware_version: str = "1.0.0-sim"


@dataclass
class AcquisitionConfig:
    """Acquisition configuration matching detector_acquisition.proto."""

    mode: int = 1  # MODE_STATIC
    num_frames: int = 1
    frame_rate: float = 10.0
    binning: int = 1
    session_id: str = ""


@dataclass
class DetectorStatus:
    """Current detector status."""

    state: DetectorState = DetectorState.INITIALIZING
    is_ready: bool = False
    error_message: Optional[str] = None
    acquisition_count: int = 0


class DetectorStateMachine:
    """
    Thread-safe detector state machine.

    Implements the state transitions defined in SPEC-TEST-001 Section 4.3.1:
    IDLE -> ARMING -> READY -> ACQUIRING -> TRANSFERRING -> IDLE

    @MX:ANCHOR: DetectorStateMachine - core state management
    @MX:WARN: Thread safety - uses lock for all state changes
    """

    def __init__(self, info: Optional[DetectorInfo] = None):
        self._lock = threading.Lock()
        self._state = DetectorState.INITIALIZING
        self._is_ready = False
        self._error_message: Optional[str] = None
        self._fault_mode_enabled = False
        self._acquisition_count = 0
        self._acquisition_time_ms = 100
        self._info = info or DetectorInfo()
        self._config = AcquisitionConfig()

    @property
    def state(self) -> DetectorState:
        with self._lock:
            return self._state

    @property
    def is_ready(self) -> bool:
        with self._lock:
            return self._is_ready

    @property
    def error_message(self) -> Optional[str]:
        with self._lock:
            return self._error_message

    @property
    def acquisition_count(self) -> int:
        with self._lock:
            return self._acquisition_count

    @property
    def info(self) -> DetectorInfo:
        with self._lock:
            return self._info

    def initialize(self) -> None:
        """Initialize detector to IDLE state."""
        with self._lock:
            self._state = DetectorState.IDLE
            self._is_ready = True
            self._error_message = None
            self._acquisition_count = 0

    def arm(self, config: AcquisitionConfig) -> bool:
        """
        Transition from IDLE to ARMING to READY.

        Args:
            config: Acquisition configuration parameters

        Returns:
            True if arming successful, False otherwise
        """
        with self._lock:
            if self._fault_mode_enabled:
                self._state = DetectorState.ERROR
                self._error_message = "ERR_ARM_FAULT"
                self._is_ready = False
                return False

            if self._state != DetectorState.IDLE:
                return False

            self._state = DetectorState.ARMING
            self._config = config

        # Simulate arming delay
        time.sleep(0.05)

        with self._lock:
            self._state = DetectorState.READY
            self._is_ready = True

        return True

    def start_acquisition(self) -> bool:
        """
        Start acquisition from READY state.

        Returns:
            True if acquisition started, False otherwise
        """
        with self._lock:
            if self._fault_mode_enabled:
                self._state = DetectorState.ERROR
                self._error_message = "ERR_ACQUISITION_FAULT"
                self._is_ready = False
                return False

            if self._state != DetectorState.READY:
                return False

            self._state = DetectorState.ACQUIRING
            self._is_ready = False
            self._acquisition_count += 1

        return True

    def complete_acquisition(self) -> None:
        """Complete acquisition and return to IDLE state."""
        with self._lock:
            if self._state == DetectorState.ACQUIRING:
                self._state = DetectorState.TRANSFERRING

        # Simulate transfer time
        time.sleep(0.01)

        with self._lock:
            self._state = DetectorState.IDLE
            self._is_ready = True

    def stop_acquisition(self) -> None:
        """Stop acquisition and return to IDLE state."""
        with self._lock:
            if self._state in (DetectorState.ACQUIRING, DetectorState.TRANSFERRING):
                self._state = DetectorState.IDLE
                self._is_ready = True

    def set_fault_mode(self, enabled: bool) -> None:
        """Enable or disable fault injection mode."""
        with self._lock:
            self._fault_mode_enabled = enabled
            if enabled and self._state != DetectorState.ERROR:
                self._error_message = "ERR_FAULT_MODE"

    def clear_fault(self) -> None:
        """Clear fault condition and return to IDLE state."""
        with self._lock:
            self._error_message = None
            self._fault_mode_enabled = False
            self._state = DetectorState.IDLE
            self._is_ready = True

    def reset(self) -> None:
        """Reset detector to initial state."""
        with self._lock:
            self._state = DetectorState.INITIALIZING
            self._is_ready = False
            self._error_message = None
            self._fault_mode_enabled = False
            self._acquisition_count = 0
            self._acquisition_time_ms = 100

    def get_status(self) -> DetectorStatus:
        """Get current detector status."""
        with self._lock:
            return DetectorStatus(
                state=self._state,
                is_ready=self._is_ready,
                error_message=self._error_message,
                acquisition_count=self._acquisition_count,
            )

    def set_acquisition_time(self, time_ms: int) -> None:
        """Set simulated acquisition time in milliseconds."""
        with self._lock:
            self._acquisition_time_ms = time_ms

    def get_acquisition_time(self) -> int:
        """Get simulated acquisition time in milliseconds."""
        with self._lock:
            return self._acquisition_time_ms
