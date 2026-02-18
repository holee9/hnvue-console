/**
 * @file IPatientTable.h
 * @brief Pure abstract interface for patient table position control
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Patient table interface
 * SPDX-License-Identifier: MIT
 *
 * Pure abstract interface with no non-virtual methods to satisfy
 * NFR-HAL-03 mockability requirements.
 */

#ifndef HNUE_HAL_IPATIENTTABLE_H
#define HNUE_HAL_IPATIENTTABLE_H

#include "HalTypes.h"

namespace hnvue::hal {

/**
 * @brief Pure abstract interface for patient table control
 *
 * Provides patient table position feedback and motorized control
 * where supported by hardware.
 *
 * Thread Safety:
 * - GetPosition() and IsMotorized() are thread-safe for concurrent calls
 * - MoveTo() is internally synchronized
 * - Position callbacks are invoked on a background thread
 */
class IPatientTable {
public:
    /**
     * @brief Virtual destructor for proper cleanup
     */
    virtual ~IPatientTable() = default;

    // =========================================================================
    // Query Methods
    // =========================================================================

    /**
     * @brief Get current table position
     * @return TablePosition with 3D position in mm
     *
     * Returns the current table position relative to isocenter:
     * - longitudinal: mm from isocenter along patient axis
     * - lateral: mm from isocenter side-to-side
     * - height: mm from floor
     *
     * Thread-safe, non-blocking.
     */
    virtual TablePosition GetPosition() = 0;

    /**
     * @brief Check if table has motorized control
     * @return true if motorized, false if manual/fixed
     *
     * Non-motorized tables report position but cannot be commanded.
     *
     * Thread-safe, non-blocking.
     */
    virtual bool IsMotorized() const = 0;

    // =========================================================================
    // Control Methods
    // =========================================================================

    /**
     * @brief Command table to move to specified position
     * @param pos Desired table position
     * @return true if command accepted, false if not motorized or invalid position
     *
     * For non-motorized tables, returns false with error.
     * Position validation ensures movement is within safe range.
     *
     * Thread-safe, internally synchronized.
     */
    virtual bool MoveTo(const TablePosition& pos) = 0;

    // =========================================================================
    // Callback Registration
    // =========================================================================

    /**
     * @brief Register callback for position change notifications
     * @param cb Function to call when table position changes
     *
     * Callback is invoked when position changes by more than a threshold
     * (implementation-defined) or on periodic polling interval.
     *
     * Thread-safe: Multiple callbacks can be registered; all will be
     * invoked for each position change.
     */
    virtual void RegisterPositionCallback(TableCallback cb) = 0;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_IPATIENTTABLE_H
