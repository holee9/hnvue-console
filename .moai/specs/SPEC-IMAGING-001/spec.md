# SPEC-IMAGING-001: HnVue Image Processing Pipeline

---

## Metadata

| Field         | Value                                          |
|---------------|------------------------------------------------|
| SPEC ID       | SPEC-IMAGING-001                               |
| Title         | HnVue Image Processing Pipeline               |
| Product       | HnVue - Diagnostic Medical Device X-ray GUI Console SW |
| Status        | Planned                                        |
| Priority      | High                                           |
| Safety Class  | IEC 62304 Class B                              |
| Created       | 2026-02-17                                     |
| Domain        | IMAGING                                        |

---

## 1. Environment

### 1.1 System Context

HnVue is a diagnostic medical device X-ray GUI console software running on a host PC. The image processing pipeline receives raw 16-bit grayscale frames from an FPGA-based flat-panel X-ray detector via USB 3.x or PCIe and produces corrected, display-ready images for the diagnostic viewer.

The pipeline operates within the larger HnVue system architecture:

```
[FPGA Detector / Acquisition Layer]
          |
          | Raw 16-bit Frame (DMA / USB 3.x / PCIe)
          v
[Image Acquisition Module]  ─── triggers ──→  [SPEC-WORKFLOW-001]
          |
          | Raw ImageBuffer
          v
[Image Processing Pipeline]  ← THIS SPEC
          |
          | Processed ImageBuffer (display-ready)
          v
[Diagnostic Viewer / DICOM Formatter]
```

### 1.2 Deployment Package

The image processing pipeline is packaged as a C++ shared library located at `libs/hnvue-imaging/`. Engine plugin DLLs reside alongside the core library and are loaded at runtime. This packaging enables engine replacement without recompiling the host application.

```
libs/
  hnvue-imaging/
    hnvue-imaging.dll        # Core pipeline host library
    hnvue-imaging.lib        # Import library
    hnvue-imaging.h          # Public API header
    IImageProcessingEngine.h # Pluggable engine interface header
    engines/
      default-engine.dll     # Built-in reference implementation
      [vendor-engine].dll    # Optional third-party replacement
    calibration/
      [schema definitions]   # Calibration data formats
```

### 1.3 Technology Stack

| Component              | Technology                                    |
|------------------------|-----------------------------------------------|
| Language               | C++17 (minimum), C++20 preferred              |
| Core Image Processing  | OpenCV 4.x (C++ API)                          |
| FFT Operations         | FFTW 3.x                                      |
| Plugin Loading         | OS dynamic library loader (LoadLibrary / dlopen) |
| Build System           | CMake 3.20+                                   |
| Unit Testing           | Google Test / Google Mock                     |
| Pixel Depth            | 16-bit grayscale throughout the pipeline      |
| Safety Standard        | IEC 62304 Class B                             |

### 1.4 Regulatory and Safety Context

This component is classified as IEC 62304 Class B software because errors in image processing affect diagnostic image quality and can influence clinical diagnosis. The following constraints apply:

- All processing stages must preserve 16-bit precision; no silent precision loss is permitted.
- Calibration data integrity must be validated before application.
- Raw input images must be preserved alongside processed output for audit and re-processing.
- The pluggable engine interface must enforce a contract that prevents upstream or downstream modules from having knowledge of which engine is active.

---

## 2. Assumptions

| ID    | Assumption                                                                                       | Confidence | Risk if Wrong                                        |
|-------|--------------------------------------------------------------------------------------------------|------------|------------------------------------------------------|
| A-001 | Raw frames are delivered as contiguous 16-bit grayscale buffers in memory (not compressed).     | High       | Pipeline must add decompression stage.               |
| A-002 | Calibration data (dark frames, gain maps, defect maps) are pre-acquired and stored on disk before image acquisition begins. | High | Calibration-on-demand requires pipeline redesign.  |
| A-003 | The host PC provides at least 4 logical CPU cores available for image processing tasks.          | Medium     | Single-threaded fallback will likely exceed timing budgets. |
| A-004 | Scatter correction (virtual grid) is protocol-dependent and may be disabled for certain exposures; its presence is determined by the acquisition protocol configuration. | High | All exposures may require scatter correction, increasing latency. |
| A-005 | OpenCV and FFTW licenses are compatible with the commercial distribution model of HnVue.         | High       | Library substitution is required with potential functionality gap. |
| A-006 | Engine plugin DLLs are loaded once at application startup and remain loaded for the session lifetime. | Medium | Hot-swap support requires additional synchronization design. |
| A-007 | Frame dimensions do not change within a single acquisition session.                              | High       | Dynamic buffer reallocation is required, adding latency. |
| A-008 | The display subsystem accepts 16-bit grayscale with an applied LUT for rendering; pipeline does not need to down-convert to 8-bit. | Medium | 8-bit conversion stage and LUT application must occur inside the pipeline. |

---

## 3. Requirements

### 3.1 Functional Requirements

#### FR-IMG-01: Offset Correction (Dark Frame Subtraction)

**When** a raw image frame is received by the pipeline, **the system shall** subtract a pre-acquired dark calibration frame (offset map) from the raw frame on a pixel-by-pixel basis, clamping results to zero to prevent underflow.

**Rationale:** Dark frames capture detector dark current and fixed-pattern noise. Subtraction removes these additive artifacts from every clinical image.

---

#### FR-IMG-02: Gain Correction (Flat-Field Normalization)

**When** offset correction has been applied to a frame, **the system shall** normalize pixel values by multiplying each pixel by a corresponding gain correction coefficient derived from a pre-acquired flood-field (gain) calibration image.

**Rationale:** Detector pixels have non-uniform sensitivity. Gain correction normalizes the response across the detector surface to produce a flat-field response.

---

#### FR-IMG-03: Defect Pixel Mapping

**When** gain correction has been applied, **the system shall** identify and replace defective pixels (dead pixels and hot pixels) by interpolating values from adjacent valid neighboring pixels, using the pre-loaded defect pixel map to identify defective locations.

**Rationale:** All flat-panel detectors have a known set of defective pixels that produce incorrect signal levels. Interpolation from neighbors prevents visible artifacts in the diagnostic image.

---

#### FR-IMG-04: Scatter Correction (Virtual Grid)

**Where** the active acquisition protocol specifies scatter correction, **when** defect pixel mapping has been completed, **the system shall** apply a frequency-domain scatter correction algorithm (virtual anti-scatter grid) to the image frame, suppressing low-frequency scatter signal without a physical grid.

**Where** the active acquisition protocol does not specify scatter correction, **the system shall** pass the frame through the scatter correction stage without modification.

**Rationale:** Scatter radiation degrades image contrast. A virtual grid algorithm replicates the effect of a physical anti-scatter grid without the physical hardware cost.

---

#### FR-IMG-05: Window and Level Adjustment with Display LUT

**When** the full correction chain has been applied to a frame, **the system shall** apply window and level mapping to the corrected 16-bit pixel values and generate a display lookup table (LUT) that maps corrected values to display intensity values according to the specified window center (Level) and window width (Window) parameters.

**While** the user adjusts Window or Level parameters interactively, **the system shall** recompute and re-apply the display LUT without re-executing the upstream correction stages (FR-IMG-01 through FR-IMG-04).

**Rationale:** Window/Level is a non-destructive display transformation that must support real-time interactive adjustment without re-triggering the computationally expensive correction pipeline.

---

#### FR-IMG-06: Noise Reduction Filtering

**When** requested by the processing configuration, **the system shall** apply a configurable noise reduction filter to the corrected image frame prior to Window/Level application.

**The system shall** support at least one of the following filter types as configured: Gaussian smoothing, median filtering, or bilateral filtering.

**Rationale:** Noise reduction improves signal-to-noise ratio for low-dose exposures, improving diagnostic confidence.

---

#### FR-IMG-07: Image Flattening

**When** requested by the processing configuration, **the system shall** apply a large-area background normalization (image flattening) to compensate for residual low-frequency shading artifacts not fully corrected by gain correction.

**Rationale:** Residual shading can be caused by heel effect, beam hardening, or imperfect flat-field calibration. Flattening corrects these to ensure uniform background intensity.

---

#### FR-IMG-08: Pluggable Engine Architecture

**The system shall** define and enforce a stable C++ abstract interface (`IImageProcessingEngine`) that encapsulates all image correction and processing operations, such that the host pipeline can load any conformant engine implementation at runtime without modification to the host pipeline, upstream acquisition modules, or downstream viewer modules.

**If** an engine plugin fails to load or initialization fails, **the system shall** fall back to the built-in default engine and report the failure to the caller without crashing.

**Rationale:** Different clinical deployments or regulatory environments may require different processing implementations. The pluggable interface enables substitution without recompilation of the entire system.

---

#### FR-IMG-09: Parallel and External Processing Module Integration

**Where** a parallel or external processing module path is configured, **the system shall** route the image buffer to the external module for processing, wait for the result within the configured timeout, and accept the processed buffer as the output of the corresponding pipeline stage.

**If** the external processing module does not respond within the configured timeout, **the system shall** either fall back to the built-in engine for that stage or propagate a timeout error to the caller, as configured.

**Rationale:** External processing modules (e.g., AI-based post-processing, vendor-specific correction) must integrate without requiring changes to the pipeline architecture.

---

#### FR-IMG-10: Calibration Data Management

**The system shall** load, validate, and cache calibration data sets (dark frame, gain map, defect pixel map, scatter correction parameters) from the configured calibration data storage location at pipeline initialization.

**When** a calibration data set is loaded, **the system shall** verify its integrity by checking stored checksums or metadata signatures and reject any data set that fails integrity validation.

**When** a new calibration data set is available, **the system shall** support hot-reload of calibration data without stopping image acquisition, applying the new calibration to frames processed after the reload point.

**Rationale:** Calibration data is acquired periodically. Hot-reload prevents interruption of clinical workflow during recalibration.

---

#### FR-IMG-11: Raw Image Preservation

**When** a frame is processed by the full pipeline, **the system shall** preserve the original unmodified raw frame buffer alongside the processed output frame and make both available to the caller.

**The system shall not** overwrite or discard the raw frame buffer during any stage of processing.

**Rationale:** IEC 62304 Class B requirements, audit traceability, and the need to re-process images with different calibration or engine settings require access to the unaltered raw source.

---

### 3.2 Non-Functional Requirements

#### NFR-IMG-01: Full Pipeline Latency

**The system shall** complete the full processing pipeline (FR-IMG-01 through FR-IMG-05, with scatter correction disabled) for a single frame within 2,000 milliseconds (2 seconds) measured from raw buffer receipt to processed buffer availability, on the reference hardware specification.

---

#### NFR-IMG-02: Preview Latency

**While** operating in preview/live-fluoroscopy mode, **the system shall** complete a reduced processing pipeline (offset correction, gain correction, window/level only) and deliver a preview-quality frame within 500 milliseconds of raw buffer receipt.

---

#### NFR-IMG-03: Engine Replaceability

**The system shall** be architecturally designed such that replacing the active engine plugin DLL requires no changes to source code in any module outside of `libs/hnvue-imaging/engines/`, and no recompilation of the host application or any upstream or downstream module is required.

---

#### NFR-IMG-04: 16-Bit Pixel Depth Preservation

**The system shall** maintain 16-bit grayscale pixel depth for all intermediate and final processed buffers throughout the entire pipeline. Intermediate computation may use 32-bit floating point or 32-bit integer precision internally, but all ImageBuffer objects stored in memory and passed between pipeline stages shall be 16-bit grayscale.

---

#### NFR-IMG-05: Memory Efficiency

**The system shall** minimize unnecessary full-frame buffer copies during pipeline processing. Each processing stage shall operate in-place on the ImageBuffer where the algorithm allows, or shall reuse pre-allocated staging buffers. Allocation of new full-frame buffers shall not occur during the processing of every frame after initialization.

---

## 4. Specifications

### 4.1 IImageProcessingEngine Interface

This interface is the central architectural element of SPEC-IMAGING-001. All image correction and processing operations are accessed exclusively through this interface. The host pipeline holds a `std::unique_ptr<IImageProcessingEngine>` and has no knowledge of the concrete implementation.

#### 4.1.1 Interface Contract

```
Interface: IImageProcessingEngine
Location: libs/hnvue-imaging/IImageProcessingEngine.h
Linkage: Pure C++ abstract base class
Ownership: Managed by host pipeline via std::unique_ptr
```

#### 4.1.2 Interface Operations

| Method Signature | Inputs | Output | Description |
|------------------|--------|--------|-------------|
| `ApplyOffsetCorrection(ImageBuffer& frame, const CalibrationData& dark)` | Frame buffer (in/out), dark calibration data | `bool` success | Subtracts dark frame from raw frame in-place. Clamps to zero. |
| `ApplyGainCorrection(ImageBuffer& frame, const CalibrationData& gain)` | Frame buffer (in/out), gain calibration data | `bool` success | Multiplies each pixel by the corresponding gain coefficient in-place. |
| `ApplyDefectPixelMap(ImageBuffer& frame, const DefectMap& map)` | Frame buffer (in/out), defect map | `bool` success | Replaces defective pixel values using neighbor interpolation in-place. |
| `ApplyScatterCorrection(ImageBuffer& frame, const ScatterParams& params)` | Frame buffer (in/out), scatter parameters | `bool` success | Applies frequency-domain scatter correction. Pass-through if correction is disabled in params. |
| `ApplyWindowLevel(ImageBuffer& frame, float window, float level)` | Frame buffer (in/out), window width, window center | `bool` success | Applies window/level LUT mapping. Reentrant for interactive adjustment. |
| `ApplyNoiseReduction(ImageBuffer& frame, const NoiseReductionConfig& config)` | Frame buffer (in/out), filter configuration | `bool` success | Applies the configured noise reduction filter. |
| `ApplyFlattening(ImageBuffer& frame, const FlatteningConfig& config)` | Frame buffer (in/out), flattening configuration | `bool` success | Applies large-area background normalization. |
| `ProcessFrame(ImageBuffer& frame, const ProcessingConfig& config)` | Frame buffer (in/out), complete processing configuration | `bool` success | Executes the complete correction pipeline in sequence using the provided configuration. Convenience aggregator. |
| `Initialize(const EngineConfig& config)` | Engine configuration | `bool` success | Initializes the engine with resource allocation and validation. Must be called once before any processing method. |
| `Shutdown()` | None | `void` | Releases all resources held by the engine. Called by the host before destroying the engine instance. |
| `GetEngineInfo()` | None | `EngineInfo` | Returns engine identification string, version, and capability flags. |

#### 4.1.3 Factory Method

The engine is created exclusively via a static factory method that loads the implementation from a plugin DLL path. The host pipeline never constructs a concrete engine class directly.

```
Static Factory:
  IImageProcessingEngine::Create(const std::string& engine_plugin_path)
  Returns: std::unique_ptr<IImageProcessingEngine>
  Throws: EngineLoadException if DLL cannot be loaded or interface cannot be resolved
  Default: If engine_plugin_path is empty string, creates the built-in default engine without DLL loading.
```

#### 4.1.4 Plugin DLL ABI Contract

Each engine plugin DLL must export the following C-linkage factory function. The `extern "C"` linkage prevents C++ name-mangling and ensures ABI compatibility across different compilers.

```
Export Name:  CreateImageProcessingEngine
Signature:    IImageProcessingEngine* CreateImageProcessingEngine()
Linkage:      extern "C"
Ownership:    Caller (host pipeline) takes ownership; must delete via DestroyImageProcessingEngine
Companion:    DestroyImageProcessingEngine(IImageProcessingEngine* engine)
```

The `DestroyImageProcessingEngine` export is required to ensure that the engine object is destroyed in the same DLL's heap, preventing cross-DLL heap corruption.

---

### 4.2 Core Data Structures

#### 4.2.1 ImageBuffer

```
Struct: ImageBuffer
Purpose: Represents a single 16-bit grayscale image frame

Fields:
  width       : uint32_t     - Frame width in pixels
  height      : uint32_t     - Frame height in pixels
  pixel_depth : uint8_t      - Fixed value: 16
  stride      : uint32_t     - Row stride in bytes (>= width * 2)
  data        : uint16_t*    - Pointer to pixel data (row-major, no alignment padding unless stride > width * 2)
  timestamp   : uint64_t     - Frame acquisition timestamp (microseconds since epoch)
  frame_id    : uint64_t     - Monotonically increasing frame sequence number

Ownership:
  The pipeline allocates the data buffer.
  IImageProcessingEngine operations must never free or reallocate the data pointer.
  The engine operates in-place on the provided buffer.
```

#### 4.2.2 CalibrationData

```
Struct: CalibrationData
Purpose: Encapsulates a single calibration frame (dark or gain map)

Fields:
  type          : CalibrationDataType   - Enum: DARK_FRAME, GAIN_MAP
  width         : uint32_t             - Must match ImageBuffer width
  height        : uint32_t             - Must match ImageBuffer height
  data_f32      : float*               - Pre-processed calibration coefficients in 32-bit float
  checksum      : uint64_t             - CRC-64 or SHA-256 of raw calibration source data
  acquisition_time : uint64_t          - Timestamp of calibration acquisition
  valid         : bool                 - True if integrity check passed at load time
```

#### 4.2.3 DefectMap

```
Struct: DefectMap
Purpose: Identifies defective pixel locations

Fields:
  count         : uint32_t             - Number of defective pixels
  pixels        : DefectPixelEntry*    - Array of defective pixel entries
  checksum      : uint64_t             - Integrity checksum
  valid         : bool                 - True if integrity check passed

Struct: DefectPixelEntry
Fields:
  x             : uint32_t             - Column index (0-based)
  y             : uint32_t             - Row index (0-based)
  type          : DefectPixelType      - Enum: DEAD_PIXEL, HOT_PIXEL, CLUSTER
  interpolation : InterpolationMethod  - Enum: BILINEAR, NEAREST_NEIGHBOR, MEDIAN_3X3
```

#### 4.2.4 ScatterParams

```
Struct: ScatterParams
Purpose: Parameters for virtual anti-scatter grid correction

Fields:
  enabled           : bool             - If false, scatter correction stage is a pass-through
  algorithm         : ScatterAlgorithm - Enum: FREQUENCY_DOMAIN_FFT, POLYNOMIAL_FIT
  cutoff_frequency  : float            - Normalized spatial frequency cutoff (0.0–1.0)
  suppression_ratio : float            - Scatter-to-primary ratio correction factor
  fftw_plan_flags   : unsigned int     - FFTW planning flags (FFTW_MEASURE, FFTW_ESTIMATE)
```

#### 4.2.5 ProcessingConfig

```
Struct: ProcessingConfig
Purpose: Complete configuration for a single full-pipeline ProcessFrame call

Fields:
  calibration_dark    : const CalibrationData*   - Dark frame calibration (required)
  calibration_gain    : const CalibrationData*   - Gain map calibration (required)
  defect_map          : const DefectMap*          - Defect pixel map (required)
  scatter             : ScatterParams             - Scatter correction parameters
  window              : float                     - Window width for display LUT
  level               : float                     - Window center for display LUT
  noise_reduction     : NoiseReductionConfig      - Noise reduction settings
  flattening          : FlatteningConfig          - Image flattening settings
  mode                : ProcessingMode            - Enum: FULL_PIPELINE, PREVIEW
  preserve_raw        : bool                      - Must be true; raw buffer preserved in RawBuffer output
```

#### 4.2.6 EngineInfo

```
Struct: EngineInfo
Purpose: Engine identification and capability advertisement

Fields:
  engine_name         : std::string    - Human-readable engine name
  engine_version      : std::string    - Semantic version string (e.g., "1.2.3")
  vendor              : std::string    - Vendor or organization name
  capabilities        : uint64_t       - Bitmask of EngineCapabilityFlags
  api_version         : uint32_t       - Interface API version the engine targets

Enum: EngineCapabilityFlags (bitmask)
  CAP_OFFSET_CORRECTION     = 0x0001
  CAP_GAIN_CORRECTION       = 0x0002
  CAP_DEFECT_PIXEL_MAP      = 0x0004
  CAP_SCATTER_CORRECTION    = 0x0008
  CAP_WINDOW_LEVEL          = 0x0010
  CAP_NOISE_REDUCTION       = 0x0020
  CAP_FLATTENING            = 0x0040
  CAP_PREVIEW_MODE          = 0x0080
  CAP_GPU_ACCELERATION      = 0x0100
  CAP_PARALLEL_FRAMES       = 0x0200
```

---

### 4.3 Processing Pipeline Sequence

The defined processing sequence is fixed. Stages must be executed in this order within a full pipeline invocation:

```
Stage 1: Offset Correction       (FR-IMG-01) - Always applied
Stage 2: Gain Correction         (FR-IMG-02) - Always applied
Stage 3: Defect Pixel Mapping    (FR-IMG-03) - Always applied
Stage 4: Scatter Correction      (FR-IMG-04) - Protocol-conditional (pass-through if disabled)
Stage 5: Noise Reduction         (FR-IMG-06) - Configuration-conditional
Stage 6: Image Flattening        (FR-IMG-07) - Configuration-conditional
Stage 7: Window / Level          (FR-IMG-05) - Always applied
```

Preview mode (NFR-IMG-02) executes a reduced sequence:

```
Preview Stage 1: Offset Correction   (FR-IMG-01)
Preview Stage 2: Gain Correction     (FR-IMG-02)
Preview Stage 3: Window / Level      (FR-IMG-05)
```

---

### 4.4 Calibration Data Management

#### 4.4.1 Storage Format

Calibration data files shall be stored in a defined binary format with a header block containing:

- Magic number (4 bytes): Identifies file as HnVue calibration data
- Format version (2 bytes): Enables forward compatibility
- Data type enum (2 bytes): DARK_FRAME, GAIN_MAP, DEFECT_MAP, SCATTER_PARAMS
- Detector width (4 bytes) and height (4 bytes)
- Acquisition timestamp (8 bytes, microseconds since epoch)
- SHA-256 checksum (32 bytes) of the data payload
- Data payload length (8 bytes)
- Data payload (variable length)

#### 4.4.2 Validation Rules

**When** calibration data is loaded from storage, **the system shall** reject the data set and report `CalibrationValidationError` if any of the following conditions are true:

- The magic number does not match the expected value.
- The format version is not supported by the current library version.
- The data type does not match the expected type for the loading context.
- The detector dimensions (width, height) do not match the configured frame dimensions.
- The SHA-256 checksum of the data payload does not match the stored checksum.
- The acquisition timestamp is more than the configured maximum age in days (configurable, default: 90 days).

#### 4.4.3 Calibration Manager

The `CalibrationManager` class within `libs/hnvue-imaging/` provides:

- `LoadDarkFrame(const std::string& path)` - Loads and validates a dark frame calibration file
- `LoadGainMap(const std::string& path)` - Loads and validates a gain map file
- `LoadDefectMap(const std::string& path)` - Loads and validates a defect pixel map file
- `LoadScatterParams(const std::string& path)` - Loads scatter correction parameters
- `HotReload(CalibrationDataType type, const std::string& path)` - Replaces an active calibration dataset; thread-safe; applies atomically to frames after the reload point
- `GetCalibrationStatus()` - Returns validity and timestamp for each loaded dataset

---

### 4.5 Raw Image Preservation Contract

**The system shall** maintain a `RawBuffer` object alongside the processed `ImageBuffer` for every frame entering the full pipeline. The `RawBuffer` shall contain a deep copy of the raw pixel data made before any processing stage executes.

```
Struct: ProcessedFrameResult
Purpose: Output of a full pipeline ProcessFrame operation

Fields:
  processed_frame : ImageBuffer      - The fully processed, display-ready frame
  raw_frame       : ImageBuffer      - A copy of the original unmodified raw frame
  processing_time_us : uint64_t      - Total pipeline execution time in microseconds
  stages_applied  : uint64_t         - Bitmask of PipelineStageFlags for stages that ran
  engine_info     : EngineInfo       - Information about the engine that processed this frame
```

---

### 4.6 Error Handling Contract

All `IImageProcessingEngine` interface methods return `bool`. A return value of `false` indicates a processing failure. The engine shall provide a complementary method:

- `GetLastError()` - Returns an `EngineError` struct containing an error code, human-readable message, and the stage at which the error occurred.

**If** any pipeline stage returns `false`, **the system shall** stop execution of subsequent stages, preserve the raw frame, and return a `ProcessedFrameResult` with `stages_applied` indicating which stages completed before the failure.

**The system shall not** throw C++ exceptions across the DLL boundary. All error signaling shall use the return-value and `GetLastError()` pattern to ensure ABI stability across compiler versions.

---

### 4.7 Thread Safety

- `IImageProcessingEngine` instances are **not** required to be thread-safe. The host pipeline shall not call processing methods on the same engine instance concurrently.
- The `CalibrationManager::HotReload` method **is** required to be thread-safe and shall use appropriate synchronization to ensure that an in-progress frame processing operation completes with the old calibration data while new frames after the reload use the new calibration data.
- The `IImageProcessingEngine::Create` factory method is thread-safe.

---

### 4.8 Performance Requirements and Measurement

#### 4.8.1 Reference Hardware Profile

Performance requirements (NFR-IMG-01, NFR-IMG-02) shall be measured on a system meeting the following minimum specification:

- CPU: Intel Core i7 (8th generation or later) or AMD Ryzen 5 3000 series or equivalent
- RAM: 16 GB DDR4
- OS: Windows 10 64-bit or Windows 11 64-bit
- No GPU acceleration assumed for baseline measurement

#### 4.8.2 Profiling Points

The pipeline shall expose internal timing measurements for each stage via the `ProcessedFrameResult.stages_applied` bitmask and a companion `stages_timing_us` array containing per-stage execution times in microseconds. This enables identification of bottleneck stages without instrumentation of internal engine code.

---

### 4.9 IEC 62304 Class B Traceability

| Requirement ID | SPEC Section | Functional Requirement                         |
|----------------|--------------|------------------------------------------------|
| FR-IMG-01      | 3.1, 4.3     | Offset Correction                              |
| FR-IMG-02      | 3.1, 4.3     | Gain Correction                                |
| FR-IMG-03      | 3.1, 4.3     | Defect Pixel Mapping                           |
| FR-IMG-04      | 3.1, 4.3     | Scatter Correction                             |
| FR-IMG-05      | 3.1, 4.3     | Window/Level Adjustment                        |
| FR-IMG-06      | 3.1, 4.3     | Noise Reduction                                |
| FR-IMG-07      | 3.1, 4.3     | Image Flattening                               |
| FR-IMG-08      | 3.1, 4.1     | Pluggable Engine Architecture                  |
| FR-IMG-09      | 3.1, 4.1     | Parallel/External Module Integration           |
| FR-IMG-10      | 3.1, 4.4     | Calibration Data Management                    |
| FR-IMG-11      | 3.1, 4.5     | Raw Image Preservation                         |
| NFR-IMG-01     | 3.2, 4.8     | Full Pipeline Latency < 2 seconds              |
| NFR-IMG-02     | 3.2, 4.8     | Preview Latency < 500 ms                       |
| NFR-IMG-03     | 3.2, 4.1     | Engine Replaceability (no upstream/downstream changes) |
| NFR-IMG-04     | 3.2, 4.2.1   | 16-bit Grayscale Throughout                    |
| NFR-IMG-05     | 3.2, 4.5     | Memory Efficiency (no unnecessary copies)      |

---

## 5. Out of Scope

The following items are explicitly out of scope for SPEC-IMAGING-001:

- Image acquisition and raw frame delivery from the FPGA detector (separate SPEC)
- DICOM encoding, storage, and transmission (separate SPEC)
- Diagnostic viewer rendering and display (separate SPEC)
- GPU/CUDA acceleration of processing stages (future enhancement)
- Dose calculation and dose area product measurement (separate SPEC)
- Patient data management (separate SPEC)
- Calibration acquisition workflow (acquiring dark frames and flood fields) (separate SPEC)

---

## 6. Related Documents

| Document                          | Relationship                                               |
|-----------------------------------|------------------------------------------------------------|
| IEC 62304:2006+AMD1:2015          | Software life cycle standard; this component is Class B    |
| IEC 62304 Class B Software Plan   | Governs development process for this library               |
| HnVue Architecture Overview       | System context for this pipeline                          |
| OpenCV 4.x C++ API Reference      | Core processing library                                    |
| FFTW 3.x Documentation            | FFT library used for scatter correction                   |
| SPEC-WORKFLOW-001                  | Acquisition workflow, triggers image acquisition for this pipeline |
| SPEC-UI-001                        | Diagnostic viewer (WPF GUI), consumes processed frames from this pipeline |
| SPEC-IMAGING-001 (this document)   | Calibration data lifecycle is managed internally by CalibrationManager (Section 4.4) |
