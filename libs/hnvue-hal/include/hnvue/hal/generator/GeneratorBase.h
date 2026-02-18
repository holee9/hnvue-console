/**
 * @file GeneratorBase.h
 * @brief Base class for common IGenerator functionality
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Generator base implementation
 * SPDX-License-Identifier: MIT
 */

#ifndef HNUE_HAL_GENERATOR_BASE_H
#define HNUE_HAL_GENERATOR_BASE_H

#include "hnvue/hal/IGenerator.h"
#include "CommandQueue.h"

#include <vector>
#include <mutex>
#include <atomic>

namespace hnvue::hal {

/**
 * @brief Base class providing common IGenerator functionality
 *
 * Provides:
 * - Command queue integration
 * - Status callback management
 * - Alarm callback management
 * - Thread-safe state management
 *
 * Concrete implementations can derive from this class to inherit
 * common functionality while implementing hardware-specific logic.
 */
class GeneratorBase : public IGenerator {
public:
    /**
     * @brief Construct with command queue configuration
     * @param max_depth Maximum queue depth
     * @param timeout_ms Command timeout in milliseconds
     * @param max_retries Maximum retry attempts
     */
    GeneratorBase(
        size_t max_depth = 16,
        uint32_t timeout_ms = 500,
        uint32_t max_retries = 3
    );

    /**
     * @brief Virtual destructor
     */
    ~GeneratorBase() override;

    // Disable copy
    GeneratorBase(const GeneratorBase&) = delete;
    GeneratorBase& operator=(const GeneratorBase&) = delete;

    // =========================================================================
    // IGenerator Interface (partial implementation)
    // =========================================================================

    void RegisterAlarmCallback(AlarmCallback callback) override;
    void RegisterStatusCallback(StatusCallback callback) override;

    // =========================================================================
    // Protected Helper Methods
    // =========================================================================

protected:
    /**
     * @brief Notify all registered status callbacks
     * @param status Status to send
     */
    void NotifyStatusCallbacks(const HvgStatus& status);

    /**
     * @brief Notify all registered alarm callbacks
     * @param alarm Alarm to send
     */
    void NotifyAlarmCallbacks(const HvgAlarm& alarm);

    /**
     * @brief Get command queue for subclass use
     * @return Reference to command queue
     */
    CommandQueue& GetCommandQueue() { return command_queue_; }

    /**
     * @brief Set current generator state (thread-safe)
     * @param state New state
     */
    void SetState(GeneratorState state);

    /**
     * @brief Get current generator state (thread-safe)
     * @return Current state
     */
    GeneratorState GetState() const;

    // =========================================================================
    // Member Variables
    // =========================================================================

    // Command queue for HVG operations
    CommandQueue command_queue_;

    // Callbacks
    std::vector<AlarmCallback> alarm_callbacks_;
    std::vector<StatusCallback> status_callbacks_;
    mutable std::mutex callbacks_mutex_;

    // State management
    std::atomic<GeneratorState> state_;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_GENERATOR_BASE_H
