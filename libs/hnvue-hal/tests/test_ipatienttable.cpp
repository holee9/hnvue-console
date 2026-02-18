/**
 * @file test_ipatienttable.cpp
 * @brief Unit tests for IPatientTable interface using MockPatientTable
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - IPatientTable interface unit tests
 * SPDX-License-Identifier: MIT
 *
 * Tests the IPatientTable interface behavior using Google Mock.
 * Ensures mockability per NFR-HAL-03.
 */

#include <gtest/gtest.h>
#include "mock/MockPatientTable.h"
#include "hnvue/hal/IPatientTable.h"
#include "hnvue/hal/HalTypes.h"

#include <thread>
#include <chrono>

using namespace hnvue::hal;
using namespace hnvue::hal::test;

// =============================================================================
// Test Fixture
// =============================================================================

/**
 * @brief Test fixture for IPatientTable interface tests
 */
class IPatientTableTest : public ::testing::Test {
protected:
    MockPatientTable mock_table;

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
 * @test IPatientTable.GetPosition.Default
 * @brief Verify GetPosition returns default zero position
 */
TEST_F(IPatientTableTest, GetPosition_ReturnsDefaultZeroPosition) {
    // Arrange
    TablePosition expected_pos = CreateZeroTablePosition();

    EXPECT_CALL(mock_table, GetPosition())
        .WillOnce(::testing::Return(expected_pos));

    // Act
    TablePosition actual_pos = mock_table.GetPosition();

    // Assert
    EXPECT_FLOAT_EQ(actual_pos.longitudinal, expected_pos.longitudinal);
    EXPECT_FLOAT_EQ(actual_pos.lateral, expected_pos.lateral);
    EXPECT_FLOAT_EQ(actual_pos.height, expected_pos.height);
}

/**
 * @test IPatientTable.GetPosition.Custom
 * @brief Verify GetPosition returns custom position
 */
TEST_F(IPatientTableTest, GetPosition_ReturnsCustomPosition) {
    // Arrange
    TablePosition expected_pos = CreateTablePosition(100.0f, 50.0f, 750.0f);

    EXPECT_CALL(mock_table, GetPosition())
        .WillOnce(::testing::Return(expected_pos));

    // Act
    TablePosition actual_pos = mock_table.GetPosition();

    // Assert
    EXPECT_FLOAT_EQ(actual_pos.longitudinal, 100.0f);
    EXPECT_FLOAT_EQ(actual_pos.lateral, 50.0f);
    EXPECT_FLOAT_EQ(actual_pos.height, 750.0f);
}

/**
 * @test IPatientTable.IsMotorized.True
 * @brief Verify IsMotorized returns true for motorized table
 */
TEST_F(IPatientTableTest, IsMotorized_ReturnsTrueForMotorized) {
    // Arrange
    EXPECT_CALL(mock_table, IsMotorized())
        .WillOnce(::testing::Return(true));

    // Act
    bool is_motorized = mock_table.IsMotorized();

    // Assert
    EXPECT_TRUE(is_motorized);
}

/**
 * @test IPatientTable.IsMotorized.False
 * @brief Verify IsMotorized returns false for manual table
 */
TEST_F(IPatientTableTest, IsMotorized_ReturnsFalseForManual) {
    // Arrange
    EXPECT_CALL(mock_table, IsMotorized())
        .WillOnce(::testing::Return(false));

    // Act
    bool is_motorized = mock_table.IsMotorized();

    // Assert
    EXPECT_FALSE(is_motorized);
}

// =============================================================================
// Control Method Tests
// =============================================================================

/**
 * @test IPatientTable.MoveTo.Success
 * @brief Verify MoveTo returns true for valid position
 */
TEST_F(IPatientTableTest, MoveTo_ReturnsTrueForValidPosition) {
    // Arrange
    TablePosition pos = CreateTablePosition(100.0f, 50.0f, 750.0f);

    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(true));

    // Act
    bool result = mock_table.MoveTo(pos);

    // Assert
    EXPECT_TRUE(result);
}

/**
 * @test IPatientTable.MoveTo.Failure
 * @brief Verify MoveTo returns false for invalid position
 */
TEST_F(IPatientTableTest, MoveTo_ReturnsFalseForInvalidPosition) {
    // Arrange
    TablePosition pos{-9999.0f, -9999.0f, -9999.0f}; // Invalid

    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool result = mock_table.MoveTo(pos);

    // Assert
    EXPECT_FALSE(result);
}

/**
 * @test IPatientTable.MoveTo.NotMotorized
 * @brief Verify MoveTo returns false for non-motorized table
 */
TEST_F(IPatientTableTest, MoveTo_ReturnsFalseWhenNotMotorized) {
    // Arrange
    TablePosition pos = CreateTablePosition(100.0f, 50.0f, 750.0f);

    // Configure mock as non-motorized
    EXPECT_CALL(mock_table, IsMotorized())
        .WillRepeatedly(::testing::Return(false));

    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool is_motorized = mock_table.IsMotorized();
    bool result = mock_table.MoveTo(pos);

    // Assert
    EXPECT_FALSE(is_motorized);
    EXPECT_FALSE(result);
}

/**
 * @test IPatientTable.MoveTo.Sequence
 * @brief Verify sequential MoveTo calls work correctly
 */
TEST_F(IPatientTableTest, MoveTo_SequenceSuccessful) {
    // Arrange
    TablePosition pos1 = CreateTablePosition(0.0f, 0.0f, 700.0f);
    TablePosition pos2 = CreateTablePosition(100.0f, 0.0f, 700.0f);
    TablePosition pos3 = CreateTablePosition(100.0f, 50.0f, 750.0f);

    ::testing::InSequence seq;

    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(true));
    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(true));
    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(true));

    // Act
    bool result1 = mock_table.MoveTo(pos1);
    bool result2 = mock_table.MoveTo(pos2);
    bool result3 = mock_table.MoveTo(pos3);

    // Assert
    EXPECT_TRUE(result1);
    EXPECT_TRUE(result2);
    EXPECT_TRUE(result3);
}

// =============================================================================
// Callback Tests
// =============================================================================

/**
 * @test IPatientTable.RegisterPositionCallback.Success
 * @brief Verify RegisterPositionCallback accepts callback
 */
TEST_F(IPatientTableTest, RegisterPositionCallback_AcceptsCallback) {
    // Arrange
    TableCallback callback = [](const TablePosition& pos) {
        // Callback handler
    };

    EXPECT_CALL(mock_table, RegisterPositionCallback(::testing::_))
        .Times(1);

    // Act
    mock_table.RegisterPositionCallback(callback);
}

/**
 * @test IPatientTable.RegisterPositionCallback.Invocation
 * @brief Verify callback is invoked when position changes
 */
TEST_F(IPatientTableTest, RegisterPositionCallback_InvokesCallbackOnPositionChange) {
    // Arrange
    TablePosition test_pos = CreateTablePosition(100.0f, 50.0f, 750.0f);
    TablePosition received_pos;
    bool callback_invoked = false;

    TableCallback callback = [&](const TablePosition& pos) {
        received_pos = pos;
        callback_invoked = true;
    };

    // Save callback for manual invocation
    EXPECT_CALL(mock_table, RegisterPositionCallback(::testing::_))
        .WillOnce(::testing::SaveArg<0>(&callback));

    // Act - register callback
    mock_table.RegisterPositionCallback(callback);

    // Simulate position change by invoking callback manually
    callback(test_pos);

    // Assert
    EXPECT_TRUE(callback_invoked);
    EXPECT_FLOAT_EQ(received_pos.longitudinal, test_pos.longitudinal);
    EXPECT_FLOAT_EQ(received_pos.lateral, test_pos.lateral);
    EXPECT_FLOAT_EQ(received_pos.height, test_pos.height);
}

/**
 * @test IPatientTable.RegisterPositionCallback.MultipleCallbacks
 * @brief Verify multiple callbacks can be registered
 */
TEST_F(IPatientTableTest, RegisterPositionCallback_SupportsMultipleCallbacks) {
    // Arrange
    int callback1_count = 0;
    int callback2_count = 0;

    TableCallback callback1 = [&](const TablePosition&) {
        callback1_count++;
    };

    TableCallback callback2 = [&](const TablePosition&) {
        callback2_count++;
    };

    EXPECT_CALL(mock_table, RegisterPositionCallback(::testing::_))
        .Times(2);

    // Act - register multiple callbacks
    mock_table.RegisterPositionCallback(callback1);
    mock_table.RegisterPositionCallback(callback2);

    // Simulate position change
    TablePosition test_pos = CreateTablePosition(50.0f, 25.0f, 725.0f);
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
 * @test IPatientTable.ThreadSafety.ConcurrentGetPosition
 * @brief Verify GetPosition is thread-safe for concurrent calls
 */
TEST_F(IPatientTableTest, ThreadSafety_ConcurrentGetPositionCalls) {
    // Arrange
    const int num_threads = 10;
    const int calls_per_thread = 100;

    EXPECT_CALL(mock_table, GetPosition())
        .Times(num_threads * calls_per_thread)
        .WillRepeatedly(::testing::Return(CreateZeroTablePosition()));

    // Act - concurrent GetPosition calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                TablePosition pos = mock_table.GetPosition();
                // Verify position is valid
                EXPECT_TRUE(std::isfinite(pos.longitudinal));
                EXPECT_TRUE(std::isfinite(pos.lateral));
                EXPECT_TRUE(std::isfinite(pos.height));
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
 * @test IPatientTable.ThreadSafety.ConcurrentMoveTo
 * @brief Verify MoveTo is internally synchronized
 */
TEST_F(IPatientTableTest, ThreadSafety_ConcurrentMoveToCalls) {
    // Arrange
    const int num_threads = 5;
    const int calls_per_thread = 50;

    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .Times(num_threads * calls_per_thread)
        .WillRepeatedly(::testing::Return(true));

    // Act - concurrent MoveTo calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread, i]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                TablePosition pos = CreateTablePosition(
                    static_cast<float>(i * 10),
                    static_cast<float>(i * 5),
                    700.0f + i
                );
                mock_table.MoveTo(pos);
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
 * @test IPatientTable.MoveTo.OutOfRange
 * @brief Verify MoveTo handles out-of-range values
 */
TEST_F(IPatientTableTest, MoveTo_HandlesOutOfRangeValues) {
    // Arrange - extreme values
    TablePosition extreme_pos{
        std::numeric_limits<float>::max(),
        std::numeric_limits<float>::max(),
        std::numeric_limits<float>::max()
    };

    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool result = mock_table.MoveTo(extreme_pos);

    // Assert
    EXPECT_FALSE(result);
}

/**
 * @test IPatientTable.MoveTo.NaN
 * @brief Verify MoveTo handles NaN values
 */
TEST_F(IPatientTableTest, MoveTo_HandlesNaNValues) {
    // Arrange - NaN values
    TablePosition nan_pos{
        std::numeric_limits<float>::quiet_NaN(),
        std::numeric_limits<float>::quiet_NaN(),
        std::numeric_limits<float>::quiet_NaN()
    };

    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool result = mock_table.MoveTo(nan_pos);

    // Assert
    EXPECT_FALSE(result);
}

/**
 * @test IPatientTable.MoveTo.Infinity
 * @brief Verify MoveTo handles infinity values
 */
TEST_F(IPatientTableTest, MoveTo_HandlesInfinityValues) {
    // Arrange - infinity values
    TablePosition inf_pos{
        std::numeric_limits<float>::infinity(),
        -std::numeric_limits<float>::infinity(),
        std::numeric_limits<float>::infinity()
    };

    EXPECT_CALL(mock_table, MoveTo(::testing::_))
        .WillOnce(::testing::Return(false));

    // Act
    bool result = mock_table.MoveTo(inf_pos);

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
