/**
 * @file IAEC.h
 * @brief Pure abstract interface for Automatic Exposure Control
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: AEC signal handling
 * SPDX-License-Identifier: MIT
 *
 * Pure abstract interface with no non-virtual methods to satisfy
 * NFR-HAL-03 mockability requirements.
 */

#ifndef HNUE_HAL_IAEC_H
#define HNUE_HAL_IAEC_H

#include "HalTypes.h"

namespace hnvue::hal {

/**
 * @brief Pure abstract interface for Automatic Exposure Control
 *
 * AEC mode switching and termination signal handling.
 * When AEC mode is active, the AEC hardware signal terminates exposure
 * when the desired dose is reached.
 *
 * Safety Classification: IEC 62304 Class C
 * AEC signal processing is safety-critical for dose control.
 *
 * Thread Safety:
 * - GetMode() and GetThreshold() are thread-safe for concurrent calls
 * - SetMode() and SetThreshold() are internally synchronized
 * - Termination callbacks are invoked on a high-priority thread
 */
class IAEC {
public:
    /**
     * @brief Virtual destructor for proper cleanup
     */
    virtual ~IAEC() = default;

    // =========================================================================
    // Mode Control (SAFETY CRITICAL)
    // =========================================================================

    /**
     * @brief Set AEC operating mode
     * @param mode AEC_MANUAL or AEC_AUTO
     * @return true if mode change successful
     *
     * AEC_MANUAL: Exposure uses fixed time parameter (ms)
     * AEC_AUTO: Exposure terminates when AEC threshold is reached
     *
     * Mode change is not permitted during active exposure.
     *
     * Thread-safe, internally synchronized.
     */
    virtual bool SetMode(AecMode mode) = 0;

    /**
     * @brief Get current AEC mode
     * @return Current AEC mode (MANUAL or AUTO)
     *
     * Thread-safe, non-blocking.
     */
    virtual AecMode GetMode() const = 0;

    // =========================================================================
    // Threshold Configuration
    // =========================================================================

    /**
     * @brief Set AEC termination threshold
     * @param threshold_pct Desired detector signal level as percentage (0-100)
     * @return true if threshold accepted
     *
     * Threshold determines when AEC termination signal is generated.
     * Value is typically specified by exposure protocol.
     *
     * Thread-safe, internally synchronized.
     */
    virtual bool SetThreshold(float threshold_pct) = 0;

    /**
     * @brief Get current AEC threshold
     * @return Current threshold percentage
     *
     * Thread-safe, non-blocking.
     */
    virtual float GetThreshold() const = 0;

    // =========================================================================
    // Callback Registration
    // =========================================================================

    /**
     * @brief Register callback for AEC termination events
     * @param cb Function to call when AEC termination signal is received
     *
     * The callback is invoked on a high-priority thread within 5 ms
     * of the AEC signal assertion (FR-HAL-07).
     *
     * AecTerminationEvent contains:
     * - threshold_reached: true if termination was due to threshold
     * - actual_dose_mgy: actual delivered dose at termination
     * - exposure_time_us: actual exposure duration
     *
     * SAFETY CRITICAL: The callback must initiate generator abort
     * sequence within 5 ms to prevent dose overrun.
     *
     * Thread-safe: Multiple callbacks can be registered; all will be
     * invoked for each termination event.
     */
    virtual void RegisterTerminationCallback(AecTerminationCallback cb) = 0;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_IAEC_H
