/**
 * @file ISafetyInterlock.h
 * @brief Composite safety interlock interface for pre-exposure safety verification
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: Pre-exposure safety interlock chain
 * SPDX-License-Identifier: MIT
 *
 * Aggregates all hardware interlock signals required by SPEC-WORKFLOW-001.
 * Covers both physical safety interlocks and device readiness checks.
 * Maps to WORKFLOW interlocks IL-01 through IL-09.
 *
 * All methods are pure abstract to satisfy NFR-HAL-03 mockability requirements.
 */

#ifndef HNUE_HAL_ISAFETYINTERLOCK_H
#define HNUE_HAL_ISAFETYINTERLOCK_H

#include "HalTypes.h"

namespace hnvue::hal {

/**
 * @brief Pure abstract interface for safety interlock verification
 *
 * This is the single entry point for WORKFLOW pre-exposure safety verification.
 * All 9 interlocks must pass before exposure is permitted.
 *
 * Interlock Mapping (IL-01 through IL-09):
 * - IL-01: door_closed - X-ray room door closed (hardware sensor)
 * - IL-02: emergency_stop_clear - Emergency stop not activated (hardware sensor)
 * - IL-03: thermal_normal - No overtemperature condition (generator thermal sensor)
 * - IL-04: generator_ready - Generator in ready state, no fault (IGenerator)
 * - IL-05: detector_ready - Detector acquisition ready (IDetector)
 * - IL-06: collimator_valid - Collimator position within valid range (ICollimator)
 * - IL-07: table_locked - Patient table locked/stable (IPatientTable)
 * - IL-08: dose_within_limits - Dose accumulation within configured limits (IDoseMonitor)
 * - IL-09: aec_configured - AEC mode properly configured for protocol (IAEC)
 *
 * Safety Classification: IEC 62304 Class C
 * Failure of any safety-critical interlock (IL-01 through IL-03) must prevent exposure.
 *
 * Thread Safety:
 * - All check methods are thread-safe for concurrent calls
 * - CheckAllInterlocks() provides atomic snapshot of all interlocks
 * - EmergencyStandby() is internally synchronized
 */
class ISafetyInterlock {
public:
    /**
     * @brief Virtual destructor for proper cleanup
     */
    virtual ~ISafetyInterlock() = default;

    // =========================================================================
    // Atomic Interlock Verification
    // =========================================================================

    /**
     * @brief Perform atomic check of all 9 interlocks
     * @return InterlockStatus structure with all interlock states
     *
     * This is the primary pre-exposure safety verification method.
     * All interlocks are checked in a single atomic operation to ensure
     * no race conditions between check and exposure initiation.
     *
     * Performance: Must complete within 10 ms to avoid exposure delay.
     *
     * Thread-safe, non-blocking.
     */
    virtual InterlockStatus CheckAllInterlocks() = 0;

    /**
     * @brief Check a single interlock by index
     * @param interlock_index Interlock index 0-8 (IL-01 through IL-09)
     * @return true if the specified interlock passes
     *
     * Convenience method for checking individual interlocks.
     * Does not provide atomic guarantee across all interlocks.
     *
     * Thread-safe, non-blocking.
     */
    virtual bool CheckInterlock(int interlock_index) = 0;

    // =========================================================================
    // Named Accessors for Safety-Critical Interlocks
    // =========================================================================

    /**
     * @brief Get door closed status
     * @return true if door is closed and secured
     *
     * IL-01: Physical hardware sensor on X-ray room door.
     * Exposure must be prevented if door is open.
     *
     * Thread-safe, non-blocking.
     */
    virtual bool GetDoorStatus() = 0;

    /**
     * @brief Get emergency stop status
     * @return true if emergency stop is NOT activated (clear)
     *
     * IL-02: Physical hardware emergency stop button.
     * Exposure must be prevented if e-stop is activated.
     *
     * Thread-safe, non-blocking.
     */
    virtual bool GetEStopStatus() = 0;

    /**
     * @brief Get thermal status
     * @return true if thermal condition is normal (no overtemperature)
     *
     * IL-03: Generator thermal sensor (and potentially other thermal sensors).
     * Exposure must be prevented if any component is overheating.
     *
     * Thread-safe, non-blocking.
     */
    virtual bool GetThermalStatus() = 0;

    // =========================================================================
    // Emergency Standby
    // =========================================================================

    /**
     * @brief Place all hardware in safe state immediately
     *
     * Called by WORKFLOW on critical hardware error (T-18 recovery).
     * Actions:
     * - Disarm generator (abort any active exposure)
     * - Stop detector acquisition
     * - Log safety event to persistent storage
     *
     * Performance: Must complete within 100 ms.
     *
     * Thread-safe, internally synchronized.
     */
    virtual void EmergencyStandby() = 0;

    // =========================================================================
    // Callback Registration
    // =========================================================================

    /**
     * @brief Register callback for interlock state changes
     * @param cb Function to call when any interlock transitions to failed
     *
     * Callback is invoked within 50 ms of state change detection.
     * Provides the full InterlockStatus structure with all interlock states.
     *
     * Thread-safe: Multiple callbacks can be registered; all will be
     * invoked for each state change. Exception thrown by one callback
     * does not prevent invocation of remaining callbacks.
     */
    virtual void RegisterInterlockCallback(InterlockCallback cb) = 0;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_ISAFETYINTERLOCK_H
