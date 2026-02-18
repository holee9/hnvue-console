/**
 * @file ImagingTypes.h
 * @brief Core data structures for HnVue image processing pipeline
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Image processing foundation types
 * SPDX-License-Identifier: MIT
 *
 * This file defines all core data structures used throughout the imaging
 * pipeline, including ImageBuffer, CalibrationData, DefectMap, and various
 * configuration structures. These types are passed across the DLL boundary
 * and must maintain ABI stability.
 */

#ifndef HNUE_IMAGING_IMAGING_TYPES_H
#define HNUE_IMAGING_IMAGING_TYPES_H

#include <cstdint>
#include <string>
#include <vector>

namespace hnvue::imaging {

// =============================================================================
// Error Codes
// =============================================================================

/**
 * @brief Imaging pipeline error codes
 */
enum class ImagingError : int32_t {
    IMAGING_OK = 0,              ///< Success
    IMAGING_ERR_INIT = 1,        ///< Initialization failed
    IMAGING_ERR_PARAM = 2,       ///< Invalid parameter
    IMAGING_ERR Calibration = 3, ///< Calibration data invalid or missing
    IMAGING_ERR_ENGINE = 4,      ///< Engine load or execution error
    IMAGING_ERR_TIMEOUT = 5,     ///< Operation timeout
    IMAGING_ERR_MEMORY = 6,      ///< Memory allocation failure
    IMAGING_ERR_NOT_SUPPORTED = 7 ///< Operation not supported
};

// =============================================================================
// Calibration Data Types
// =============================================================================

/**
 * @brief Calibration data type enumeration
 */
enum class CalibrationDataType : int32_t {
    CALIB_TYPE_UNSPECIFIED = 0,
    DARK_FRAME = 1,     ///< Dark frame (offset correction)
    GAIN_MAP = 2,       ///< Gain map (flat-field correction)
    DEFECT_MAP = 3,     ///< Defect pixel map
    SCATTER_PARAMS = 4  ///< Scatter correction parameters
};

// =============================================================================
// Defect Pixel Types
// =============================================================================

/**
 * @brief Defect pixel type classification
 */
enum class DefectPixelType : int32_t {
    DEFECT_TYPE_UNSPECIFIED = 0,
    DEAD_PIXEL = 1,     ///< Pixel with no response (under-response)
    HOT_PIXEL = 2,      ///< Pixel with excessive response
    CLUSTER = 3         ///< Group of adjacent defective pixels
};

/**
 * @brief Interpolation method for defect pixel correction
 */
enum class InterpolationMethod : int32_t {
    INTERP_UNSPECIFIED = 0,
    NEAREST_NEIGHBOR = 1,  ///< Use nearest valid pixel
    BILINEAR = 2,          ///< Bilinear interpolation from 4 neighbors
    MEDIAN_3X3 = 3         ///< Median of 3x3 neighborhood
};

// =============================================================================
// Scatter Correction Types
// =============================================================================

/**
 * @brief Scatter correction algorithm
 */
enum class ScatterAlgorithm : int32_t {
    SCATTER_ALGO_UNSPECIFIED = 0,
    FREQUENCY_DOMAIN_FFT = 1,  ///< FFT-based frequency domain filtering
    POLYNOMIAL_FIT = 2         ///< Polynomial background fitting
};

// =============================================================================
// Noise Reduction Types
// =============================================================================

/**
 * @brief Noise reduction filter type
 */
enum class NoiseFilterType : int32_t {
    NOISE_FILTER_UNSPECIFIED = 0,
    GAUSSIAN = 1,   ///< Gaussian smoothing
    MEDIAN = 2,     ///< Median filtering
    BILATERAL = 3   ///< Bilateral filtering (edge-preserving)
};

// =============================================================================
// Processing Mode
// =============================================================================

/**
 * @brief Processing mode enumeration
 */
enum class ProcessingMode : int32_t {
    MODE_UNSPECIFIED = 0,
    FULL_PIPELINE = 1,  ///< Complete correction pipeline
    PREVIEW = 2         ///< Reduced pipeline for real-time preview
};

// =============================================================================
// Core Data Structures
// =============================================================================

/**
 * @brief Single 16-bit grayscale image frame
 *
 * Represents a frame buffer containing 16-bit grayscale pixel data.
 * The pipeline operates in-place on this buffer when possible.
 *
 * Ownership:
 * - The caller allocates and owns the data buffer
 * - Processing engine never frees or reallocates the data pointer
 * - All operations modify the buffer in-place
 */
struct ImageBuffer {
    uint32_t width = 0;        ///< Frame width in pixels
    uint32_t height = 0;       ///< Frame height in pixels
    uint8_t pixel_depth = 16;  ///< Fixed at 16-bit grayscale
    uint32_t stride = 0;       ///< Row stride in bytes (>= width * 2)
    uint16_t* data = nullptr;  ///< Pointer to pixel data (row-major)
    uint64_t timestamp_us = 0; ///< Acquisition timestamp (microseconds since epoch)
    uint64_t frame_id = 0;     ///< Monotonically increasing sequence number
};

/**
 * @brief Calibration data container
 *
 * Encapsulates a single calibration dataset (dark frame or gain map).
 * Calibration coefficients are stored as 32-bit floats for precision.
 */
struct CalibrationData {
    CalibrationDataType type = CalibrationDataType::CALIB_TYPE_UNSPECIFIED;
    uint32_t width = 0;           ///< Must match ImageBuffer width
    uint32_t height = 0;          ///< Must match ImageBuffer height
    float* data_f32 = nullptr;    ///< Pre-processed calibration coefficients
    uint64_t checksum = 0;        ///< CRC-64 or SHA-256 of source data
    uint64_t acquisition_time_us = 0; ///< Calibration acquisition timestamp
    bool valid = false;           ///< True if integrity check passed

    /**
     * @brief Default constructor
     */
    CalibrationData() = default;

    /**
     * @brief Destructor - does NOT free data_f32 (caller-owned)
     */
    ~CalibrationData() = default;
};

/**
 * @brief Single defective pixel entry
 */
struct DefectPixelEntry {
    uint32_t x = 0;                      ///< Column index (0-based)
    uint32_t y = 0;                      ///< Row index (0-based)
    DefectPixelType type = DefectPixelType::DEFECT_TYPE_UNSPECIFIED;
    InterpolationMethod interpolation = InterpolationMethod::INTERP_UNSPECIFIED;
};

/**
 * @brief Defect pixel map
 *
 * Contains all known defective pixel locations for the detector.
 */
struct DefectMap {
    uint32_t count = 0;              ///< Number of defective pixels
    DefectPixelEntry* pixels = nullptr; ///< Array of defect entries
    uint64_t checksum = 0;           ///< Integrity checksum
    bool valid = false;              ///< True if integrity check passed

    /**
     * @brief Default constructor
     */
    DefectMap() = default;

    /**
     * @brief Destructor - does NOT free pixels array (caller-owned)
     */
    ~DefectMap() = default;
};

/**
 * @brief Scatter correction parameters
 *
 * Configures the virtual anti-scatter grid algorithm.
 */
struct ScatterParams {
    bool enabled = false;                    ///< If false, pass-through
    ScatterAlgorithm algorithm = ScatterAlgorithm::SCATTER_ALGO_UNSPECIFIED;
    float cutoff_frequency = 0.1f;           ///< Normalized frequency (0.0-1.0)
    float suppression_ratio = 0.5f;          ///< Scatter-to-primary ratio
    unsigned int fftw_plan_flags = 0;        ///< FFTW planning flags

    /**
     * @brief Default constructor - disabled scatter correction
     */
    ScatterParams() = default;
};

/**
 * @brief Noise reduction configuration
 */
struct NoiseReductionConfig {
    NoiseFilterType filter_type = NoiseFilterType::NOISE_FILTER_UNSPECIFIED;
    float kernel_size = 3.0f;      ///< Kernel size (odd number: 3, 5, 7...)
    float sigma = 1.0f;            ///< Sigma for Gaussian/Bilateral
    bool enabled = false;          ///< If false, noise reduction is skipped

    /**
     * @brief Default constructor - noise reduction disabled
     */
    NoiseReductionConfig() = default;
};

/**
 * @brief Image flattening configuration
 *
 * Configures large-area background normalization.
 */
struct FlatteningConfig {
    bool enabled = false;              ///< If false, flattening is skipped
    float polynomial_order = 2.0f;     ///< Order of polynomial fit
    float sigma_background = 50.0f;    ///< Background estimation sigma

    /**
     * @brief Default constructor - flattening disabled
     */
    FlatteningConfig() = default;
};

/**
 * @brief Complete processing configuration
 *
 * Aggregates all parameters needed for a full pipeline ProcessFrame call.
 */
struct ProcessingConfig {
    const CalibrationData* calibration_dark = nullptr;  ///< Dark frame (required)
    const CalibrationData* calibration_gain = nullptr;  ///< Gain map (required)
    const DefectMap* defect_map = nullptr;              ///< Defect map (required)
    ScatterParams scatter;                              ///< Scatter correction
    float window = 4000.0f;                             ///< Window width for display LUT
    float level = 2000.0f;                              ///< Window center for display LUT
    NoiseReductionConfig noise_reduction;               ///< Noise reduction
    FlatteningConfig flattening;                        ///< Image flattening
    ProcessingMode mode = ProcessingMode::FULL_PIPELINE; ///< Processing mode
    bool preserve_raw = true;                           ///< Must be true (FR-IMG-11)

    /**
     * @brief Default constructor
     */
    ProcessingConfig() = default;
};

/**
 * @brief Engine capability flags
 */
enum EngineCapabilityFlags : uint64_t {
    CAP_NONE = 0,
    CAP_OFFSET_CORRECTION = 0x0001,    ///< Dark frame subtraction
    CAP_GAIN_CORRECTION = 0x0002,      ///< Flat-field normalization
    CAP_DEFECT_PIXEL_MAP = 0x0004,     ///< Defect pixel correction
    CAP_SCATTER_CORRECTION = 0x0008,   ///< Virtual grid
    CAP_WINDOW_LEVEL = 0x0010,         ///< Window/Level LUT
    CAP_NOISE_REDUCTION = 0x0020,      ///< Noise filtering
    CAP_FLATTENING = 0x0040,           ///< Background normalization
    CAP_PREVIEW_MODE = 0x0080,         ///< Fast preview pipeline
    CAP_GPU_ACCELERATION = 0x0100,     ///< GPU acceleration available
    CAP_PARALLEL_FRAMES = 0x0200       ///< Parallel frame processing
};

/**
 * @brief Engine information structure
 *
 * Provides engine identification and capability advertisement.
 */
struct EngineInfo {
    std::string engine_name;      ///< Human-readable engine name
    std::string engine_version;   ///< Semantic version (e.g., "1.0.0")
    std::string vendor;           ///< Vendor or organization name
    uint64_t capabilities = 0;    ///< Bitmask of EngineCapabilityFlags
    uint32_t api_version = 0;     ///< Interface API version

    /**
     * @brief Default constructor
     */
    EngineInfo() = default;

    /**
     * @brief Check if a capability is supported
     * @param cap Capability flag to check
     * @return true if the capability is supported
     */
    inline bool HasCapability(EngineCapabilityFlags cap) const {
        return (capabilities & static_cast<uint64_t>(cap)) != 0;
    }
};

/**
 * @brief Engine error information
 *
 * Provides details about the last error that occurred.
 */
struct EngineError {
    ImagingError error_code = ImagingError::IMAGING_OK; ///< Error code
    std::string error_message;                           ///< Human-readable message
    std::string failed_stage;                           ///< Pipeline stage that failed

    /**
     * @brief Default constructor - no error
     */
    EngineError() = default;

    /**
     * @brief Check if there is an error
     * @return true if error_code is not IMAGING_OK
     */
    inline bool HasError() const {
        return error_code != ImagingError::IMAGING_OK;
    }
};

/**
 * @brief Pipeline stage flags for tracking
 */
enum PipelineStageFlags : uint64_t {
    STAGE_NONE = 0,
    STAGE_OFFSET_CORRECTION = 0x0001,
    STAGE_GAIN_CORRECTION = 0x0002,
    STAGE_DEFECT_PIXEL_MAP = 0x0004,
    STAGE_SCATTER_CORRECTION = 0x0008,
    STAGE_NOISE_REDUCTION = 0x0010,
    STAGE_FLATTENING = 0x0020,
    STAGE_WINDOW_LEVEL = 0x0040,
    STAGE_ALL = 0xFFFF
};

/**
 * @brief Result of a full pipeline ProcessFrame operation
 *
 * Contains both the processed output and the preserved raw input
 * along with timing and diagnostic information.
 */
struct ProcessedFrameResult {
    ImageBuffer processed_frame;      ///< Fully processed, display-ready frame
    ImageBuffer raw_frame;            ///< Copy of original unmodified raw frame
    uint64_t processing_time_us = 0;  ///< Total execution time (microseconds)
    uint64_t stages_applied = 0;      ///< Bitmask of PipelineStageFlags
    EngineInfo engine_info;           ///< Engine that processed the frame

    /**
     * @brief Default constructor
     */
    ProcessedFrameResult() = default;

    /**
     * @brief Check if a specific stage was applied
     * @param stage Stage flag to check
     * @return true if the stage was applied
     */
    inline bool HasStage(PipelineStageFlags stage) const {
        return (stages_applied & static_cast<uint64_t>(stage)) != 0;
    }
};

/**
 * @brief Engine configuration for initialization
 */
struct EngineConfig {
    uint32_t max_frame_width = 0;    ///< Maximum frame width to support
    uint32_t max_frame_height = 0;   ///< Maximum frame height to support
    std::string calibration_path;    ///< Path to calibration data directory
    bool enable_gpu = false;         ///< Request GPU acceleration if available
    uint32_t num_threads = 0;        ///< Number of processing threads (0 = auto)

    /**
     * @brief Default constructor
     */
    EngineConfig() = default;
};

// =============================================================================
// Stage Timing Information (for performance profiling)
// =============================================================================

/**
 * @brief Per-stage timing information
 *
 * Provides detailed timing breakdown for each pipeline stage.
 * Used for performance profiling and bottleneck identification.
 */
struct StageTiming {
    uint64_t offset_correction_us = 0;
    uint64_t gain_correction_us = 0;
    uint64_t defect_pixel_map_us = 0;
    uint64_t scatter_correction_us = 0;
    uint64_t noise_reduction_us = 0;
    uint64_t flattening_us = 0;
    uint64_t window_level_us = 0;

    /**
     * @brief Get total processing time
     * @return Sum of all stage times
     */
    inline uint64_t Total() const {
        return offset_correction_us + gain_correction_us +
               defect_pixel_map_us + scatter_correction_us +
               noise_reduction_us + flattening_us + window_level_us;
    }
};

} // namespace hnvue::imaging

#endif // HNUE_IMAGING_IMAGING_TYPES_H
