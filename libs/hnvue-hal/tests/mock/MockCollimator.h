/**
 * @file MockCollimator.h
 * @brief Google Mock implementation of ICollimator interface for unit testing
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Mock collimator for isolated unit testing
 * SPDX-License-Identifier: MIT
 *
 * This mock class enables SOUP-isolated unit testing per NFR-HAL-03.
 * All interface methods are mockable with customizable expectations.
 */

#ifndef HNUE_HAL_TESTS_MOCK_COLLIMATOR_H
#define HNUE_HAL_TESTS_MOCK_COLLIMATOR_H

#include "hnvue/hal/ICollimator.h"
#include <gmock/gmock.h>

namespace hnvue::hal::test {

/**
 * @brief Google Mock implementation of ICollimator interface
 *
 * Provides full mockability for all ICollimator methods with:
 * - EXPECT_CALL() for behavior verification
 * - ON_CALL() for default return values
 * - WithArgs() for argument matching
 * - WillOnce()/WillRepeatedly() for action customization
 *
 * Thread Safety: Mock is thread-safe where the interface is thread-safe.
 * Use InSequence() for ordered call expectations.
 */
class MockCollimator : public ICollimator {
public:
    /**
     * @brief Default constructor with relaxed defaults
     *
     * By default, all methods return sensible default values:
     * - GetPosition(): Returns centered position (all blades at 100mm)
     * - IsMotorized(): Returns true (motorized)
     * - SetPosition(): Returns true (success)
     */
    MockCollimator() {
        // Set default return values for common calls
        CollimatorPosition default_pos;
        default_pos.left = 100.0f;
        default_pos.right = 100.0f;
        default_pos.top = 100.0f;
        default_pos.bottom = 100.0f;

        ON_CALL(*this, GetPosition())
            .WillByDefault(::testing::Return(default_pos));

        ON_CALL(*this, IsMotorized())
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, SetPosition(::testing::_))
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, RegisterPositionCallback(::testing::_))
            .WillByDefault(::testing::Return());
    }

    /**
     * @brief Virtual destructor for proper cleanup
     */
    ~MockCollimator() override = default;

    // =========================================================================
    // Mock Methods - All ICollimator interface methods
    // =========================================================================

    /**
     * @brief Mock GetPosition
     *
     * Usage:
     *   EXPECT_CALL(mock, GetPosition())
     *       .WillOnce(Return(expected_position));
     */
    MOCK_METHOD(
        CollimatorPosition,
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
     * @brief Mock SetPosition
     *
     * Usage:
     *   EXPECT_CALL(mock, SetPosition(Field(&CollimatorPosition::left, 50.0f)))
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        SetPosition,
        (const CollimatorPosition&),
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
        (CollimatorCallback),
        (override)
    );
};

// =============================================================================
// Helper Functions for Common Mock Patterns
// =============================================================================

/**
 * @brief Create a centered CollimatorPosition for testing
 */
inline CollimatorPosition CreateCenteredCollimatorPosition() {
    CollimatorPosition pos;
    pos.left = 100.0f;
    pos.right = 100.0f;
    pos.top = 100.0f;
    pos.bottom = 100.0f;
    return pos;
}

/**
 * @brief Create a small field CollimatorPosition for testing
 */
inline CollimatorPosition CreateSmallFieldCollimatorPosition() {
    CollimatorPosition pos;
    pos.left = 50.0f;
    pos.right = 50.0f;
    pos.top = 50.0f;
    pos.bottom = 50.0f;
    return pos;
}

/**
 * @brief Create a rectangular CollimatorPosition for testing
 */
inline CollimatorPosition CreateRectangularCollimatorPosition(float left, float right, float top, float bottom) {
    CollimatorPosition pos;
    pos.left = left;
    pos.right = right;
    pos.top = top;
    pos.bottom = bottom;
    return pos;
}

} // namespace hnvue::hal::test

#endif // HNUE_HAL_TESTS_MOCK_COLLIMATOR_H
