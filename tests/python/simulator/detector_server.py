#!/usr/bin/env python3
"""
Detector Acquisition gRPC Server

SPEC-TEST-001 FR-TEST-06.2: Detector simulator implements USB 3.x/PCIe
communication protocol including register read/write and DMA image data transfer.

This server implements the DetectorAcquisition gRPC service defined in:
    libs/hnvue-hal/proto/detector_acquisition.proto

Usage:
    python -m simulator.detector_server --port 50051

@MX:SPEC: SPEC-TEST-001 FR-TEST-06.2
"""

import argparse
import asyncio
import logging
import time
from concurrent import futures
from typing import AsyncIterator

import grpc
from grpc import aio

# Note: Generated protobuf files should be placed in simulator/generated/
# For now, we define message classes inline for standalone operation

from models.detector_state import (
    DetectorStateMachine,
    DetectorState,
    AcquisitionConfig,
    DetectorInfo,
)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


# Protobuf message definitions (inline for standalone operation)
# These match detector_acquisition.proto definitions

class DetectorAcquisitionServicer:
    """
    gRPC servicer implementing DetectorAcquisition service.

    @MX:ANCHOR: DetectorAcquisitionServicer - gRPC service implementation
    @MX:WARN: Implements IEC 62304 Class B interface
    """

    def __init__(self, state_machine: DetectorStateMachine):
        self._state = state_machine

    async def StreamFrames(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> AsyncIterator[dict]:
        """
        Server-streaming: deliver raw frames to consumer.

        Args:
            request: AcquisitionConfig dict with mode, num_frames, frame_rate, binning, session_id

        Yields:
            RawFrame dicts with sequence_number, timestamp_us, width, height, bit_depth, pixel_data, session_id
        """
        config = AcquisitionConfig(
            mode=request.get("mode", 1),
            num_frames=request.get("num_frames", 1),
            frame_rate=request.get("frame_rate", 10.0),
            binning=request.get("binning", 1),
            session_id=request.get("session_id", ""),
        )

        # Arm the detector
        if not self._state.arm(config):
            await context.abort(
                grpc.StatusCode.FAILED_PRECONDITION,
                "Failed to arm detector",
            )
            return

        # Start acquisition
        if not self._state.start_acquisition():
            await context.abort(
                grpc.StatusCode.FAILED_PRECONDITION,
                "Failed to start acquisition",
            )
            return

        frame_count = 0
        max_frames = config.num_frames if config.num_frames > 0 else float("inf")
        frame_interval = 1.0 / config.frame_rate if config.frame_rate > 0 else 0.1

        info = self._state.info

        try:
            while frame_count < max_frames:
                # Check if acquisition was stopped
                if self._state.state not in (DetectorState.ACQUIRING, DetectorState.TRANSFERRING):
                    break

                # Generate synthetic frame data
                # Width and height adjusted by binning
                width = info.pixel_width // config.binning
                height = info.pixel_height // config.binning
                bit_depth = min(info.max_bit_depth, 16)

                # Create synthetic pixel data (flat field with noise)
                import numpy as np

                np.random.seed(frame_count)
                base_value = 1000 + (frame_count * 10) % 5000
                noise = np.random.normal(0, 50, (height, width)).astype(np.int16)
                pixel_data = np.clip(base_value + noise, 0, 2**bit_depth - 1).astype(np.uint16)
                pixel_bytes = pixel_data.tobytes()

                frame = {
                    "sequence_number": frame_count,
                    "timestamp_us": int(time.time() * 1_000_000),
                    "width": width,
                    "height": height,
                    "bit_depth": bit_depth,
                    "pixel_data": pixel_bytes,
                    "session_id": config.session_id,
                }

                yield frame
                frame_count += 1

                # Wait for next frame
                await asyncio.sleep(frame_interval)

        finally:
            self._state.complete_acquisition()

    async def StartAcquisition(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> dict:
        """
        Begin acquisition session.

        Args:
            request: AcquisitionConfig dict

        Returns:
            DetectorResponse dict with success, error_msg, error_code
        """
        config = AcquisitionConfig(
            mode=request.get("mode", 1),
            num_frames=request.get("num_frames", 1),
            frame_rate=request.get("frame_rate", 10.0),
            binning=request.get("binning", 1),
            session_id=request.get("session_id", ""),
        )

        # Arm the detector
        if not self._state.arm(config):
            return {
                "success": False,
                "error_msg": "Failed to arm detector",
                "error_code": 1,
            }

        # Start acquisition
        if not self._state.start_acquisition():
            return {
                "success": False,
                "error_msg": "Failed to start acquisition",
                "error_code": 2,
            }

        # Simulate acquisition time
        acquisition_time_ms = self._state.get_acquisition_time()
        await asyncio.sleep(acquisition_time_ms / 1000.0)

        # Complete acquisition
        self._state.complete_acquisition()

        return {"success": True, "error_msg": "", "error_code": 0}

    async def StopAcquisition(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> dict:
        """
        Stop acquisition session.

        Args:
            request: StopRequest dict with session_id, reason

        Returns:
            DetectorResponse dict
        """
        self._state.stop_acquisition()
        logger.info(f"Acquisition stopped: {request.get('reason', 'No reason provided')}")

        return {"success": True, "error_msg": "", "error_code": 0}

    async def RunCalibration(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> dict:
        """
        Run flat-field or dark-field calibration.

        Args:
            request: CalibrationType dict with type, num_frames

        Returns:
            CalibrationResult dict with success, output_path, error_msg
        """
        calib_type = request.get("type", 1)  # CALIB_DARK_FIELD
        num_frames = request.get("num_frames", 10)

        # Simulate calibration
        await asyncio.sleep(num_frames * 0.1)

        calib_names = {
            1: "dark_field",
            2: "flat_field",
            3: "defect_map",
        }
        calib_name = calib_names.get(calib_type, "unknown")

        return {
            "success": True,
            "output_path": f"/tmp/calibration/{calib_name}_{int(time.time())}.bin",
            "error_msg": "",
        }

    async def GetDetectorInfo(
        self,
        request: dict,
        context: grpc.aio.ServicerContext,
    ) -> dict:
        """
        Query static detector properties.

        Args:
            request: Empty dict

        Returns:
            DetectorInfo dict
        """
        info = self._state.info
        return {
            "vendor": info.vendor,
            "model": info.model,
            "serial_number": info.serial_number,
            "pixel_width": info.pixel_width,
            "pixel_height": info.pixel_height,
            "pixel_pitch_um": info.pixel_pitch_um,
            "max_bit_depth": info.max_bit_depth,
            "max_frame_rate": info.max_frame_rate,
            "firmware_version": info.firmware_version,
        }


async def serve(port: int) -> None:
    """Start the gRPC server."""
    state_machine = DetectorStateMachine()
    state_machine.initialize()

    server = aio.server(futures.ThreadPoolExecutor(max_workers=10))
    # Note: Add DetectorAcquisitionServicer to server when protobuf is generated
    # detector_acquisition_pb2_grpc.add_DetectorAcquisitionServicer_to_server(
    #     DetectorAcquisitionServicer(state_machine), server
    # )

    server.add_insecure_port(f"[::]:{port}")
    await server.start()
    logger.info(f"Detector Acquisition gRPC Server started on port {port}")

    try:
        await server.wait_for_termination()
    except KeyboardInterrupt:
        logger.info("Shutting down server...")
        await server.stop(5)


def main() -> None:
    """Main entry point."""
    parser = argparse.ArgumentParser(description="Detector Acquisition gRPC Server")
    parser.add_argument("--port", type=int, default=50051, help="Server port")
    args = parser.parse_args()

    asyncio.run(serve(args.port))


if __name__ == "__main__":
    main()
