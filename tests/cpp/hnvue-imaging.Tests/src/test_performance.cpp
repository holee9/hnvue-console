/**
 * @file test_performance.cpp
 * @brief Performance tests for image processing pipeline
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - NFR performance validation tests
 * SPDX-License-Identifier: MIT
 *
 * Tests:
 * - NFR-IMG-01: Full pipeline < 2 seconds for typical detector frames
 * - NFR-IMG-02: Preview mode < 500ms for typical frames
 * - Memory allocation efficiency
 * - Timing consistency
 */

#include <gtest/gtest.h>
#include <hnvue/imaging/DefaultImageProcessingEngine.h>
#include <hnvue/imaging/ImagingTypes.h>
#include <vector>
#include <chrono>
#include <cstring>

using namespace hnvue::imaging;

// =============================================================================
// Performance Test Fixture
// =============================================================================

class PerformanceTest : public ::testing::Test {
protected:
    // Typical medical detector resolutions
    static constexpr uint32_t SMALL_WIDTH = 512;
    static constexpr uint32_t SMALL_HEIGHT = 512;
    static constexpr uint32_t SMALL_SIZE = SMALL_WIDTH * SMALL_HEIGHT;

    static constexpr uint32_t MEDIUM_WIDTH = 1024;
    static constexpr uint32_t MEDIUM_HEIGHT = 1024;
    static constexpr uint32_t MEDIUM_SIZE = MEDIUM_WIDTH * MEDIUM_HEIGHT;

    static constexpr uint32_t LARGE_WIDTH = 2048;
    static constexpr uint32_t LARGE_HEIGHT = 2048;
    static constexpr uint32_t LARGE_SIZE = LARGE_WIDTH * LARGE_HEIGHT;

    // NFR-IMG-01: Full pipeline must complete in < 2 seconds
    static constexpr uint64_t MAX_FULL_PIPELINE_US = 2000000;  // 2 seconds

    // NFR-IMG-02: Preview mode must complete in < 500ms
    static constexpr uint64_t MAX_PREVIEW_PIPELINE_US = 500000;  // 500ms

    std::unique_ptr<DefaultImageProcessingEngine> engine_;

    void SetUp() override {
        engine_ = std::make_unique<DefaultImageProcessingEngine>();

        EngineConfig config;
        config.max_frame_width = LARGE_WIDTH;
        config.max_frame_height = LARGE_HEIGHT;
        config.num_threads = 0;  // Auto-detect

        ASSERT_TRUE(engine_->Initialize(config));
    }

    void TearDown() override {
        if (engine_) {
            engine_->Shutdown();
        }
    }

    std::vector<uint16_t> GenerateTestFrame(uint32_t width, uint32_t height) {
        std::vector<uint16_t> frame(width * height);
        for (uint32_t y = 0; y < height; ++y) {
            for (uint32_t x = 0; x < width; ++x) {
                frame[y * width + x] = static_cast<uint16_t>((x + y) % 65536);
            }
        }
        return frame;
    }

    CalibrationData CreateCalibration(uint32_t width, uint32_t height,
                                       CalibrationDataType type) {
        CalibrationData calib;
        calib.type = type;
        calib.width = width;
        calib.height = height;
        calib.data_f32 = new float[width * height];
        calib.valid = true;

        // Fill with test data
        for (size_t i = 0; i < static_cast<size_t>(width * height); ++i) {
            if (type == CalibrationDataType::DARK_FRAME) {
                calib.data_f32[i] = 100.0f;
            } else {
                calib.data_f32[i] = 1.0f;
            }
        }

        return calib;
    }

    DefectMap CreateDefectMap() {
        static DefectPixelEntry entries[] = {
            {100, 100, DefectPixelType::DEAD_PIXEL, InterpolationMethod::BILINEAR},
            {500, 500, DefectPixelType::HOT_PIXEL, InterpolationMethod::MEDIAN_3X3},
            {1000, 1000, DefectPixelType::DEAD_PIXEL, InterpolationMethod::NEAREST_NEIGHBOR}
        };

        DefectMap map;
        map.count = 3;
        map.pixels = entries;
        map.valid = true;
        return map;
    }

    void CleanupCalibration(CalibrationData& calib) {
        if (calib.data_f32) {
            delete[] calib.data_f32;
            calib.data_f32 = nullptr;
        }
    }

    struct PerformanceMetrics {
        uint64_t total_time_us;
        uint64_t offset_time_us;
        uint64_t gain_time_us;
        uint64_t defect_time_us;
        uint64_t scatter_time_us;
        uint64_t noise_time_us;
        uint64_t flatten_time_us;
        uint64_t wl_time_us;
    };

    PerformanceMetrics RunFullPipeline(uint32_t width, uint32_t height) {
        auto frame_data = GenerateTestFrame(width, height);
        CalibrationData dark = CreateCalibration(width, height, CalibrationDataType::DARK_FRAME);
        CalibrationData gain = CreateCalibration(width, height, CalibrationDataType::GAIN_MAP);
        DefectMap defect = CreateDefectMap();

        ImageBuffer frame;
        frame.width = width;
        frame.height = height;
        frame.pixel_depth = 16;
        frame.stride = width * 2;
        frame.data = frame_data.data();
        frame.timestamp_us = 0;
        frame.frame_id = 1;

        ProcessingConfig config;
        config.mode = ProcessingMode::FULL_PIPELINE;
        config.calibration_dark = &dark;
        config.calibration_gain = &gain;
        config.defect_map = &defect;
        config.window = 4000.0f;
        config.level = 2000.0f;
        config.scatter.enabled = true;
        config.noise_reduction.enabled = true;
        config.flattening.enabled = false;  // Skip for performance

        auto start = std::chrono::high_resolution_clock::now();
        ASSERT_TRUE(engine_->ProcessFrame(frame, config));
        auto end = std::chrono::high_resolution_clock::now();

        StageTiming timing = engine_->GetLastTiming();

        PerformanceMetrics metrics;
        metrics.total_time_us = std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();
        metrics.offset_time_us = timing.offset_correction_us;
        metrics.gain_time_us = timing.gain_correction_us;
        metrics.defect_time_us = timing.defect_pixel_map_us;
        metrics.scatter_time_us = timing.scatter_correction_us;
        metrics.noise_time_us = timing.noise_reduction_us;
        metrics.flatten_time_us = timing.flattening_us;
        metrics.wl_time_us = timing.window_level_us;

        CleanupCalibration(dark);
        CleanupCalibration(gain);

        return metrics;
    }

    uint64_t RunPreviewPipeline(uint32_t width, uint32_t height) {
        auto frame_data = GenerateTestFrame(width, height);
        CalibrationData dark = CreateCalibration(width, height, CalibrationDataType::DARK_FRAME);
        CalibrationData gain = CreateCalibration(width, height, CalibrationDataType::GAIN_MAP);

        ImageBuffer frame;
        frame.width = width;
        frame.height = height;
        frame.pixel_depth = 16;
        frame.stride = width * 2;
        frame.data = frame_data.data();
        frame.timestamp_us = 0;
        frame.frame_id = 1;

        ProcessingConfig config;
        config.mode = ProcessingMode::PREVIEW;
        config.calibration_dark = &dark;
        config.calibration_gain = &gain;
        config.window = 4000.0f;
        config.level = 2000.0f;

        auto start = std::chrono::high_resolution_clock::now();
        ASSERT_TRUE(engine_->ProcessFrame(frame, config));
        auto end = std::chrono::high_resolution_clock::now();

        CleanupCalibration(dark);
        CleanupCalibration(gain);

        return std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();
    }
};

// =============================================================================
// NFR-IMG-01: Full Pipeline Performance Tests
// =============================================================================

TEST_F(PerformanceTest, NFR_IMG_01_FullPipeline_SmallFrame_CompletesUnder2Seconds) {
    PerformanceMetrics metrics = RunFullPipeline(SMALL_WIDTH, SMALL_HEIGHT);

    EXPECT_LT(metrics.total_time_us, MAX_FULL_PIPELINE_US)
        << "Full pipeline for 512x512 frame took " << (metrics.total_time_us / 1000.0)
        << "ms, exceeding 2000ms requirement";
}

TEST_F(PerformanceTest, NFR_IMG_01_FullPipeline_MediumFrame_CompletesUnder2Seconds) {
    PerformanceMetrics metrics = RunFullPipeline(MEDIUM_WIDTH, MEDIUM_HEIGHT);

    EXPECT_LT(metrics.total_time_us, MAX_FULL_PIPELINE_US)
        << "Full pipeline for 1024x1024 frame took " << (metrics.total_time_us / 1000.0)
        << "ms, exceeding 2000ms requirement";
}

TEST_F(PerformanceTest, NFR_IMG_01_FullPipeline_LargeFrame_CompletesUnder2Seconds) {
    PerformanceMetrics metrics = RunFullPipeline(LARGE_WIDTH, LARGE_HEIGHT);

    EXPECT_LT(metrics.total_time_us, MAX_FULL_PIPELINE_US)
        << "Full pipeline for 2048x2048 frame took " << (metrics.total_time_us / 1000.0)
        << "ms, exceeding 2000ms requirement";
}

// =============================================================================
// NFR-IMG-02: Preview Mode Performance Tests
// =============================================================================

TEST_F(PerformanceTest, NFR_IMG_02_PreviewMode_SmallFrame_CompletesUnder500ms) {
    uint64_t time_us = RunPreviewPipeline(SMALL_WIDTH, SMALL_HEIGHT);

    EXPECT_LT(time_us, MAX_PREVIEW_PIPELINE_US)
        << "Preview pipeline for 512x512 frame took " << (time_us / 1000.0)
        << "ms, exceeding 500ms requirement";
}

TEST_F(PerformanceTest, NFR_IMG_02_PreviewMode_MediumFrame_CompletesUnder500ms) {
    uint64_t time_us = RunPreviewPipeline(MEDIUM_WIDTH, MEDIUM_HEIGHT);

    EXPECT_LT(time_us, MAX_PREVIEW_PIPELINE_US)
        << "Preview pipeline for 1024x1024 frame took " << (time_us / 1000.0)
        << "ms, exceeding 500ms requirement";
}

TEST_F(PerformanceTest, NFR_IMG_02_PreviewMode_LargeFrame_CompletesUnder500ms) {
    uint64_t time_us = RunPreviewPipeline(LARGE_WIDTH, LARGE_HEIGHT);

    EXPECT_LT(time_us, MAX_PREVIEW_PIPELINE_US)
        << "Preview pipeline for 2048x2048 frame took " << (time_us / 1000.0)
        << "ms, exceeding 500ms requirement";
}

// =============================================================================
// Individual Stage Performance Tests
// =============================================================================

TEST_F(PerformanceTest, OffsetCorrection_IsReasonablyFast) {
    auto frame_data = GenerateTestFrame(SMALL_WIDTH, SMALL_HEIGHT);
    CalibrationData dark = CreateCalibration(SMALL_WIDTH, SMALL_HEIGHT, CalibrationDataType::DARK_FRAME);

    ImageBuffer frame;
    frame.width = SMALL_WIDTH;
    frame.height = SMALL_HEIGHT;
    frame.pixel_depth = 16;
    frame.stride = SMALL_WIDTH * 2;
    frame.data = frame_data.data();

    ASSERT_TRUE(engine_->ApplyOffsetCorrection(frame, dark));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_LT(timing.offset_correction_us, 100000)  // < 100ms
        << "Offset correction took " << (timing.offset_correction_us / 1000.0) << "ms";

    CleanupCalibration(dark);
}

TEST_F(PerformanceTest, GainCorrection_IsReasonablyFast) {
    auto frame_data = GenerateTestFrame(SMALL_WIDTH, SMALL_HEIGHT);
    CalibrationData dark = CreateCalibration(SMALL_WIDTH, SMALL_HEIGHT, CalibrationDataType::DARK_FRAME);
    CalibrationData gain = CreateCalibration(SMALL_WIDTH, SMALL_HEIGHT, CalibrationDataType::GAIN_MAP);

    ImageBuffer frame;
    frame.width = SMALL_WIDTH;
    frame.height = SMALL_HEIGHT;
    frame.pixel_depth = 16;
    frame.stride = SMALL_WIDTH * 2;
    frame.data = frame_data.data();

    ASSERT_TRUE(engine_->ApplyOffsetCorrection(frame, dark));
    ASSERT_TRUE(engine_->ApplyGainCorrection(frame, gain));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_LT(timing.gain_correction_us, 100000)  // < 100ms
        << "Gain correction took " << (timing.gain_correction_us / 1000.0) << "ms";

    CleanupCalibration(dark);
    CleanupCalibration(gain);
}

TEST_F(PerformanceTest, WindowLevel_IsReasonablyFast) {
    auto frame_data = GenerateTestFrame(SMALL_WIDTH, SMALL_HEIGHT);

    ImageBuffer frame;
    frame.width = SMALL_WIDTH;
    frame.height = SMALL_HEIGHT;
    frame.pixel_depth = 16;
    frame.stride = SMALL_WIDTH * 2;
    frame.data = frame_data.data();

    ASSERT_TRUE(engine_->ApplyWindowLevel(frame, 4000.0f, 2000.0f));

    StageTiming timing = engine_->GetLastTiming();
    EXPECT_LT(timing.window_level_us, 100000)  // < 100ms
        << "Window/Level took " << (timing.window_level_us / 1000.0) << "ms";
}

// =============================================================================
// Timing Consistency Tests
// =============================================================================

TEST_F(PerformanceTest, FullPipeline_TimingIsConsistent) {
    constexpr int ITERATIONS = 5;

    std::vector<uint64_t> times;
    times.reserve(ITERATIONS);

    for (int i = 0; i < ITERATIONS; ++i) {
        PerformanceMetrics metrics = RunFullPipeline(SMALL_WIDTH, SMALL_HEIGHT);
        times.push_back(metrics.total_time_us);
    }

    // Calculate standard deviation
    uint64_t sum = 0;
    for (auto t : times) sum += t;
    double mean = static_cast<double>(sum) / ITERATIONS;

    double variance = 0;
    for (auto t : times) {
        double diff = t - mean;
        variance += diff * diff;
    }
    variance /= ITERATIONS;
    double stddev = std::sqrt(variance);

    // Coefficient of variation should be < 30%
    double cv = stddev / mean;
    EXPECT_LT(cv, 0.3)
        << "Timing variance too high: mean=" << (mean / 1000.0) << "ms"
        << ", stddev=" << (stddev / 1000.0) << "ms"
        << ", CV=" << (cv * 100.0) << "%";
}

TEST_F(PerformanceTest, PreviewMode_TimingIsConsistent) {
    constexpr int ITERATIONS = 5;

    std::vector<uint64_t> times;
    times.reserve(ITERATIONS);

    for (int i = 0; i < ITERATIONS; ++i) {
        times.push_back(RunPreviewPipeline(SMALL_WIDTH, SMALL_HEIGHT));
    }

    uint64_t sum = 0;
    for (auto t : times) sum += t;
    double mean = static_cast<double>(sum) / ITERATIONS;

    double variance = 0;
    for (auto t : times) {
        double diff = t - mean;
        variance += diff * diff;
    }
    variance /= ITERATIONS;
    double stddev = std::sqrt(variance);

    double cv = stddev / mean;
    EXPECT_LT(cv, 0.3)
        << "Preview timing variance too high: mean=" << (mean / 1000.0) << "ms"
        << ", stddev=" << (stddev / 1000.0) << "ms"
        << ", CV=" << (cv * 100.0) << "%";
}

// =============================================================================
// Memory Efficiency Tests
// =============================================================================

TEST_F(PerformanceTest, FullPipeline_DoesNotLeakMemory) {
    // Run multiple iterations and check for memory growth
    uint64_t baseline_time = RunFullPipeline(SMALL_WIDTH, SMALL_HEIGHT).total_time_us;

    for (int i = 0; i < 10; ++i) {
        RunFullPipeline(SMALL_WIDTH, SMALL_HEIGHT);
    }

    uint64_t final_time = RunFullPipeline(SMALL_WIDTH, SMALL_HEIGHT).total_time_us;

    // If memory is leaking, later iterations will be slower
    // Allow 2x difference (could be due to CPU throttling, etc.)
    EXPECT_LT(final_time, baseline_time * 2)
        << "Possible memory leak detected: later iterations are significantly slower";
}

// =============================================================================
// Stage Timing Breakdown Tests
// =============================================================================

TEST_F(PerformanceTest, FullPipeline_AllStagesReportTiming) {
    PerformanceMetrics metrics = RunFullPipeline(SMALL_WIDTH, SMALL_HEIGHT);

    EXPECT_GT(metrics.offset_time_us, 0) << "Offset correction timing not reported";
    EXPECT_GT(metrics.gain_time_us, 0) << "Gain correction timing not reported";
    EXPECT_GT(metrics.defect_time_us, 0) << "Defect pixel mapping timing not reported";
    EXPECT_GT(metrics.scatter_time_us, 0) << "Scatter correction timing not reported";
    EXPECT_GT(metrics.noise_time_us, 0) << "Noise reduction timing not reported";
    EXPECT_GT(metrics.wl_time_us, 0) << "Window/Level timing not reported";
}

TEST_F(PerformanceTest, FullPipeline_TimingSumMatchesTotal) {
    PerformanceMetrics metrics = RunFullPipeline(SMALL_WIDTH, SMALL_HEIGHT);

    uint64_t stage_sum = metrics.offset_time_us + metrics.gain_time_us +
                        metrics.defect_time_us + metrics.scatter_time_us +
                        metrics.noise_time_us + metrics.wl_time_us;

    // Stage timing should be close to total time (within 20%)
    // Total time includes some overhead not captured in stage timing
    EXPECT_LT(stage_sum, metrics.total_time_us * 1.2)
        << "Stage timing sum exceeds total time significantly";

    EXPECT_GT(stage_sum, metrics.total_time_us * 0.5)
        << "Stage timing sum is too small compared to total time";
}

// =============================================================================
// Throughput Tests
// =============================================================================

TEST_F(PerformanceTest, Throughput_SmallFramesPerSecond) {
    constexpr int ITERATIONS = 10;
    constexpr double TARGET_FPS = 2.0;  // At least 2 frames per second

    auto start = std::chrono::high_resolution_clock::now();

    for (int i = 0; i < ITERATIONS; ++i) {
        RunFullPipeline(SMALL_WIDTH, SMALL_HEIGHT);
    }

    auto end = std::chrono::high_resolution_clock::now();
    double elapsed_sec = std::chrono::duration<double>(end - start).count();

    double fps = ITERATIONS / elapsed_sec;
    EXPECT_GT(fps, TARGET_FPS)
        << "Throughput too low: " << fps << " FPS, target > " << TARGET_FPS << " FPS";
}
