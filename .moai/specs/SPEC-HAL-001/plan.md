# SPEC-HAL-001: Implementation Plan

**SPEC ID**: SPEC-HAL-001
**Module**: `hnvue-hal` — Hardware Abstraction Layer
**Prerequisite**: SPEC-INFRA-001 (monorepo CMake structure must be established)

---

## 1. Technical Approach

### 1.1 Architecture Strategy

The implementation follows an interface-first, layered strategy:

1. Define all public abstract interfaces (`I*.h` headers) before writing any implementation code. This allows consumers (SPEC-IMAGING-001, SPEC-WORKFLOW-001) to begin integration work immediately using mock objects.
2. Implement the HVG communication layer (`GeneratorRS232Impl`, `GeneratorEthernetImpl`) as independent, testable units with no coupling to other `hnvue-*` libraries.
3. Implement the plugin loader (`DetectorPluginLoader`) with full ABI validation and error isolation at the plugin boundary.
4. Implement supporting utilities (`DmaRingBuffer`, `AecController`, `DeviceManager`) last, as they assemble the other components.
5. Implement the simulator variants (`GeneratorSimulator`, detector stub) to enable hardware-free integration testing from the start.

### 1.2 Key Design Decisions

**Plugin ABI Isolation**: Vendor detector DLLs are loaded via `LoadLibrary`/`dlopen` with C-linkage factory functions. The C++ interface is behind the C ABI boundary to eliminate C++ ABI compatibility issues between toolchain versions.

**Command Queue Thread Model**: The HVG command queue uses a mutex-protected FIFO with condition variable signaling. A dedicated dispatch thread processes commands from the queue, making the enqueue operation non-blocking for callers.

**Callback Thread Safety**: Status and alarm callbacks are delivered from a dedicated reader thread (for serial/Ethernet) or a timer thread (for simulator). The HAL guarantees that callbacks are not invoked concurrently, but they run on a background thread, so callers must synchronize shared state access.

**DMA Buffer Design**: The ring buffer uses `std::atomic` indices for lock-free head/tail management in the single-producer/single-consumer (SPSC) case, which is the common deployment scenario for detector frame streaming.

**Error Propagation**: All public API functions return `HalError` codes rather than throwing exceptions, consistent with safety-critical C++ coding guidelines (MISRA C++ / AUTOSAR Adaptive Platform style). Exceptions are permitted only internally and must be caught at component boundaries.

### 1.3 Technology Stack

| Component | Technology | Rationale |
|---|---|---|
| Language | C++17 | Required for project (structured bindings, `std::optional`, `if constexpr`) |
| Serial Communication | Windows COMx (WinAPI `CreateFile`) | Native, no SOUP dependency for serial |
| TCP/IP Communication | Boost.Asio or WinAPI Winsock2 | Async I/O without additional SOUP if WinAPI used |
| Protobuf | protobuf 3.x (vcpkg) | Standard interface serialization |
| Build | CMake 3.25+, vcpkg | Monorepo consistency |
| Unit Test | Google Test + Google Mock | IEC 62304 compatible, widely adopted |
| Code Analysis | clang-tidy, cppcheck | Static analysis for SOUP compliance |

---

## 2. Implementation Milestones

### Primary Goal: Public Interface Definitions

**Deliverables:**
- `include/hnvue/hal/HalTypes.h` — All shared enumerations and data types
- `include/hnvue/hal/IDetector.h` — Detector plugin abstract interface
- `include/hnvue/hal/IGenerator.h` — HVG driver abstract interface
- `include/hnvue/hal/ICollimator.h` — Collimator abstract interface
- `include/hnvue/hal/IPatientTable.h` — Patient table abstract interface
- `include/hnvue/hal/IAEC.h` — AEC abstract interface
- `include/hnvue/hal/IDoseMonitor.h` — Dose monitor abstract interface
- `include/hnvue/hal/PluginAbi.h` — C-linkage ABI contract
- `include/hnvue/hal/DeviceManager.h` — Device lifecycle manager declaration
- `include/hnvue/hal/DmaRingBuffer.h` — Ring buffer utility declaration
- `proto/hvg_control.proto` — HVG standard protobuf interface
- `proto/detector_acquisition.proto` — Detector integration protobuf interface
- `tests/mock/Mock*.h` — Google Mock headers for all six interfaces
- `CMakeLists.txt` (header-only + proto targets)

**Success Criteria:**
- All interfaces compile cleanly with `-Wall -Wextra -Wpedantic`
- All mock headers instantiate without linker errors in a test build
- Consumer projects can include headers and compile against mocks

**IEC 62304 Output**: Interface design document (IDD) and software unit specification for each header file.

---

### Secondary Goal: DMA Ring Buffer and Supporting Utilities

**Deliverables:**
- `src/buffer/DmaRingBuffer.cpp` — SPSC ring buffer implementation
- `tests/test_dma_ring_buffer.cpp` — Unit tests including timing assertions
- `AecController.h` / `AecController.cpp` — AEC mode switching and signal handling

**Success Criteria:**
- `DmaRingBuffer` unit tests pass with frame-to-callback latency verified <= 100 ms (synthetic benchmark)
- Thread-sanitizer (`-fsanitize=thread`) reports no data races in concurrent producer/consumer test
- Ring buffer handles DROP_OLDEST and BLOCK_PRODUCER overwrite policies correctly under overflow conditions

---

### Secondary Goal: HVG Communication Layer

**Deliverables:**
- `src/generator/GeneratorSimulator.h` / `.cpp`
- `src/generator/GeneratorRS232Impl.h` / `.cpp`
- `src/generator/GeneratorEthernetImpl.h` / `.cpp`
- `tests/test_generator_simulator.cpp`
- `tests/test_generator_command_queue.cpp`

**Success Criteria:**
- `GeneratorSimulator` passes all command queue tests including timeout, retry, and abort priority promotion
- Command round-trip latency verified <= 50 ms in simulator benchmark test
- Alarm callback delivery verified <= 50 ms in simulator test
- `GeneratorRS232Impl` and `GeneratorEthernetImpl` compile and link against Windows COMx/Winsock2 APIs
- At least one hardware integration test documented (marked `HARDWARE_REQUIRED` in CTest labels)

---

### Secondary Goal: Detector Plugin Loader

**Deliverables:**
- `src/plugin/DetectorPluginLoader.h` / `.cpp`
- `tests/test_detector_plugin_loader.cpp`
- A reference stub plugin DLL (`hnvue-hal-detector-stub.dll`) for testing

**Success Criteria:**
- Plugin loader correctly loads the stub plugin, calls `GetPluginManifest()`, and validates version
- Plugin loader rejects DLLs that do not export `CreateDetector` with a structured error
- Plugin loader catches exceptions thrown by a deliberately broken test plugin and isolates them
- Plugin unload and reload cycle completes without memory leak (verified with AddressSanitizer)

---

### Final Goal: DeviceManager and Integration

**Deliverables:**
- `src/DeviceManager.cpp` — Full implementation
- Integration test: `tests/test_device_manager_integration.cpp` using simulator + stub plugin

**Success Criteria:**
- `DeviceManager` initializes all simulator devices from a JSON config file
- Simulated exposure sequence (SetExposureParams → StartExposure → status callbacks → ExposureResult) executes end-to-end
- `DeviceManager` shutdown completes in reverse initialization order without deadlock
- `hnvue-hal.dll` installs with only public symbols exported (verified with `dumpbin /EXPORTS`)

---

### Optional Goal: Linux/Cross-Platform Compatibility

**Deliverables:**
- `GeneratorRS232Impl.cpp` POSIX serial path (`/dev/ttyS*`)
- `DmaRingBuffer.cpp` POSIX shared memory fallback (for embedded Linux targets)

**Success Criteria:**
- HAL library builds and all unit tests pass on Ubuntu 22.04 with GCC 12

---

## 3. Technical Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Vendor detector SDK ABI incompatibility | Medium | High | Define strict C-linkage ABI in `PluginAbi.h`; require vendors to follow contract; validate at load time |
| Serial port timing variability on Windows | Medium | Medium | Use configurable timeout with generous default (500 ms); implement retry; test on target hardware early |
| Race condition in DMA callback | Low | High | Apply thread-sanitizer in CI; enforce SPSC constraint in API; document threading model explicitly |
| IEC 62304 Class C documentation overhead | High | Medium | Template SWRS and SDS from the start; use requirement traceability matrix from this SPEC |
| Hardware unavailability during development | High | Low | Simulator and stub plugin cover 100% of unit tests; hardware tests are labeled separately |

---

## 4. IEC 62304 Process Artifacts

| Artifact | Source | Responsibility |
|---|---|---|
| Software Safety Classification | Section 1.2 of spec.md | System architect |
| Software Requirements Specification (SRS) | This SPEC (spec.md) | SPEC author |
| Software Architecture Document (SAD) | Section 4 of spec.md | Lead developer |
| Software Design Specification (SDS) | Per-file design comments + Doxygen | Implementation developer |
| Unit Test Plan | tests/CMakeLists.txt + test files | Developer |
| V&V Traceability Matrix | Section 5 of spec.md (traceability table) | QA |
| SOUP List | vcpkg.json + SOUP_LIST.md | Developer |
| Anomaly Report Template | `.moai/templates/anomaly-report.md` | QA |

---

## 5. Definition of Ready (Before Implementation Starts)

- [ ] SPEC-INFRA-001 is complete: monorepo CMake structure exists, vcpkg configured
- [ ] `libs/hnvue-hal/` directory skeleton created
- [ ] At least one vendor detector SDK integration partner identified for ABI validation
- [ ] Target HVG device communication protocol documentation available
- [ ] IEC 62304 safety classification review sign-off obtained

---

## 6. Definition of Done

- [ ] All public interface headers compile with no warnings under `-Wall -Wextra`
- [ ] All unit tests in `libs/hnvue-hal/tests/` pass under `ctest -R hnvue-hal`
- [ ] Code coverage >= 85% for all non-hardware implementation files
- [ ] Thread-sanitizer reports no data races in concurrent tests
- [ ] Address-sanitizer reports no memory errors in plugin lifecycle tests
- [ ] `hnvue-hal.dll` builds and exports only declared public symbols
- [ ] Traceability matrix (SPEC Section 5) is complete and verified
- [ ] SDS-level comments present in all `.cpp` implementation files
- [ ] No `TODO` or `FIXME` markers in production code (moved to GitHub issues)
- [ ] Hardware integration test results documented (or formally deferred with rationale)

---

*SPEC-HAL-001 Plan v1.0 | HnVue Diagnostic X-ray Console | 2026-02-17*
