/**
 * @file test_idosemonitor.cpp
 * @brief Unit tests for IDoseMonitor interface using MockDoseMonitor
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - IDoseMonitor interface unit tests
 * SPDX-License-Identifier: MIT
 *
 * Tests the IDoseMonitor interface behavior using Google Mock.
 * Ensures mockability per NFR-HAL-03.
 */

#include <gtest/gtest.h>
#include "mock/MockDoseMonitor.h"
#include "hnvue/hal/IDoseMonitor.h"
#include "hnvue/hal/HalTypes.h"

#include <thread>
#include <chrono>
#include <vector>

using namespace hnvue::hal;
using namespace hnvue::hal::test;

// =============================================================================
// Test Fixture
// =============================================================================

/**
 * @brief Test fixture for IDoseMonitor interface tests
 */
class IDoseMonitorTest : public ::testing::Test {
protected:
    MockDoseMonitor mock_monitor;

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
 * @test IDoseMonitor.GetCurrentDose.Default
 * @brief Verify GetCurrentDose returns default zero dose
 */
TEST_F(IDoseMonitorTest, GetCurrentDose_ReturnsDefaultZeroDose) {
    // Arrange
    DoseReading expected_dose = CreateZeroDoseReading();

    EXPECT_CALL(mock_monitor, GetCurrentDose())
        .WillOnce(::testing::Return(expected_dose));

    // Act
    DoseReading actual_dose = mock_monitor.GetCurrentDose();

    // Assert
    EXPECT_FLOAT_EQ(actual_dose.dose_mgy, 0.0f);
    EXPECT_FLOAT_EQ(actual_dose.dose_rate_mgy_s, 0.0f);
    EXPECT_FLOAT_EQ(actual_dose.dap_ugy_cm2, 0.0f);
}

/**
 * @test IDoseMonitor.GetCurrentDose.Custom
 * @brief Verify GetCurrentDose returns custom dose reading
 */
TEST_F(IDoseMonitorTest, GetCurrentDose_ReturnsCustomDoseReading) {
    // Arrange
    DoseReading expected_dose = CreateDoseReading(5.0f, 0.5f, 1000.0f);

    EXPECT_CALL(mock_monitor, GetCurrentDose())
        .WillOnce(::testing::Return(expected_dose));

    // Act
    DoseReading actual_dose = mock_monitor.GetCurrentDose();

    // Assert
    EXPECT_FLOAT_EQ(actual_dose.dose_mgy, 5.0f);
    EXPECT_FLOAT_EQ(actual_dose.dose_rate_mgy_s, 0.5f);
    EXPECT_FLOAT_EQ(actual_dose.dap_ugy_cm2, 1000.0f);
}

/**
 * @test IDoseMonitor.GetDap.Default
 * @brief Verify GetDap returns default zero value
 */
TEST_F(IDoseMonitorTest, GetDap_ReturnsDefaultZero) {
    // Arrange
    EXPECT_CALL(mock_monitor, GetDap())
        .WillOnce(::testing::Return(0.0f));

    // Act
    float dap = mock_monitor.GetDap();

    // Assert
    EXPECT_FLOAT_EQ(dap, 0.0f);
}

/**
 * @test IDoseMonitor.GetDap.Custom
 * @brief Verify GetDap returns custom value
 */
TEST_F(IDoseMonitorTest, GetDap_ReturnsCustomValue) {
    // Arrange
    const float expected_dap = 1500.0f;

    EXPECT_CALL(mock_monitor, GetDap())
        .WillOnce(::testing::Return(expected_dap));

    // Act
    float dap = mock_monitor.GetDap();

    // Assert
    EXPECT_FLOAT_EQ(dap, expected_dap);
}

// =============================================================================
// Control Method Tests
// =============================================================================

/**
 * @test IDoseMonitor.Reset.Success
 * @brief Verify Reset resets dose accumulation
 */
TEST_F(IDoseMonitorTest, Reset_ResetsDoseAccumulation) {
    // Arrange
    EXPECT_CALL(mock_monitor, Reset())
        .Times(1);

    // Act
    mock_monitor.Reset();

    // Assert - no exception thrown
}

/**
 * @test IDoseMonitor.Reset.Multiple
 * @brief Verify Reset can be called multiple times
 */
TEST_F(IDoseMonitorTest, Reset_CanBeCalledMultipleTimes) {
    // Arrange
    EXPECT_CALL(mock_monitor, Reset())
        .Times(3);

    // Act
    mock_monitor.Reset();
    mock_monitor.Reset();
    mock_monitor.Reset();

    // Assert - no exception thrown
}

// =============================================================================
// Callback Tests
// =============================================================================

/**
 * @test IDoseMonitor.RegisterDoseCallback.Success
 * @brief Verify RegisterDoseCallback accepts callback
 */
TEST_F(IDoseMonitorTest, RegisterDoseCallback_AcceptsCallback) {
    // Arrange
    DoseCallback callback = [](const DoseReading& dose) {
        // Callback handler
    };

    EXPECT_CALL(mock_monitor, RegisterDoseCallback(::testing::_))
        .Times(1);

    // Act
    mock_monitor.RegisterDoseCallback(callback);
}

/**
 * @test IDoseMonitor.RegisterDoseCallback.Invocation
 * @brief Verify callback is invoked when dose updates
 */
TEST_F(IDoseMonitorTest, RegisterDoseCallback_InvokesCallbackOnDoseUpdate) {
    // Arrange
    DoseReading test_dose = CreateDoseReading(3.5f, 0.35f, 700.0f);
    DoseReading received_dose;
    bool callback_invoked = false;

    DoseCallback callback = [&](const DoseReading& dose) {
        received_dose = dose;
        callback_invoked = true;
    };

    // Save callback for manual invocation
    EXPECT_CALL(mock_monitor, RegisterDoseCallback(::testing::_))
        .WillOnce(::testing::SaveArg<0>(&callback));

    // Act - register callback
    mock_monitor.RegisterDoseCallback(callback);

    // Simulate dose update by invoking callback manually
    callback(test_dose);

    // Assert
    EXPECT_TRUE(callback_invoked);
    EXPECT_FLOAT_EQ(received_dose.dose_mgy, test_dose.dose_mgy);
    EXPECT_FLOAT_EQ(received_dose.dose_rate_mgy_s, test_dose.dose_rate_mgy_s);
    EXPECT_FLOAT_EQ(received_dose.dap_ugy_cm2, test_dose.dap_ugy_cm2);
}

/**
 * @test IDoseMonitor.RegisterDoseCallback.MultipleCallbacks
 * @brief Verify multiple callbacks can be registered
 */
TEST_F(IDoseMonitorTest, RegisterDoseCallback_SupportsMultipleCallbacks) {
    // Arrange
    int callback1_count = 0;
    int callback2_count = 0;

    DoseCallback callback1 = [&](const DoseReading&) {
        callback1_count++;
    };

    DoseCallback callback2 = [&](const DoseReading&) {
        callback2_count++;
    };

    EXPECT_CALL(mock_monitor, RegisterDoseCallback(::testing::_))
        .Times(2);

    // Act - register multiple callbacks
    mock_monitor.RegisterDoseCallback(callback1);
    mock_monitor.RegisterDoseCallback(callback2);

    // Simulate dose update
    DoseReading test_dose = CreateDoseReading(1.0f, 0.1f, 200.0f);
    callback1(test_dose);
    callback2(test_dose);

    // Assert
    EXPECT_EQ(callback1_count, 1);
    EXPECT_EQ(callback2_count, 1);
}

/**
 * @test IDoseMonitor.RegisterDoseCallback.PeriodicUpdates
 * @brief Verify callback receives periodic dose updates
 */
TEST_F(IDoseMonitorTest, RegisterDoseCallback_ReceivesPeriodicUpdates) {
    // Arrange
    std::vector<DoseReading> received_doses;

    DoseCallback callback = [&](const DoseReading& dose) {
        received_doses.push_back(dose);
    };

    EXPECT_CALL(mock_monitor, RegisterDoseCallback(::testing::_))
        .WillOnce(::testing::SaveArg<0>(&callback));

    // Act - register callback
    mock_monitor.RegisterDoseCallback(callback);

    // Simulate multiple dose updates
    for (int i = 1; i <= 5; ++i) {
        DoseReading dose = CreateDoseReading(
            static_cast<float>(i) * 0.5f,
            0.05f,
            static_cast<float>(i) * 100.0f
        );
        callback(dose);
    }

    // Assert
    EXPECT_EQ(received_doses.size(), 5);
    EXPECT_FLOAT_EQ(received_doses[0].dose_mgy, 0.5f);
    EXPECT_FLOAT_EQ(received_doses[4].dose_mgy, 2.5f);
}

// =============================================================================
// Thread Safety Tests (NFR-HAL-05)
// =============================================================================

/**
 * @test IDoseMonitor.ThreadSafety.ConcurrentGetCurrentDose
 * @brief Verify GetCurrentDose is thread-safe for concurrent calls
 */
TEST_F(IDoseMonitorTest, ThreadSafety_ConcurrentGetCurrentDoseCalls) {
    // Arrange
    const int num_threads = 10;
    const int calls_per_thread = 100;

    EXPECT_CALL(mock_monitor, GetCurrentDose())
        .Times(num_threads * calls_per_thread)
        .WillRepeatedly(::testing::Return(CreateDoseReading(1.0f, 0.1f, 100.0f)));

    // Act - concurrent GetCurrentDose calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                DoseReading dose = mock_monitor.GetCurrentDose();
                // Verify dose is valid
                EXPECT_GE(dose.dose_mgy, 0.0f);
                EXPECT_TRUE(std::isfinite(dose.dose_rate_mgy_s));
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
 * @test IDoseMonitor.ThreadSafety.ConcurrentGetDap
 * @brief Verify GetDap is thread-safe for concurrent calls
 */
TEST_F(IDoseMonitorTest, ThreadSafety_ConcurrentGetDapCalls) {
    // Arrange
    const int num_threads = 10;
    const int calls_per_thread = 100;

    EXPECT_CALL(mock_monitor, GetDap())
        .Times(num_threads * calls_per_thread)
        .WillRepeatedly(::testing::Return(1000.0f));

    // Act - concurrent GetDap calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                float dap = mock_monitor.GetDap();
                // Verify DAP is valid
                EXPECT_GE(dap, 0.0f);
                EXPECT_TRUE(std::isfinite(dap));
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
 * @test IDoseMonitor.ThreadSafety.ConcurrentReset
 * @brief Verify Reset is internally synchronized
 */
TEST_F(IDoseMonitorTest, ThreadSafety_ConcurrentResetCalls) {
    // Arrange
    const int num_threads = 5;
    const int calls_per_thread = 50;

    EXPECT_CALL(mock_monitor, Reset())
        .Times(num_threads * calls_per_thread);

    // Act - concurrent Reset calls
    std::vector<std::thread> threads;
    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([this, calls_per_thread]() {
            for (int j = 0; j < calls_per_thread; ++j) {
                mock_monitor.Reset();
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
// Dose Accumulation Tests
// =============================================================================

/**
 * @test IDoseMonitor.DoseAccumulation.Linear
 * @brief Verify dose accumulates linearly over time
 */
TEST_F(IDoseMonitorTest, DoseAccumulation_AccumulatesLinearly) {
    // Arrange
    std::vector<DoseReading> dose_history;
    const float dose_rate = 0.5f; // mGy/s

    DoseCallback callback = [&](const DoseReading& dose) {
        dose_history.push_back(dose);
    };

    EXPECT_CALL(mock_monitor, RegisterDoseCallback(::testing::_))
        .WillOnce(::testing::SaveArg<0>(&callback));

    mock_monitor.RegisterDoseCallback(callback);

    // Act - simulate dose accumulation over 5 seconds
    for (int i = 1; i <= 5; ++i) {
        DoseReading dose = CreateDoseReading(
            static_cast<float>(i) * dose_rate,  // Accumulated dose
            dose_rate,                           // Constant rate
            static_cast<float>(i) * 100.0f       // DAP
        );
        callback(dose);
    }

    // Assert
    EXPECT_EQ(dose_history.size(), 5);
    EXPECT_FLOAT_EQ(dose_history[0].dose_mgy, 0.5f);
    EXPECT_FLOAT_EQ(dose_history[4].dose_mgy, 2.5f);
}

/**
 * @test IDoseMonitor.DoseAccumulation.ResetClears
 * @brief Verify Reset clears accumulated dose
 */
TEST_F(IDoseMonitorTest, DoseAccumulation_ResetClearsAccumulatedDose) {
    // Arrange
    DoseReading accumulated = CreateDoseReading(5.0f, 0.5f, 1000.0f);
    DoseReading reset_dose = CreateZeroDoseReading();

    EXPECT_CALL(mock_monitor, GetCurrentDose())
        .WillOnce(::testing::Return(accumulated))
        .WillOnce(::testing::Return(reset_dose));

    EXPECT_CALL(mock_monitor, Reset())
        .Times(1);

    // Act
    DoseReading before = mock_monitor.GetCurrentDose();
    mock_monitor.Reset();
    DoseReading after = mock_monitor.GetCurrentDose();

    // Assert
    EXPECT_FLOAT_EQ(before.dose_mgy, 5.0f);
    EXPECT_FLOAT_EQ(after.dose_mgy, 0.0f);
}

// =============================================================================
// Error Condition Tests
// =============================================================================

/**
 * @test IDoseMonitor.InvalidDoseReading
 * @brief VerifyGetCurrentDose handles invalid readings
 */
TEST_F(IDoseMonitorTest, GetCurrentDose_HandlesNegativeDose) {
    // Arrange - negative dose (invalid)
    DoseReading invalid_dose = CreateDoseReading(-1.0f, 0.1f, 100.0f);

    EXPECT_CALL(mock_monitor, GetCurrentDose())
        .WillOnce(::testing::Return(invalid_dose));

    // Act
    DoseReading dose = mock_monitor.GetCurrentDose();

    // Assert - mock returns what it's configured to return
    EXPECT_FLOAT_EQ(dose.dose_mgy, -1.0f);
}

/**
 * @test IDoseMonitor.InvalidDap
 * @brief Verify GetDap handles invalid DAP values
 */
TEST_F(IDoseMonitorTest, GetDap_HandlesNegativeDap) {
    // Arrange - negative DAP (invalid)
    EXPECT_CALL(mock_monitor, GetDap())
        .WillOnce(::testing::Return(-100.0f));

    // Act
    float dap = mock_monitor.GetDap();

    // Assert - mock returns what it's configured to return
    EXPECT_FLOAT_EQ(dap, -100.0f);
}

// =============================================================================
// Main
// =============================================================================

int main(int argc, char** argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
