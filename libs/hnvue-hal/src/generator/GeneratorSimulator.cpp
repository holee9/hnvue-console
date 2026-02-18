/**
 * @file GeneratorSimulator.cpp
 * @brief HVG Simulator implementation
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Simulator (no actual hardware control)
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/hal/generator/GeneratorSimulator.h"

#include <spdlog/spdlog.h>
#include <spdlog/sinks/stdout_color_sinks.h>

namespace hnvue::hal {

// =============================================================================
// Constructor/Destructor
// =============================================================================

GeneratorSimulator::GeneratorSimulator()
    : GeneratorSimulator(SimulatorConfig{})
{
}

GeneratorSimulator::GeneratorSimulator(const SimulatorConfig& config)
    : config_(config)
    , state_(GeneratorState::GEN_IDLE)
    , running_(false)
    , params_set_(false)
{
    // Build capabilities from config
    capabilities_.min_kvp = config_.min_kvp;
    capabilities_.max_kvp = config_.max_kvp;
    capabilities_.min_ma = config_.min_ma;
    capabilities_.max_ma = config_.max_ma;
    capabilities_.min_ms = config_.min_ms;
    capabilities_.max_ms = config_.max_ms;
    capabilities_.has_aec = config_.has_aec;
    capabilities_.has_dual_focus = config_.has_dual_focus;
    capabilities_.vendor_name = config_.vendor_name;
    capabilities_.model_name = config_.model_name;
    capabilities_.firmware_version = config_.firmware_version;

    // Initialize status
    current_status_.state = GeneratorState::GEN_IDLE;
    current_status_.interlock_ok = true;
    current_status_.actual_kvp = 0.0f;
    current_status_.actual_ma = 0.0f;
    current_status_.timestamp_us = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::system_clock::now().time_since_epoch()
    ).count();

    // Start status update thread
    running_ = true;
    status_thread_ = std::thread(&GeneratorSimulator::StatusUpdateThread, this);

    spdlog::info("[GeneratorSimulator] Initialized with vendor={}, model={}, fw={}",
                 config_.vendor_name, config_.model_name, config_.firmware_version);
}

GeneratorSimulator::~GeneratorSimulator() {
    running_ = false;
    state_cv_.notify_all();

    if (status_thread_.joinable()) {
        status_thread_.join();
    }

    spdlog::info("[GeneratorSimulator] Destroyed");
}

// =============================================================================
// IGenerator Interface Implementation
// =============================================================================

HvgStatus GeneratorSimulator::GetStatus() {
    std::lock_guard<std::mutex> lock(state_mutex_);

    current_status_.timestamp_us = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::system_clock::now().time_since_epoch()
    ).count();

    return current_status_;
}

bool GeneratorSimulator::SetExposureParams(const ExposureParams& params) {
    if (!ValidateParams(params)) {
        spdlog::warn("[GeneratorSimulator] Invalid exposure parameters: kvp={}, ma={}, ms={}",
                     params.kvp, params.ma, params.ms);
        return false;
    }

    std::lock_guard<std::mutex> lock(state_mutex_);
    current_params_ = params;
    params_set_ = true;

    // Transition to READY state
    if (state_.load() == GeneratorState::GEN_IDLE) {
        state_.store(GeneratorState::GEN_READY);
        current_status_.state = GeneratorState::GEN_READY;
    }

    spdlog::debug("[GeneratorSimulator] Parameters set: kvp={}, ma={}, ms={}",
                  params.kvp, params.ma, params.ms);
    return true;
}

ExposureResult GeneratorSimulator::StartExposure() {
    std::lock_guard<std::mutex> lock(state_mutex_);

    if (!params_set_) {
        spdlog::warn("[GeneratorSimulator] Cannot start exposure: parameters not set");
        return ExposureResult{false, 0, 0, 0, 0, "Parameters not set"};
    }

    GeneratorState current_state = state_.load();
    if (current_state != GeneratorState::GEN_READY) {
        spdlog::warn("[GeneratorSimulator] Cannot start exposure: invalid state={}",
                     static_cast<int>(current_state));
        return ExposureResult{false, 0, 0, 0, 0, "Invalid state for exposure"};
    }

    // Transition to ARMED then EXPOSING
    state_.store(GeneratorState::GEN_ARMED);
    current_status_.state = GeneratorState::GEN_ARMED;

    // Simulate arm latency
    std::this_thread::sleep_for(config_.response_latency);

    // Start exposure in background thread
    state_.store(GeneratorState::GEN_EXPOSING);
    current_status_.state = GeneratorState::GEN_EXPOSING;
    current_status_.actual_kvp = current_params_.kvp;
    current_status_.actual_ma = current_params_.ma;

    // Notify status callbacks
    NotifyStatusCallbacks(current_status_);

    // Simulate exposure in background
    int32_t duration_ms = static_cast<int32_t>(current_params_.ms);
    std::thread([this, duration_ms]() {
        SimulateExposure(duration_ms);
    }).detach();

    spdlog::info("[GeneratorSimulator] Exposure started: kvp={}, ma={}, ms={}ms",
                 current_params_.kvp, current_params_.ma, current_params_.ms);

    return ExposureResult{
        true,
        current_params_.kvp,
        current_params_.ma,
        current_params_.ms,
        current_params_.ma * current_params_.ms / 1000.0f,
        ""
    };
}

void GeneratorSimulator::AbortExposure() {
    GeneratorState current_state = state_.load();

    if (current_state != GeneratorState::GEN_EXPOSING &&
        current_state != GeneratorState::GEN_ARMED) {
        // No-op if not exposing
        return;
    }

    spdlog::info("[GeneratorSimulator] Exposure aborted");

    std::lock_guard<std::mutex> lock(state_mutex_);
    state_.store(GeneratorState::GEN_IDLE);
    current_status_.state = GeneratorState::GEN_IDLE;
    current_status_.actual_kvp = 0.0f;
    current_status_.actual_ma = 0.0f;

    NotifyStatusCallbacks(current_status_);
}

void GeneratorSimulator::RegisterAlarmCallback(AlarmCallback callback) {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);
    alarm_callbacks_.push_back(std::move(callback));
    spdlog::debug("[GeneratorSimulator] Alarm callback registered");
}

void GeneratorSimulator::RegisterStatusCallback(StatusCallback callback) {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);
    status_callbacks_.push_back(std::move(callback));
    spdlog::debug("[GeneratorSimulator] Status callback registered");
}

HvgCapabilities GeneratorSimulator::GetCapabilities() {
    return capabilities_;
}

// =============================================================================
// Simulator-Specific Methods
// =============================================================================

void GeneratorSimulator::GenerateTestAlarm(
    int32_t alarm_code,
    const std::string& description,
    AlarmSeverity severity
) {
    HvgAlarm alarm;
    alarm.alarm_code = alarm_code;
    alarm.description = description;
    alarm.severity = severity;
    alarm.timestamp_us = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::system_clock::now().time_since_epoch()
    ).count();

    spdlog::info("[GeneratorSimulator] Test alarm generated: code={}, desc={}, severity={}",
                 alarm_code, description, static_cast<int>(severity));

    NotifyAlarmCallbacks(alarm);
}

// =============================================================================
// Internal Methods
// =============================================================================

bool GeneratorSimulator::ValidateParams(const ExposureParams& params) {
    if (params.kvp < config_.min_kvp || params.kvp > config_.max_kvp) {
        return false;
    }

    if (params.ma < config_.min_ma || params.ma > config_.max_ma) {
        return false;
    }

    if (params.ms < config_.min_ms || params.ms > config_.max_ms) {
        return false;
    }

    // Validate focus if dual focus is supported
    if (config_.has_dual_focus && !params.focus.empty()) {
        if (params.focus != "large" && params.focus != "small") {
            return false;
        }
    }

    // Validate AEC mode
    if (params.aec_mode == AecMode::AEC_AUTO && !config_.has_aec) {
        return false;
    }

    return true;
}

void GeneratorSimulator::StatusUpdateThread() {
    spdlog::debug("[GeneratorSimulator] Status update thread started");

    while (running_.load()) {
        std::unique_lock<std::mutex> lock(state_mutex_);

        // Calculate sleep time based on state
        int sleep_ms = 100;  // Default: 10 Hz (100ms)

        if (state_.load() == GeneratorState::GEN_EXPOSING) {
            sleep_ms = static_cast<int>(1000.0 / config_.status_frequency_hz);
        }

        lock.unlock();

        std::this_thread::sleep_for(std::chrono::milliseconds(sleep_ms));

        // Update status
        lock.lock();
        current_status_.timestamp_us = std::chrono::duration_cast<std::chrono::microseconds>(
            std::chrono::system_clock::now().time_since_epoch()
        ).count();

        NotifyStatusCallbacks(current_status_);
        lock.unlock();
    }

    spdlog::debug("[GeneratorSimulator] Status update thread stopped");
}

void GeneratorSimulator::SimulateExposure(int32_t duration_ms) {
    // Simulate exposure for specified duration
    std::this_thread::sleep_for(std::chrono::milliseconds(duration_ms));

    std::lock_guard<std::mutex> lock(state_mutex_);
    state_.store(GeneratorState::GEN_IDLE);
    current_status_.state = GeneratorState::GEN_IDLE;
    current_status_.actual_kvp = 0.0f;
    current_status_.actual_ma = 0.0f;

    spdlog::info("[GeneratorSimulator] Exposure completed after {}ms", duration_ms);

    NotifyStatusCallbacks(current_status_);
}

void GeneratorSimulator::NotifyStatusCallbacks(const HvgStatus& status) {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);

    for (const auto& callback : status_callbacks_) {
        try {
            callback(status);
        } catch (const std::exception& e) {
            spdlog::error("[GeneratorSimulator] Status callback exception: {}", e.what());
        }
    }
}

void GeneratorSimulator::NotifyAlarmCallbacks(const HvgAlarm& alarm) {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);

    for (const auto& callback : alarm_callbacks_) {
        try {
            callback(alarm);
        } catch (const std::exception& e) {
            spdlog::error("[GeneratorSimulator] Alarm callback exception: {}", e.what());
        }
    }
}

} // namespace hnvue::hal
