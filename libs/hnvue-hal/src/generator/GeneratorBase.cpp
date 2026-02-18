/**
 * @file GeneratorBase.cpp
 * @brief Base class implementation
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Generator base implementation
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/hal/generator/GeneratorBase.h"

#include <spdlog/spdlog.h>

namespace hnvue::hal {

// =============================================================================
// Constructor/Destructor
// =============================================================================

GeneratorBase::GeneratorBase(size_t max_depth, uint32_t timeout_ms, uint32_t max_retries)
    : command_queue_(max_depth, timeout_ms, max_retries)
    , state_(GeneratorState::GEN_IDLE)
{
    spdlog::debug("[GeneratorBase] Initialized with queue_depth={}, timeout={}ms",
                  max_depth, timeout_ms);
}

GeneratorBase::~GeneratorBase() {
    spdlog::debug("[GeneratorBase] Destroyed");
}

// =============================================================================
// IGenerator Interface Implementation
// =============================================================================

void GeneratorBase::RegisterAlarmCallback(AlarmCallback callback) {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);
    alarm_callbacks_.push_back(std::move(callback));
    spdlog::debug("[GeneratorBase] Alarm callback registered (total={})",
                  alarm_callbacks_.size());
}

void GeneratorBase::RegisterStatusCallback(StatusCallback callback) {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);
    status_callbacks_.push_back(std::move(callback));
    spdlog::debug("[GeneratorBase] Status callback registered (total={})",
                  status_callbacks_.size());
}

// =============================================================================
// Protected Helper Methods
// =============================================================================

void GeneratorBase::NotifyStatusCallbacks(const HvgStatus& status) {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);

    for (const auto& callback : status_callbacks_) {
        try {
            callback(status);
        } catch (const std::exception& e) {
            spdlog::error("[GeneratorBase] Status callback exception: {}", e.what());
        }
    }
}

void GeneratorBase::NotifyAlarmCallbacks(const HvgAlarm& alarm) {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);

    for (const auto& callback : alarm_callbacks_) {
        try {
            callback(alarm);
        } catch (const std::exception& e) {
            spdlog::error("[GeneratorBase] Alarm callback exception: {}", e.what());
        }
    }
}

void GeneratorBase::SetState(GeneratorState state) {
    state_.store(state);
    spdlog::debug("[GeneratorBase] State changed to {}", static_cast<int>(state));
}

GeneratorState GeneratorBase::GetState() const {
    return state_.load();
}

} // namespace hnvue::hal
