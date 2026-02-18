/**
 * @file MockGenerator.h
 * @brief Google Mock implementation of IGenerator interface for unit testing
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: Mock generator for isolated unit testing
 * SPDX-License-Identifier: MIT
 *
 * This mock class enables SOUP-isolated unit testing per NFR-HAL-03.
 * All interface methods are mockable with customizable expectations.
 */

#ifndef HNUE_HAL_TESTS_MOCK_GENERATOR_H
#define HNUE_HAL_TESTS_MOCK_GENERATOR_H

#include "hnvue/hal/IGenerator.h"
#include <gmock/gmock.h>

namespace hnvue::hal::test {

/**
 * @brief Google Mock implementation of IGenerator interface
 *
 * Provides full mockability for all IGenerator methods with:
 * - EXPECT_CALL() for behavior verification
 * - ON_CALL() for default return values
 * - WithArgs() for argument matching
 * - WillOnce()/WillRepeatedly() for action customization
 *
 * Thread Safety: Mock is thread-safe where the interface is thread-safe.
 * Use InSequence() for ordered call expectations.
 *
 * Safety Classification: IEC 62304 Class C
 * This mock MUST be used for all safety-critical generator testing.
 */
class MockGenerator : public IGenerator {
public:
    /**
     * @brief Default constructor with relaxed defaults
     *
     * By default, all methods return sensible default values:
     * - GetStatus(): Returns idle HvgStatus
     * - GetCapabilities(): Returns default capabilities
     * - SetExposureParams(): Returns true (success)
     * - StartExposure(): Returns failed ExposureResult
     * - AbortExposure(): No-op (safe to call)
     */
    MockGenerator() {
        // Set default return values for common calls
        ON_CALL(*this, GetStatus())
            .WillByDefault(::testing::Return(HvgStatus{}));

        ON_CALL(*this, GetCapabilities())
            .WillByDefault(::testing::Return(HvgCapabilities{}));

        ON_CALL(*this, SetExposureParams(::testing::_))
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, StartExposure())
            .WillByDefault(::testing::Return(ExposureResult{false, 0, 0, 0, 0, "Default mock failure"}));

        ON_CALL(*this, RegisterAlarmCallback(::testing::_))
            .WillByDefault(::testing::Return());

        ON_CALL(*this, RegisterStatusCallback(::testing::_))
            .WillByDefault(::testing::Return());
    }

    /**
     * @brief Virtual destructor for proper cleanup
     */
    ~MockGenerator() override = default;

    // =========================================================================
    // Mock Methods - All IGenerator interface methods
    // =========================================================================

    /**
     * @brief Mock GetStatus
     *
     * Usage:
     *   EXPECT_CALL(mock, GetStatus())
     *       .Times(::testing::AtLeast(1))
     *       .WillRepeatedly(Return(expected_status));
     */
    MOCK_METHOD(
        HvgStatus,
        GetStatus,
        (),
        (override)
    );

    /**
     * @brief Mock SetExposureParams
     *
     * Usage:
     *   EXPECT_CALL(mock, SetExposureParams(Field(&ExposureParams::kvp, 80.0f)))
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        SetExposureParams,
        (const ExposureParams&),
        (override)
    );

    /**
     * @brief Mock StartExposure
     *
     * Usage:
     *   EXPECT_CALL(mock, StartExposure())
     *       .WillOnce(Return(ExposureResult{true, 80.0f, 100.0f, 100.0f, 10.0f, ""}));
     */
    MOCK_METHOD(
        ExposureResult,
        StartExposure,
        (),
        (override)
    );

    /**
     * @brief Mock AbortExposure
     *
     * Usage:
     *   EXPECT_CALL(mock, AbortExposure())
     *       .Times(1);
     */
    MOCK_METHOD(
        void,
        AbortExposure,
        (),
        (override)
    );

    /**
     * @brief Mock RegisterAlarmCallback
     *
     * Usage:
     *   EXPECT_CALL(mock, RegisterAlarmCallback(::testing::_))
     *       .WillOnce(::testing::SaveArg<0>(&alarm_callback));
     *
     * Then invoke callback manually:
     *   alarm_callback(test_alarm);
     */
    MOCK_METHOD(
        void,
        RegisterAlarmCallback,
        (AlarmCallback),
        (override)
    );

    /**
     * @brief Mock RegisterStatusCallback
     *
     * Usage:
     *   EXPECT_CALL(mock, RegisterStatusCallback(::testing::_))
     *       .WillOnce(::testing::SaveArg<0>(&status_callback));
     *
     * Then invoke callback manually:
     *   status_callback(test_status);
     */
    MOCK_METHOD(
        void,
        RegisterStatusCallback,
        (StatusCallback),
        (override)
    );

    /**
     * @brief Mock GetCapabilities
     *
     * Usage:
     *   EXPECT_CALL(mock, GetCapabilities())
     *       .WillOnce(Return(expected_capabilities));
     */
    MOCK_METHOD(
        HvgCapabilities,
        GetCapabilities,
        (),
        (override)
    );
};

// =============================================================================
// Helper Functions for Common Mock Patterns
// =============================================================================

/**
 * @brief Create default HvgCapabilities for testing
 */
inline HvgCapabilities CreateDefaultHvgCapabilities() {
    HvgCapabilities caps;
    caps.min_kvp = 40.0f;
    caps.max_kvp = 150.0f;
    caps.min_ma = 0.1f;
    caps.max_ma = 1000.0f;
    caps.min_ms = 1.0f;
    caps.max_ms = 10000.0f;
    caps.has_aec = true;
    caps.has_dual_focus = true;
    caps.vendor_name = "MockVendor";
    caps.model_name = "MockGenerator";
    caps.firmware_version = "1.0.0";
    return caps;
}

/**
 * @brief Create an idle HvgStatus for testing
 */
inline HvgStatus CreateIdleHvgStatus() {
    HvgStatus status;
    status.actual_kvp = 0.0f;
    status.actual_ma = 0.0f;
    status.state = GeneratorState::GEN_IDLE;
    status.interlock_ok = true;
    status.timestamp_us = 0;
    return status;
}

/**
 * @brief Create a ready HvgStatus for testing
 */
inline HvgStatus CreateReadyHvgStatus() {
    HvgStatus status;
    status.actual_kvp = 0.0f;
    status.actual_ma = 0.0f;
    status.state = GeneratorState::GEN_READY;
    status.interlock_ok = true;
    status.timestamp_us = 0;
    return status;
}

/**
 * @brief Create an exposing HvgStatus for testing
 */
inline HvgStatus CreateExposingHvgStatus(float kvp, float ma) {
    HvgStatus status;
    status.actual_kvp = kvp;
    status.actual_ma = ma;
    status.state = GeneratorState::GEN_EXPOSING;
    status.interlock_ok = true;
    status.timestamp_us = 0;
    return status;
}

/**
 * @brief Create a successful ExposureResult for testing
 */
inline ExposureResult CreateSuccessfulExposureResult(float kvp, float ma, float ms) {
    ExposureResult result;
    result.success = true;
    result.actual_kvp = kvp;
    result.actual_ma = ma;
    result.actual_ms = ms;
    result.actual_mas = (ma * ms) / 1000.0f;
    result.error_msg = "";
    return result;
}

} // namespace hnvue::hal::test

#endif // HNUE_HAL_TESTS_MOCK_GENERATOR_H
