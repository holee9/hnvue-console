/**
 * @file test_default_engine.cpp
 * @brief Unit tests for DefaultImageProcessingEngine
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Default engine implementation tests
 * SPDX-License-Identifier: MIT
 *
 * Tests:
 * - Initialization and shutdown
 * - Individual pipeline stage execution
 * - Full pipeline execution (FULL_PIPELINE and PREVIEW modes)
 * - Error handling and validation
 */

#include <gtest/gtest.h>
#include <hnvue/imaging/DefaultImageProcessingEngine.h>
#include <hnvue/imaging/ImagingTypes.h>
#include <vector>
#include <cstring>

using namespace hnvue::imaging;

// =============================================================================
// Test Fixture
// =============================================================================

class DefaultImageProcessingEngineTest : public ::testing::Test {
protected:
    static constexpr uint32_t TEST_WIDTH = 256;
    static constexpr uint32_t TEST_HEIGHT = 256;
    static constexpr uint32_t TEST_SIZE = TEST_WIDTH * TEST_HEIGHT;

    std::unique_ptr<DefaultImageProcessingEngine> engine_;
    std::vector<uint16_t> frame_data_;
    std::vector<float> dark_data_;
    std::vector<float> gain_data_;
    std::vector<DefectPixelEntry> defect_entries_;

    void SetUp() override {
        engine_ = std::make_unique<DefaultImageProcessingEngine>();

        // Initialize frame data with gradient pattern
        frame_data_.resize(TEST_SIZE);
        for (uint32_t y = 0; y < TEST_HEIGHT; ++y) {
            for (uint32_t x = 0; x < TEST_WIDTH; ++x) {
                frame_data_[y * TEST_WIDTH + x] = static_cast<uint16_t>(
                    (x + y) * 100 % 65536);
            }
        }

        // Initialize dark frame data (constant offset)
        dark_data_.resize(TEST_SIZE);
        for (size_t i = 0; i < TEST_SIZE; ++i) {
            dark_data_[i] = 100.0f;  // Dark current offset
        }

        // Initialize gain map data (unity gain with slight variation)
        gain_data_.resize(TEST_SIZE);
        for (size_t i = 0; i < TEST_SIZE; ++i) {
            gain_data_[i] = 1.0f + (i % 100) / 10000.0f;
        }

        // Create a few defect pixels
        defect_entries_.resize(3);
        defect_entries_[0] = {128, 128, DefectPixelType::DEAD_PIXEL,
                             InterpolationMethod::BILINEAR};
        defect_entries_[1] = {64, 64, DefectPixelType::HOT_PIXEL,
                             InterpolationMethod::MEDIAN_3X3};
        defect_entries_[2] = {200, 200, DefectPixelType::DEAD_PIXEL,
                             InterpolationMethod::NEAREST_NEIGHBOR};
    }

    void TearDown() override {
        if (engine_) {
            engine_->Shutdown();
        }
    }

    ImageBuffer CreateTestFrame() {
        ImageBuffer frame;
        frame.width = TEST_WIDTH;
        frame.height = TEST_HEIGHT;
        frame.pixel_depth = 16;
        frame.stride = TEST_WIDTH * 2;
        frame.data = frame_data_.data();
        frame.timestamp_us = 1234567890ULL;
        frame.frame_id = 1;
        return frame;
    }

    CalibrationData CreateDarkCalibration() {
        CalibrationData dark;
        dark.type = CalibrationDataType::DARK_FRAME;
        dark.width = TEST_WIDTH;
        dark.height = TEST_HEIGHT;
        dark.data_f32 = dark_data_.data();
        dark.valid = true;
        dark.checksum = 0xDEADBEEF;
        return dark;
    }

    CalibrationData CreateGainCalibration() {
        CalibrationData gain;
        gain.type = CalibrationDataType::GAIN_MAP;
        gain.width = TEST_WIDTH;
        gain.height = TEST_HEIGHT;
        gain.data_f32 = gain_data_.data();
        gain.valid = true;
        gain.checksum = 0xBADDCAFE;
        return gain;
    }

    DefectMap CreateDefectMap() {
        DefectMap map;
        map.count = static_cast<uint32_t>(defect_entries_.size());
        map.pixels = defect_entries_.data();
        map.valid = true;
        map.checksum = 0DEFECT01;
        return map;
    }

    bool InitializeEngine() {
        EngineConfig config;
        config.max_frame_width = TEST_WIDTH;
        config.max_frame_height = TEST_HEIGHT;
        config.num_threads = 1;
        return engine_->Initialize(config);
    }
};

// =============================================================================
// Initialization Tests
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, InitializeSucceeds) {
    EngineConfig config;
    config.max_frame_width = TEST_WIDTH;
    config.max_frame_height = TEST_HEIGHT;

    EXPECT_TRUE(engine_->Initialize(config));
}

TEST_F(DefaultImageProcessingEngineTest, InitializeFailsWithZeroDimensions) {
    EngineConfig config;
    config.max_frame_width = 0;
    config.max_frame_height = 0;

    EXPECT_FALSE(engine_->Initialize(config));
}

TEST_F(DefaultImageProcessingEngineTest, InitializeFailsTwice) {
    EngineConfig config;
    config.max_frame_width = TEST_WIDTH;
    config.max_frame_height = TEST_HEIGHT;

    EXPECT_TRUE(engine_->Initialize(config));
    EXPECT_FALSE(engine_->Initialize(config));  // Second call should fail
}

TEST_F(DefaultImageProcessingEngineTest, ShutdownIsIdempotent) {
    EngineConfig config;
    config.max_frame_width = TEST_WIDTH;
    config.max_frame_height = TEST_HEIGHT;

    engine_->Initialize(config);
    engine_->Shutdown();
    engine_->Shutdown();  // Should not crash

    SUCCEED();
}

TEST_F(DefaultImageProcessingEngineTest, GetEngineInfo) {
    EngineInfo info = engine_->GetEngineInfo();

    EXPECT_EQ(info.engine_name, "DefaultImageProcessingEngine");
    EXPECT_FALSE(info.engine_version.empty());
    EXPECT_EQ(info.vendor, "HnVue");
}

TEST_F(DefaultImageProcessingEngineTest, EngineCapabilities) {
    EngineInfo info = engine_->GetEngineInfo();

    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_OFFSET_CORRECTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_GAIN_CORRECTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_DEFECT_PIXEL_MAP));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_SCATTER_CORRECTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_WINDOW_LEVEL));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_NOISE_REDUCTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_FLATTENING));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_PREVIEW_MODE));
}

// =============================================================================
// Offset Correction Tests (FR-IMG-01)
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, ApplyOffsetCorrectionWithoutInitializeFails) {
    ImageBuffer frame = CreateTestFrame();
    CalibrationData dark = CreateDarkCalibration();

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyOffsetCorrectionSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    CalibrationData dark = CreateDarkCalibration();

    uint16_t original_value = frame.data[0];
    EXPECT_TRUE(engine_->ApplyOffsetCorrection(frame, dark));

    // After offset correction, pixel should be lower (dark subtraction)
    // Values are clamped to zero
    EXPECT_TRUE(frame.data[0] <= original_value);
}

TEST_F(DefaultImageProcessingEngineTest, ApplyOffsetCorrectionWithInvalidCalibrationFails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    CalibrationData dark = CreateDarkCalibration();
    dark.valid = false;  // Mark as invalid

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyOffsetCorrectionWithWrongTypeFails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    CalibrationData gain = CreateGainCalibration();  // Pass gain as dark

    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, gain));
}

TEST_F(DefaultImageProcessingEngineTest, OffsetCorrectionTiming) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    CalibrationData dark = CreateDarkCalibration();

    engine_->ApplyOffsetCorrection(frame, dark);
    StageTiming timing = engine_->GetLastTiming();

    EXPECT_GT(timing.offset_correction_us, 0);
}

// =============================================================================
// Gain Correction Tests (FR-IMG-02)
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, ApplyGainCorrectionSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    CalibrationData gain = CreateGainCalibration();

    // First apply offset to get values in reasonable range
    CalibrationData dark = CreateDarkCalibration();
    engine_->ApplyOffsetCorrection(frame, dark);

    EXPECT_TRUE(engine_->ApplyGainCorrection(frame, gain));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyGainCorrectionWithInvalidCalibrationFails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    CalibrationData gain = CreateGainCalibration();
    gain.valid = false;

    EXPECT_FALSE(engine_->ApplyGainCorrection(frame, gain));
}

TEST_F(DefaultImageProcessingEngineTest, GainCorrectionTiming) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    CalibrationData dark = CreateDarkCalibration();
    CalibrationData gain = CreateGainCalibration();

    engine_->ApplyOffsetCorrection(frame, dark);
    engine_->ApplyGainCorrection(frame, gain);

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_GT(timing.gain_correction_us, 0);
}

// =============================================================================
// Defect Pixel Mapping Tests (FR-IMG-03)
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, ApplyDefectPixelMapSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    DefectMap map = CreateDefectMap();

    EXPECT_TRUE(engine_->ApplyDefectPixelMap(frame, map));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyDefectPixelMapWithEmptyMapSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    DefectMap map;
    map.count = 0;
    map.pixels = nullptr;
    map.valid = false;  // Empty map is OK

    EXPECT_TRUE(engine_->ApplyDefectPixelMap(frame, map));
}

TEST_F(DefaultImageProcessingEngineTest, DefectPixelMapTiming) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    DefectMap map = CreateDefectMap();

    engine_->ApplyDefectPixelMap(frame, map);
    StageTiming timing = engine_->GetLastTiming();

    EXPECT_GT(timing.defect_pixel_map_us, 0);
}

// =============================================================================
// Scatter Correction Tests (FR-IMG-04)
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, ApplyScatterCorrectionWhenDisabledSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    ScatterParams params;
    params.enabled = false;

    EXPECT_TRUE(engine_->ApplyScatterCorrection(frame, params));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyScatterCorrectionWhenEnabledSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    ScatterParams params;
    params.enabled = true;
    params.cutoff_frequency = 0.1f;
    params.suppression_ratio = 0.5f;

    EXPECT_TRUE(engine_->ApplyScatterCorrection(frame, params));
}

TEST_F(DefaultImageProcessingEngineTest, ScatterCorrectionTiming) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    ScatterParams params;
    params.enabled = true;

    engine_->ApplyScatterCorrection(frame, params);
    StageTiming timing = engine_->GetLastTiming();

    EXPECT_GT(timing.scatter_correction_us, 0);
}

// =============================================================================
// Window/Level Tests (FR-IMG-05)
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, ApplyWindowLevelSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();

    EXPECT_TRUE(engine_->ApplyWindowLevel(frame, 4000.0f, 2000.0f));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyWindowLevelWithZeroWindowFails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();

    EXPECT_FALSE(engine_->ApplyWindowLevel(frame, 0.0f, 2000.0f));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyWindowLevelWithNegativeWindowFails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();

    EXPECT_FALSE(engine_->ApplyWindowLevel(frame, -100.0f, 2000.0f));
}

TEST_F(DefaultImageProcessingEngineTest, WindowLevelTiming) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();

    engine_->ApplyWindowLevel(frame, 4000.0f, 2000.0f);
    StageTiming timing = engine_->GetLastTiming();

    EXPECT_GT(timing.window_level_us, 0);
}

// =============================================================================
// Noise Reduction Tests (FR-IMG-06)
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, ApplyNoiseReductionWhenDisabledSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    NoiseReductionConfig config;
    config.enabled = false;

    EXPECT_TRUE(engine_->ApplyNoiseReduction(frame, config));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyGaussianNoiseReductionSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    NoiseReductionConfig config;
    config.enabled = true;
    config.filter_type = NoiseFilterType::GAUSSIAN;
    config.kernel_size = 3.0f;
    config.sigma = 1.0f;

    EXPECT_TRUE(engine_->ApplyNoiseReduction(frame, config));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyMedianNoiseReductionSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    NoiseReductionConfig config;
    config.enabled = true;
    config.filter_type = NoiseFilterType::MEDIAN;
    config.kernel_size = 3.0f;

    EXPECT_TRUE(engine_->ApplyNoiseReduction(frame, config));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyBilateralNoiseReductionSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    NoiseReductionConfig config;
    config.enabled = true;
    config.filter_type = NoiseFilterType::BILATERAL;
    config.kernel_size = 3.0f;
    config.sigma = 1.0f;

    EXPECT_TRUE(engine_->ApplyNoiseReduction(frame, config));
}

TEST_F(DefaultImageProcessingEngineTest, NoiseReductionTiming) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    NoiseReductionConfig config;
    config.enabled = true;
    config.filter_type = NoiseFilterType::GAUSSIAN;

    engine_->ApplyNoiseReduction(frame, config);
    StageTiming timing = engine_->GetLastTiming();

    EXPECT_GT(timing.noise_reduction_us, 0);
}

// =============================================================================
// Flattening Tests (FR-IMG-07)
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, ApplyFlatteningWhenDisabledSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    FlatteningConfig config;
    config.enabled = false;

    EXPECT_TRUE(engine_->ApplyFlattening(frame, config));
}

TEST_F(DefaultImageProcessingEngineTest, ApplyFlatteningWhenEnabledSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    FlatteningConfig config;
    config.enabled = true;
    config.sigma_background = 50.0f;

    EXPECT_TRUE(engine_->ApplyFlattening(frame, config));
}

TEST_F(DefaultImageProcessingEngineTest, FlatteningTiming) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    FlatteningConfig config;
    config.enabled = true;

    engine_->ApplyFlattening(frame, config);
    StageTiming timing = engine_->GetLastTiming();

    EXPECT_GT(timing.flattening_us, 0);
}

// =============================================================================
// Full Pipeline Tests
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, ProcessFrameInPreviewModeSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    ProcessingConfig config;
    config.mode = ProcessingMode::PREVIEW;
    config.calibration_dark = &CreateDarkCalibration();
    config.calibration_gain = &CreateGainCalibration();
    config.window = 4000.0f;
    config.level = 2000.0f;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_GT(timing.offset_correction_us, 0);
    EXPECT_GT(timing.gain_correction_us, 0);
    EXPECT_GT(timing.window_level_us, 0);
    // Other stages should be zero in preview mode
}

TEST_F(DefaultImageProcessingEngineTest, ProcessFrameInFullPipelineSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    ProcessingConfig config;
    config.mode = ProcessingMode::FULL_PIPELINE;
    config.calibration_dark = &CreateDarkCalibration();
    config.calibration_gain = &CreateGainCalibration();
    config.defect_map = &CreateDefectMap();
    config.window = 4000.0f;
    config.level = 2000.0f;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_GT(timing.offset_correction_us, 0);
    EXPECT_GT(timing.gain_correction_us, 0);
    EXPECT_GT(timing.defect_pixel_map_us, 0);
    EXPECT_GT(timing.window_level_us, 0);
}

TEST_F(DefaultImageProcessingEngineTest, ProcessFrameWithNullCalibrationFails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    ProcessingConfig config;
    config.mode = ProcessingMode::FULL_PIPELINE;
    config.calibration_dark = nullptr;  // Missing required calibration
    config.calibration_gain = &CreateGainCalibration();
    config.defect_map = &CreateDefectMap();

    EXPECT_FALSE(engine_->ProcessFrame(frame, config));
}

TEST_F(DefaultImageProcessingEngineTest, ProcessFrameWithoutInitializeFails) {
    // Don't initialize
    ImageBuffer frame = CreateTestFrame();
    ProcessingConfig config;
    config.calibration_dark = &CreateDarkCalibration();
    config.calibration_gain = &CreateGainCalibration();
    config.defect_map = &CreateDefectMap();

    EXPECT_FALSE(engine_->ProcessFrame(frame, config));
}

// =============================================================================
// Error Handling Tests
// =============================================================================

TEST_F(DefaultImageProcessingEngineTest, GetLastErrorReturnsOkInitially) {
    EngineError error = engine_->GetLastError();
    EXPECT_FALSE(error.HasError());
}

TEST_F(DefaultImageProcessingEngineTest, GetLastErrorContainsStageInfo) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame = CreateTestFrame();
    frame.data = nullptr;  // Invalid frame

    CalibrationData dark = CreateDarkCalibration();
    engine_->ApplyOffsetCorrection(frame, dark);

    EngineError error = engine_->GetLastError();
    EXPECT_TRUE(error.HasError());
    EXPECT_FALSE(error.failed_stage.empty());
}

TEST_F(DefaultImageProcessingEngineTest, ProcessingInvalidFrameFails) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame;
    frame.width = 0;
    frame.height = 0;
    frame.data = nullptr;

    CalibrationData dark = CreateDarkCalibration();
    EXPECT_FALSE(engine_->ApplyOffsetCorrection(frame, dark));
}
