/**
 * @file MockSafetyInterlock.h
 * @brief Google Mock implementation of ISafetyInterlock interface for unit testing
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: Mock safety interlock for isolated unit testing
 * SPDX-License-Identifier: MIT
 *
 * This mock class enables SOUP-isolated unit testing per NFR-HAL-03.
 * All interface methods are mockable with customizable expectations.
 */

#ifndef HNUE_HAL_TESTS_MOCK_SAFETY_INTERLOCK_H
#define HNUE_HAL_TESTS_MOCK_SAFETY_INTERLOCK_H

#include "hnvue/hal/ISafetyInterlock.h"
#include <gmock/gmock.h>

namespace hnvue::hal::test {

/**
 * @brief Google Mock implementation of ISafetyInterlock interface
 *
 * Provides full mockability for all ISafetyInterlock methods with:
 * - EXPECT_CALL() for behavior verification
 * - ON_CALL() for default return values
 * - WithArgs() for argument matching
 * - WillOnce()/WillRepeatedly() for action customization
 *
 * Thread Safety: Mock is thread-safe where the interface is thread-safe.
 * Use InSequence() for ordered call expectations.
 *
 * Safety Classification: IEC 62304 Class C
 * This mock MUST be used for all safety-critical interlock testing.
 */
class MockSafetyInterlock : public ISafetyInterlock {
public:
    /**
     * @brief Default constructor with relaxed defaults
     *
     * By default, all methods return sensible default values:
     * - CheckAllInterlocks(): Returns all-pass InterlockStatus
     * - CheckInterlock(): Returns true (pass)
     * - GetDoorStatus(): Returns true (closed)
     * - GetEStopStatus(): Returns true (clear)
     * - GetThermalStatus(): Returns true (normal)
     * - EmergencyStandby(): No-op (safe to call)
     */
    MockSafetyInterlock() {
        // Set default return values for common calls
        InterlockStatus default_status;
        default_status.door_closed = true;
        default_status.emergency_stop_clear = true;
        default_status.thermal_normal = true;
        default_status.generator_ready = true;
        default_status.detector_ready = true;
        default_status.collimator_valid = true;
        default_status.table_locked = true;
        default_status.dose_within_limits = true;
        default_status.aec_configured = true;
        default_status.all_passed = true;
        default_status.timestamp_us = 0;

        ON_CALL(*this, CheckAllInterlocks())
            .WillByDefault(::testing::Return(default_status));

        ON_CALL(*this, CheckInterlock(::testing::_))
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, GetDoorStatus())
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, GetEStopStatus())
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, GetThermalStatus())
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, EmergencyStandby())
            .WillByDefault(::testing::Return());

        ON_CALL(*this, RegisterInterlockCallback(::testing::_))
            .WillByDefault(::testing::Return());
    }

    /**
     * @brief Virtual destructor for proper cleanup
     */
    ~MockSafetyInterlock() override = default;

    // =========================================================================
    // Mock Methods - All ISafetyInterlock interface methods
    // =========================================================================

    /**
     * @brief Mock CheckAllInterlocks
     *
     * Usage:
     *   EXPECT_CALL(mock, CheckAllInterlocks())
     *       .WillOnce(Return(expected_status));
     */
    MOCK_METHOD(
        InterlockStatus,
        CheckAllInterlocks,
        (),
        (override)
    );

    /**
     * @brief Mock CheckInterlock
     *
     * Usage:
     *   EXPECT_CALL(mock, CheckInterlock(0))
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        CheckInterlock,
        (int),
        (override)
    );

    /**
     * @brief Mock GetDoorStatus
     *
     * Usage:
     *   EXPECT_CALL(mock, GetDoorStatus())
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        GetDoorStatus,
        (),
        (override)
    );

    /**
     * @brief Mock GetEStopStatus
     *
     * Usage:
     *   EXPECT_CALL(mock, GetEStopStatus())
     *       .WillOnce(Return(false));
     */
    MOCK_METHOD(
        bool,
        GetEStopStatus,
        (),
        (override)
    );

    /**
     * @brief Mock GetThermalStatus
     *
     * Usage:
     *   EXPECT_CALL(mock, GetThermalStatus())
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        GetThermalStatus,
        (),
        (override)
    );

    /**
     * @brief Mock EmergencyStandby
     *
     * Usage:
     *   EXPECT_CALL(mock, EmergencyStandby())
     *       .Times(1);
     */
    MOCK_METHOD(
        void,
        EmergencyStandby,
        (),
        (override)
    );

    /**
     * @brief Mock RegisterInterlockCallback
     *
     * Usage:
     *   EXPECT_CALL(mock, RegisterInterlockCallback(::testing::_))
     *       .WillOnce(::testing::SaveArg<0>(&callback));
     *
     * Then invoke callback manually:
     *   callback(test_status);
     */
    MOCK_METHOD(
        void,
        RegisterInterlockCallback,
        (InterlockCallback),
        (override)
    );
};

// =============================================================================
// Helper Functions for Common Mock Patterns
// =============================================================================

/**
 * @brief Create an all-pass InterlockStatus for testing
 */
inline InterlockStatus CreateAllPassInterlockStatus() {
    InterlockStatus status;
    status.door_closed = true;
    status.emergency_stop_clear = true;
    status.thermal_normal = true;
    status.generator_ready = true;
    status.detector_ready = true;
    status.collimator_valid = true;
    status.table_locked = true;
    status.dose_within_limits = true;
    status.aec_configured = true;
    status.all_passed = true;
    status.timestamp_us = 0;
    return status;
}

/**
 * @brief Create a specific InterlockStatus for testing
 */
inline InterlockStatus CreateInterlockStatus(
    bool door_closed,
    bool e_stop_clear,
    bool thermal_normal,
    bool generator_ready,
    bool detector_ready
) {
    InterlockStatus status;
    status.door_closed = door_closed;
    status.emergency_stop_clear = e_stop_clear;
    status.thermal_normal = thermal_normal;
    status.generator_ready = generator_ready;
    status.detector_ready = detector_ready;
    status.collimator_valid = true;
    status.table_locked = true;
    status.dose_within_limits = true;
    status.aec_configured = true;
    status.all_passed = door_closed && e_stop_clear && thermal_normal &&
                       generator_ready && detector_ready && true && true && true && true;
    status.timestamp_us = 0;
    return status;
}

} // namespace hnvue::hal::test

#endif // HNUE_HAL_TESTS_MOCK_SAFETY_INTERLOCK_H
