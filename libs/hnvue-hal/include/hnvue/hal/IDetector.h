/**
 * @file IDetector.h
 * @brief Pure abstract interface for vendor detector adapter plugins
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Detector acquisition interface
 * SPDX-License-Identifier: MIT
 *
 * This interface defines the complete contract for vendor detector adapter
 * plugins. All implementations must be pure abstract (no non-virtual methods)
 * to satisfy NFR-HAL-03 mockability requirements for SOUP-isolated unit testing.
 */

#ifndef HNUE_HAL_IDETECTOR_H
#define HNUE_HAL_IDETECTOR_H

#include "HalTypes.h"

#include <memory>

namespace hnvue::hal {

/**
 * @brief Pure abstract interface for detector control
 *
 * Vendor detector SDK adapter plugins implement this interface.
 * The interface is designed to be fully mockable for unit testing.
 *
 * Thread Safety:
 * - GetDetectorInfo() and GetStatus() are thread-safe for concurrent calls
 * - StartAcquisition() and StopAcquisition() are internally synchronized
 * - Frame callbacks are invoked on a background thread
 */
class IDetector {
public:
    /**
     * @brief Virtual destructor for proper cleanup
     */
    virtual ~IDetector() = default;

    // =========================================================================
    // Query Methods
    // =========================================================================

    /**
     * @brief Get static detector information
     * @return DetectorInfo structure with vendor, model, and capabilities
     *
     * Thread-safe, non-blocking. Returns cached information available
     * immediately after plugin initialization.
     */
    virtual DetectorInfo GetDetectorInfo() = 0;

    /**
     * @brief Get current detector acquisition status
     * @return DetectorStatus structure with current state
     *
     * Thread-safe, non-blocking.
     */
    virtual DetectorStatus GetStatus() = 0;

    // =========================================================================
    // Acquisition Control
    // =========================================================================

    /**
     * @brief Start a new acquisition session
     * @param cfg Acquisition configuration parameters
     * @return true if acquisition started successfully
     *
     * Must be called before any frames are delivered.
     * If an acquisition is already active, returns false with error.
     *
     * Thread-safe, internally synchronized.
     */
    virtual bool StartAcquisition(const AcquisitionConfig& cfg) = 0;

    /**
     * @brief Stop the current acquisition session
     * @return true if acquisition stopped successfully
     *
     * Stops frame delivery and releases any internal resources.
     * Safe to call even if no acquisition is active.
     *
     * Thread-safe, internally synchronized.
     */
    virtual bool StopAcquisition() = 0;

    // =========================================================================
    // Calibration
    // =========================================================================

    /**
     * @brief Run a calibration sequence
     * @param type Type of calibration (dark field, flat field, defect map)
     * @param num_frames Number of frames to acquire for calibration
     * @return CalibrationResult with output path or error details
     *
     * Calibration is a blocking operation that may take several seconds.
     * Cannot be called while an acquisition is active.
     *
     * Thread-safe, internally synchronized.
     */
    virtual CalibrationResult RunCalibration(CalibType type, int32_t num_frames) = 0;

    // =========================================================================
    // Callback Registration
    // =========================================================================

    /**
     * @brief Register callback for frame delivery
     * @param cb Function to call for each acquired frame
     *
     * The callback is invoked on a background thread for each frame
     * as it becomes available from the detector. The RawFrame reference
     * is valid only for the duration of the callback.
     *
     * Frame delivery latency must be <= 100 ms from DMA write complete
     * to callback invocation (NFR-HAL-01).
     *
     * Thread-safe: Multiple callbacks can be registered; all will be
     * invoked for each frame. Exception thrown by one callback does not
     * prevent invocation of remaining callbacks.
     */
    virtual void RegisterFrameCallback(FrameCallback cb) = 0;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_IDETECTOR_H
