/**
 * @file MockPatientTable.h
 * @brief Google Mock implementation of IPatientTable interface for unit testing
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Mock patient table for isolated unit testing
 * SPDX-License-Identifier: MIT
 *
 * This mock class enables SOUP-isolated unit testing per NFR-HAL-03.
 * All interface methods are mockable with customizable expectations.
 */

#ifndef HNUE_HAL_TESTS_MOCK_PATIENT_TABLE_H
#define HNUE_HAL_TESTS_MOCK_PATIENT_TABLE_H

#include "hnvue/hal/IPatientTable.h"
#include <gmock/gmock.h>

namespace hnvue::hal::test {

/**
 * @brief Google Mock implementation of IPatientTable interface
 *
 * Provides full mockability for all IPatientTable methods with:
 * - EXPECT_CALL() for behavior verification
 * - ON_CALL() for default return values
 * - WithArgs() for argument matching
 * - WillOnce()/WillRepeatedly() for action customization
 *
 * Thread Safety: Mock is thread-safe where the interface is thread-safe.
 * Use InSequence() for ordered call expectations.
 */
class MockPatientTable : public IPatientTable {
public:
    /**
     * @brief Default constructor with relaxed defaults
     *
     * By default, all methods return sensible default values:
     * - GetPosition(): Returns zero position (at isocenter)
     * - IsMotorized(): Returns true (motorized)
     * - MoveTo(): Returns true (success)
     */
    MockPatientTable() {
        // Set default return values for common calls
        ON_CALL(*this, GetPosition())
            .WillByDefault(::testing::Return(TablePosition{}));

        ON_CALL(*this, IsMotorized())
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, MoveTo(::testing::_))
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, RegisterPositionCallback(::testing::_))
            .WillByDefault(::testing::Return());
    }

    /**
     * @brief Virtual destructor for proper cleanup
     */
    ~MockPatientTable() override = default;

    // =========================================================================
    // Mock Methods - All IPatientTable interface methods
    // =========================================================================

    /**
     * @brief Mock GetPosition
     *
     * Usage:
     *   EXPECT_CALL(mock, GetPosition())
     *       .WillOnce(Return(expected_position));
     */
    MOCK_METHOD(
        TablePosition,
        GetPosition,
        (),
        (override)
    );

    /**
     * @brief Mock IsMotorized
     *
     * Usage:
     *   EXPECT_CALL(mock, IsMotorized())
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        IsMotorized,
        (),
        (const, override)
    );

    /**
     * @brief Mock MoveTo
     *
     * Usage:
     *   EXPECT_CALL(mock, MoveTo(Field(&TablePosition::longitudinal, 100.0f)))
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        MoveTo,
        (const TablePosition&),
        (override)
    );

    /**
     * @brief Mock RegisterPositionCallback
     *
     * Usage:
     *   EXPECT_CALL(mock, RegisterPositionCallback(::testing::_))
     *       .WillOnce(::testing::SaveArg<0>(&callback));
     *
     * Then invoke callback manually:
     *   callback(test_position);
     */
    MOCK_METHOD(
        void,
        RegisterPositionCallback,
        (TableCallback),
        (override)
    );
};

// =============================================================================
// Helper Functions for Common Mock Patterns
// =============================================================================

/**
 * @brief Create a zero TablePosition (at isocenter) for testing
 */
inline TablePosition CreateZeroTablePosition() {
    TablePosition pos;
    pos.longitudinal = 0.0f;
    pos.lateral = 0.0f;
    pos.height = 0.0f;
    return pos;
}

/**
 * @brief Create a specific TablePosition for testing
 */
inline TablePosition CreateTablePosition(float longitudinal, float lateral, float height) {
    TablePosition pos;
    pos.longitudinal = longitudinal;
    pos.lateral = lateral;
    pos.height = height;
    return pos;
}

} // namespace hnvue::hal::test

#endif // HNUE_HAL_TESTS_MOCK_PATIENT_TABLE_H
