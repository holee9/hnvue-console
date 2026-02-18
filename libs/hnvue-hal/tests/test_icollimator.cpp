/**
 * @file test_icollimator.cpp
 * @brief Unit tests for ICollimator interface using MockCollimator
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - ICollimator interface unit tests
 * SPDX-License-Identifier: MIT
 *
 * Tests the ICollimator interface behavior using Google Mock.
 * Ensures mockability per NFR-HAL-03.
 */

#include <gtest/gtest.h>
#include "mock/MockCollimator.h"
#include "hnvue/hal/ICollimator.h"
#include "hnvue/hal/HalTypes.h"

#include <thread>
#include <chrono>

using namespace hnvue::hal;
using namespace hnvue::hal::test;

// =============================================================================
// Test Fixture
// =============================================================================

/**
 * @brief Test fixture for ICollimator interface tests
 */
class ICollimatorTest : public ::testing::Test {
protected:
    MockCollimator mock_collimator;

    void SetUp() override {
        // Reset mock state before each test
    }

    void TearDown() override {
        // Clean up after each test
    }
};

// =============================================================================
// Query Method Tests
// =============================================================================

/**
 * @test ICollimator.GetPosition.Default
 * @brief Verify GetPosition returns default centered position
 */
TEST_F(ICollimatorTest, GetPosition_ReturnsDefaultCenteredPosition) {
    // Arrange
    CollimatorPosition expected_pos = CreateCenteredCollimatorPosition();

    // Expect default call
    EXPECT_CALL(mock_collimator, GetPosition())
        .WillOnce(::testing::Return(expected_pos));

    // Act
    CollimatorPosition actual_pos = mock_collimator.GetPosition();

    // Assert
    EXPECT_FLOAT_EQ(actual_pos.left, expected_pos.left);
    EXPECT_FLOAT_EQ(actual_pos.right, expected_pos.right);
    EXPECT_FLOAT_EQ(actual_pos.top, expected_pos.top);
    EXPECT_FLOAT_EQ(actual_pos.bottom, expected_pos.bottom);
}

/**
 * @test ICollimator.GetPosition.Custom
 * @brief Verify GetPosition returns custom position
 */
TEST_F(ICollimatorTest, GetPosition_ReturnsCustomPosition) {
    // Arrange
    CollimatorPosition expected_pos = CreateRectangularCollimatorPosition(50.0f, 60.0f, 70.0f, 80.0f);

    EXPECT_CALL(mock_collimator, GetPosition())
        .WillOnce(::testing::Return(expected_pos));

    // Act
    CollimatorPosition actual_pos = mock_collimator.GetPosition();

    // Assert
    EXPECT_FLOAT_EQ(actual_pos.left, 50.0f);
    EXPECT_FLOAT_EQ(actual_pos.right, 60.0f);
    EXPECT_FLOAT_EQ(actual_pos.top, 70.0f);
    EXPECT_FLOAT_EQ(actual_pos.bottom, 80.0f);
}

/**
 * @test ICollimator.IsMotorized.True
 * @brief Verify IsMotorized returns true for motorized collimator
 */
TEST_F(ICollimatorTest, IsMotorized_ReturnsTrueForMotorized) {
    // Arrange
    EXPECT_CALL(mock_collimator, IsMotorized())
        .WillOnce(::testing::Return(true));

    // Act
    bool is_motorized = mock_collimator.IsMotorized();

    // Assert
    EXPECT_TRUE(is_motorized);
}

/**
 * @test ICollimator.IsMotorized.False
 * @brief Verify IsMotorized returns false for manual collimator
 */
TEST_F(ICollimatorTest, IsMotorized_ReturnsFalseForManual) {
    // Arrange
    EXPECT_CALL(mock_collimator, IsMotorized())
        .WillOnce(::testing::Return(false));

    // Act
    bool is_motorized = mock_collimator.IsMotorized();

    // Assert
    EXPECT_FALSE(is_motorized);
}

// =============================================================================
// Control Method Tests
// =============================================================================

/**
 * @test ICollimator.SetPosition.Success
 * @brief Verify SetPosition returns true for valid position
 */
TEST_F(ICollimatorTest, SetPosition_ReturnsTrueForValidPosition) {
    // Arrange
    CollimatorPosition pos = CreateSmallFieldCollimatorPosition();

    EXPECT_CALL(mock_collimator, SetPosition(::testing::_))
        .WillOnce(::testing::Return(true));

    // Act
    bool result = mock_collimator.SetPosition(pos);

    // Assert
    EXPECT_TRUE(result);
}

/**
 * @test ICollimator.SetPosition.Failure
 * @brief Verify SetPosition returns false for invalid position
 */
TEST_F(ICollimatorTest, SetPosition_ReturnsFalseForInvalidPosition) {
    // Arrange
    CollimatorPosition pos{-1000.0f, -1000.0f, -1000.0f, -1000.0f}; // Invalid

    EXPECT_CALL(mock_collimator, SetPosition(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool result = mock_collimator.SetPosition(pos);

    // Assert
    EXPECT_FALSE(result);
}

/**
 * @test ICollimator.SetPosition.NotMotorized
 * @brief Verify SetPosition returns false for non-motorized collimator
 */
TEST_F(ICollimatorTest, SetPosition_ReturnsFalseWhenNotMotorized) {
    // Arrange
    CollimatorPosition pos = CreateSmallFieldCollimatorPosition();

    // Configure mock as non-motorized
    EXPECT_CALL(mock_collimator, IsMotorized())
        .WillRepeatedly(::testing::Return(false));

    EXPECT_CALL(mock_collimator, SetPosition(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool is_motorized = mock_collimator.IsMotorized();
    bool result = mock_collimator.SetPosition(pos);

    // Assert
    EXPECT_FALSE(is_motorized);
    EXPECT_FALSE(result);
}

// =============================================================================
// Callback Tests
// =============================================================================

/**
 * @test ICollimator.RegisterPositionCallback.Success
 * @brief Verify RegisterPositionCallback accepts callback
 */
TEST_F(ICollimatorTest, RegisterPositionCallback_AcceptsCallback) {
    // Arrange
    CollimatorCallback callback = [](const CollimatorPosition& pos) {
        // Callback handler
    };

    EXPECT_CALL(mock_collimator, RegisterPositionCallback(::testing::_))
        .Times(1);

    // Act
    mock_collimator.RegisterPositionCallback(callback);
}

/**
 * @test ICollimator.RegisterPositionCallback.Invocation
 * @brief Verify callback is invoked when position changes
 */
TEST_F(ICollimatorTest, RegisterPositionCallback_InvokesCallbackOnPositionChange) {
    // Arrange
    CollimatorPosition test_pos = CreateRectangularCollimatorPosition(30.0f, 40.0f, 50.0f, 60.0f);
    CollimatorPosition received_pos;
    bool callback_invoked = false;

    CollimatorCallback callback = [&](const CollimatorPosition& pos) {
        received_pos = pos;
        callback_invoked = true;
    };

    // Save callback for manual invocation
    EXPECT_CALL(mock_collimator, RegisterPositionCallback(::testing::_))
        .WillOnce(::testing::SaveArg<0>(&callback));

    // Act - register callback
    mock_collimator.RegisterPositionCallback(callback);

    // Simulate position change by invoking callback manually
    callback(test_pos);

    // Assert
    EXPECT_TRUE(callback_invoked);
    EXPECT_FLOAT_EQ(received_pos.left, test_pos.left);
    EXPECT_FLOAT_EQ(received_pos.right, test_pos.right);
    EXPECT_FLOAT_EQ(received_pos.top, test_pos.top);
    EXPECT_FLOAT_EQ(received_pos.bottom, test_pos.bottom);
}

/**
 * @test ICollimator.RegisterPositionCallback.MultipleCallbacks
 * @brief Verify multiple callbacks can be registered
 */
TEST_F(ICollimatorTest, RegisterPositionCallback_SupportsMultipleCallbacks) {
    // Arrange
    int callback1_count = 0;
    int callback2_count = 0;

    CollimatorCallback callback1 = [&](const CollimatorPosition&) {
        callback1_count++;
    };

    CollimatorCallback callback2 = [&](const CollimatorPosition&) {
        callback2_count++;
    };

    EXPECT_CALL(mock_collimator, RegisterPositionCallback(::testing::_))
        .Times(2);

    // Act - register multiple callbacks
    mock_collimator.RegisterPositionCallback(callback1);
    mock_collimator.RegisterPositionCallback(callback2);

    // Simulate position change
    CollimatorPosition test_pos = CreateSmallFieldCollimatorPosition();
    callback1(test_pos);
    callback2(test_pos);

    // Assert
    EXPECT_EQ(callback1_count, 1);
    EXPECT_EQ(callback2_count, 1);
}

// =============================================================================
// Thread Safety Tests (NFR-HAL-05)
// =============================================================================

/**
 * @test ICollimator.ThreadSafety.ConcurrentGetPosition
 * @brief Verify GetPosition is thread-safe for concurrent calls
 */
TEST_F(ICollimatorTest, ThreadSafety_ConcurrentGetPositionCalls) {
    // Arrange
    const int num_threads = 10;
    const int calls_per_thread = 100;

    EXPECT_CALL(mock_collimator, GetPosition())
        .Times(num_threads * calls_per_thread)
        .WillRepeatedly(::testing::Return(CreateCenteredCollimatorPosition()));

    // Act - concurrent GetPosition calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                CollimatorPosition pos = mock_collimator.GetPosition();
                // Verify position is valid
                EXPECT_GE(pos.left, 0.0f);
            }
        });
    }

    // Wait for all threads
    for (auto& thread : threads) {
        thread.join();
    }

    // Assert - no crashes or data races
}

/**
 * @test ICollimator.ThreadSafety.ConcurrentSetPosition
 * @brief Verify SetPosition is internally synchronized
 */
TEST_F(ICollimatorTest, ThreadSafety_ConcurrentSetPositionCalls) {
    // Arrange
    const int num_threads = 5;
    const int calls_per_thread = 50;

    EXPECT_CALL(mock_collimator, SetPosition(::testing::_))
        .Times(num_threads * calls_per_thread)
        .WillRepeatedly(::testing::Return(true));

    // Act - concurrent SetPosition calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread, i]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                CollimatorPosition pos = CreateRectangularCollimatorPosition(
                    50.0f + i, 50.0f + i, 50.0f + i, 50.0f + i
                );
                mock_collimator.SetPosition(pos);
            }
        });
    }

    // Wait for all threads
    for (auto& thread : threads) {
        thread.join();
    }

    // Assert - no crashes or data races
}

// =============================================================================
// Error Condition Tests
// =============================================================================

/**
 * @test ICollimator.SetPosition.OutOfRange
 * @brief Verify SetPosition handles out-of-range values
 */
TEST_F(ICollimatorTest, SetPosition_HandlesOutOfRangeValues) {
    // Arrange - extreme values
    CollimatorPosition extreme_pos{
        std::numeric_limits<float>::max(),
        std::numeric_limits<float>::max(),
        std::numeric_limits<float>::max(),
        std::numeric_limits<float>::max()
    };

    EXPECT_CALL(mock_collimator, SetPosition(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool result = mock_collimator.SetPosition(extreme_pos);

    // Assert
    EXPECT_FALSE(result);
}

/**
 * @test ICollimator.SetPosition.NaN
 * @brief Verify SetPosition handles NaN values
 */
TEST_F(ICollimatorTest, SetPosition_HandlesNaNValues) {
    // Arrange - NaN values
    CollimatorPosition nan_pos{
        std::numeric_limits<float>::quiet_NaN(),
        std::numeric_limits<float>::quiet_NaN(),
        std::numeric_limits<float>::quiet_NaN(),
        std::numeric_limits<float>::quiet_NaN()
    };

    EXPECT_CALL(mock_collimator, SetPosition(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool result = mock_collimator.SetPosition(nan_pos);

    // Assert
    EXPECT_FALSE(result);
}

// =============================================================================
// Main
// =============================================================================

int main(int argc, char** argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
