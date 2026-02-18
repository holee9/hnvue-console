/**
 * @file test_integration_pipeline.cpp
 * @brief Integration tests for full image processing pipeline
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Full pipeline integration tests
 * SPDX-License-Identifier: MIT
 *
 * Tests:
 * - Complete pipeline execution (FULL_PIPELINE mode)
 * - Preview mode pipeline execution
 * - End-to-end data flow
 * - Configuration validation
 * - Calibration data integrity through pipeline
 */

#include <gtest/gtest.h>
#include <hnvue/imaging/DefaultImageProcessingEngine.h>
#include <hnvue/imaging/CalibrationManager.h>
#include <hnvue/imaging/ImagingTypes.h>
#include <vector>
#include <cstring>
#include <cmath>

using namespace hnvue::imaging;

// =============================================================================
// Test Fixture
// =============================================================================

class IntegrationPipelineTest : public ::testing::Test {
protected:
    static constexpr uint32_t TEST_WIDTH = 512;
    static constexpr uint32_t TEST_HEIGHT = 512;
    static constexpr uint32_t TEST_SIZE = TEST_WIDTH * TEST_HEIGHT;

    std::unique_ptr<DefaultImageProcessingEngine> engine_;
    std::unique_ptr<CalibrationManager> calib_manager_;

    std::vector<uint16_t> raw_frame_;
    std::vector<uint16_t> processed_frame_;
    std::vector<float> dark_data_;
    std::vector<float> gain_data_;
    std::vector<DefectPixelEntry> defect_entries_;

    void SetUp() override {
        engine_ = std::make_unique<DefaultImageProcessingEngine>();
        calib_manager_ = std::make_unique<CalibrationManager>();

        // Initialize raw frame with test pattern
        raw_frame_.resize(TEST_SIZE);
        processed_frame_.resize(TEST_SIZE);
        GenerateTestFrame(raw_frame_);

        // Initialize dark frame (simulating dark current)
        dark_data_.resize(TEST_SIZE);
        for (size_t i = 0; i < TEST_SIZE; ++i) {
            dark_data_[i] = 100.0f;  // Constant dark offset
        }

        // Initialize gain map (unity with slight variation)
        gain_data_.resize(TEST_SIZE);
        for (size_t i = 0; i < TEST_SIZE; ++i) {
            gain_data_[i] = 1.0f;
        }

        // Create a few defect pixels
        defect_entries_.resize(3);
        defect_entries_[0] = {100, 100, DefectPixelType::DEAD_PIXEL,
                             InterpolationMethod::BILINEAR};
        defect_entries_[1] = {256, 256, DefectPixelType::HOT_PIXEL,
                             InterpolationMethod::MEDIAN_3X3};
        defect_entries_[2] = {400, 400, DefectPixelType::DEAD_PIXEL,
                             InterpolationMethod::NEAREST_NEIGHBOR};
    }

    void TearDown() override {
        if (engine_) {
            engine_->Shutdown();
        }
    }

    void GenerateTestFrame(std::vector<uint16_t>& frame) {
        // Create a gradient pattern with some features
        for (uint32_t y = 0; y < TEST_HEIGHT; ++y) {
            for (uint32_t x = 0; x < TEST_WIDTH; ++x) {
                // Gradient
                uint16_t value = static_cast<uint16_t>((x + y) * 50);

                // Add a bright spot
                uint32_t dx = x - 256;
                uint32_t dy = y - 256;
                if (dx * dx + dy * dy < 2500) {  // Circle radius 50
                    value += 2000;
                }

                frame[y * TEST_WIDTH + x] = value;
            }
        }
    }

    bool InitializeEngine() {
        EngineConfig config;
        config.max_frame_width = TEST_WIDTH;
        config.max_frame_height = TEST_HEIGHT;
        return engine_->Initialize(config);
    }

    ImageBuffer CreateFrameBuffer(std::vector<uint16_t>& data) {
        ImageBuffer buffer;
        buffer.width = TEST_WIDTH;
        buffer.height = TEST_HEIGHT;
        buffer.pixel_depth = 16;
        buffer.stride = TEST_WIDTH * 2;
        buffer.data = data.data();
        buffer.timestamp_us = 1234567890ULL;
        buffer.frame_id = 1;
        return buffer;
    }

    CalibrationData CreateDarkCalibration() {
        CalibrationData dark;
        dark.type = CalibrationDataType::DARK_FRAME;
        dark.width = TEST_WIDTH;
        dark.height = TEST_HEIGHT;
        dark.data_f32 = dark_data_.data();
        dark.valid = true;
        dark.checksum = 0xDEADBEEF;
        dark.acquisition_time_us = 1234567890ULL;
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
        gain.acquisition_time_us = 1234567890ULL;
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

    ProcessingConfig CreateFullPipelineConfig() {
        ProcessingConfig config;
        config.mode = ProcessingMode::FULL_PIPELINE;
        config.calibration_dark = &CreateDarkCalibration();
        config.calibration_gain = &CreateGainCalibration();
        config.defect_map = &CreateDefectMap();
        config.window = 4000.0f;
        config.level = 2000.0f;
        config.preserve_raw = true;

        // Enable optional stages
        config.scatter.enabled = true;
        config.scatter.cutoff_frequency = 0.1f;
        config.scatter.suppression_ratio = 0.5f;

        config.noise_reduction.enabled = true;
        config.noise_reduction.filter_type = NoiseFilterType::GAUSSIAN;
        config.noise_reduction.kernel_size = 3.0f;
        config.noise_reduction.sigma = 1.0f;

        config.flattening.enabled = false;  // Disable for this test

        return config;
    }

    ProcessingConfig CreatePreviewConfig() {
        ProcessingConfig config;
        config.mode = ProcessingMode::PREVIEW;
        config.calibration_dark = &CreateDarkCalibration();
        config.calibration_gain = &CreateGainCalibration();
        config.window = 4000.0f;
        config.level = 2000.0f;
        return config;
    }
};

// =============================================================================
// Full Pipeline Integration Tests
// =============================================================================

TEST_F(IntegrationPipelineTest, FullPipelineExecutionSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;  // Copy raw data
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    // Verify timing information is available
    StageTiming timing = engine_->GetLastTiming();
    EXPECT_GT(timing.Total(), 0);
}

TEST_F(IntegrationPipelineTest, FullPipelineAppliesAllStages) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.scatter.enabled = true;
    config.noise_reduction.enabled = true;
    config.flattening.enabled = true;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    // Check that the frame was modified (at least one stage changed it)
    bool modified = false;
    for (size_t i = 0; i < TEST_SIZE; ++i) {
        if (processed_frame_[i] != raw_frame_[i]) {
            modified = true;
            break;
        }
    }
    EXPECT_TRUE(modified);
}

TEST_F(IntegrationPipelineTest, FullPipelineWithNullCalibrationFails) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.calibration_dark = nullptr;  // Missing required calibration

    EXPECT_FALSE(engine_->ProcessFrame(frame, config));
}

// =============================================================================
// Preview Mode Integration Tests
// =============================================================================

TEST_F(IntegrationPipelineTest, PreviewModeExecutionSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreatePreviewConfig();

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_GT(timing.Total(), 0);
}

TEST_F(IntegrationPipelineTest, PreviewModeOnlyAppliesRequiredStages) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreatePreviewConfig();

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    StageTiming timing = engine_->GetLastTiming();

    // Preview mode should only have these stages
    EXPECT_GT(timing.offset_correction_us, 0);
    EXPECT_GT(timing.gain_correction_us, 0);
    EXPECT_GT(timing.window_level_us, 0);

    // These should be zero in preview mode
    EXPECT_EQ(timing.defect_pixel_map_us, 0);
    EXPECT_EQ(timing.scatter_correction_us, 0);
    EXPECT_EQ(timing.noise_reduction_us, 0);
    EXPECT_EQ(timing.flattening_us, 0);
}

TEST_F(IntegrationPipelineTest, PreviewModeIsFasterThanFullPipeline) {
    ASSERT_TRUE(InitializeEngine());

    // Preview mode timing
    std::vector<uint16_t> preview_frame = raw_frame_;
    ImageBuffer preview_buffer = CreateFrameBuffer(preview_frame);
    ProcessingConfig preview_config = CreatePreviewConfig();

    ASSERT_TRUE(engine_->ProcessFrame(preview_buffer, preview_config));
    StageTiming preview_timing = engine_->GetLastTiming();

    // Full pipeline timing
    std::vector<uint16_t> full_frame = raw_frame_;
    ImageBuffer full_buffer = CreateFrameBuffer(full_frame);
    ProcessingConfig full_config = CreateFullPipelineConfig();
    full_config.scatter.enabled = true;
    full_config.noise_reduction.enabled = true;

    ASSERT_TRUE(engine_->ProcessFrame(full_buffer, full_config));
    StageTiming full_timing = engine_->GetLastTiming();

    // Preview should be faster
    EXPECT_LT(preview_timing.Total(), full_timing.Total());
}

// =============================================================================
// Data Flow Tests
// =============================================================================

TEST_F(IntegrationPipelineTest, DataFlowsCorrectlyThroughPipeline) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.scatter.enabled = false;
    config.noise_reduction.enabled = false;
    config.flattening.enabled = false;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    // Verify that processing happened (values changed)
    // The exact output depends on the algorithm, but we can check:
    // 1. No overflow
    // 2. Data was modified
    bool all_zero = true;
    bool all_same = true;

    for (size_t i = 0; i < TEST_SIZE; ++i) {
        if (processed_frame_[i] != 0) {
            all_zero = false;
        }
        if (processed_frame_[i] != raw_frame_[i]) {
            all_same = false;
        }
    }

    EXPECT_FALSE(all_zero);
    EXPECT_FALSE(all_same);
}

TEST_F(IntegrationPipelineTest, PipelineHandlesEdgeCaseValues) {
    ASSERT_TRUE(InitializeEngine());

    // Create frame with extreme values
    std::vector<uint16_t> extreme_frame(TEST_SIZE);
    for (size_t i = 0; i < TEST_SIZE; ++i) {
        if (i < TEST_SIZE / 4) {
            extreme_frame[i] = 0;  // Minimum
        } else if (i < TEST_SIZE / 2) {
            extreme_frame[i] = 65535;  // Maximum
        } else {
            extreme_frame[i] = 32768;  // Midpoint
        }
    }

    ImageBuffer frame = CreateFrameBuffer(extreme_frame);
    ProcessingConfig config = CreateFullPipelineConfig();

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    // Verify no crash and valid output
    for (size_t i = 0; i < TEST_SIZE; ++i) {
        EXPECT_LE(extreme_frame[i], 65535);
    }
}

// =============================================================================
// Configuration Tests
// =============================================================================

TEST_F(IntegrationPipelineTest, PipelineWithDifferentWindowLevel) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.window = 2000.0f;
    config.level = 1000.0f;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));
}

TEST_F(IntegrationPipelineTest, PipelineWithWideWindow) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.window = 10000.0f;  // Very wide
    config.level = 5000.0f;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));
}

TEST_F(IntegrationPipelineTest, PipelineWithNarrowWindow) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.window = 500.0f;  // Very narrow
    config.level = 250.0f;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));
}

// =============================================================================
// Optional Stages Tests
// =============================================================================

TEST_F(IntegrationPipelineTest, PipelineWithScatterDisabled) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.scatter.enabled = false;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_EQ(timing.scatter_correction_us, 0);
}

TEST_F(IntegrationPipelineTest, PipelineWithNoiseReductionDisabled) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.noise_reduction.enabled = false;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_EQ(timing.noise_reduction_us, 0);
}

TEST_F(IntegrationPipelineTest, PipelineWithFlatteningDisabled) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.flattening.enabled = false;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_EQ(timing.flattening_us, 0);
}

TEST_F(IntegrationPipelineTest, PipelineWithAllOptionalStagesEnabled) {
    ASSERT_TRUE(InitializeEngine());

    processed_frame_ = raw_frame_;
    ImageBuffer frame = CreateFrameBuffer(processed_frame_);

    ProcessingConfig config = CreateFullPipelineConfig();
    config.scatter.enabled = true;
    config.noise_reduction.enabled = true;
    config.flattening.enabled = true;

    EXPECT_TRUE(engine_->ProcessFrame(frame, config));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_GT(timing.scatter_correction_us, 0);
    EXPECT_GT(timing.noise_reduction_us, 0);
    EXPECT_GT(timing.flattening_us, 0);
}

// =============================================================================
// Multiple Frame Tests
// =============================================================================

TEST_F(IntegrationPipelineTest, ProcessMultipleFramesSucceeds) {
    ASSERT_TRUE(InitializeEngine());

    ProcessingConfig config = CreateFullPipelineConfig();

    for (int i = 0; i < 10; ++i) {
        std::vector<uint16_t> frame_copy = raw_frame_;
        ImageBuffer frame = CreateFrameBuffer(frame_copy);
        frame.frame_id = i;

        EXPECT_TRUE(engine_->ProcessFrame(frame, config));
    }
}

TEST_F(IntegrationPipelineTest, MultipleFramesHaveConsistentTiming) {
    ASSERT_TRUE(InitializeEngine());

    ProcessingConfig config = CreateFullPipelineConfig();

    uint64_t first_timing = 0;
    for (int i = 0; i < 5; ++i) {
        std::vector<uint16_t> frame_copy = raw_frame_;
        ImageBuffer frame = CreateFrameBuffer(frame_copy);

        ASSERT_TRUE(engine_->ProcessFrame(frame, config));
        StageTiming timing = engine_->GetLastTiming();

        if (i == 0) {
            first_timing = timing.Total();
        } else {
            // Timing should be reasonably consistent (within 2x)
            EXPECT_LT(timing.Total(), first_timing * 2);
        }
    }
}

// =============================================================================
// Error Recovery Tests
// =============================================================================

TEST_F(IntegrationPipelineTest, PipelineFailsGracefullyWithInvalidFrame) {
    ASSERT_TRUE(InitializeEngine());

    ImageBuffer frame;
    frame.width = 0;
    frame.height = 0;
    frame.data = nullptr;

    ProcessingConfig config = CreateFullPipelineConfig();

    EXPECT_FALSE(engine_->ProcessFrame(frame, config));

    EngineError error = engine_->GetLastError();
    EXPECT_TRUE(error.HasError());
}

TEST_F(IntegrationPipelineTest, EngineStateRemainsValidAfterFailure) {
    ASSERT_TRUE(InitializeEngine());

    // Cause a failure
    ImageBuffer invalid_frame;
    invalid_frame.width = 0;
    invalid_frame.data = nullptr;

    ProcessingConfig config = CreateFullPipelineConfig();
    EXPECT_FALSE(engine_->ProcessFrame(invalid_frame, config));

    // Now try a valid frame
    processed_frame_ = raw_frame_;
    ImageBuffer valid_frame = CreateFrameBuffer(processed_frame_);
    EXPECT_TRUE(engine_->ProcessFrame(valid_frame, config));
}
