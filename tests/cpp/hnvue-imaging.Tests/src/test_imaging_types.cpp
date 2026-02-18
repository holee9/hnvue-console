/**
 * @file test_imaging_types.cpp
 * @brief Unit tests for imaging data structures (ImagingTypes.h)
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Data structure validation tests
 * SPDX-License-Identifier: MIT
 *
 * Tests:
 * - ImageBuffer initialization and validation
 * - CalibrationData structure integrity
 * - DefectMap structure integrity
 * - Enum value ranges and conversions
 * - Configuration structure defaults
 */

#include <gtest/gtest.h>
#include <hnvue/imaging/ImagingTypes.h>
#include <cstring>
#include <limits>

using namespace hnvue::imaging;

// =============================================================================
// Test Fixtures
// =============================================================================

/**
 * @brief Fixture for ImageBuffer tests
 */
class ImageBufferTest : public ::testing::Test {
protected:
    static constexpr uint32_t TEST_WIDTH = 512;
    static constexpr uint32_t TEST_HEIGHT = 512;
    static constexpr uint32_t TEST_SIZE = TEST_WIDTH * TEST_HEIGHT;

    std::vector<uint16_t> test_data_;

    void SetUp() override {
        test_data_.resize(TEST_SIZE);
        for (size_t i = 0; i < TEST_SIZE; ++i) {
            test_data_[i] = static_cast<uint16_t>(i & 0xFFFF);
        }
    }

    ImageBuffer CreateValidBuffer() {
        ImageBuffer buffer;
        buffer.width = TEST_WIDTH;
        buffer.height = TEST_HEIGHT;
        buffer.pixel_depth = 16;
        buffer.stride = TEST_WIDTH * 2;
        buffer.data = test_data_.data();
        buffer.timestamp_us = 1234567890ULL;
        buffer.frame_id = 42;
        return buffer;
    }
};

/**
 * @brief Fixture for CalibrationData tests
 */
class CalibrationDataTest : public ::testing::Test {
protected:
    static constexpr uint32_t TEST_WIDTH = 512;
    static constexpr uint32_t TEST_HEIGHT = 512;
    static constexpr uint32_t TEST_SIZE = TEST_WIDTH * TEST_HEIGHT;

    std::vector<float> test_data_;

    void SetUp() override {
        test_data_.resize(TEST_SIZE);
        for (size_t i = 0; i < TEST_SIZE; ++i) {
            test_data_[i] = 1.0f + (i % 1000) / 1000.0f;  // Gain-like values
        }
    }

    CalibrationData CreateValidCalibration(CalibrationDataType type) {
        CalibrationData calib;
        calib.type = type;
        calib.width = TEST_WIDTH;
        calib.height = TEST_HEIGHT;
        calib.data_f32 = test_data_.data();
        calib.checksum = 0xDEADBEEF;
        calib.acquisition_time_us = 1234567890ULL;
        calib.valid = true;
        return calib;
    }
};

/**
 * @brief Fixture for DefectMap tests
 */
class DefectMapTest : public ::testing::Test {
protected:
    std::vector<DefectPixelEntry> test_entries_;

    void SetUp() override {
        // Create some test defect entries
        test_entries_.resize(5);
        test_entries_[0] = {10, 20, DefectPixelType::DEAD_PIXEL,
                           InterpolationMethod::BILINEAR};
        test_entries_[1] = {100, 200, DefectPixelType::HOT_PIXEL,
                           InterpolationMethod::MEDIAN_3X3};
        test_entries_[2] = {511, 511, DefectPixelType::CLUSTER,
                           InterpolationMethod::NEAREST_NEIGHBOR};
        test_entries_[3] = {0, 0, DefectPixelType::DEAD_PIXEL,
                           InterpolationMethod::BILINEAR};
        test_entries_[4] = {256, 128, DefectPixelType::HOT_PIXEL,
                           InterpolationMethod::MEDIAN_3X3};
    }

    DefectMap CreateValidDefectMap() {
        DefectMap map;
        map.count = static_cast<uint32_t>(test_entries_.size());
        map.pixels = test_entries_.data();
        map.checksum = 0xBADDCAFE;
        map.valid = true;
        return map;
    }
};

// =============================================================================
// ImageBuffer Tests
// =============================================================================

TEST_F(ImageBufferTest, DefaultInitialization) {
    ImageBuffer buffer;

    EXPECT_EQ(buffer.width, 0);
    EXPECT_EQ(buffer.height, 0);
    EXPECT_EQ(buffer.pixel_depth, 16);
    EXPECT_EQ(buffer.stride, 0);
    EXPECT_EQ(buffer.data, nullptr);
    EXPECT_EQ(buffer.timestamp_us, 0);
    EXPECT_EQ(buffer.frame_id, 0);
}

TEST_F(ImageBufferTest, ValidBufferCreation) {
    ImageBuffer buffer = CreateValidBuffer();

    EXPECT_EQ(buffer.width, TEST_WIDTH);
    EXPECT_EQ(buffer.height, TEST_HEIGHT);
    EXPECT_EQ(buffer.pixel_depth, 16);
    EXPECT_EQ(buffer.stride, TEST_WIDTH * 2);
    EXPECT_NE(buffer.data, nullptr);
    EXPECT_EQ(buffer.timestamp_us, 1234567890ULL);
    EXPECT_EQ(buffer.frame_id, 42);
}

TEST_F(ImageBufferTest, StrideCalculation) {
    ImageBuffer buffer = CreateValidBuffer();

    // Stride must be at least width * bytes_per_pixel
    EXPECT_GE(buffer.stride, buffer.width * 2);

    // For row-major 16-bit data
    EXPECT_EQ(buffer.stride % 2, 0);
}

TEST_F(ImageBufferTest, PixelDepthFixed) {
    ImageBuffer buffer;
    EXPECT_EQ(buffer.pixel_depth, 16);  // Fixed at 16-bit
}

// =============================================================================
// CalibrationData Tests
// =============================================================================

TEST_F(CalibrationDataTest, DefaultInitialization) {
    CalibrationData calib;

    EXPECT_EQ(calib.type, CalibrationDataType::CALIB_TYPE_UNSPECIFIED);
    EXPECT_EQ(calib.width, 0);
    EXPECT_EQ(calib.height, 0);
    EXPECT_EQ(calib.data_f32, nullptr);
    EXPECT_EQ(calib.checksum, 0);
    EXPECT_EQ(calib.acquisition_time_us, 0);
    EXPECT_FALSE(calib.valid);
}

TEST_F(CalibrationDataTest, DarkFrameCreation) {
    CalibrationData calib = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_EQ(calib.type, CalibrationDataType::DARK_FRAME);
    EXPECT_EQ(calib.width, TEST_WIDTH);
    EXPECT_EQ(calib.height, TEST_HEIGHT);
    EXPECT_NE(calib.data_f32, nullptr);
    EXPECT_TRUE(calib.valid);
}

TEST_F(CalibrationDataTest, GainMapCreation) {
    CalibrationData calib = CreateValidCalibration(CalibrationDataType::GAIN_MAP);

    EXPECT_EQ(calib.type, CalibrationDataType::GAIN_MAP);
    EXPECT_EQ(calib.width, TEST_WIDTH);
    EXPECT_EQ(calib.height, TEST_HEIGHT);
    EXPECT_NE(calib.data_f32, nullptr);
    EXPECT_TRUE(calib.valid);
}

TEST_F(CalibrationDataTest, ChecksumStorage) {
    CalibrationData calib = CreateValidCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_NE(calib.checksum, 0);
    EXPECT_EQ(calib.checksum, 0xDEADBEEF);
}

// =============================================================================
// DefectMap Tests
// =============================================================================

TEST_F(DefectMapTest, DefaultInitialization) {
    DefectMap map;

    EXPECT_EQ(map.count, 0);
    EXPECT_EQ(map.pixels, nullptr);
    EXPECT_EQ(map.checksum, 0);
    EXPECT_FALSE(map.valid);
}

TEST_F(DefectMapTest, ValidDefectMapCreation) {
    DefectMap map = CreateValidDefectMap();

    EXPECT_EQ(map.count, 5);
    EXPECT_NE(map.pixels, nullptr);
    EXPECT_NE(map.checksum, 0);
    EXPECT_TRUE(map.valid);
}

TEST_F(DefectMapTest, DefectPixelEntryFields) {
    DefectPixelEntry entry = test_entries_[0];

    EXPECT_EQ(entry.x, 10);
    EXPECT_EQ(entry.y, 20);
    EXPECT_EQ(entry.type, DefectPixelType::DEAD_PIXEL);
    EXPECT_EQ(entry.interpolation, InterpolationMethod::BILINEAR);
}

// =============================================================================
// Enum Tests
// =============================================================================

TEST(ImagingErrorTest, ErrorCodesAreNegativeOrZero) {
    EXPECT_LE(static_cast<int>(ImagingError::IMAGING_OK), 0);
    EXPECT_GT(static_cast<int>(ImagingError::IMAGING_ERR_INIT), 0);
    EXPECT_GT(static_cast<int>(ImagingError::IMAGING_ERR_PARAM), 0);
}

TEST(CalibrationDataTypeTest, DarkFrameValue) {
    EXPECT_EQ(static_cast<int>(CalibrationDataType::DARK_FRAME), 1);
}

TEST(CalibrationDataTypeTest, GainMapValue) {
    EXPECT_EQ(static_cast<int>(CalibrationDataType::GAIN_MAP), 2);
}

TEST(DefectPixelTypeTest, DeadPixelValue) {
    EXPECT_EQ(static_cast<int>(DefectPixelType::DEAD_PIXEL), 1);
}

TEST(DefectPixelTypeTest, HotPixelValue) {
    EXPECT_EQ(static_cast<int>(DefectPixelType::HOT_PIXEL), 2);
}

TEST(InterpolationMethodTest, NearestNeighborValue) {
    EXPECT_EQ(static_cast<int>(InterpolationMethod::NEAREST_NEIGHBOR), 1);
}

TEST(ScatterAlgorithmTest, FrequencyDomainFFTValue) {
    EXPECT_EQ(static_cast<int>(ScatterAlgorithm::FREQUENCY_DOMAIN_FFT), 1);
}

TEST(NoiseFilterTypeTest, GaussianValue) {
    EXPECT_EQ(static_cast<int>(NoiseFilterType::GAUSSIAN), 1);
}

TEST(ProcessingModeTest, FullPipelineValue) {
    EXPECT_EQ(static_cast<int>(ProcessingMode::FULL_PIPELINE), 1);
}

TEST(ProcessingModeTest, PreviewValue) {
    EXPECT_EQ(static_cast<int>(ProcessingMode::PREVIEW), 2);
}

// =============================================================================
// Configuration Structure Tests
// =============================================================================

TEST(ScatterParamsTest, DefaultInitialization) {
    ScatterParams params;

    EXPECT_FALSE(params.enabled);
    EXPECT_EQ(params.algorithm, ScatterAlgorithm::SCATTER_ALGO_UNSPECIFIED);
    EXPECT_FLOAT_EQ(params.cutoff_frequency, 0.1f);
    EXPECT_FLOAT_EQ(params.suppression_ratio, 0.5f);
    EXPECT_EQ(params.fftw_plan_flags, 0);
}

TEST(NoiseReductionConfigTest, DefaultInitialization) {
    NoiseReductionConfig config;

    EXPECT_FALSE(config.enabled);
    EXPECT_EQ(config.filter_type, NoiseFilterType::NOISE_FILTER_UNSPECIFIED);
    EXPECT_FLOAT_EQ(config.kernel_size, 3.0f);
    EXPECT_FLOAT_EQ(config.sigma, 1.0f);
}

TEST(FlatteningConfigTest, DefaultInitialization) {
    FlatteningConfig config;

    EXPECT_FALSE(config.enabled);
    EXPECT_FLOAT_EQ(config.polynomial_order, 2.0f);
    EXPECT_FLOAT_EQ(config.sigma_background, 50.0f);
}

TEST(ProcessingConfigTest, DefaultInitialization) {
    ProcessingConfig config;

    EXPECT_EQ(config.calibration_dark, nullptr);
    EXPECT_EQ(config.calibration_gain, nullptr);
    EXPECT_EQ(config.defect_map, nullptr);
    EXPECT_FALSE(config.scatter.enabled);
    EXPECT_FLOAT_EQ(config.window, 4000.0f);
    EXPECT_FLOAT_EQ(config.level, 2000.0f);
    EXPECT_FALSE(config.noise_reduction.enabled);
    EXPECT_FALSE(config.flattening.enabled);
    EXPECT_EQ(config.mode, ProcessingMode::FULL_PIPELINE);
    EXPECT_TRUE(config.preserve_raw);  // FR-IMG-11: Must be true by default
}

// =============================================================================
// EngineInfo Tests
// =============================================================================

TEST(EngineInfoTest, DefaultInitialization) {
    EngineInfo info;

    EXPECT_TRUE(info.engine_name.empty());
    EXPECT_TRUE(info.engine_version.empty());
    EXPECT_TRUE(info.vendor.empty());
    EXPECT_EQ(info.capabilities, 0);
    EXPECT_EQ(info.api_version, 0);
}

TEST(EngineInfoTest, HasCapabilityMethod) {
    EngineInfo info;
    info.capabilities = static_cast<uint64_t>(EngineCapabilityFlags::CAP_OFFSET_CORRECTION) |
                       static_cast<uint64_t>(EngineCapabilityFlags::CAP_GAIN_CORRECTION);

    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_OFFSET_CORRECTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_GAIN_CORRECTION));
    EXPECT_FALSE(info.HasCapability(EngineCapabilityFlags::CAP_DEFECT_PIXEL_MAP));
}

TEST(EngineInfoTest, NoCapabilityWhenZero) {
    EngineInfo info;
    EXPECT_FALSE(info.HasCapability(EngineCapabilityFlags::CAP_OFFSET_CORRECTION));
}

// =============================================================================
// EngineError Tests
// =============================================================================

TEST(EngineErrorTest, DefaultInitialization) {
    EngineError error;

    EXPECT_EQ(error.error_code, ImagingError::IMAGING_OK);
    EXPECT_TRUE(error.error_message.empty());
    EXPECT_TRUE(error.failed_stage.empty());
}

TEST(EngineErrorTest, HasErrorMethod) {
    EngineError error;

    EXPECT_FALSE(error.HasError());

    error.error_code = ImagingError::IMAGING_ERR_PARAM;
    EXPECT_TRUE(error.HasError());
}

// =============================================================================
// ProcessedFrameResult Tests
// =============================================================================

TEST(ProcessedFrameResultTest, DefaultInitialization) {
    ProcessedFrameResult result;

    EXPECT_EQ(result.processed_frame.width, 0);
    EXPECT_EQ(result.raw_frame.width, 0);
    EXPECT_EQ(result.processing_time_us, 0);
    EXPECT_EQ(result.stages_applied, 0);
}

TEST(ProcessedFrameResultTest, HasStageMethod) {
    ProcessedFrameResult result;
    result.stages_applied = static_cast<uint64_t>(PipelineStageFlags::STAGE_OFFSET_CORRECTION) |
                           static_cast<uint64_t>(PipelineStageFlags::STAGE_GAIN_CORRECTION);

    EXPECT_TRUE(result.HasStage(PipelineStageFlags::STAGE_OFFSET_CORRECTION));
    EXPECT_TRUE(result.HasStage(PipelineStageFlags::STAGE_GAIN_CORRECTION));
    EXPECT_FALSE(result.HasStage(PipelineStageFlags::STAGE_DEFECT_PIXEL_MAP));
}

// =============================================================================
// StageTiming Tests
// =============================================================================

TEST(StageTimingTest, DefaultInitialization) {
    StageTiming timing;

    EXPECT_EQ(timing.offset_correction_us, 0);
    EXPECT_EQ(timing.gain_correction_us, 0);
    EXPECT_EQ(timing.defect_pixel_map_us, 0);
    EXPECT_EQ(timing.scatter_correction_us, 0);
    EXPECT_EQ(timing.noise_reduction_us, 0);
    EXPECT_EQ(timing.flattening_us, 0);
    EXPECT_EQ(timing.window_level_us, 0);
}

TEST(StageTimingTest, TotalMethod) {
    StageTiming timing;
    timing.offset_correction_us = 100;
    timing.gain_correction_us = 200;
    timing.window_level_us = 50;

    EXPECT_EQ(timing.Total(), 350);
}

TEST(StageTimingTest, TotalWithZeroValues) {
    StageTiming timing;
    EXPECT_EQ(timing.Total(), 0);
}

// =============================================================================
// EngineConfig Tests
// =============================================================================

TEST(EngineConfigTest, DefaultInitialization) {
    EngineConfig config;

    EXPECT_EQ(config.max_frame_width, 0);
    EXPECT_EQ(config.max_frame_height, 0);
    EXPECT_TRUE(config.calibration_path.empty());
    EXPECT_FALSE(config.enable_gpu);
    EXPECT_EQ(config.num_threads, 0);
}
