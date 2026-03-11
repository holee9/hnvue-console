"""
HnVue Python HAL Simulator Package

SPEC-TEST-001 Phase 2: Python-based HW simulator testbench
Implements gRPC servers for detector and generator simulation.

This package provides:
- DetectorAcquisition gRPC server (detector_server.py)
- HvgControl gRPC server (generator_server.py)
- State machine models (models/)

Usage:
    python -m simulator.detector_server --port 50051
    python -m simulator.generator_server --port 50052
"""

__version__ = "1.0.0"
__author__ = "HnVue Team"
