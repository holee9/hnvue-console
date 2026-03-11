#!/usr/bin/env python3
"""
Generator (HVG) Control gRPC Server

SPEC-TEST-001 FR-TEST-06.3: Generator simulator implements serial communication
protocol including kV, mA, and exposure time commands and status responses.

This server implements the HvgControl gRPC service defined in:
    libs/hnvue-hal/proto/hvg_control.proto

Usage:
    python -m simulator.generator_server --port 50052

@MX:SPEC: SPEC-TEST-001 FR-TEST-06.3
@MX:WARN: IEC 62304 Class C - Safety-critical radiation control
"""

import argparse
import asyncio
import logging
import time
from collections.abc import AsyncIterator
from concurrent import futures

import grpc
from grpc import aio
from models.generator_state import (
    ExposureParams,
    GeneratorStateMachine,
)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


# Safety interlock callback type
SafetyInterlockCallback = callable


class HvgControlServicer:
    """
    gRPC servicer implementing HvgControl service.

    @MX:ANCHOR: HvgControlServicer - gRPC service implementation
    @MX:WARN: SAFETY-CRITICAL - IEC 62304 Class C radiation control
    All exposure operations must validate safety interlock
    """

    def __init__(
        self,
        state_machine: GeneratorStateMachine,
        safety_interlock_callback: SafetyInterlockCallback = None,
    ):
        self._state = state_machine
        self._safety_interlock_callback = safety_interlock_callback

    async def SetExposureParams(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> dict:
        """
        Set exposure parameters before arming.

        SAFETY-CRITICAL: Validates all parameters against IEC 60601-2-44 limits.

        Args:
            request: ExposureParams dict with kvp, ma, ms, mas, aec_mode, focus

        Returns:
            HvgResponse dict with success, error_msg, error_code
        """
        params = ExposureParams(
            kvp=request.get("kvp", 0.0),
            ma=request.get("ma", 0.0),
            ms=request.get("ms", 0.0),
            mas=request.get("mas", 0.0),
            aec_mode=request.get("aec_mode", 1),
            focus=request.get("focus", "large"),
        )

        success, error_msg = self._state.set_exposure_params(params)

        return {
            "success": success,
            "error_msg": error_msg,
            "error_code": 1 if not success else 0,
        }

    async def StartExposure(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> dict:
        """
        Arm and initiate X-ray exposure.

        SAFETY-CRITICAL: Controls ionizing radiation emission.
        Validates safety interlock before and during exposure.

        Args:
            request: ExposureRequest dict with request_id

        Returns:
            ExposureResult dict with success, actual_kvp, actual_ma, actual_ms, actual_mas, error_msg
        """
        request_id = request.get("request_id", "")
        logger.info(f"StartExposure requested: {request_id}")

        # Prepare the generator
        if not self._state.prepare():
            return {
                "success": False,
                "actual_kvp": 0.0,
                "actual_ma": 0.0,
                "actual_ms": 0.0,
                "actual_mas": 0.0,
                "error_msg": "Failed to prepare generator",
            }

        # Get exposure parameters
        params = self._state.get_last_exposure_params()

        # Trigger exposure (blocking until complete)
        success, error_msg, actual_kvp, actual_ma, actual_ms = self._state.trigger_exposure(params)

        actual_mas = actual_ma * actual_ms / 1000.0

        return {
            "success": success,
            "actual_kvp": actual_kvp,
            "actual_ma": actual_ma,
            "actual_ms": actual_ms,
            "actual_mas": actual_mas,
            "error_msg": error_msg,
        }

    async def AbortExposure(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> dict:
        """
        Abort an in-progress exposure immediately.

        SAFETY-CRITICAL: Emergency stop function.

        Args:
            request: AbortRequest dict with reason

        Returns:
            HvgResponse dict
        """
        reason = request.get("reason", "")
        logger.warning(f"AbortExposure: {reason}")

        self._state.abort_exposure()

        return {"success": True, "error_msg": "", "error_code": 0}

    async def StreamStatus(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> AsyncIterator[dict]:
        """
        Server-streaming: real-time HVG status at >= 10 Hz.

        Args:
            request: StatusRequest dict (empty)

        Yields:
            HvgStatus dicts at 10 Hz
        """
        while context.is_active():
            status = self._state.stream_status()
            yield {
                "actual_kvp": status.actual_kvp,
                "actual_ma": status.actual_ma,
                "state": status.state.value,
                "interlock_ok": status.interlock_ok,
                "timestamp_us": int(time.time() * 1_000_000),
            }
            await asyncio.sleep(0.1)  # 10 Hz

    async def StreamAlarms(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> AsyncIterator[dict]:
        """
        Server-streaming: alarm events with < 50 ms delivery latency.

        Args:
            request: AlarmRequest dict (empty)

        Yields:
            HvgAlarm dicts when alarms occur
        """
        last_alarm_count = 0

        while context.is_active():
            alarms = self._state.get_alarms()
            if len(alarms) > last_alarm_count:
                # Yield new alarms
                for alarm in alarms[last_alarm_count:]:
                    yield {
                        "alarm_code": alarm.alarm_code,
                        "description": alarm.description,
                        "severity": alarm.severity.value,
                        "timestamp_us": alarm.timestamp_us,
                    }
                last_alarm_count = len(alarms)

            await asyncio.sleep(0.05)  # 50 ms check interval

    async def GetCapabilities(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> dict:
        """
        Query static HVG device capabilities.

        Args:
            request: Empty dict

        Returns:
            HvgCapabilities dict
        """
        caps = self._state.capabilities
        return {
            "min_kvp": caps.min_kvp,
            "max_kvp": caps.max_kvp,
            "min_ma": caps.min_ma,
            "max_ma": caps.max_ma,
            "min_ms": caps.min_ms,
            "max_ms": caps.max_ms,
            "has_aec": caps.has_aec,
            "has_dual_focus": caps.has_dual_focus,
            "vendor_name": caps.vendor_name,
            "model_name": caps.model_name,
            "firmware_version": caps.firmware_version,
        }


async def serve(port: int, safety_interlock: SafetyInterlockCallback = None) -> None:
    """Start the gRPC server."""
    state_machine = GeneratorStateMachine(safety_interlock_callback=safety_interlock)
    state_machine.initialize()

    server = aio.server(futures.ThreadPoolExecutor(max_workers=10))
    # Note: Add HvgControlServicer to server when protobuf is generated
    # hvg_control_pb2_grpc.add_HvgControlServicer_to_server(
    #     HvgControlServicer(state_machine, safety_interlock), server
    # )

    server.add_insecure_port(f"[::]:{port}")
    await server.start()
    logger.info(f"HVG Control gRPC Server started on port {port}")

    try:
        await server.wait_for_termination()
    except KeyboardInterrupt:
        logger.info("Shutting down server...")
        await server.stop(5)


def main() -> None:
    """Main entry point."""
    parser = argparse.ArgumentParser(description="HVG Control gRPC Server")
    parser.add_argument("--port", type=int, default=50052, help="Server port")
    args = parser.parse_args()

    asyncio.run(serve(args.port))


if __name__ == "__main__":
    main()
