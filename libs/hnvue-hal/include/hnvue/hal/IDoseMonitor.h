/**
 * @file IDoseMonitor.h
 * @brief Pure abstract interface for radiation dose monitoring
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Dose monitoring interface
 * SPDX-License-Identifier: MIT
 *
 * Pure abstract interface with no non-virtual methods to satisfy
 * NFR-HAL-03 mockability requirements.
 */

#ifndef HNUE_HAL_IDOSEMONITOR_H
#define HNUE_HAL_IDOSEMONITOR_H

#include "HalTypes.h"

namespace hnvue::hal {

/**
 * @brief Pure abstract interface for radiation dose monitoring
 *
 * Provides real-time dose monitoring and accumulation tracking.
 * Supports cumulative dose limits (IL-08) for safety interlock verification.
 *
 * Thread Safety:
 * - GetCurrentDose() and GetDap() are thread-safe for concurrent calls
 * - Reset() is internally synchronized
 * - Dose callbacks are invoked on a background thread
 */
class IDoseMonitor {
public:
    /**
     * @brief Virtual destructor for proper cleanup
     */
    virtual ~IDoseMonitor() = default;

    // =========================================================================
    // Query Methods
    // =========================================================================

    /**
     * @brief Get current dose reading
     * @return DoseReading with accumulated dose and dose rate
     *
     * Returns the current radiation dose measurement:
     * - dose_mgy: Accumulated dose in milligray
     * - dose_rate_mgy_s: Current dose rate in mGy/s
     * - dap_ugy_cm2: Dose Area Product in uGy*cm^2
     * - timestamp_us: Measurement timestamp
     *
     * Thread-safe, non-blocking.
     */
    virtual DoseReading GetCurrentDose() = 0;

    /**
     * @brief Get Dose Area Product (DAP)
     * @return DAP value in uGy*cm^2
     *
     * DAP is a measure of total radiation energy delivered,
     * accounting for both dose and beam area.
     *
     * Thread-safe, non-blocking.
     */
    virtual float GetDap() = 0;

    // =========================================================================
    // Control Methods
    // =========================================================================

    /**
     * @brief Reset dose accumulation counters
     *
     * Resets accumulated dose to zero. Typically called at the
     * start of a new examination or patient session.
     *
     * Thread-safe, internally synchronized.
     */
    virtual void Reset() = 0;

    // =========================================================================
    // Callback Registration
    // =========================================================================

    /**
     * @brief Register callback for dose update notifications
     * @param cb Function to call when dose reading is updated
     *
     * Callback is invoked at a regular interval (implementation-defined,
     * typically 1-10 Hz) with the current dose reading.
     *
     * Used for real-time dose display and accumulation limit monitoring
     * (IL-08 interlock verification).
     *
     * Thread-safe: Multiple callbacks can be registered; all will be
     * invoked for each dose update.
     */
    virtual void RegisterDoseCallback(DoseCallback cb) = 0;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_IDOSEMONITOR_H
