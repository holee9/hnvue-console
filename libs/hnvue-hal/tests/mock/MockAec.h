/**
 * @file MockAec.h
 * @brief Google Mock implementation of IAEC interface for unit testing
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: Mock AEC for isolated unit testing
 * SPDX-License-Identifier: MIT
 *
 * This mock class enables SOUP-isolated unit testing per NFR-HAL-03.
 * All interface methods are mockable with customizable expectations.
 */

#ifndef HNUE_HAL_TESTS_MOCK_AEC_H
#define HNUE_HAL_TESTS_MOCK_AEC_H

#include "hnvue/hal/IAEC.h"
#include <gmock/gmock.h>

namespace hnvue::hal::test {

/**
 * @brief Google Mock implementation of IAEC interface
 *
 * Provides full mockability for all IAEC methods with:
 * - EXPECT_CALL() for behavior verification
 * - ON_CALL() for default return values
 * - WithArgs() for argument matching
 * - WillOnce()/WillRepeatedly() for action customization
 *
 * Thread Safety: Mock is thread-safe where the interface is thread-safe.
 * Use InSequence() for ordered call expectations.
 *
 * Safety Classification: IEC 62304 Class C
 * This mock MUST be used for all safety-critical AEC testing.
 */
class MockAec : public IAEC {
public:
    /**
     * @brief Default constructor with relaxed defaults
     *
     * By default, all methods return sensible default values:
     * - GetMode(): Returns AEC_MANUAL
     * - GetThreshold(): Returns 50.0%
     * - SetMode(): Returns true (success)
     * - SetThreshold(): Returns true (success)
     */
    MockAec() {
        // Set default return values for common calls
        ON_CALL(*this, GetMode())
            .WillByDefault(::testing::Return(AecMode::AEC_MANUAL));

        ON_CALL(*this, GetThreshold())
            .WillByDefault(::testing::Return(50.0f));

        ON_CALL(*this, SetMode(::testing::_))
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, SetThreshold(::testing::_))
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, RegisterTerminationCallback(::testing::_))
            .WillByDefault(::testing::Return());
    }

    /**
     * @brief Virtual destructor for proper cleanup
     */
    ~MockAec() override = default;

    // =========================================================================
    // Mock Methods - All IAEC interface methods
    // =========================================================================

    /**
     * @brief Mock SetMode
     *
     * Usage:
     *   EXPECT_CALL(mock, SetMode(AecMode::AEC_AUTO))
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        SetMode,
        (AecMode),
        (override)
    );

    /**
     * @brief Mock GetMode
     *
     * Usage:
     *   EXPECT_CALL(mock, GetMode())
     *       .WillOnce(Return(AecMode::AEC_AUTO));
     */
    MOCK_METHOD(
        AecMode,
        GetMode,
        (),
        (const, override)
    );

    /**
     * @brief Mock SetThreshold
     *
     * Usage:
     *   EXPECT_CALL(mock, SetThreshold(75.0f))
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        SetThreshold,
        (float),
        (override)
    );

    /**
     * @brief Mock GetThreshold
     *
     * Usage:
     *   EXPECT_CALL(mock, GetThreshold())
     *       .WillOnce(Return(75.0f));
     */
    MOCK_METHOD(
        float,
        GetThreshold,
        (),
        (const, override)
    );

    /**
     * @brief Mock RegisterTerminationCallback
     *
     * Usage:
     *   EXPECT_CALL(mock, RegisterTerminationCallback(::testing::_))
     *       .WillOnce(::testing::SaveArg<0>(&callback));
     *
     * Then invoke callback manually:
     *   AecTerminationEvent event{true, 5.0f, 100000};
     *   callback(event);
     */
    MOCK_METHOD(
        void,
        RegisterTerminationCallback,
        (AecTerminationCallback),
        (override)
    );
};

// =============================================================================
// Helper Functions for Common Mock Patterns
// =============================================================================

/**
 * @brief Create a successful AEC termination event for testing
 */
inline AecTerminationEvent CreateSuccessfulAecTerminationEvent(float dose_mgy, int64_t exposure_time_us) {
    AecTerminationEvent event;
    event.threshold_reached = true;
    event.actual_dose_mgy = dose_mgy;
    event.exposure_time_us = exposure_time_us;
    return event;
}

/**
 * @brief Create a failed AEC termination event for testing
 */
inline AecTerminationEvent CreateFailedAecTerminationEvent() {
    AecTerminationEvent event;
    event.threshold_reached = false;
    event.actual_dose_mgy = 0.0f;
    event.exposure_time_us = 0;
    return event;
}

} // namespace hnvue::hal::test

#endif // HNUE_HAL_TESTS_MOCK_AEC_H
