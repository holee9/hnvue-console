/**
 * @file AecController.h
 * @brief AEC Controller implementation for automatic exposure control
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: AEC signal handling
 * SPDX-License-Identifier: MIT
 *
 * Implements IAEC interface with mode switching, threshold configuration,
 * and fast termination signal handling (< 5ms abort requirement).
 */

#ifndef HNUE_HAL_AEC_CONTROLLER_H
#define HNUE_HAL_AEC_CONTROLLER_H

#include "hnvue/hal/IAEC.h"
#include "hnvue/hal/IGenerator.h"
#include "hnvue/hal/HalTypes.h"

#include <atomic>
#include <functional>
#include <memory>
#include <mutex>
#include <vector>

namespace hnvue::hal {

/**
 * @brief AEC Controller implementation
 *
 * Implements IAEC interface with thread-safe mode switching and
 * real-time termination signal handling.
 *
 * Safety Classification: IEC 62304 Class C
 * AEC signal processing directly affects patient dose.
 *
 * Thread Safety:
 * - All public methods are thread-safe
 * - Uses atomic operations for mode and threshold
 * - Callbacks invoked on high-priority thread
 */
class AecController : public IAEC {
public:
    /**
     * @brief Construct AEC controller with generator integration
     * @param generator Generator interface for abort on termination
     *
     * Generator integration is required for safety-critical abort
     * functionality. If generator is null, termination callbacks
     * will still be invoked but abort will not be automatic.
     */
    explicit AecController(IGenerator* generator = nullptr);

    /**
     * @brief Virtual destructor
     */
    ~AecController() override;

    // Disable copy construction and assignment
    AecController(const AecController&) = delete;
    AecController& operator=(const AecController&) = delete;

    // =========================================================================
    // IAEC Implementation
    // =========================================================================

    /**
     * @brief Set AEC operating mode
     * @param mode AEC_MANUAL or AEC_AUTO
     * @return true if mode change successful
     *
     * Mode change fails if:
     * - Exposure is currently active (state check)
     * - Invalid mode specified
     */
    bool SetMode(AecMode mode) override;

    /**
     * @brief Get current AEC mode
     * @return Current AEC mode (MANUAL or AUTO)
     *
     * Thread-safe, non-blocking using atomic load.
     */
    AecMode GetMode() const override;

    /**
     * @brief Set AEC termination threshold
     * @param threshold_pct Desired detector signal level (0-100)
     * @return true if threshold accepted
     *
     * Threshold validation:
     * - Range: 0.0 to 100.0 percent
     * - Typical values: 40.0 to 80.0 percent
     */
    bool SetThreshold(float threshold_pct) override;

    /**
     * @brief Get current AEC threshold
     * @return Current threshold percentage
     *
     * Thread-safe, non-blocking using atomic load.
     */
    float GetThreshold() const override;

    /**
     * @brief Register callback for AEC termination events
     * @param cb Function to call when AEC termination signal received
     *
     * Multiple callbacks supported. All will be invoked for each
     * termination event. Exception in one callback does not prevent
     * invocation of remaining callbacks.
     */
    void RegisterTerminationCallback(AecTerminationCallback cb) override;

    // =========================================================================
    // Hardware Simulation Interface (for testing)
    // =========================================================================

    /**
     * @brief Simulate AEC termination signal from hardware
     * @param event Termination event details
     *
     * This method simulates the hardware AEC signal assertion.
     * In production, this is called by the hardware interface layer
     * when the physical AEC signal is detected.
     *
     * Callbacks are invoked within 5ms as required by FR-HAL-07.
     */
    void SimulateTerminationSignal(const AecTerminationEvent& event);

    /**
     * @brief Set exposure state for mode change validation
     * @param exposing True if exposure is currently active
     *
     * Used by generator integration to prevent mode changes during
     * active exposure.
     */
    void SetExposureState(bool exposing);

private:
    // Generator interface for abort on termination
    IGenerator* generator_;

    // AEC configuration (atomic for thread-safe access)
    std::atomic<AecMode> mode_;
    std::atomic<float> threshold_;

    // Exposure state for mode change validation
    std::atomic<bool> is_exposing_;

    // Termination callbacks (mutex-protected for registration)
    std::vector<AecTerminationCallback> termination_callbacks_;
    mutable std::mutex callbacks_mutex_;

    // Invoke all registered callbacks with exception safety
    void InvokeTerminationCallbacks(const AecTerminationEvent& event);
};

} // namespace hnvue::hal

#endif // HNUE_HAL_AEC_CONTROLLER_H
