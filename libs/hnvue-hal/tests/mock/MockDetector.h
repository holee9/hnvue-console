/**
 * @file MockDetector.h
 * @brief Google Mock implementation of IDetector interface for unit testing
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Mock detector for isolated unit testing
 * SPDX-License-Identifier: MIT
 *
 * This mock class enables SOUP-isolated unit testing per NFR-HAL-03.
 * All interface methods are mockable with customizable expectations.
 */

#ifndef HNUE_HAL_TESTS_MOCK_DETECTOR_H
#define HNUE_HAL_TESTS_MOCK_DETECTOR_H

#include "hnvue/hal/IDetector.h"
#include <gmock/gmock.h>

namespace hnvue::hal::test {

/**
 * @brief Google Mock implementation of IDetector interface
 *
 * Provides full mockability for all IDetector methods with:
 * - EXPECT_CALL() for behavior verification
 * - ON_CALL() for default return values
 * - WithArgs() for argument matching
 * - WillOnce()/WillRepeatedly() for action customization
 *
 * Thread Safety: Mock is thread-safe where the interface is thread-safe.
 * Use InSequence() for ordered call expectations.
 */
class MockDetector : public IDetector {
public:
    /**
     * @brief Default constructor with relaxed defaults
     *
     * By default, all methods return sensible default values:
     * - GetDetectorInfo(): Returns empty DetectorInfo
     * - GetStatus(): Returns idle DetectorStatus
     * - StartAcquisition(): Returns true (success)
     * - StopAcquisition(): Returns true (success)
     * - RunCalibration(): Returns failed CalibrationResult
     */
    MockDetector() {
        // Set default return values for common calls
        ON_CALL(*this, GetDetectorInfo())
            .WillByDefault(::testing::Return(DetectorInfo{}));

        ON_CALL(*this, GetStatus())
            .WillByDefault(::testing::Return(DetectorStatus{}));

        ON_CALL(*this, StartAcquisition(::testing::_))
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, StopAcquisition())
            .WillByDefault(::testing::Return(true));

        ON_CALL(*this, RunCalibration(::testing::_, ::testing::_))
            .WillByDefault(::testing::Return(CalibrationResult{false, "", "Default mock failure"}));
    }

    /**
     * @brief Virtual destructor for proper cleanup
     */
    ~MockDetector() override = default;

    // =========================================================================
    // Mock Methods - All IDetector interface methods
    // =========================================================================

    /**
     * @brief Mock GetDetectorInfo
     *
     * Usage:
     *   EXPECT_CALL(mock, GetDetectorInfo())
     *       .WillOnce(Return(expected_info));
     */
    MOCK_METHOD(
        DetectorInfo,
        GetDetectorInfo,
        (),
        (override)
    );

    /**
     * @brief Mock GetStatus
     *
     * Usage:
     *   EXPECT_CALL(mock, GetStatus())
     *       .Times(::testing::AtLeast(1))
     *       .WillRepeatedly(Return(expected_status));
     */
    MOCK_METHOD(
        DetectorStatus,
        GetStatus,
        (),
        (override)
    );

    /**
     * @brief Mock StartAcquisition
     *
     * Usage:
     *   EXPECT_CALL(mock, StartAcquisition(Field(&AcquisitionConfig::num_frames, 100)))
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        StartAcquisition,
        (const AcquisitionConfig&),
        (override)
    );

    /**
     * @brief Mock StopAcquisition
     *
     * Usage:
     *   EXPECT_CALL(mock, StopAcquisition())
     *       .WillOnce(Return(true));
     */
    MOCK_METHOD(
        bool,
        StopAcquisition,
        (),
        (override)
    );

    /**
     * @brief Mock RunCalibration
     *
     * Usage:
     *   EXPECT_CALL(mock, RunCalibration(CalibType::CALIB_DARK_FIELD, 10))
     *       .WillOnce(Return(CalibrationResult{true, "/path/output.calib", ""}));
     */
    MOCK_METHOD(
        CalibrationResult,
        RunCalibration,
        (CalibType, int32_t),
        (override)
    );

    /**
     * @brief Mock RegisterFrameCallback
     *
     * Usage:
     *   EXPECT_CALL(mock, RegisterFrameCallback(::testing::_))
     *       .WillOnce(::testing::SaveArg<0>(&callback));
     *
     * Then invoke callback manually:
     *   callback(test_frame);
     */
    MOCK_METHOD(
        void,
        RegisterFrameCallback,
        (FrameCallback),
        (override)
    );
};

// =============================================================================
// Helper Functions for Common Mock Patterns
// =============================================================================

/**
 * @brief Create a default DetectorInfo for testing
 */
inline DetectorInfo CreateDefaultDetectorInfo() {
    DetectorInfo info;
    info.vendor = "MockVendor";
    info.model = "MockModel";
    info.serial_number = "SN12345";
    info.pixel_width = 2000;
    info.pixel_height = 2000;
    info.pixel_pitch_um = 200.0f;
    info.max_bit_depth = 16;
    info.max_frame_rate = 30.0f;
    info.firmware_version = "1.0.0";
    return info;
}

/**
 * @brief Create an idle DetectorStatus for testing
 */
inline DetectorStatus CreateIdleDetectorStatus() {
    DetectorStatus status;
    status.is_acquiring = false;
    status.current_session_id = "";
    status.frames_acquired = 0;
    status.temperature_c = 25.0f;
    return status;
}

/**
 * @brief Create an acquiring DetectorStatus for testing
 */
inline DetectorStatus CreateAcquiringDetectorStatus() {
    DetectorStatus status;
    status.is_acquiring = true;
    status.current_session_id = "test-session";
    status.frames_acquired = 50;
    status.temperature_c = 28.5f;
    return status;
}

} // namespace hnvue::hal::test

#endif // HNUE_HAL_TESTS_MOCK_DETECTOR_H
