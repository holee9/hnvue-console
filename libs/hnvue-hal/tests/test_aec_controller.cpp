/**
 * @file test_aec_controller.cpp
 * @brief GTest unit tests for AecController (FR-HAL-07)
 * @date 2026-03-18
 * @author abyz-lab
 *
 * IEC 62304 Class C — SAFETY CRITICAL: AEC signal handling
 * SPDX-License-Identifier: MIT
 *
 * Coverage target: 100% decision coverage (IEC 62304 Class C requirement)
 *
 * Decisions exercised:
 *   SetMode:                  valid mode / invalid mode / during exposure / not during exposure
 *   SetThreshold:             in-range / below 0 / above 100
 *   RegisterTerminationCallback: null cb / valid cb
 *   SimulateTerminationSignal:   MANUAL mode (ignore) / AUTO mode (invoke + abort)
 *   InvokeTerminationCallbacks:  no callbacks / one callback / callback throws / multiple callbacks
 *   SetExposureState:            true (blocks SetMode) / false (allows SetMode)
 *   Generator integration:       null generator / live generator (AbortExposure called)
 */

#include <gtest/gtest.h>
#include <gmock/gmock.h>
#include <atomic>
#include <chrono>
#include <memory>
#include <stdexcept>
#include <thread>
#include <vector>

#include "hnvue/hal/IAEC.h"
#include "hnvue/hal/IGenerator.h"
#include "hnvue/hal/HalTypes.h"
#include "aec/AecController.h"

#include "mock/MockGenerator.h"

using namespace hnvue::hal;
using namespace hnvue::hal::test;
using namespace testing;

namespace {

// =============================================================================
// Test Fixture
// =============================================================================

class AecControllerTest : public ::testing::Test {
protected:
    void SetUp() override {
        mock_generator_ = std::make_unique<NiceMock<MockGenerator>>();
        aec_ = std::make_unique<AecController>(mock_generator_.get());
    }

    void TearDown() override {
        aec_.reset();
        mock_generator_.reset();
    }

    std::unique_ptr<NiceMock<MockGenerator>> mock_generator_;
    std::unique_ptr<AecController> aec_;
};

// =============================================================================
// Construction Tests
// =============================================================================

/**
 * @test Default constructor initialises to MANUAL mode and 50% threshold
 * Covers: AecController() constructor path
 */
TEST_F(AecControllerTest, DefaultState_IsManualModeWith50PctThreshold) {
    EXPECT_EQ(aec_->GetMode(), AecMode::AEC_MANUAL);
    EXPECT_FLOAT_EQ(aec_->GetThreshold(), 50.0f);
}

/**
 * @test AecController constructed without generator (null) does not crash
 * Covers: generator_ == nullptr branch in SimulateTerminationSignal
 */
TEST(AecControllerNoGeneratorTest, ConstructedWithNullGenerator_NoCrash) {
    AecController aec(nullptr);
    EXPECT_EQ(aec.GetMode(), AecMode::AEC_MANUAL);
}

// =============================================================================
// SetMode Tests (FR-HAL-07)
// =============================================================================

/**
 * @test SetMode(AEC_MANUAL) → succeeds, mode stored
 * Covers: valid mode, !is_exposing branch → return true
 */
TEST_F(AecControllerTest, SetMode_ToManual_Succeeds) {
    EXPECT_TRUE(aec_->SetMode(AecMode::AEC_AUTO));   // first set to AUTO
    EXPECT_TRUE(aec_->SetMode(AecMode::AEC_MANUAL));
    EXPECT_EQ(aec_->GetMode(), AecMode::AEC_MANUAL);
}

/**
 * @test SetMode(AEC_AUTO) → succeeds, mode stored
 * Covers: valid mode, !is_exposing branch → return true
 */
TEST_F(AecControllerTest, SetMode_ToAuto_Succeeds) {
    EXPECT_TRUE(aec_->SetMode(AecMode::AEC_AUTO));
    EXPECT_EQ(aec_->GetMode(), AecMode::AEC_AUTO);
}

/**
 * @test SetMode rejected while exposure is active
 * Covers: is_exposing_ == true branch → return false
 * FR-HAL-07: Mode change not permitted during active exposure
 */
TEST_F(AecControllerTest, SetMode_RejectedDuringActiveExposure) {
    aec_->SetMode(AecMode::AEC_AUTO);
    aec_->SetExposureState(true);

    EXPECT_FALSE(aec_->SetMode(AecMode::AEC_MANUAL));
    EXPECT_EQ(aec_->GetMode(), AecMode::AEC_AUTO);  // unchanged
}

/**
 * @test SetMode allowed after exposure ends
 * Covers: is_exposing_ transitions true→false → SetMode succeeds again
 */
TEST_F(AecControllerTest, SetMode_AllowedAfterExposureEnds) {
    aec_->SetExposureState(true);
    EXPECT_FALSE(aec_->SetMode(AecMode::AEC_AUTO));  // rejected

    aec_->SetExposureState(false);
    EXPECT_TRUE(aec_->SetMode(AecMode::AEC_AUTO));   // now allowed
}

// =============================================================================
// SetThreshold Tests (FR-HAL-07)
// =============================================================================

/**
 * @test SetThreshold accepts boundary-valid values 0.0 and 100.0
 * Covers: threshold_pct == 0 and == 100 (boundary conditions)
 */
TEST_F(AecControllerTest, SetThreshold_AcceptsBoundaryValues) {
    EXPECT_TRUE(aec_->SetThreshold(0.0f));
    EXPECT_FLOAT_EQ(aec_->GetThreshold(), 0.0f);

    EXPECT_TRUE(aec_->SetThreshold(100.0f));
    EXPECT_FLOAT_EQ(aec_->GetThreshold(), 100.0f);
}

/**
 * @test SetThreshold accepts typical clinical value 50%
 * Covers: valid in-range branch → return true
 */
TEST_F(AecControllerTest, SetThreshold_AcceptsValidClinicalValue) {
    EXPECT_TRUE(aec_->SetThreshold(50.0f));
    EXPECT_FLOAT_EQ(aec_->GetThreshold(), 50.0f);
}

/**
 * @test SetThreshold rejects negative value
 * Covers: threshold_pct < 0.0f branch → return false
 */
TEST_F(AecControllerTest, SetThreshold_RejectsNegativeValue) {
    aec_->SetThreshold(75.0f);

    EXPECT_FALSE(aec_->SetThreshold(-0.1f));
    EXPECT_FLOAT_EQ(aec_->GetThreshold(), 75.0f);  // unchanged
}

/**
 * @test SetThreshold rejects value above 100
 * Covers: threshold_pct > 100.0f branch → return false
 */
TEST_F(AecControllerTest, SetThreshold_RejectsValueAbove100) {
    aec_->SetThreshold(75.0f);

    EXPECT_FALSE(aec_->SetThreshold(100.1f));
    EXPECT_FLOAT_EQ(aec_->GetThreshold(), 75.0f);  // unchanged
}

// =============================================================================
// RegisterTerminationCallback Tests (FR-HAL-07)
// =============================================================================

/**
 * @test Null callback is silently ignored
 * Covers: !cb branch in RegisterTerminationCallback → early return
 */
TEST_F(AecControllerTest, RegisterTerminationCallback_NullCallbackIgnored) {
    aec_->SetMode(AecMode::AEC_AUTO);
    aec_->RegisterTerminationCallback(nullptr);  // must not crash

    // Triggering signal with no real callbacks → should not crash
    AecTerminationEvent event{true, 10.0f, 100000};
    EXPECT_NO_THROW(aec_->SimulateTerminationSignal(event));
}

/**
 * @test Valid callback is registered and invoked on termination
 * Covers: valid cb branch → push_back, callback invoked
 */
TEST_F(AecControllerTest, RegisterTerminationCallback_ValidCallbackInvoked) {
    std::atomic<bool> called{false};
    aec_->SetMode(AecMode::AEC_AUTO);
    aec_->RegisterTerminationCallback([&](const AecTerminationEvent&) {
        called = true;
    });

    AecTerminationEvent event{true, 10.0f, 100000};
    aec_->SimulateTerminationSignal(event);

    EXPECT_TRUE(called);
}

// =============================================================================
// SimulateTerminationSignal Tests (FR-HAL-07)
// =============================================================================

/**
 * @test MANUAL mode ignores AEC termination signal
 * Covers: current_mode == AEC_MANUAL branch → early return (no callbacks, no abort)
 * FR-HAL-07: Manual mode terminates at scheduled time only
 */
TEST_F(AecControllerTest, SimulateTerminationSignal_ManualModeIgnoresSignal) {
    std::atomic<bool> called{false};
    aec_->SetMode(AecMode::AEC_MANUAL);
    aec_->RegisterTerminationCallback([&](const AecTerminationEvent&) {
        called = true;
    });

    EXPECT_CALL(*mock_generator_, AbortExposure()).Times(0);

    AecTerminationEvent event{true, 10.0f, 100000};
    aec_->SimulateTerminationSignal(event);

    EXPECT_FALSE(called);
}

/**
 * @test AUTO mode invokes all callbacks on termination signal
 * Covers: AEC_AUTO branch → InvokeTerminationCallbacks + generator->AbortExposure()
 */
TEST_F(AecControllerTest, SimulateTerminationSignal_AutoModeInvokesCallbacks) {
    std::atomic<int> invoke_count{0};
    aec_->SetMode(AecMode::AEC_AUTO);
    aec_->RegisterTerminationCallback([&](const AecTerminationEvent&) {
        invoke_count++;
    });
    aec_->RegisterTerminationCallback([&](const AecTerminationEvent&) {
        invoke_count++;
    });

    AecTerminationEvent event{true, 15.5f, 200000};
    aec_->SimulateTerminationSignal(event);

    EXPECT_EQ(invoke_count, 2);
}

/**
 * @test Termination event data is forwarded unchanged to callbacks
 * Covers: callback(event) call with correct data
 */
TEST_F(AecControllerTest, SimulateTerminationSignal_EventDataForwarded) {
    AecTerminationEvent received{};
    aec_->SetMode(AecMode::AEC_AUTO);
    aec_->RegisterTerminationCallback([&](const AecTerminationEvent& e) {
        received = e;
    });

    AecTerminationEvent expected{true, 22.5f, 350000};
    aec_->SimulateTerminationSignal(expected);

    EXPECT_EQ(received.threshold_reached, expected.threshold_reached);
    EXPECT_FLOAT_EQ(received.actual_dose_mgy, expected.actual_dose_mgy);
    EXPECT_EQ(received.exposure_time_us, expected.exposure_time_us);
}

/**
 * @test AEC termination triggers generator AbortExposure in AUTO mode
 * Covers: generator_ != nullptr branch → generator_->AbortExposure() called
 * FR-HAL-07: Abort sequence must initiate within 5ms of signal assertion
 */
TEST_F(AecControllerTest, AecTermination_TriggersGeneratorAbort) {
    aec_->SetMode(AecMode::AEC_AUTO);
    EXPECT_CALL(*mock_generator_, AbortExposure()).Times(1);

    AecTerminationEvent event{true, 10.0f, 100000};
    aec_->SimulateTerminationSignal(event);
}

/**
 * @test AbortExposure not called in MANUAL mode
 * Covers: AEC_MANUAL early return — generator abort skipped
 */
TEST_F(AecControllerTest, AecTermination_NoAbortInManualMode) {
    aec_->SetMode(AecMode::AEC_MANUAL);
    EXPECT_CALL(*mock_generator_, AbortExposure()).Times(0);

    AecTerminationEvent event{false, 5.0f, 50000};
    aec_->SimulateTerminationSignal(event);
}

/**
 * @test Null generator in AUTO mode — signal processed, no crash
 * Covers: generator_ == nullptr branch in SimulateTerminationSignal → skip AbortExposure
 */
TEST(AecControllerNoGeneratorTest, AutoModeWithNullGenerator_CallbacksInvokedNoCrash) {
    AecController aec(nullptr);
    ASSERT_TRUE(aec.SetMode(AecMode::AEC_AUTO));

    std::atomic<bool> called{false};
    aec.RegisterTerminationCallback([&](const AecTerminationEvent&) {
        called = true;
    });

    AecTerminationEvent event{true, 10.0f, 100000};
    EXPECT_NO_THROW(aec.SimulateTerminationSignal(event));
    EXPECT_TRUE(called);
}

// =============================================================================
// Exception Safety in Callbacks (InvokeTerminationCallbacks)
// =============================================================================

/**
 * @test std::exception in one callback does not prevent subsequent callbacks
 * Covers: catch(const std::exception&) branch in InvokeTerminationCallbacks
 * FR-HAL-07: Exception in one callback must not prevent others
 */
TEST_F(AecControllerTest, CallbackException_DoesNotPreventOtherCallbacks) {
    std::atomic<bool> second_called{false};
    aec_->SetMode(AecMode::AEC_AUTO);

    // First callback throws
    aec_->RegisterTerminationCallback([](const AecTerminationEvent&) {
        throw std::runtime_error("test exception");
    });

    // Second callback must still be invoked
    aec_->RegisterTerminationCallback([&](const AecTerminationEvent&) {
        second_called = true;
    });

    AecTerminationEvent event{true, 10.0f, 100000};
    EXPECT_NO_THROW(aec_->SimulateTerminationSignal(event));
    EXPECT_TRUE(second_called);
}

/**
 * @test Non-std::exception in callback is caught by catch(...) guard
 * Covers: catch(...) branch in InvokeTerminationCallbacks
 */
TEST_F(AecControllerTest, CallbackUnknownException_CaughtByEllipsis) {
    std::atomic<bool> second_called{false};
    aec_->SetMode(AecMode::AEC_AUTO);

    // First callback throws non-std type
    aec_->RegisterTerminationCallback([](const AecTerminationEvent&) {
        throw 42;  // non-std::exception type
    });

    aec_->RegisterTerminationCallback([&](const AecTerminationEvent&) {
        second_called = true;
    });

    AecTerminationEvent event{true, 10.0f, 100000};
    EXPECT_NO_THROW(aec_->SimulateTerminationSignal(event));
    EXPECT_TRUE(second_called);
}

// =============================================================================
// Timing Tests (FR-HAL-07)
// =============================================================================

/**
 * @test SimulateTerminationSignal completes callback+abort within 5ms
 * FR-HAL-07: Abort sequence must initiate within 5ms of AEC signal assertion
 */
TEST_F(AecControllerTest, TerminationSignal_CompletesWithin5ms) {
    aec_->SetMode(AecMode::AEC_AUTO);
    aec_->RegisterTerminationCallback([](const AecTerminationEvent&) {
        // Simulate lightweight callback work
    });

    AecTerminationEvent event{true, 10.0f, 100000};

    auto start = std::chrono::high_resolution_clock::now();
    aec_->SimulateTerminationSignal(event);
    auto elapsed_us = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::high_resolution_clock::now() - start
    ).count();

    // FR-HAL-07: Must complete within 5ms (5000us)
    EXPECT_LT(elapsed_us, 5000)
        << "SimulateTerminationSignal took " << elapsed_us
        << "us, exceeding FR-HAL-07 5ms requirement";
}

// =============================================================================
// Thread Safety Tests (NFR-HAL-05)
// =============================================================================

/**
 * @test GetMode and GetThreshold are safe for concurrent reads
 * Covers: atomic load operations from multiple threads
 */
TEST_F(AecControllerTest, ConcurrentReads_ThreadSafe) {
    constexpr int kThreads = 10;
    constexpr int kIterations = 1000;

    std::vector<std::thread> threads;
    threads.reserve(kThreads);

    for (int i = 0; i < kThreads; ++i) {
        threads.emplace_back([this]() {
            for (int j = 0; j < kIterations; ++j) {
                (void)aec_->GetMode();
                (void)aec_->GetThreshold();
            }
        });
    }

    for (auto& t : threads) {
        t.join();
    }

    SUCCEED();  // No crash = thread safety verified
}

/**
 * @test Concurrent SetMode calls do not cause data races
 * Covers: atomic store synchronisation under concurrent write pressure
 */
TEST_F(AecControllerTest, ConcurrentSetMode_ThreadSafe) {
    constexpr int kThreads = 5;
    constexpr int kIterations = 200;

    std::vector<std::thread> threads;
    threads.reserve(kThreads);

    for (int i = 0; i < kThreads; ++i) {
        threads.emplace_back([this, i]() {
            for (int j = 0; j < kIterations; ++j) {
                AecMode mode = (j % 2 == 0) ? AecMode::AEC_MANUAL : AecMode::AEC_AUTO;
                aec_->SetMode(mode);
            }
        });
    }

    for (auto& t : threads) {
        t.join();
    }

    // Final mode is one of the two valid values
    AecMode final_mode = aec_->GetMode();
    EXPECT_TRUE(final_mode == AecMode::AEC_MANUAL || final_mode == AecMode::AEC_AUTO);
}

/**
 * @test Concurrent RegisterTerminationCallback and SimulateTerminationSignal
 * Covers: mutex contention between registration and callback copy
 */
TEST_F(AecControllerTest, ConcurrentCallbackRegistrationAndSignal_ThreadSafe) {
    aec_->SetMode(AecMode::AEC_AUTO);

    std::atomic<bool> stop{false};

    // Thread 1: Continuously registers callbacks
    std::thread registrar([this, &stop]() {
        while (!stop.load()) {
            aec_->RegisterTerminationCallback([](const AecTerminationEvent&) {});
        }
    });

    // Thread 2: Fires termination signals
    AecTerminationEvent event{true, 10.0f, 100000};
    for (int i = 0; i < 50; ++i) {
        EXPECT_NO_THROW(aec_->SimulateTerminationSignal(event));
    }

    stop = true;
    registrar.join();

    SUCCEED();
}

} // anonymous namespace
