/**
 * @file test_error_handling.cpp
 * @brief Error handling and edge case tests
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Error handling validation tests
 * SPDX-License-Identifier: MIT
 *
 * Tests:
 * - Null pointer handling
 * - Invalid dimensions
 * - Overflow/underflow scenarios
 * - Invalid calibration data
 * - Error state recovery
 */

#include <gtest/gtest.h>
#include <hnvue/imaging/DefaultImageProcessingEngine.h>
#include <hnvue/imaging/CalibrationManager.h>
#include <hnvue/imaging/ImagingTypes.h>
#include <vector>
#include <limits>

using namespace hnvue::imaging;

// =============================================================================
// Error Handling Test Fixture
// =============================================================================

class ErrorHandlingTest : public ::testing::Test {
protected:
    std::unique_ptr<DefaultImageProcessingEngine> engine_;

    void SetUp() override {
        engine_ = std::make_unique<DefaultImageProcessingEngine>();
    }

    void TearDown() override {
        if (engine_) {
            engine_->Shutdown();
        }
    }

    bool InitializeEngine() {
        EngineConfig config;
        config.max_frame_width = 512;
        config.max_frame_height = 512;
        return engine_->Initialize(config);
    }

    ImageBuffer CreateValidFrame() {
        static std::vector<uint16_t> data(512 * 512, 10000);
        ImageBuffer frame;
        frame.width = 512;
        frame.height = 512;
        frame.pixel_depth = 16;
        frame.stride = 512 * 2;
        frame.data = data.data();
        frame.timestamp_us = 1234567890ULL;
        frame.frame_id = 1;
        return frame;
    }

    CalibrationData CreateValidCalibration(CalibrationDataType type) {
        static std::vector<float> dark_data(512 * 512, 100.0f);
        static std::vector<float> gain_data(512 * 512, 1.0f);

        CalibrationData calib;
        calib.type = type;
        calib.width = 512;
        calib.height = 512;
        calib.data_f32 = (type == CalibrationDataType::DARK_FRAME) ? dark_data.data() : gain_data.data();
        calib.valid = true;
        calib.checksum = 0xDEADBEEF;
        calib.acquisition_time_us = 1234567890ULL;
        return calib;
    }
};

// =============================================================================
// Null Pointer Tests
// =============================================================================

TEST_F(ErrorHandlingTest, NullDataFrame_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    frame.data = nullptr;

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));

    EngineError error = engine_->GetLastError();
    EXPECT_TRUE(error.HasError());
}

TEST_F(ErrorHandlingTest, NullCalibrationData_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    CalibrationData dark;
    dark.data_f32 = nullptr;
    dark.valid = true;  // But data is null

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(ErrorHandlingTest, NullDefectMapPixels_IsHandledGracefully) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    DefectMap map;
    map.count = 0;
    map.pixels = nullptr;
    map.valid = false;  // Empty map is OK

    EXPECT_TRUE(engine_->ApplyDefectPixelMap(frame, map));
}

// =============================================================================
// Invalid Dimension Tests
// =============================================================================

TEST_F(ErrorHandlingTest, ZeroWidthFrame_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    frame.width = 0;

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(ErrorHandlingTest, ZeroHeightFrame_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    frame.height = 0;

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(ErrorHandlingTest, InvalidPixelDepth_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    frame.pixel_depth = 8;  // Wrong depth

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(ErrorHandlingTest, InsufficientStride_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    frame.stride = frame.width;  // Too small (should be at least width * 2)

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

// =============================================================================
// Dimension Mismatch Tests
// =============================================================================

TEST_F(ErrorHandlingTest, CalibrationDimensionMismatch_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();  // 512x512

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);
    dark.width = 256;  // Wrong dimensions
    dark.height = 512;

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(ErrorHandlingTest, CalibrationSizeMismatch_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();  // 512x512

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);
    dark.width = 512;
    dark.height = 256;  // Wrong height

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

// =============================================================================
// Invalid Calibration Data Tests
// =============================================================================

TEST_F(ErrorHandlingTest, InvalidCalibrationFlag_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);
    dark.valid = false;  // Mark as invalid

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(ErrorHandlingTest, WrongCalibrationType_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    CalibrationData gain = CreateValidCalibration(CalibrationDataType::GAIN_MAP);

    // Try to use gain map as dark frame
    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, gain));
}

TEST_F(ErrorHandlingTest, DarkFrameAsGain_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_FALSE(engine_->ApplyGainCorrection(frame, dark));
}

// =============================================================================
// Uninitialized Engine Tests
// =============================================================================

TEST_F(ErrorHandlingTest, UninitializedEngine_OffsetCorrectionFails) {
    // Don't initialize

    ImageBuffer frame = CreateValidFrame();
    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));

    EngineError error = engine_->GetLastError();
    EXPECT_EQ(error.error_code, ImagingError::IMAGING_ERR_INIT);
}

TEST_F(ErrorHandlingTest, UninitializedEngine_GainCorrectionFails) {
    ImageBuffer frame = CreateValidFrame();
    CalibrationData gain = CreateValidCalibration(CalibrationDataType::GAIN_MAP);

    EXPECT_FALSE(engine_->ApplyGainCorrection(frame, gain));

    EngineError error = engine_->GetLastError();
    EXPECT_EQ(error.error_code, ImagingError::IMAGING_ERR_INIT);
}

TEST_F(ErrorHandlingTest, UninitializedEngine_ProcessFrameFails) {
    ImageBuffer frame = CreateValidFrame();
    ProcessingConfig config;
    config.mode = ProcessingMode::PREVIEW;
    config.calibration_dark = &CreateValidCalibration(CalibrationDataType::DARK_FRAME);
    config.calibration_gain = &CreateValidCalibration(CalibrationDataType::GAIN_MAP);

    EXPECT_FALSE(engine_->ProcessFrame(frame, config));

    EngineError error = engine_->GetLastError();
    EXPECT_EQ(error.error_code, ImagingError::IMAGING_ERR_INIT);
}

// =============================================================================
// Initialization Error Tests
// =============================================================================

TEST_F(ErrorHandlingTest, InitializeWithZeroDimensions_Fails) {
    EngineConfig config;
    config.max_frame_width = 0;
    config.max_frame_height = 0;

    EXPECT_FALSE(engine_->Initialize(config));
}

TEST_F(ErrorHandlingTest, DoubleInitialize_Fails) {
    EngineConfig config;
    config.max_frame_width = 512;
    config.max_frame_height = 512;

    ASSERT_TRUE(engine_->Initialize(config));
    EXPECT_FALSE(engine_->Initialize(config));  // Second call fails
}

// =============================================================================
// Window/Level Error Tests
// =============================================================================

TEST_F(ErrorHandlingTest, ZeroWindowWidth_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();

    EXPECT_FALSE(engine_->ApplyWindowLevel(frame, 0.0f, 2000.0f));

    EngineError error = engine_->GetLastError();
    EXPECT_EQ(error.error_code, ImagingError::IMAGING_ERR_PARAM);
}

TEST_F(ErrorHandlingTest, NegativeWindowWidth_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();

    EXPECT_FALSE(engine_->ApplyWindowLevel(frame, -100.0f, 2000.0f));
}

// =============================================================================
// ProcessFrame Configuration Error Tests
// =============================================================================

TEST_F(ErrorHandlingTest, ProcessFrameWithNullDarkCalibration_Fails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    ProcessingConfig config;
    config.mode = ProcessingMode::FULL_PIPELINE;
    config.calibration_dark = nullptr;  // Missing
    config.calibration_gain = &CreateValidCalibration(CalibrationDataType::GAIN_MAP);
    config.defect_map = nullptr;  // Missing

    EXPECT_FALSE(engine_->ProcessFrame(frame, config));
}

TEST_F(ErrorHandlingTest, ProcessFrameWithNullGainCalibration_Fails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    ProcessingConfig config;
    config.mode = ProcessingMode::FULL_PIPELINE;
    config.calibration_dark = &CreateValidCalibration(CalibrationDataType::DARK_FRAME);
    config.calibration_gain = nullptr;  // Missing
    config.defect_map = nullptr;  // Missing

    EXPECT_FALSE(engine_->ProcessFrame(frame, config));
}

TEST_F(ErrorHandlingTest, ProcessFrameWithNullDefectMap_Fails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    ProcessingConfig config;
    config.mode = ProcessingMode::FULL_PIPELINE;
    config.calibration_dark = &CreateValidCalibration(CalibrationDataType::DARK_FRAME);
    config.calibration_gain = &CreateValidCalibration(CalibrationDataType::GAIN_MAP);
    config.defect_map = nullptr;  // Missing

    EXPECT_FALSE(engine_->ProcessFrame(frame, config));
}

// =============================================================================
// Noise Reduction Error Tests
// =============================================================================

TEST_F(ErrorHandlingTest, UnknownNoiseFilterType_ReturnsError) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();
    NoiseReductionConfig config;
    config.enabled = true;
    config.filter_type = static_cast<NoiseFilterType>(999);  // Invalid
    config.kernel_size = 3.0f;

    EXPECT_FALSE(engine_->ApplyNoiseReduction(frame, config));
}

// =============================================================================
// Error State Recovery Tests
// =============================================================================

TEST_F(ErrorHandlingTest, ErrorStateClearedOnSuccess) {
    ASSERT_TRUE(InitializeEngine());

    // Cause an error
    ImageBuffer invalid_frame;
    invalid_frame.width = 0;
    invalid_frame.data = nullptr;

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);
    EXPECT_FALSE(engine_->ApplyOffsetCorrection(invalid_frame, dark));
    EXPECT_TRUE(engine_->GetLastError().HasError());

    // Now succeed
    ImageBuffer valid_frame = CreateValidFrame();
    EXPECT_TRUE(engine_->ApplyOffsetCorrection(valid_frame, dark));
    EXPECT_FALSE(engine_->GetLastError().HasError());
}

TEST_F(ErrorHandlingTest, MultipleErrorsReportCorrectly) {
    ASSERT_TRUE(InitializeEngine());

    // First error
    ImageBuffer invalid_frame;
    invalid_frame.width = 0;
    invalid_frame.data = nullptr;

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);
    EXPECT_FALSE(engine_->ApplyOffsetCorrection(invalid_frame, dark));

    EngineError error1 = engine_->GetLastError();

    // Second error
    EXPECT_FALSE(engine_->ApplyWindowLevel(invalid_frame, 4000.0f, 2000.0f));

    EngineError error2 = engine_->GetLastError();

    // Both should be errors
    EXPECT_TRUE(error1.HasError());
    EXPECT_TRUE(error2.HasError());
}

// =============================================================================
// Edge Case Value Tests
// =============================================================================

TEST_F(ErrorHandlingTest, AllZeroFrame_HandledCorrectly) {
    ASSERT_TRUE(InitializeEngine());

    static std::vector<uint16_t> zero_data(512 * 512, 0);
    ImageBuffer frame = CreateValidFrame();
    frame.data = zero_data.data();

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    // Should succeed even with all zeros
    EXPECT_TRUE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(ErrorHandlingTest, AllMaxValueFrame_HandledCorrectly) {
    ASSERT_TRUE(InitializeEngine());

    static std::vector<uint16_t> max_data(512 * 512, 65535);
    ImageBuffer frame = CreateValidFrame();
    frame.data = max_data.data();

    CalibrationData dark = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_TRUE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(ErrorHandlingTest, LargeDefectMap_HandledCorrectly) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();

    // Create a large defect map
    static std::vector<DefectPixelEntry> many_defects(1000);
    for (size_t i = 0; i < many_defects.size(); ++i) {
        many_defects[i] = {
            static_cast<uint32_t>(i % 512),
            static_cast<uint32_t>((i / 512) % 512),
            DefectPixelType::DEAD_PIXEL,
            InterpolationMethod::BILINEAR
        };
    }

    DefectMap map;
    map.count = static_cast<uint32_t>(many_defects.size());
    map.pixels = many_defects.data();
    map.valid = true;

    EXPECT_TRUE(engine_->ApplyDefectPixelMap(frame, map));
}

TEST_F(ErrorHandlingTest, OutOfBoundsDefectPixel_HandledGracefully) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateValidFrame();

    // Create a defect map with out-of-bounds pixels
    static DefectPixelEntry bad_defects[] = {
        {1000, 1000, DefectPixelType::DEAD_PIXEL, InterpolationMethod::BILINEAR},  // Out of bounds
        {0, 0, DefectPixelType::DEAD_PIXEL, InterpolationMethod::BILINEAR}  // Valid
    };

    DefectMap map;
    map.count = 2;
    map.pixels = bad_defects;
    map.valid = true;

    // Should succeed - out of bounds pixels are skipped
    EXPECT_TRUE(engine_->ApplyDefectPixelMap(frame, map));
}
