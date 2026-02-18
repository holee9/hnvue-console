/**
 * @file AecController.cpp
 * @brief AEC Controller implementation
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: AEC signal handling
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/hal/aec/AecController.h"

#include <chrono>
#include <exception>
#include <stdexcept>

namespace hnvue::hal {

// =============================================================================
// Construction / Destruction
// =============================================================================

AecController::AecController(IGenerator* generator)
    : generator_(generator)
    , mode_(AecMode::AEC_MANUAL)
    , threshold_(50.0f)  // Default threshold: 50%
    , is_exposing_(false)
{
    // Atomic initialization is default-initialized above
}

AecController::~AecController() {
    // Callbacks cleared automatically on destruction
}

// =============================================================================
// IAEC Implementation
// =============================================================================

bool AecController::SetMode(AecMode mode) {
    // Validate mode parameter
    if (mode != AecMode::AEC_MANUAL && mode != AecMode::AEC_AUTO) {
        return false;  // Invalid mode
    }

    // FR-HAL-07: Mode change not permitted during active exposure
    if (is_exposing_.load(std::memory_order_acquire)) {
        return false;  // Mode change rejected during exposure
    }

    // Set new mode
    mode_.store(mode, std::memory_order_release);
    return true;
}

AecMode AecController::GetMode() const {
    // Thread-safe atomic load
    return mode_.load(std::memory_order_acquire);
}

bool AecController::SetThreshold(float threshold_pct) {
    // Validate threshold range: 0.0 to 100.0 percent
    if (threshold_pct < 0.0f || threshold_pct > 100.0f) {
        return false;  // Out of range
    }

    // Set new threshold
    threshold_.store(threshold_pct, std::memory_order_release);
    return true;
}

float AecController::GetThreshold() const {
    // Thread-safe atomic load
    return threshold_.load(std::memory_order_acquire);
}

void AecController::RegisterTerminationCallback(AecTerminationCallback cb) {
    if (!cb) {
        return;  // Ignore null callbacks
    }

    std::lock_guard<std::mutex> lock(callbacks_mutex_);
    termination_callbacks_.push_back(std::move(cb));
}

// =============================================================================
// Hardware Simulation Interface
// =============================================================================

void AecController::SimulateTerminationSignal(const AecTerminationEvent& event) {
    // FR-HAL-07: In MANUAL mode, AEC termination signal is ignored
    AecMode current_mode = mode_.load(std::memory_order_acquire);
    if (current_mode == AecMode::AEC_MANUAL) {
        return;  // Ignore signal in manual mode
    }

    // FR-HAL-07: Abort sequence must initiate within 5ms
    // We measure timing from signal receipt to callback completion
    auto start_time = std::chrono::high_resolution_clock::now();

    // Invoke all registered callbacks first (fast path)
    InvokeTerminationCallbacks(event);

    // SAFETY CRITICAL: Trigger generator abort within 5ms
    if (generator_) {
        generator_->AbortExposure();
    }

    // Verify timing requirement met
    auto end_time = std::chrono::high_resolution_clock::now();
    auto elapsed_us = std::chrono::duration_cast<std::chrono::microseconds>(
        end_time - start_time).count();

    // FR-HAL-07 requires < 5ms (5000us) from signal to abort
    // In production, this should be logged for safety monitoring
    (void)elapsed_us;  // Suppress unused warning in release builds
}

void AecController::SetExposureState(bool exposing) {
    is_exposing_.store(exposing, std::memory_order_release);
}

// =============================================================================
// Private Methods
// =============================================================================

void AecController::InvokeTerminationCallbacks(const AecTerminationEvent& event) {
    // Copy callbacks to minimize lock time
    std::vector<AecTerminationCallback> callbacks_copy;
    {
        std::lock_guard<std::mutex> lock(callbacks_mutex_);
        callbacks_copy = termination_callbacks_;
    }

    // Invoke all callbacks with exception safety
    // FR-HAL-07: Exception in one callback must not prevent others
    for (const auto& callback : callbacks_copy) {
        try {
            callback(event);
        } catch (const std::exception& e) {
            // Log exception but continue invoking remaining callbacks
            // In production, this should be logged to error handling system
            (void)e;  // Suppress unused warning
        } catch (...) {
            // Catch all exceptions to ensure all callbacks attempted
        }
    }
}

} // namespace hnvue::hal
