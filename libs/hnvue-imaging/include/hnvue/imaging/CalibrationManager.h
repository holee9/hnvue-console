/**
 * @file CalibrationManager.h
 * @brief Calibration data management with loading, validation, and hot-reload
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Calibration data manager
 * SPDX-License-Identifier: MIT
 *
 * Manages calibration data lifecycle including loading from disk,
 * integrity validation, caching, and hot-reload support.
 */

#ifndef HNUE_IMAGING_CALIBRATION_MANAGER_H
#define HNUE_IMAGING_CALIBRATION_MANAGER_H

#include "ImagingTypes.h"

#include <string>
#include <memory>
#include <mutex>
#include <unordered_map>

namespace hnvue::imaging {

/**
 * @brief Calibration file format header
 *
 * Binary format header for calibration data files.
 */
#pragma pack(push, 1)
struct CalibrationFileHeader {
    uint8_t magic[4];          ///< Magic number: "HNC\1"
    uint16_t format_version;   ///< Format version (current: 1)
    uint16_t data_type;        ///< CalibrationDataType enum value
    uint32_t width;            ///< Detector width in pixels
    uint32_t height;           ///< Detector height in pixels
    uint64_t acquisition_time;///< Acquisition timestamp (microseconds)
    uint8_t checksum[32];      ///< SHA-256 of data payload
    uint64_t payload_length;   ///< Data payload length in bytes
};
#pragma pack(pop)

/**
 * @brief Calibration status information
 */
struct CalibrationStatus {
    bool dark_frame_loaded = false;
    bool gain_map_loaded = false;
    bool defect_map_loaded = false;
    bool scatter_params_loaded = false;

    uint64_t dark_frame_timestamp = 0;
    uint64_t gain_map_timestamp = 0;
    uint64_t defect_map_timestamp = 0;
    uint64_t scatter_params_timestamp = 0;

    bool dark_frame_valid = false;
    bool gain_map_valid = false;
    bool defect_map_valid = false;
    bool scatter_params_valid = false;
};

/**
 * @brief Calibration data manager
 *
 * Provides thread-safe loading, validation, and caching of calibration data.
 * Supports hot-reload for updating calibration during operation.
 */
class CalibrationManager {
public:
    /**
     * @brief Constructor
     * @param max_age_days Maximum calibration age in days (default: 90)
     */
    explicit CalibrationManager(uint32_t max_age_days = 90);

    /**
     * @brief Destructor
     */
    ~CalibrationManager();

    // Disable copy and move
    CalibrationManager(const CalibrationManager&) = delete;
    CalibrationManager& operator=(const CalibrationManager&) = delete;
    CalibrationManager(CalibrationManager&&) = delete;
    CalibrationManager& operator=(CalibrationManager&&) = delete;

    // =========================================================================
    // Loading Methods
    // =========================================================================

    /**
     * @brief Load dark frame calibration from file
     * @param path Path to calibration file
     * @return CalibrationData structure, with valid=false on error
     */
    CalibrationData LoadDarkFrame(const std::string& path);

    /**
     * @brief Load gain map calibration from file
     * @param path Path to calibration file
     * @return CalibrationData structure, with valid=false on error
     */
    CalibrationData LoadGainMap(const std::string& path);

    /**
     * @brief Load defect pixel map from file
     * @param path Path to calibration file
     * @return DefectMap structure, with valid=false on error
     */
    DefectMap LoadDefectMap(const std::string& path);

    /**
     * @brief Load scatter correction parameters from file
     * @param path Path to calibration file
     * @return ScatterParams structure
     */
    ScatterParams LoadScatterParams(const std::string& path);

    // =========================================================================
    // Hot-Reload Methods
    // =========================================================================

    /**
     * @brief Hot-reload calibration data (thread-safe)
     * @param type Type of calibration to reload
     * @param path Path to new calibration file
     * @return true if reload succeeded
     *
     * Replaces active calibration dataset atomically.
     * In-progress frame processing completes with old calibration,
     * new frames use the new calibration.
     */
    bool HotReload(CalibrationDataType type, const std::string& path);

    /**
     * @brief Get pointer to cached calibration data
     * @param type Type of calibration
     * @return Const pointer to cached data, or nullptr if not loaded
     *
     * Thread-safe. Returns pointer to cached calibration data.
     */
    const CalibrationData* GetCalibration(CalibrationDataType type) const;

    /**
     * @brief Get pointer to cached defect map
     * @return Const pointer to defect map, or nullptr if not loaded
     */
    const DefectMap* GetDefectMap() const;

    /**
     * @brief Get pointer to cached scatter parameters
     * @return Const pointer to scatter params, or nullptr if not loaded
     */
    const ScatterParams* GetScatterParams() const;

    // =========================================================================
    // Status Query
    // =========================================================================

    /**
     * @brief Get calibration status for all datasets
     * @return CalibrationStatus structure
     */
    CalibrationStatus GetCalibrationStatus() const;

    // =========================================================================
    // Configuration
    // =========================================================================

    /**
     * @brief Set maximum calibration age
     * @param days Maximum age in days
     */
    void SetMaxAge(uint32_t days);

    /**
     * @brief Set expected frame dimensions
     * @param width Frame width in pixels
     * @param height Frame height in pixels
     *
     * Calibration data will be validated against these dimensions.
     */
    void SetFrameDimensions(uint32_t width, uint32_t height);

private:
    // =========================================================================
    // Internal Helper Methods
    // =========================================================================

    /**
     * @brief Load calibration file header
     * @param path File path
     * @param header Output header structure
     * @return true if header loaded and validated
     */
    bool LoadHeader(const std::string& path, CalibrationFileHeader& header);

    /**
     * @brief Validate calibration header
     * @param header Header to validate
     * @param expected_type Expected data type
     * @return true if header is valid
     */
    bool ValidateHeader(const CalibrationFileHeader& header,
                        CalibrationDataType expected_type);

    /**
     * @brief Verify checksum of data payload
     * @param data Data buffer
     * @param length Data length
     * @param expected_checksum Expected SHA-256 checksum
     * @return true if checksum matches
     */
    bool VerifyChecksum(const uint8_t* data, uint64_t length,
                        const uint8_t* expected_checksum);

    /**
     * @brief Compute SHA-256 checksum
     * @param data Data buffer
     * @param length Data length
     * @param output Output buffer (32 bytes)
     */
    void ComputeChecksum(const uint8_t* data, uint64_t length,
                         uint8_t* output);

    // =========================================================================
    // Member Variables
    // =========================================================================

    mutable std::mutex mutex_;

    uint32_t max_age_days_;
    uint32_t expected_width_ = 0;
    uint32_t expected_height_ = 0;

    // Cached calibration data
    std::unordered_map<CalibrationDataType, CalibrationData> calibration_cache_;
    DefectMap defect_map_;
    ScatterParams scatter_params_;
};

} // namespace hnvue::imaging

#endif // HNUE_IMAGING_CALIBRATION_MANAGER_H
