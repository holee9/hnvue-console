/**
 * @file ICollimator.h
 * @brief Pure abstract interface for collimator field of view control
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Collimator control interface
 * SPDX-License-Identifier: MIT
 *
 * Pure abstract interface with no non-virtual methods to satisfy
 * NFR-HAL-03 mockability requirements.
 */

#ifndef HNUE_HAL_ICOLLIMATOR_H
#define HNUE_HAL_ICOLLIMATOR_H

#include "HalTypes.h"

namespace hnvue::hal {

/**
 * @brief Pure abstract interface for collimator control
 *
 * Provides field of view (FOV) control through collimator blade positioning.
 * Supports both motorized and read-only collimator implementations.
 *
 * Thread Safety:
 * - GetPosition() and IsMotorized() are thread-safe for concurrent calls
 * - SetPosition() is internally synchronized
 * - Position callbacks are invoked on a background thread
 */
class ICollimator {
public:
    /**
     * @brief Virtual destructor for proper cleanup
     */
    virtual ~ICollimator() = default;

    // =========================================================================
    // Query Methods
    // =========================================================================

    /**
     * @brief Get current collimator blade position
     * @return CollimatorPosition with blade positions in mm
     *
     * Returns the current field size defined by the four collimator blades.
     * Position values are mm from center for each blade.
     *
     * Thread-safe, non-blocking.
     */
    virtual CollimatorPosition GetPosition() = 0;

    /**
     * @brief Check if collimator has motorized control
     * @return true if motorized, false if manual/read-only
     *
     * Read-only collimators report position but cannot be commanded.
     *
     * Thread-safe, non-blocking.
     */
    virtual bool IsMotorized() const = 0;

    // =========================================================================
    // Control Methods
    // =========================================================================

    /**
     * @brief Command collimator to move to specified position
     * @param pos Desired collimator blade positions
     * @return true if command accepted, false if not motorized or invalid position
     *
     * For non-motorized collimators, returns false with error.
     * Position validation (IL-06) ensures blades are within safe range.
     *
     * Thread-safe, internally synchronized.
     */
    virtual bool SetPosition(const CollimatorPosition& pos) = 0;

    // =========================================================================
    // Callback Registration
    // =========================================================================

    /**
     * @brief Register callback for position change notifications
     * @param cb Function to call when collimator position changes
     *
     * Callback is invoked when position changes by more than a threshold
     * (implementation-defined) or on periodic polling interval.
     *
     * Thread-safe: Multiple callbacks can be registered; all will be
     * invoked for each position change.
     */
    virtual void RegisterPositionCallback(CollimatorCallback cb) = 0;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_ICOLLIMATOR_H
