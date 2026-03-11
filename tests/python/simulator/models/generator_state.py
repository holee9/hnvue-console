"""
Generator (HVG) State Machine Model

SPEC-TEST-001 FR-TEST-06.3: Generator simulator implements serial
communication protocol simulation including kV, mA, and exposure time commands.

State Machine: STANDBY -> READY -> PREPARING -> EXPOSING -> COMPLETE -> STANDBY

@MX:SPEC: SPEC-TEST-001 FR-TEST-06.3
@MX:WARN: IEC 62304 Class C - Safety-critical radiation control
"""

import threading
import time
from collections.abc import Callable
from dataclasses import dataclass
from enum import Enum


class GeneratorState(Enum):
    """Generator state enumeration matching hvg_control.proto GeneratorState."""

    UNSPECIFIED = 0
    IDLE = 1
    READY = 2
    ARMED = 3
    EXPOSING = 4
    ERROR = 5
    PREPARING = 6


class AlarmSeverity(Enum):
    """Alarm severity levels matching hvg_control.proto."""

    UNSPECIFIED = 0
    INFO = 1
    WARNING = 2
    ERROR = 3
    CRITICAL = 4


@dataclass
class ExposureParams:
    """Exposure parameters matching hvg_control.proto."""

    kvp: float = 0.0  # kV: range 40.0-150.0
    ma: float = 0.0  # mA: range 0.1-1000.0
    ms: float = 0.0  # Exposure time: 1-10000 ms
    mas: float = 0.0  # mAs (alternative to ma+ms)
    aec_mode: int = 1  # AEC_MANUAL
    focus: str = "large"


@dataclass
class HvgCapabilities:
    """HVG capabilities matching hvg_control.proto."""

    min_kvp: float = 40.0
    max_kvp: float = 150.0
    min_ma: float = 0.1
    max_ma: float = 1000.0
    min_ms: float = 1.0
    max_ms: float = 10000.0
    has_aec: bool = True
    has_dual_focus: bool = True
    vendor_name: str = "Simulated HVG Corp"
    model_name: str = "SHVG-2000"
    firmware_version: str = "1.0.0-sim"


@dataclass
class HvgStatus:
    """Current HVG status."""

    state: GeneratorState = GeneratorState.IDLE
    is_ready: bool = False
    fault_code: str | None = None
    actual_kvp: float = 0.0
    actual_ma: float = 0.0
    interlock_ok: bool = True
    exposure_count: int = 0


@dataclass
class HvgAlarm:
    """HVG alarm event."""

    alarm_code: int = 0
    description: str = ""
    severity: AlarmSeverity = AlarmSeverity.INFO
    timestamp_us: int = 0


class GeneratorStateMachine:
    """
    Thread-safe generator state machine.

    Implements the state transitions defined in SPEC-TEST-001 Section 4.3.2:
    STANDBY -> READY -> PREPARING -> EXPOSING -> COMPLETE -> STANDBY

    @MX:ANCHOR: GeneratorStateMachine - core state management
    @MX:WARN: SAFETY-CRITICAL - Controls ionizing radiation emission
    All exposure operations must check safety interlock
    """

    # Parameter limits per IEC 60601-2-44
    MIN_KVP = 40.0
    MAX_KVP = 150.0
    MIN_MA = 0.1
    MAX_MA = 1000.0
    MIN_MS = 1.0
    MAX_MS = 10000.0

    def __init__(
        self,
        capabilities: HvgCapabilities | None = None,
        safety_interlock_callback: Callable[[], bool] | None = None,
    ):
        self._lock = threading.Lock()
        self._state = GeneratorState.IDLE
        self._is_ready = False
        self._fault_code: str | None = None
        self._fault_mode_enabled = False
        self._last_exposure_params = ExposureParams()
        self._exposure_count = 0
        self._capabilities = capabilities or HvgCapabilities()
        self._safety_interlock_callback = safety_interlock_callback
        self._exposure_cancelled = False
        self._alarms: list[HvgAlarm] = []

    @property
    def state(self) -> GeneratorState:
        with self._lock:
            return self._state

    @property
    def is_ready(self) -> bool:
        with self._lock:
            return self._is_ready

    @property
    def fault_code(self) -> str | None:
        with self._lock:
            return self._fault_code

    @property
    def exposure_count(self) -> int:
        with self._lock:
            return self._exposure_count

    @property
    def capabilities(self) -> HvgCapabilities:
        with self._lock:
            return self._capabilities

    def initialize(self) -> None:
        """Initialize generator to IDLE state."""
        with self._lock:
            self._state = GeneratorState.IDLE
            self._is_ready = False
            self._fault_code = None
            self._exposure_count = 0
            self._alarms.clear()

    def prepare(self) -> bool:
        """
        Transition from IDLE to PREPARING to READY.

        Returns:
            True if preparation successful, False otherwise
        """
        with self._lock:
            if self._fault_mode_enabled:
                self._state = GeneratorState.ERROR
                self._fault_code = "ERR_PREP_FAULT"
                self._is_ready = False
                return False

            if self._state != GeneratorState.IDLE:
                return False

            self._state = GeneratorState.PREPARING
            self._is_ready = False

        # Simulate preparation time (50ms)
        time.sleep(0.05)

        with self._lock:
            self._state = GeneratorState.READY
            self._is_ready = True

        return True

    def set_exposure_params(self, params: ExposureParams) -> tuple[bool, str]:
        """
        Validate and set exposure parameters.

        Args:
            params: Exposure parameters to set

        Returns:
            Tuple of (success, error_message)
        """
        # Validate kV
        if not (self.MIN_KVP <= params.kvp <= self.MAX_KVP):
            return False, f"kV must be between {self.MIN_KVP} and {self.MAX_KVP}"

        # Validate mA
        if not (self.MIN_MA <= params.ma <= self.MAX_MA):
            return False, f"mA must be between {self.MIN_MA} and {self.MAX_MA}"

        # Validate ms
        if not (self.MIN_MS <= params.ms <= self.MAX_MS):
            return False, f"ms must be between {self.MIN_MS} and {self.MAX_MS}"

        with self._lock:
            self._last_exposure_params = params

        return True, ""

    def trigger_exposure(self, params: ExposureParams) -> tuple[bool, str, float, float, float]:
        """
        Trigger X-ray exposure.

        SAFETY-CRITICAL: This method controls ionizing radiation emission.

        Args:
            params: Exposure parameters

        Returns:
            Tuple of (success, error_message, actual_kvp, actual_ma, actual_ms)
        """
        # SAFETY-CRITICAL: Check safety interlock before exposure
        if self._safety_interlock_callback is not None:
            if not self._safety_interlock_callback():
                return False, "Exposure blocked by safety interlock", 0.0, 0.0, 0.0

        with self._lock:
            # Validate state
            if self._state != GeneratorState.READY or not self._is_ready:
                return False, "Generator not ready", 0.0, 0.0, 0.0

            # Check fault mode
            if self._fault_mode_enabled:
                self._state = GeneratorState.ERROR
                self._fault_code = "ERR_EXPOSURE_FAULT"
                self._is_ready = False
                return False, "Fault mode enabled", 0.0, 0.0, 0.0

            # Start exposure
            self._state = GeneratorState.EXPOSING
            self._is_ready = False
            self._last_exposure_params = params
            self._exposure_count += 1
            self._exposure_cancelled = False
            exposure_time_ms = params.ms

        # Simulate exposure with safety interlock monitoring
        actual_kvp = params.kvp
        actual_ma = params.ma
        actual_ms = 0.0

        try:
            start_time = time.time()
            end_time = start_time + (exposure_time_ms / 1000.0)

            while time.time() < end_time:
                # SAFETY-CRITICAL: Check interlock during exposure
                if self._safety_interlock_callback is not None:
                    if not self._safety_interlock_callback():
                        self._add_alarm(
                            HvgAlarm(
                                alarm_code=1001,
                                description="Exposure aborted due to safety interlock",
                                severity=AlarmSeverity.CRITICAL,
                                timestamp_us=int(time.time() * 1_000_000),
                            )
                        )
                        with self._lock:
                            self._state = GeneratorState.IDLE
                            self._is_ready = False
                        return (
                            False,
                            "Exposure aborted by safety interlock",
                            actual_kvp,
                            actual_ma,
                            actual_ms,
                        )

                # Check for external cancellation
                with self._lock:
                    if self._exposure_cancelled:
                        return False, "Exposure cancelled", actual_kvp, actual_ma, actual_ms

                time.sleep(0.001)  # 1ms check interval
                actual_ms = (time.time() - start_time) * 1000.0

            actual_ms = exposure_time_ms

        finally:
            with self._lock:
                self._state = GeneratorState.IDLE
                self._is_ready = False

        return True, "", actual_kvp, actual_ma, actual_ms

    def abort_exposure(self) -> None:
        """Abort current exposure immediately."""
        with self._lock:
            if self._state == GeneratorState.EXPOSING:
                self._exposure_cancelled = True
                self._state = GeneratorState.IDLE
                self._is_ready = False

    def set_fault_mode(self, enabled: bool) -> None:
        """Enable or disable fault injection mode."""
        with self._lock:
            self._fault_mode_enabled = enabled
            if enabled and self._state != GeneratorState.ERROR:
                self._fault_code = "ERR_FAULT_MODE"

    def clear_fault(self) -> None:
        """Clear fault condition and return to IDLE state."""
        with self._lock:
            self._fault_code = None
            self._fault_mode_enabled = False
            self._state = GeneratorState.IDLE
            self._is_ready = False

    def reset(self) -> None:
        """Reset generator to initial state."""
        with self._lock:
            self._state = GeneratorState.IDLE
            self._is_ready = False
            self._fault_code = None
            self._fault_mode_enabled = False
            self._exposure_count = 0
            self._last_exposure_params = ExposureParams()
            self._exposure_cancelled = False
            self._alarms.clear()

    def get_status(self) -> HvgStatus:
        """Get current generator status."""
        with self._lock:
            return HvgStatus(
                state=self._state,
                is_ready=self._is_ready,
                fault_code=self._fault_code,
                actual_kvp=self._last_exposure_params.kvp,
                actual_ma=self._last_exposure_params.ma,
                interlock_ok=True,
                exposure_count=self._exposure_count,
            )

    def get_last_exposure_params(self) -> ExposureParams:
        """Get last exposure parameters used."""
        with self._lock:
            return self._last_exposure_params

    def _add_alarm(self, alarm: HvgAlarm) -> None:
        """Add an alarm to the alarm list."""
        with self._lock:
            self._alarms.append(alarm)
            # Keep only last 100 alarms
            if len(self._alarms) > 100:
                self._alarms = self._alarms[-100:]

    def get_alarms(self) -> list[HvgAlarm]:
        """Get all alarms."""
        with self._lock:
            return list(self._alarms)

    def stream_status(self) -> HvgStatus:
        """Get streaming status update (for gRPC streaming)."""
        return self.get_status()
