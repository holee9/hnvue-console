/**
 * @file MockDoseMonitor.h
 * @brief Google Mock implementation of IDoseMonitor interface for unit testing
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Mock dose monitor for isolated unit testing
 * SPDX-License-Identifier: MIT
 *
 * This mock class enables SOUP-isolated unit testing per NFR-HAL-03.
 * All interface methods are mockable with customizable expectations.
 */

#ifndef HNUE_HAL_TESTS_MOCK_DOSE_MONITOR_H
#define HNUE_HAL_TESTS_MOCK_DOSE_MONITOR_H

#include "hnvue/hal/IDoseMonitor.h"
#include <gmock/gmock.h>

namespace hnvue::hal::test {

/**
 * @brief Google Mock implementation of IDoseMonitor interface
 *
 * Provides full mockability for all IDoseMonitor methods with:
 * - EXPECT_CALL() for behavior verification
 * - ON_CALL() for default return values
 * - WithArgs() for argument matching
 * - WillOnce()/WillRepeatedly() for action customization
 *
 * Thread Safety: Mock is thread-safe where the interface is thread-safe.
 * Use InSequence() for ordered call expectations.
 */
class MockDoseMonitor : public IDoseMonitor {
public:
    /**
     * @brief Default constructor with relaxed defaults
     *
     * By default, all methods return sensible default values:
     * - GetCurrentDose(): Returns zero dose reading
     * - GetDap(): Returns 0.0 uGy*cm^2
     * - Reset(): No-op (safe to call)
     */
    MockDoseMonitor() {
        // Set default return values for common calls
        ON_CALL(*this, GetCurrentDose())
            .WillByDefault(::testing::Return(DoseReading{}));

        ON_CALL(*this, GetDap())
            .WillByDefault(::testing::Return(0.0f));

        ON_CALL(*this, Reset())
            .WillByDefault(::testing::Return());

        ON_CALL(*this, RegisterDoseCallback(::testing::_))
            .WillByDefault(::testing::Return());
    }

    /**
     * @brief Virtual destructor for proper cleanup
     */
    ~MockDoseMonitor() override = default;

    // =========================================================================
    // Mock Methods - All IDoseMonitor interface methods
    // =========================================================================

    /**
     * @brief Mock GetCurrentDose
     *
     * Usage:
     *   EXPECT_CALL(mock, GetCurrentDose())
     *       .WillOnce(Return(expected_dose));
     */
    MOCK_METHOD(
        DoseReading,
        GetCurrentDose,
        (),
        (override)
    );

    /**
     * @brief Mock GetDap
     *
     * Usage:
     *   EXPECT_CALL(mock, GetDap())
     *       .WillOnce(Return(100.0f));
     */
    MOCK_METHOD(
        float,
        GetDap,
        (),
        (override)
    );

    /**
     * @brief Mock Reset
     *
     * Usage:
     *   EXPECT_CALL(mock, Reset())
     *       .Times(1);
     */
    MOCK_METHOD(
        void,
        Reset,
        (),
        (override)
    );

    /**
     * @brief Mock RegisterDoseCallback
     *
     * Usage:
     *   EXPECT_CALL(mock, RegisterDoseCallback(::testing::_))
     *       .WillOnce(::testing::SaveArg<0>(&callback));
     *
     * Then invoke callback manually:
     *   callback(test_dose);
     */
    MOCK_METHOD(
        void,
        RegisterDoseCallback,
        (DoseCallback),
        (override)
    );
};

// =============================================================================
// Helper Functions for Common Mock Patterns
// =============================================================================

/**
 * @brief Create a zero DoseReading for testing
 */
inline DoseReading CreateZeroDoseReading() {
    DoseReading reading;
    reading.dose_mgy = 0.0f;
    reading.dose_rate_mgy_s = 0.0f;
    reading.dap_ugy_cm2 = 0.0f;
    reading.timestamp_us = 0;
    return reading;
}

/**
 * @brief Create a specific DoseReading for testing
 */
inline DoseReading CreateDoseReading(float dose_mgy, float dose_rate_mgy_s, float dap_ugy_cm2) {
    DoseReading reading;
    reading.dose_mgy = dose_mgy;
    reading.dose_rate_mgy_s = dose_rate_mgy_s;
    reading.dap_ugy_cm2 = dap_ugy_cm2;
    reading.timestamp_us = 0;
    return reading;
}

} // namespace hnvue::hal::test

#endif // HNUE_HAL_TESTS_MOCK_DOSE_MONITOR_H
