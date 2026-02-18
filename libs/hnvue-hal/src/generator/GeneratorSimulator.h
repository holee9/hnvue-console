/**
 * @file GeneratorSimulator.h
 * @brief HVG Simulator implementing IGenerator for testing without physical hardware
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Simulator (no actual hardware control)
 * SPDX-License-Identifier: MIT
 */

#ifndef HNUE_HAL_GENERATOR_SIMULATOR_H
#define HNUE_HAL_GENERATOR_SIMULATOR_H

#include "hnvue/hal/IGenerator.h"
#include "CommandQueue.h"

#include <atomic>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <vector>
#include <chrono>

namespace hnvue::hal {

/**
 * @brief Simulator configuration
 */
struct SimulatorConfig {
    // Capability ranges
    float min_kvp = 40.0f;
    float max_kvp = 150.0f;
    float min_ma = 0.1f;
    float max_ma = 1000.0f;
    float min_ms = 1.0f;
    float max_ms = 10000.0f;

    // Feature flags
    bool has_aec = true;
    bool has_dual_focus = true;

    // Simulation behavior
    std::chrono::microseconds response_latency{1000};  // 1ms default
    double status_frequency_hz = 10.0;                 // 10 Hz during exposure

    // Device info
    std::string vendor_name = "Simulated HVG";
    std::string model_name = "HVG-SIM-001";
    std::string firmware_version = "1.0.0-sim";
};

/**
 * @brief HVG Simulator for testing and development
 *
 * Implements IGenerator interface without requiring physical hardware.
 * Simulates realistic HVG behavior including:
 * - State machine (IDLE -> READY -> ARMED -> EXPOSING -> IDLE)
 * - Status updates at configurable rate (>= 10 Hz during exposure)
 * - Alarm generation for testing
 * - Configurable capabilities
 *
 * Thread Safety: All public methods are thread-safe.
 */
class GeneratorSimulator : public IGenerator {
public:
    /**
     * @brief Construct simulator with default configuration
     */
    GeneratorSimulator();

    /**
     * @brief Construct simulator with custom configuration
     * @param config Simulator configuration
     */
    explicit GeneratorSimulator(const SimulatorConfig& config);

    /**
     * @brief Destructor - stops background threads
     */
    ~GeneratorSimulator() override;

    // Disable copy
    GeneratorSimulator(const GeneratorSimulator&) = delete;
    GeneratorSimulator& operator=(const GeneratorSimulator&) = delete;

    // =========================================================================
    // IGenerator Interface Implementation
    // =========================================================================

    HvgStatus GetStatus() override;
    bool SetExposureParams(const ExposureParams& params) override;
    ExposureResult StartExposure() override;
    void AbortExposure() override;

    void RegisterAlarmCallback(AlarmCallback callback) override;
    void RegisterStatusCallback(StatusCallback callback) override;

    HvgCapabilities GetCapabilities() override;

    // =========================================================================
    // Simulator-Specific Methods
    // =========================================================================

    /**
     * @brief Generate a test alarm
     * @param alarm_code Alarm code
     * @param description Alarm description
     * @param severity Alarm severity
     */
    void GenerateTestAlarm(
        int32_t alarm_code,
        const std::string& description,
        AlarmSeverity severity
    );

    /**
     * @brief Get current simulator configuration
     * @return Current configuration
     */
    const SimulatorConfig& GetConfig() const { return config_; }

    /**
     * @brief Check if simulator is currently exposing
     * @return true if exposing
     */
    bool IsExposing() const { return state_.load() == GeneratorState::GEN_EXPOSING; }

private:
    // =========================================================================
    // Internal Methods
    // =========================================================================

    /**
     * @brief Validate exposure parameters against capabilities
     * @param params Parameters to validate
     * @return true if parameters are valid
     */
    bool ValidateParams(const ExposureParams& params);

    /**
     * @brief Background thread for status updates
     */
    void StatusUpdateThread();

    /**
     * @brief Simulate exposure process
     * @param duration_ms Exposure duration in milliseconds
     */
    void SimulateExposure(int32_t duration_ms);

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

    // =========================================================================
    // Member Variables
    // =========================================================================

    // Configuration
    SimulatorConfig config_;
    HvgCapabilities capabilities_;

    // State
    std::atomic<GeneratorState> state_;
    ExposureParams current_params_;
    HvgStatus current_status_;

    // Callbacks
    std::vector<AlarmCallback> alarm_callbacks_;
    std::vector<StatusCallback> status_callbacks_;
    mutable std::mutex callbacks_mutex_;

    // Synchronization
    mutable std::mutex state_mutex_;
    std::condition_variable state_cv_;

    // Background threads
    std::thread status_thread_;
    std::atomic<bool> running_;

    // Parameters flag
    std::atomic<bool> params_set_;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_GENERATOR_SIMULATOR_H
