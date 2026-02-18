/**
 * @file HalTypes.h
 * @brief Shared data types and enumerations for HnVue HAL interfaces
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B/C - HAL foundation types
 * SPDX-License-Identifier: MIT
 */

#ifndef HNUE_HAL_HAL_TYPES_H
#define HNUE_HAL_HAL_TYPES_H

#include <cstdint>
#include <functional>
#include <string>
#include <vector>

namespace hnvue::hal {

// =============================================================================
// Error Codes
// =============================================================================

/**
 * @brief HAL error codes
 *
 * All error conditions exposed through the HAL use these codes.
 * HAL_OK (0) indicates success.
 */
enum class HalError : int32_t {
    HAL_OK = 0,              ///< Success
    HAL_ERR_TIMEOUT = 1,     ///< Command response timeout
    HAL_ERR_COMM = 2,        ///< Communication channel failure
    HAL_ERR_PLUGIN = 3,      ///< Plugin load or ABI error
    HAL_ERR_PARAM = 4,       ///< Invalid parameter value
    HAL_ERR_STATE = 5,       ///< Command invalid in current device state
    HAL_ERR_HARDWARE = 6,    ///< Physical hardware fault
    HAL_ERR_ABORT = 7,       ///< Operation aborted by request
    HAL_ERR_NOT_SUPPORTED = 8 ///< Operation not supported by hardware
};

// =============================================================================
// HVG Types (from hvg_control.proto)
// =============================================================================

/**
 * @brief Automatic Exposure Control mode
 */
enum class AecMode : int32_t {
    AEC_MODE_UNSPECIFIED = 0,
    AEC_MANUAL = 1,
    AEC_AUTO = 2
};

/**
 * @brief Generator state enumeration
 */
enum class GeneratorState : int32_t {
    GEN_STATE_UNSPECIFIED = 0,
    GEN_IDLE = 1,
    GEN_READY = 2,
    GEN_ARMED = 3,
    GEN_EXPOSING = 4,
    GEN_ERROR = 5
};

/**
 * @brief Alarm severity levels
 */
enum class AlarmSeverity : int32_t {
    ALARM_SEVERITY_UNSPECIFIED = 0,
    ALARM_INFO = 1,
    ALARM_WARNING = 2,
    ALARM_ERROR = 3,
    ALARM_CRITICAL = 4
};

/**
 * @brief Exposure parameters
 */
struct ExposureParams {
    float kvp = 0.0f;         ///< kV: range 40.0–150.0
    float ma = 0.0f;          ///< mA: range 0.1–1000.0
    float ms = 0.0f;          ///< Exposure time in milliseconds: 1–10000
    float mas = 0.0f;         ///< mAs (alternative to ma+ms)
    AecMode aec_mode = AecMode::AEC_MANUAL;
    std::string focus;        ///< "large" | "small"
};

/**
 * @brief Generic HVG response
 */
struct HvgResponse {
    bool success = false;
    std::string error_msg;
    int32_t error_code = 0;
};

/**
 * @brief Result of exposure operation
 */
struct ExposureResult {
    bool success = false;
    float actual_kvp = 0.0f;
    float actual_ma = 0.0f;
    float actual_ms = 0.0f;
    float actual_mas = 0.0f;
    std::string error_msg;
};

/**
 * @brief Real-time HVG status
 */
struct HvgStatus {
    float actual_kvp = 0.0f;
    float actual_ma = 0.0f;
    GeneratorState state = GeneratorState::GEN_STATE_UNSPECIFIED;
    bool interlock_ok = false;
    int64_t timestamp_us = 0;  ///< microseconds since epoch
};

/**
 * @brief Alarm event message
 */
struct HvgAlarm {
    int32_t alarm_code = 0;
    std::string description;
    AlarmSeverity severity = AlarmSeverity::ALARM_SEVERITY_UNSPECIFIED;
    int64_t timestamp_us = 0;
};

/**
 * @brief HVG device capabilities
 */
struct HvgCapabilities {
    float min_kvp = 0.0f;
    float max_kvp = 0.0f;
    float min_ma = 0.0f;
    float max_ma = 0.0f;
    float min_ms = 0.0f;
    float max_ms = 0.0f;
    bool has_aec = false;
    bool has_dual_focus = false;
    std::string vendor_name;
    std::string model_name;
    std::string firmware_version;
};

// =============================================================================
// Detector Types (from detector_acquisition.proto)
// =============================================================================

/**
 * @brief Acquisition mode enumeration
 */
enum class AcquisitionMode : int32_t {
    ACQUISITION_MODE_UNSPECIFIED = 0,
    MODE_STATIC = 1,
    MODE_CONTINUOUS = 2,
    MODE_TRIGGERED = 3
};

/**
 * @brief Calibration type enumeration
 */
enum class CalibType : int32_t {
    CALIB_TYPE_UNSPECIFIED = 0,
    CALIB_DARK_FIELD = 1,
    CALIB_FLAT_FIELD = 2,
    CALIB_DEFECT_MAP = 3
};

/**
 * @brief Acquisition configuration parameters
 */
struct AcquisitionConfig {
    AcquisitionMode mode = AcquisitionMode::ACQUISITION_MODE_UNSPECIFIED;
    int32_t num_frames = 0;    ///< 0 = continuous
    float frame_rate = 0.0f;   ///< frames per second
    int32_t binning = 1;       ///< 1 | 2 | 4
    std::string session_id;
};

/**
 * @brief Raw detector frame data
 */
struct RawFrame {
    int64_t sequence_number = 0;
    int64_t timestamp_us = 0;
    int32_t width = 0;
    int32_t height = 0;
    int32_t bit_depth = 0;
    std::vector<uint8_t> pixel_data;  ///< row-major, native byte order
    std::string session_id;
};

/**
 * @brief Generic detector response
 */
struct DetectorResponse {
    bool success = false;
    std::string error_msg;
    int32_t error_code = 0;
};

/**
 * @brief Calibration result
 */
struct CalibrationResult {
    bool success = false;
    std::string output_path;
    std::string error_msg;
};

/**
 * @brief Detector information
 */
struct DetectorInfo {
    std::string vendor;
    std::string model;
    std::string serial_number;
    int32_t pixel_width = 0;
    int32_t pixel_height = 0;
    float pixel_pitch_um = 0.0f;
    int32_t max_bit_depth = 0;
    float max_frame_rate = 0.0f;
    std::string firmware_version;
};

/**
 * @brief Detector status
 */
struct DetectorStatus {
    bool is_acquiring = false;
    std::string current_session_id;
    int32_t frames_acquired = 0;
    float temperature_c = 0.0f;
};

// =============================================================================
// Additional Hardware Types
// =============================================================================

/**
 * @brief Collimator blade position in millimeters
 */
struct CollimatorPosition {
    float left = 0.0f;   ///< mm from center
    float right = 0.0f;  ///< mm from center
    float top = 0.0f;    ///< mm from center
    float bottom = 0.0f; ///< mm from center
};

/**
 * @brief Patient table position in millimeters
 */
struct TablePosition {
    float longitudinal = 0.0f;  ///< mm from isocenter
    float lateral = 0.0f;       ///< mm from isocenter
    float height = 0.0f;        ///< mm from floor
};

/**
 * @brief AEC termination event
 */
struct AecTerminationEvent {
    bool threshold_reached = false;
    float actual_dose_mgy = 0.0f;
    int64_t exposure_time_us = 0;
};

/**
 * @brief Dose reading
 */
struct DoseReading {
    float dose_mgy = 0.0f;        ///< Accumulated dose in mGy
    float dose_rate_mgy_s = 0.0f; ///< Dose rate in mGy/s
    float dap_ugy_cm2 = 0.0f;     ///< Dose Area Product in uGy*cm^2
    int64_t timestamp_us = 0;
};

// =============================================================================
// Safety Interlock Types
// =============================================================================

/**
 * @brief Unified interlock status aggregating all 9 interlocks
 *
 * Maps to WORKFLOW interlocks IL-01 through IL-09:
 * - IL-01: door_closed (X-ray room door closed)
 * - IL-02: emergency_stop_clear (Emergency stop not activated)
 * - IL-03: thermal_normal (No overtemperature condition)
 * - IL-04: generator_ready (Generator in ready state)
 * - IL-05: detector_ready (Detector acquisition ready)
 * - IL-06: collimator_valid (Collimator position valid)
 * - IL-07: table_locked (Patient table locked)
 * - IL-08: dose_within_limits (Dose accumulation within limits)
 * - IL-09: aec_configured (AEC mode properly configured)
 */
struct InterlockStatus {
    // Physical Safety Interlocks
    bool door_closed = false;          ///< IL-01: X-ray room door closed (hardware sensor)
    bool emergency_stop_clear = false; ///< IL-02: Emergency stop not activated (hardware sensor)
    bool thermal_normal = false;       ///< IL-03: No overtemperature condition

    // Device Readiness Interlocks
    bool generator_ready = false;      ///< IL-04: Generator in ready state
    bool detector_ready = false;       ///< IL-05: Detector acquisition ready
    bool collimator_valid = false;     ///< IL-06: Collimator position within valid range
    bool table_locked = false;         ///< IL-07: Patient table locked/stable
    bool dose_within_limits = false;   ///< IL-08: Dose accumulation within limits
    bool aec_configured = false;       ///< IL-09: AEC mode properly configured

    // Aggregate convenience
    bool all_passed = false;           ///< true only if all above are true
    uint64_t timestamp_us = 0;         ///< Timestamp of interlock check (microseconds)
};

// =============================================================================
// Plugin ABI Types
// =============================================================================

/**
 * @brief Plugin configuration passed to CreateDetector factory
 */
struct PluginConfig {
    std::string plugin_path;
    std::string config_file_path;
    void* user_context = nullptr;  ///< User-defined context pointer
};

/**
 * @brief Plugin manifest describing plugin capabilities
 */
struct PluginManifest {
    uint32_t api_version = 0;       ///< HAL API version (e.g., 0x01000000 for v1.0.0)
    uint32_t plugin_version = 0;    ///< Plugin version
    std::string plugin_name;
    std::string vendor_name;
    std::string model_name;
    uint32_t max_frame_width = 0;
    uint32_t max_frame_height = 0;
    float max_frame_rate = 0.0f;
};

// =============================================================================
// Callback Types
// =============================================================================

/// Callback for HVG alarm events
using AlarmCallback = std::function<void(const HvgAlarm&)>;

/// Callback for HVG status updates
using StatusCallback = std::function<void(const HvgStatus&)>;

/// Callback for detector frame delivery
using FrameCallback = std::function<void(const RawFrame&)>;

/// Callback for collimator position changes
using CollimatorCallback = std::function<void(const CollimatorPosition&)>;

/// Callback for table position changes
using TableCallback = std::function<void(const TablePosition&)>;

/// Callback for AEC termination events
using AecTerminationCallback = std::function<void(const AecTerminationEvent&)>;

/// Callback for dose updates
using DoseCallback = std::function<void(const DoseReading&)>;

/// Callback for interlock state changes
using InterlockCallback = std::function<void(const InterlockStatus&)>;

/// Callback for HVG command completion
using CommandCompletionCallback = std::function<void(const HvgResponse&)>;

} // namespace hnvue::hal

#endif // HNUE_HAL_HAL_TYPES_H
