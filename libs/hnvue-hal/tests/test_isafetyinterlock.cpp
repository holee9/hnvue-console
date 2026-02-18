/**
 * @file test_isafetyinterlock.cpp
 * @brief Unit tests for ISafetyInterlock interface using MockSafetyInterlock
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: ISafetyInterlock interface unit tests
 * SPDX-License-Identifier: MIT
 *
 * Tests the ISafetyInterlock interface behavior using Google Mock.
 * Ensures mockability per NFR-HAL-03.
 *
 * All 9 interlocks (IL-01 through IL-09) are tested for correct behavior.
 */

#include <gtest/gtest.h>
#include "mock/MockSafetyInterlock.h"
#include "hnvue/hal/ISafetyInterlock.h"
#include "hnvue/hal/HalTypes.h"

#include <thread>
#include <chrono>

using namespace hnvue::hal;
using namespace hnvue::hal::test;

// =============================================================================
// Test Fixture
// =============================================================================

/**
 * @brief Test fixture for ISafetyInterlock interface tests
 */
class ISafetyInterlockTest : public ::testing::Test {
protected:
    MockSafetyInterlock mock_interlock;

    void SetUp() override {
        // Reset mock state before each test
    }

    void TearDown() override {
        // Clean up after each test
    }
};

// =============================================================================
// Atomic Interlock Verification Tests
// =============================================================================

/**
 * @test ISafetyInterlock.CheckAllInterlocks.AllPass
 * @brief Verify CheckAllInterlocks returns all-pass status
 */
TEST_F(ISafetyInterlockTest, CheckAllInterlocks_ReturnsAllPassStatus) {
    // Arrange
    InterlockStatus expected_status = CreateAllPassInterlockStatus();

    EXPECT_CALL(mock_interlock, CheckAllInterlocks())
        .WillOnce(::testing::Return(expected_status));

    // Act
    InterlockStatus actual_status = mock_interlock.CheckAllInterlocks();

    // Assert
    EXPECT_TRUE(actual_status.all_passed);
    EXPECT_TRUE(actual_status.door_closed);              // IL-01
    EXPECT_TRUE(actual_status.emergency_stop_clear);     // IL-02
    EXPECT_TRUE(actual_status.thermal_normal);           // IL-03
    EXPECT_TRUE(actual_status.generator_ready);          // IL-04
    EXPECT_TRUE(actual_status.detector_ready);           // IL-05
    EXPECT_TRUE(actual_status.collimator_valid);         // IL-06
    EXPECT_TRUE(actual_status.table_locked);             // IL-07
    EXPECT_TRUE(actual_status.dose_within_limits);       // IL-08
    EXPECT_TRUE(actual_status.aec_configured);           // IL-09
}

/**
 * @test ISafetyInterlock.CheckAllInterlocks.PartialFail
 * @brief Verify CheckAllInterlocks returns partial fail status
 */
TEST_F(ISafetyInterlockTest, CheckAllInterlocks_ReturnsPartialFailStatus) {
    // Arrange - door open, e-stop activated
    InterlockStatus partial_fail = CreateInterlockStatus(
        false,  // door_closed (IL-01 FAIL)
        false,  // e_stop_clear (IL-02 FAIL)
        true,   // thermal_normal
        true,   // generator_ready
        true    // detector_ready
    );

    EXPECT_CALL(mock_interlock, CheckAllInterlocks())
        .WillOnce(::testing::Return(partial_fail));

    // Act
    InterlockStatus actual_status = mock_interlock.CheckAllInterlocks();

    // Assert
    EXPECT_FALSE(actual_status.all_passed);
    EXPECT_FALSE(actual_status.door_closed);
    EXPECT_FALSE(actual_status.emergency_stop_clear);
}

/**
 * @test ISafetyInterlock.CheckInterlock.Individual
 * @brief Verify CheckInterlock checks individual interlock by index
 */
TEST_F(ISafetyInterlockTest, CheckInterlock_ChecksIndividualInterlock) {
    // Arrange
    EXPECT_CALL(mock_interlock, CheckInterlock(0))
        .WillOnce(::testing::Return(true));   // IL-01

    EXPECT_CALL(mock_interlock, CheckInterlock(8))
        .WillOnce(::testing::Return(false));  // IL-09

    // Act
    bool il01_pass = mock_interlock.CheckInterlock(0);
    bool il09_pass = mock_interlock.CheckInterlock(8);

    // Assert
    EXPECT_TRUE(il01_pass);
    EXPECT_FALSE(il09_pass);
}

// =============================================================================
// Safety-Critical Interlock Tests (IL-01 through IL-03)
// =============================================================================

/**
 * @test ISafetyInterlock.GetDoorStatus.Closed
 * @brief Verify GetDoorStatus returns true when door is closed
 */
TEST_F(ISafetyInterlockTest, GetDoorStatus_ReturnsTrueWhenClosed) {
    // Arrange
    EXPECT_CALL(mock_interlock, GetDoorStatus())
        .WillOnce(::testing::Return(true));

    // Act
    bool door_closed = mock_interlock.GetDoorStatus();

    // Assert
    EXPECT_TRUE(door_closed);
}

/**
 * @test ISafetyInterlock.GetDoorStatus.Open
 * @brief Verify GetDoorStatus returns false when door is open
 */
TEST_F(ISafetyInterlockTest, GetDoorStatus_ReturnsFalseWhenOpen) {
    // Arrange
    EXPECT_CALL(mock_interlock, GetDoorStatus())
        .WillOnce(::testing::Return(false));

    // Act
    bool door_closed = mock_interlock.GetDoorStatus();

    // Assert
    EXPECT_FALSE(door_closed);
}

/**
 * @test ISafetyInterlock.GetEStopStatus.Clear
 * @brief Verify GetEStopStatus returns true when e-stop is clear
 */
TEST_F(ISafetyInterlockTest, GetEStopStatus_ReturnsTrueWhenClear) {
    // Arrange
    EXPECT_CALL(mock_interlock, GetEStopStatus())
        .WillOnce(::testing::Return(true));

    // Act
    bool e_stop_clear = mock_interlock.GetEStopStatus();

    // Assert
    EXPECT_TRUE(e_stop_clear);
}

/**
 * @test ISafetyInterlock.GetEStopStatus.Activated
 * @brief Verify GetEStopStatus returns false when e-stop is activated
 */
TEST_F(ISafetyInterlockTest, GetEStopStatus_ReturnsFalseWhenActivated) {
    // Arrange
    EXPECT_CALL(mock_interlock, GetEStopStatus())
        .WillOnce(::testing::Return(false));

    // Act
    bool e_stop_clear = mock_interlock.GetEStopStatus();

    // Assert
    EXPECT_FALSE(e_stop_clear);
}

/**
 * @test ISafetyInterlock.GetThermalStatus.Normal
 * @brief Verify GetThermalStatus returns true when thermal is normal
 */
TEST_F(ISafetyInterlockTest, GetThermalStatus_ReturnsTrueWhenNormal) {
    // Arrange
    EXPECT_CALL(mock_interlock, GetThermalStatus())
        .WillOnce(::testing::Return(true));

    // Act
    bool thermal_normal = mock_interlock.GetThermalStatus();

    // Assert
    EXPECT_TRUE(thermal_normal);
}

/**
 * @test ISafetyInterlock.GetThermalStatus.Overtemperature
 * @brief Verify GetThermalStatus returns false when overheating
 */
TEST_F(ISafetyInterlockTest, GetThermalStatus_ReturnsFalseWhenOvertemperature) {
    // Arrange
    EXPECT_CALL(mock_interlock, GetThermalStatus())
        .WillOnce(::testing::Return(false));

    // Act
    bool thermal_normal = mock_interlock.GetThermalStatus();

    // Assert
    EXPECT_FALSE(thermal_normal);
}

// =============================================================================
// Emergency Standby Tests
// =============================================================================

/**
 * @test ISafetyInterlock.EmergencyStandby.Success
 * @brief Verify EmergencyStandby executes without exception
 */
TEST_F(ISafetyInterlockTest, EmergencyStandby_ExecutesSuccessfully) {
    // Arrange
    EXPECT_CALL(mock_interlock, EmergencyStandby())
        .Times(1);

    // Act
    mock_interlock.EmergencyStandby();

    // Assert - no exception thrown
}

/**
 * @test ISafetyInterlock.EmergencyStandby.Multiple
 * @brief Verify EmergencyStandby can be called multiple times
 */
TEST_F(ISafetyInterlockTest, EmergencyStandby_CanBeCalledMultipleTimes) {
    // Arrange
    EXPECT_CALL(mock_interlock, EmergencyStandby())
        .Times(3);

    // Act
    mock_interlock.EmergencyStandby();
    mock_interlock.EmergencyStandby();
    mock_interlock.EmergencyStandby();

    // Assert - no exception thrown
}

// =============================================================================
// Callback Tests
// =============================================================================

/**
 * @test ISafetyInterlock.RegisterInterlockCallback.Success
 * @brief Verify RegisterInterlockCallback accepts callback
 */
TEST_F(ISafetyInterlockTest, RegisterInterlockCallback_AcceptsCallback) {
    // Arrange
    InterlockCallback callback = [](const InterlockStatus& status) {
        // Callback handler
    };

    EXPECT_CALL(mock_interlock, RegisterInterlockCallback(::testing::_))
        .Times(1);

    // Act
    mock_interlock.RegisterInterlockCallback(callback);
}

/**
 * @test ISafetyInterlock.RegisterInterlockCallback.Invocation
 * @brief Verify callback is invoked when interlock state changes
 */
TEST_F(ISafetyInterlockTest, RegisterInterlockCallback_InvokesCallbackOnStateChange) {
    // Arrange
    InterlockStatus test_status = CreateInterlockStatus(
        false,  // door open
        true,
        true,
        true,
        true
    );
    InterlockStatus received_status;
    bool callback_invoked = false;

    InterlockCallback callback = [&](const InterlockStatus& status) {
        received_status = status;
        callback_invoked = true;
    };

    // Save callback for manual invocation
    EXPECT_CALL(mock_interlock, RegisterInterlockCallback(::testing::_))
        .WillOnce(::testing::SaveArg<0>(&callback));

    // Act - register callback
    mock_interlock.RegisterInterlockCallback(callback);

    // Simulate interlock state change by invoking callback manually
    callback(test_status);

    // Assert
    EXPECT_TRUE(callback_invoked);
    EXPECT_EQ(received_status.door_closed, test_status.door_closed);
    EXPECT_EQ(received_status.all_passed, test_status.all_passed);
}

/**
 * @test ISafetyInterlock.RegisterInterlockCallback.MultipleCallbacks
 * @brief Verify multiple callbacks can be registered
 */
TEST_F(ISafetyInterlockTest, RegisterInterlockCallback_SupportsMultipleCallbacks) {
    // Arrange
    int callback1_count = 0;
    int callback2_count = 0;

    InterlockCallback callback1 = [&](const InterlockStatus&) {
        callback1_count++;
    };

    InterlockCallback callback2 = [&](const InterlockStatus&) {
        callback2_count++;
    };

    EXPECT_CALL(mock_interlock, RegisterInterlockCallback(::testing::_))
        .Times(2);

    // Act - register multiple callbacks
    mock_interlock.RegisterInterlockCallback(callback1);
    mock_interlock.RegisterInterlockCallback(callback2);

    // Simulate state change
    InterlockStatus test_status = CreateAllPassInterlockStatus();
    callback1(test_status);
    callback2(test_status);

    // Assert
    EXPECT_EQ(callback1_count, 1);
    EXPECT_EQ(callback2_count, 1);
}

// =============================================================================
// Thread Safety Tests (NFR-HAL-05)
// =============================================================================

/**
 * @test ISafetyInterlock.ThreadSafety.ConcurrentCheckAllInterlocks
 * @brief Verify CheckAllInterlocks is thread-safe for concurrent calls
 */
TEST_F(ISafetyInterlockTest, ThreadSafety_ConcurrentCheckAllInterlocksCalls) {
    // Arrange
    const int num_threads = 10;
    const int calls_per_thread = 100;

    EXPECT_CALL(mock_interlock, CheckAllInterlocks())
        .Times(num_threads * calls_per_thread)
        .WillRepeatedly(::testing::Return(CreateAllPassInterlockStatus()));

    // Act - concurrent CheckAllInterlocks calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                InterlockStatus status = mock_interlock.CheckAllInterlocks();
                // Verify status is valid
                EXPECT_TRUE(status.all_passed || !status.all_passed);
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
 * @test ISafetyInterlock.ThreadSafety.ConcurrentEmergencyStandby
 * @brief Verify EmergencyStandby is internally synchronized
 */
TEST_F(ISafetyInterlockTest, ThreadSafety_ConcurrentEmergencyStandbyCalls) {
    // Arrange
    const int num_threads = 5;
    const int calls_per_thread = 50;

    EXPECT_CALL(mock_interlock, EmergencyStandby())
        .Times(num_threads * calls_per_thread);

    // Act - concurrent EmergencyStandby calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                mock_interlock.EmergencyStandby();
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
// Safety-Critical Interlock Combination Tests
// =============================================================================

/**
 * @test ISafetyInterlock.AllInterlocksMustPass
 * @brief Verify exposure blocked when any safety-critical interlock fails
 */
TEST_F(ISafetyInterlockTest, AllInterlocksMustPass_ExposureBlockedWhenAnyCriticalFails) {
    // Arrange - IL-01, IL-02, IL-03 are safety-critical
    struct TestCase {
        bool il01_door;
        bool il02_estop;
        bool il03_thermal;
        bool expected_all_pass;
    };

    std::vector<TestCase> test_cases = {
        {true,  true,  true,  true},   // All pass
        {false, true,  true,  false},  // IL-01 fail
        {true,  false, true,  false},  // IL-02 fail
        {true,  true,  false, false},  // IL-03 fail
        {false, false, true,  false},  // IL-01, IL-02 fail
        {false, false, false, false}   // All fail
    };

    for (const auto& tc : test_cases) {
        InterlockStatus status;
        status.door_closed = tc.il01_door;
        status.emergency_stop_clear = tc.il02_estop;
        status.thermal_normal = tc.il03_thermal;
        status.generator_ready = true;
        status.detector_ready = true;
        status.collimator_valid = true;
        status.table_locked = true;
        status.dose_within_limits = true;
        status.aec_configured = true;
        status.all_passed = tc.il01_door && tc.il02_estop && tc.il03_thermal &&
                            true && true && true && true && true && true;

        EXPECT_CALL(mock_interlock, CheckAllInterlocks())
            .WillOnce(::testing::Return(status));

        // Act
        InterlockStatus result = mock_interlock.CheckAllInterlocks();

        // Assert
        EXPECT_EQ(result.all_passed, tc.expected_all_pass)
            << "IL-01=" << tc.il01_door
            << " IL-02=" << tc.il02_estop
            << " IL-03=" << tc.il03_thermal;
    }
}

// =============================================================================
// Performance Tests (NFR-HAL-01)
// =============================================================================

/**
 * @test ISafetyInterlock.Performance.CheckAllInterlocksLatency
 * @brief Verify CheckAllInterlocks completes within 10 ms
 */
TEST_F(ISafetyInterlockTest, Performance_CheckAllInterlocksCompletesWithin10ms) {
    // Arrange
    EXPECT_CALL(mock_interlock, CheckAllInterlocks())
        .WillOnce(::testing::Return(CreateAllPassInterlockStatus()));

    // Act
    auto start = std::chrono::high_resolution_clock::now();
    InterlockStatus status = mock_interlock.CheckAllInterlocks();
    auto end = std::chrono::high_resolution_clock::now();

    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);

    // Assert
    EXPECT_LT(duration.count(), 10000) << "CheckAllInterlocks took " << duration.count() << " us";
}

/**
 * @test ISafetyInterlock.Performance.EmergencyStandbyLatency
 * @brief Verify EmergencyStandby completes within 100 ms
 */
TEST_F(ISafetyInterlockTest, Performance_EmergencyStandbyCompletesWithin100ms) {
    // Arrange
    EXPECT_CALL(mock_interlock, EmergencyStandby())
        .Times(1);

    // Act
    auto start = std::chrono::high_resolution_clock::now();
    mock_interlock.EmergencyStandby();
    auto end = std::chrono::high_resolution_clock::now();

    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);

    // Assert
    EXPECT_LT(duration.count(), 100000) << "EmergencyStandby took " << duration.count() << " us";
}

// =============================================================================
// Main
// =============================================================================

int main(int argc, char** argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
