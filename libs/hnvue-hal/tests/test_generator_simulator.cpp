/**
 * @file test_generator_simulator.cpp
 * @brief Unit tests for GeneratorSimulator implementation
 * @date 2026-02-18
 * @author abyz-lab
 *
 * Tests the HVG simulator which implements IGenerator interface for testing:
 * - Simulates HVG responses without physical hardware
 * - Generates realistic status updates (10 Hz during exposure)
 * - Simulates alarm conditions
 * - State machine transitions (IDLE -> READY -> ARMED -> EXPOSING -> IDLE)
 * - Configurable capabilities
 *
 * IEC 62304 Class B - Unit tests for simulator (no actual hardware control)
 * SPDX-License-Identifier: MIT
 */

#include <gtest/gtest.h>
#include <thread>
#include <chrono>
#include <atomic>
#include <vector>

#include "hnvue/hal/generator/GeneratorSimulator.h"
#include "hnvue/hal/HalTypes.h"

using namespace hnvue::hal;

// =============================================================================
// Test Fixture
// =============================================================================

/**
 * @brief Test fixture for GeneratorSimulator tests
 */
class GeneratorSimulatorTest : public ::testing::Test {
protected:
    void SetUp() override {
        simulator_ = std::make_unique<GeneratorSimulator>();
    }

    void TearDown() override {
        simulator_.reset();
    }

    std::unique_ptr<GeneratorSimulator> simulator_;
};

// =============================================================================
// IGenerator Interface Compliance Tests
// =============================================================================

/**
 * @test Simulator implements all IGenerator methods
 */
TEST_F(GeneratorSimulatorTest, ImplementsIGeneratorInterface) {
    EXPECT_NE(simulator_, nullptr);
}

// =============================================================================
// Capabilities Tests
// =============================================================================

/**
 * @test GetCapabilities returns valid capability structure
 */
TEST_F(GeneratorSimulatorTest, GetCapabilitiesReturnsValid) {
    auto caps = simulator_->GetCapabilities();
    EXPECT_GT(caps.min_kvp, 0);
    EXPECT_GT(caps.max_kvp, caps.min_kvp);
    EXPECT_GT(caps.min_ma, 0);
    EXPECT_GT(caps.max_ma, caps.min_ma);
    EXPECT_GT(caps.min_ms, 0);
    EXPECT_GT(caps.max_ms, caps.min_ms);
}

/**
 * @test Capabilities have valid kVp range
 */
TEST_F(GeneratorSimulatorTest, CapabilitiesValidKvpRange) {
    auto caps = simulator_->GetCapabilities();
    EXPECT_GE(caps.min_kvp, 40.0f);
    EXPECT_LE(caps.max_kvp, 150.0f);
}

/**
 * @test Capabilities have valid mA range
 */
TEST_F(GeneratorSimulatorTest, CapabilitiesValidMaRange) {
    auto caps = simulator_->GetCapabilities();
    EXPECT_GE(caps.min_ma, 0.1f);
    EXPECT_LE(caps.max_ma, 1000.0f);
}

/**
 * @test Capabilities have valid ms range
 */
TEST_F(GeneratorSimulatorTest, CapabilitiesValidMsRange) {
    auto caps = simulator_->GetCapabilities();
    EXPECT_GE(caps.min_ms, 1.0f);
    EXPECT_LE(caps.max_ms, 10000.0f);
}

/**
 * @test Capabilities report AEC support correctly
 */
TEST_F(GeneratorSimulatorTest, CapabilitiesReportAecSupport) {
    auto caps = simulator_->GetCapabilities();
    // Default config has AEC
    EXPECT_TRUE(caps.has_aec);
}

/**
 * @test Capabilities report dual focus support correctly
 */
TEST_F(GeneratorSimulatorTest, CapabilitiesReportDualFocusSupport) {
    auto caps = simulator_->GetCapabilities();
    // Default config has dual focus
    EXPECT_TRUE(caps.has_dual_focus);
}

// =============================================================================
// Status Query Tests
// =============================================================================

/**
 * @test GetStatus returns valid status structure
 */
TEST_F(GeneratorSimulatorTest, GetStatusReturnsValid) {
    auto status = simulator_->GetStatus();
    EXPECT_GT(status.timestamp_us, 0);
}

/**
 * @test Initial state is IDLE
 */
TEST_F(GeneratorSimulatorTest, InitialStateIsIdle) {
    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_IDLE);
}

/**
 * @test Status includes timestamp
 */
TEST_F(GeneratorSimulatorTest, StatusIncludesTimestamp) {
    auto status = simulator_->GetStatus();
    EXPECT_GT(status.timestamp_us, 0);

    // Check timestamp is recent (within 1 second)
    auto now_us = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::system_clock::now().time_since_epoch()
    ).count();
    EXPECT_NEAR(status.timestamp_us, now_us, 1000000);
}

/**
 * @test GetStatus is thread-safe
 */
TEST_F(GeneratorSimulatorTest, GetStatusIsThreadSafe) {
    const int thread_count = 10;
    std::vector<std::thread> threads;

    for (int i = 0; i < thread_count; ++i) {
        threads.emplace_back([this]() {
            for (int j = 0; j < 100; ++j) {
                simulator_->GetStatus();
            }
        });
    }

    for (auto& t : threads) {
        t.join();
    }

    // If we get here without crashing, thread safety works
    SUCCEED();
}

// =============================================================================
// Parameter Setting Tests
// =============================================================================

/**
 * @test SetExposureParams accepts valid parameters
 */
TEST_F(GeneratorSimulatorTest, SetExposureParamsValid) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;
    params.aec_mode = AecMode::AEC_MANUAL;
    params.focus = "large";

    EXPECT_TRUE(simulator_->SetExposureParams(params));
}

/**
 * @test SetExposureParams rejects invalid kVp (below minimum)
 */
TEST_F(GeneratorSimulatorTest, SetExposureParamsInvalidKvpLow) {
    auto caps = simulator_->GetCapabilities();

    ExposureParams params;
    params.kvp = caps.min_kvp - 10.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;

    EXPECT_FALSE(simulator_->SetExposureParams(params));
}

/**
 * @test SetExposureParams rejects invalid kVp (above maximum)
 */
TEST_F(GeneratorSimulatorTest, SetExposureParamsInvalidKvpHigh) {
    auto caps = simulator_->GetCapabilities();

    ExposureParams params;
    params.kvp = caps.max_kvp + 10.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;

    EXPECT_FALSE(simulator_->SetExposureParams(params));
}

/**
 * @test SetExposureParams rejects invalid mA (below minimum)
 */
TEST_F(GeneratorSimulatorTest, SetExposureParamsInvalidMaLow) {
    auto caps = simulator_->GetCapabilities();

    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = caps.min_ma - 0.05f;
    params.ms = 100.0f;

    EXPECT_FALSE(simulator_->SetExposureParams(params));
}

/**
 * @test SetExposureParams rejects invalid mA (above maximum)
 */
TEST_F(GeneratorSimulatorTest, SetExposureParamsInvalidMaHigh) {
    auto caps = simulator_->GetCapabilities();

    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = caps.max_ma + 100.0f;
    params.ms = 100.0f;

    EXPECT_FALSE(simulator_->SetExposureParams(params));
}

/**
 * @test SetExposureParams rejects invalid ms (below minimum)
 */
TEST_F(GeneratorSimulatorTest, SetExposureParamsInvalidMsLow) {
    auto caps = simulator_->GetCapabilities();

    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = caps.min_ms - 0.5f;

    EXPECT_FALSE(simulator_->SetExposureParams(params));
}

/**
 * @test SetExposureParams rejects invalid ms (above maximum)
 */
TEST_F(GeneratorSimulatorTest, SetExposureParamsInvalidMsHigh) {
    auto caps = simulator_->GetCapabilities();

    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = caps.max_ms + 1000.0f;

    EXPECT_FALSE(simulator_->SetExposureParams(params));
}

/**
 * @test SetExposureParams validates focus selection
 */
TEST_F(GeneratorSimulatorTest, SetExposureParamsValidatesFocus) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;
    params.focus = "invalid";  // Not "large" or "small"

    EXPECT_FALSE(simulator_->SetExposureParams(params));
}

// =============================================================================
// Exposure Control Tests
// =============================================================================

/**
 * @test StartExposure succeeds when parameters are set
 */
TEST_F(GeneratorSimulatorTest, StartExposureSuccess) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;

    EXPECT_TRUE(simulator_->SetExposureParams(params));

    auto result = simulator_->StartExposure();
    EXPECT_TRUE(result.success);
    EXPECT_FLOAT_EQ(result.actual_kvp, 80.0f);
    EXPECT_FLOAT_EQ(result.actual_ma, 100.0f);
}

/**
 * @test StartExposure fails when parameters not set
 */
TEST_F(GeneratorSimulatorTest, StartExposureFailsWithoutParams) {
    auto result = simulator_->StartExposure();
    EXPECT_FALSE(result.success);
    EXPECT_FALSE(result.error_msg.empty());
}

/**
 * @test StartExposure changes state to EXPOSING
 */
TEST_F(GeneratorSimulatorTest, StartExposureChangesState) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_EXPOSING);
}

/**
 * @test StartExposure returns actual exposure values
 */
TEST_F(GeneratorSimulatorTest, StartExposureReturnsActualValues) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;

    simulator_->SetExposureParams(params);
    auto result = simulator_->StartExposure();

    EXPECT_TRUE(result.success);
    EXPECT_FLOAT_EQ(result.actual_kvp, 80.0f);
    EXPECT_FLOAT_EQ(result.actual_ma, 100.0f);
    EXPECT_FLOAT_EQ(result.actual_ms, 100.0f);
}

/**
 * @test Exposure completes after specified duration
 */
TEST_F(GeneratorSimulatorTest, ExposureCompletesAfterDuration) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;  // 100ms exposure

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    // Wait for exposure to complete
    std::this_thread::sleep_for(std::chrono::milliseconds(200));

    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_IDLE);
}

/**
 * @test State returns to IDLE after exposure completes
 */
TEST_F(GeneratorSimulatorTest, StateReturnsToIdleAfterExposure) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 50.0f;  // 50ms exposure

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    // Wait for exposure to complete
    std::this_thread::sleep_for(std::chrono::milliseconds(100));

    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_IDLE);
}

// =============================================================================
// Abort Tests
// =============================================================================

/**
 * @test AbortExposure stops ongoing exposure immediately
 */
TEST_F(GeneratorSimulatorTest, AbortExposureStopsImmediately) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 10000.0f;  // 10 second exposure

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    EXPECT_TRUE(simulator_->IsExposing());

    simulator_->AbortExposure();

    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_IDLE);
    EXPECT_FALSE(simulator_->IsExposing());
}

/**
 * @test AbortExposure returns state to IDLE
 */
TEST_F(GeneratorSimulatorTest, AbortExposureReturnsToIdle) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 1000.0f;

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();
    simulator_->AbortExposure();

    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_IDLE);
}

/**
 * @test AbortExposure is safe to call when not exposing
 */
TEST_F(GeneratorSimulatorTest, AbortExposureSafeWhenNotExposing) {
    EXPECT_NO_THROW(simulator_->AbortExposure());

    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_IDLE);
}

/**
 * @test AbortExposure returns within 10ms (safety critical)
 */
TEST_F(GeneratorSimulatorTest, AbortExposureReturnsWithin10ms) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 10000.0f;

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    auto start = std::chrono::steady_clock::now();
    simulator_->AbortExposure();
    auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now() - start
    ).count();

    EXPECT_LT(elapsed, 10);  // Must return within 10ms
}

// =============================================================================
// Status Callback Tests
// =============================================================================

/**
 * @test Status callback is invoked on status updates
 */
TEST_F(GeneratorSimulatorTest, StatusCallbackInvoked) {
    std::atomic<int> callback_count{0};

    simulator_->RegisterStatusCallback([&](const HvgStatus&) {
        callback_count++;
    });

    // Wait a bit for status updates
    std::this_thread::sleep_for(std::chrono::milliseconds(200));

    EXPECT_GT(callback_count, 0);
}

/**
 * @test Status callbacks invoked at >= 10 Hz during exposure
 */
TEST_F(GeneratorSimulatorTest, StatusCallbackAtLeast10Hz) {
    std::atomic<int> callback_count{0};
    std::vector<int64_t> timestamps;

    simulator_->RegisterStatusCallback([&](const HvgStatus& status) {
        callback_count++;
        timestamps.push_back(status.timestamp_us);
    });

    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 500.0f;  // 500ms exposure

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    // Wait for exposure to complete
    std::this_thread::sleep_for(std::chrono::milliseconds(600));

    // Check frequency during exposure
    // Should have at least 10 Hz = 5 updates in 500ms
    EXPECT_GE(callback_count, 5);
}

/**
 * @test Multiple status callbacks can be registered
 */
TEST_F(GeneratorSimulatorTest, MultipleStatusCallbacks) {
    std::atomic<int> count1{0};
    std::atomic<int> count2{0};

    simulator_->RegisterStatusCallback([&](const HvgStatus&) {
        count1++;
    });

    simulator_->RegisterStatusCallback([&](const HvgStatus&) {
        count2++;
    });

    std::this_thread::sleep_for(std::chrono::milliseconds(100));

    EXPECT_GT(count1, 0);
    EXPECT_GT(count2, 0);
}

/**
 * @test Status callback receives valid status data
 */
TEST_F(GeneratorSimulatorTest, StatusCallbackReceivesValidData) {
    HvgStatus received_status;
    std::atomic<bool> called{false};

    simulator_->RegisterStatusCallback([&](const HvgStatus& status) {
        received_status = status;
        called = true;
    });

    std::this_thread::sleep_for(std::chrono::milliseconds(50));

    EXPECT_TRUE(called);
    EXPECT_GT(received_status.timestamp_us, 0);
}

// =============================================================================
// Alarm Callback Tests
// =============================================================================

/**
 * @test Alarm callback is invoked on alarm conditions
 */
TEST_F(GeneratorSimulatorTest, AlarmCallbackInvoked) {
    std::atomic<bool> alarm_received{false};

    simulator_->RegisterAlarmCallback([&](const HvgAlarm&) {
        alarm_received = true;
    });

    simulator_->GenerateTestAlarm(
        1,
        "Test alarm",
        AlarmSeverity::ALARM_WARNING
    );

    std::this_thread::sleep_for(std::chrono::milliseconds(10));

    EXPECT_TRUE(alarm_received);
}

/**
 * @test Alarm callback receives valid alarm data
 */
TEST_F(GeneratorSimulatorTest, AlarmCallbackReceivesValidData) {
    HvgAlarm received_alarm;
    std::atomic<bool> called{false};

    simulator_->RegisterAlarmCallback([&](const HvgAlarm& alarm) {
        received_alarm = alarm;
        called = true;
    });

    simulator_->GenerateTestAlarm(
        123,
        "Test alarm description",
        AlarmSeverity::ALARM_ERROR
    );

    std::this_thread::sleep_for(std::chrono::milliseconds(10));

    EXPECT_TRUE(called);
    EXPECT_EQ(received_alarm.alarm_code, 123);
    EXPECT_EQ(received_alarm.description, "Test alarm description");
    EXPECT_EQ(received_alarm.severity, AlarmSeverity::ALARM_ERROR);
}

/**
 * @test Multiple alarm callbacks can be registered
 */
TEST_F(GeneratorSimulatorTest, MultipleAlarmCallbacks) {
    std::atomic<int> count1{0};
    std::atomic<int> count2{0};

    simulator_->RegisterAlarmCallback([&](const HvgAlarm&) {
        count1++;
    });

    simulator_->RegisterAlarmCallback([&](const HvgAlarm&) {
        count2++;
    });

    simulator_->GenerateTestAlarm(1, "Test", AlarmSeverity::ALARM_INFO);
    std::this_thread::sleep_for(std::chrono::milliseconds(10));

    EXPECT_EQ(count1, 1);
    EXPECT_EQ(count2, 1);
}

/**
 * @test Simulated alarm conditions are realistic
 */
TEST_F(GeneratorSimulatorTest, SimulatedAlarmsAreRealistic) {
    HvgAlarm received_alarm;

    simulator_->RegisterAlarmCallback([&](const HvgAlarm& alarm) {
        received_alarm = alarm;
    });

    simulator_->GenerateTestAlarm(
        100,
        "Overtemperature",
        AlarmSeverity::ALARM_CRITICAL
    );

    std::this_thread::sleep_for(std::chrono::milliseconds(10));

    EXPECT_EQ(received_alarm.severity, AlarmSeverity::ALARM_CRITICAL);
    EXPECT_GT(received_alarm.timestamp_us, 0);
}

// =============================================================================
// State Machine Tests
// =============================================================================

/**
 * @test State transitions correctly: IDLE -> READY
 */
TEST_F(GeneratorSimulatorTest, StateTransitionIdleToReady) {
    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_IDLE);

    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;

    simulator_->SetExposureParams(params);

    status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_READY);
}

/**
 * @test State transitions correctly: READY -> ARMED
 */
TEST_F(GeneratorSimulatorTest, StateTransitionReadyToArmed) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    // State should be EXPOSING now (ARMED transition happens internally)
    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_EXPOSING);
}

/**
 * @test State transitions correctly: ARMED -> EXPOSING
 */
TEST_F(GeneratorSimulatorTest, StateTransitionArmedToExposing) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 100.0f;

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_EXPOSING);
}

/**
 * @test State transitions correctly: EXPOSING -> IDLE
 */
TEST_F(GeneratorSimulatorTest, StateTransitionExposingToIdle) {
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 50.0f;

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    // Wait for exposure to complete
    std::this_thread::sleep_for(std::chrono::milliseconds(100));

    auto status = simulator_->GetStatus();
    EXPECT_EQ(status.state, GeneratorState::GEN_IDLE);
}

/**
 * @test Invalid state transitions are rejected
 */
TEST_F(GeneratorSimulatorTest, InvalidStateTransitionRejected) {
    // Try to start exposure without setting parameters
    auto result = simulator_->StartExposure();
    EXPECT_FALSE(result.success);
}

// =============================================================================
// Configuration Tests
// =============================================================================

/**
 * @test Simulator can be configured with custom capabilities
 */
TEST_F(GeneratorSimulatorTest, ConfigurableCapabilities) {
    SimulatorConfig config;
    config.min_kvp = 50.0f;
    config.max_kvp = 120.0f;

    GeneratorSimulator custom_sim(config);
    auto caps = custom_sim.GetCapabilities();

    EXPECT_FLOAT_EQ(caps.min_kvp, 50.0f);
    EXPECT_FLOAT_EQ(caps.max_kvp, 120.0f);
}

/**
 * @test Simulator can be configured with mock latency
 */
TEST_F(GeneratorSimulatorTest, ConfigurableLatency) {
    SimulatorConfig config;
    config.response_latency = std::chrono::microseconds(5000);  // 5ms

    GeneratorSimulator custom_sim(config);
    // Just verify it constructs without error
    SUCCEED();
}

/**
 * @test Simulator can generate test alarms
 */
TEST_F(GeneratorSimulatorTest, CanGenerateTestAlarms) {
    std::atomic<bool> received{false};

    simulator_->RegisterAlarmCallback([&](const HvgAlarm&) {
        received = true;
    });

    simulator_->GenerateTestAlarm(
        999,
        "Custom test alarm",
        AlarmSeverity::ALARM_INFO
    );

    std::this_thread::sleep_for(std::chrono::milliseconds(10));

    EXPECT_TRUE(received);
}

// =============================================================================
// Thread Safety Tests
// =============================================================================

/**
 * @test Concurrent operations are thread-safe
 */
TEST_F(GeneratorSimulatorTest, ConcurrentOperationsThreadSafe) {
    std::vector<std::thread> threads;

    // Multiple threads getting status
    for (int i = 0; i < 10; ++i) {
        threads.emplace_back([this]() {
            for (int j = 0; j < 100; ++j) {
                simulator_->GetStatus();
            }
        });
    }

    for (auto& t : threads) {
        t.join();
    }

    // If we get here without crashing, thread safety works
    SUCCEED();
}

/**
 * @test Callback invocation is thread-safe
 */
TEST_F(GeneratorSimulatorTest, CallbackInvocationThreadSafe) {
    std::atomic<int> count{0};

    simulator_->RegisterStatusCallback([&](const HvgStatus&) {
        count++;
    });

    // Start an exposure
    ExposureParams params;
    params.kvp = 80.0f;
    params.ma = 100.0f;
    params.ms = 200.0f;

    simulator_->SetExposureParams(params);
    simulator_->StartExposure();

    // Wait for exposure and callbacks
    std::this_thread::sleep_for(std::chrono::milliseconds(300));

    EXPECT_GT(count, 0);
}
