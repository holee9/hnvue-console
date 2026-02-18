/**
 * @file DefaultImageProcessingEngine.cpp
 * @brief Implementation of DefaultImageProcessingEngine
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Default image processing engine implementation
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/imaging/DefaultImageProcessingEngine.h"

#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <chrono>
#include <cstring>
#include <algorithm>

namespace hnvue::imaging {

// =============================================================================
// Internal Helper Classes
// =============================================================================

namespace internal {

/**
 * @brief OpenCV helper wrapper
 *
 * Provides RAII wrappers for OpenCV resources and utility functions.
 */
class OpenCVHelper {
public:
    /**
     * @brief Create a cv::Mat wrapper from ImageBuffer
     * @param buffer Source image buffer
     * @return cv::Mat wrapping the same data (no copy)
     *
     * The returned Mat shares ownership of the data pointer.
     * Caller must ensure the buffer remains valid.
     */
    static cv::Mat WrapBuffer(const ImageBuffer& buffer) {
        if (buffer.data == nullptr || buffer.width == 0 || buffer.height == 0) {
            return cv::Mat();
        }
        return cv::Mat(buffer.height, buffer.width, CV_16UC1,
                       buffer.data, buffer.stride);
    }

    /**
     * @brief Create a cv::Mat with copied data from ImageBuffer
     * @param buffer Source image buffer
     * @return cv::Mat with copied data
     */
    static cv::Mat CopyBuffer(const ImageBuffer& buffer) {
        cv::Mat wrapped = WrapBuffer(buffer);
        return wrapped.clone();
    }

    /**
     * @brief Create ImageBuffer wrapper from cv::Mat
     * @param mat Source cv::Mat
     * @param target_buffer Target buffer to populate
     *
     * The target buffer's data pointer will point to the Mat's data.
     * The Mat must remain valid for the lifetime of the buffer.
     */
    static void WrapMat(const cv::Mat& mat, ImageBuffer& target_buffer) {
        target_buffer.width = mat.cols;
        target_buffer.height = mat.rows;
        target_buffer.pixel_depth = 16;
        target_buffer.stride = mat.step;
        target_buffer.data = reinterpret_cast<uint16_t*>(mat.data);
    }

    /**
     * @brief Validate two calibration data structures for dimension match
     */
    static bool DimensionsMatch(const CalibrationData& calib,
                                 const ImageBuffer& frame) {
        return calib.width == frame.width && calib.height == frame.height;
    }
};

/**
 * @brief FFTW helper wrapper for scatter correction
 *
 * Manages FFTW plans and provides frequency-domain filtering operations.
 */
class FFTWHelper {
public:
    FFTWHelper() = default;
    ~FFTWHelper() = default;

    /**
     * @brief Apply frequency-domain high-pass filter for scatter correction
     * @param frame Frame to process (modified in-place)
     * @param cutoff_frequency Normalized cutoff frequency (0.0-1.0)
     * @param suppression_ratio Scatter suppression ratio
     * @return true if processing succeeded
     */
    bool ApplyScatterCorrection(ImageBuffer& frame,
                                 float cutoff_frequency,
                                 float suppression_ratio) {
        if (frame.data == nullptr || frame.width == 0 || frame.height == 0) {
            return false;
        }

        cv::Mat mat = OpenCVHelper::WrapBuffer(frame);

        // For scatter correction, we use a simple high-pass filter in OpenCV
        // This is a simplified implementation - production may use full FFTW

        // Convert to float for frequency domain processing
        cv::Mat float_mat;
        mat.convertTo(float_mat, CV_32F);

        // Apply Gaussian blur to estimate low-frequency background (scatter)
        cv::Mat background;
        int kernel_size = static_cast<int>(
            std::min(frame.width, frame.height) * (1.0f - cutoff_frequency) * 2.0f + 1.0f);
        if (kernel_size < 3) kernel_size = 3;
        if (kernel_size % 2 == 0) kernel_size += 1;

        cv::GaussianBlur(float_mat, background,
                         cv::Size(kernel_size, kernel_size), 0);

        // Subtract scatter component with suppression ratio
        cv::Mat result = float_mat - (background * suppression_ratio);

        // Convert back to 16-bit with clipping
        result = cv::max(result, 0.0f);
        result = cv::min(result, 65535.0f);
        result.convertTo(mat, CV_16U);

        return true;
    }
};

/**
 * @brief Calibration data cache
 *
 * Manages cached calibration data and validation.
 */
class CalibrationCache {
public:
    CalibrationCache() = default;
    ~CalibrationCache() = default;

    /**
     * @brief Store calibration data in cache
     */
    void Store(const std::string& key, const CalibrationData& data) {
        std::lock_guard<std::mutex> lock(mutex_);
        cache_[key] = data;
    }

    /**
     * @brief Retrieve calibration data from cache
     */
    bool Retrieve(const std::string& key, CalibrationData& data) const {
        std::lock_guard<std::mutex> lock(mutex_);
        auto it = cache_.find(key);
        if (it != cache_.end()) {
            data = it->second;
            return true;
        }
        return false;
    }

    /**
     * @brief Clear all cached data
     */
    void Clear() {
        std::lock_guard<std::mutex> lock(mutex_);
        cache_.clear();
    }

private:
    mutable std::mutex mutex_;
    std::unordered_map<std::string, CalibrationData> cache_;
};

} // namespace internal

// =============================================================================
// DefaultImageProcessingEngine Implementation
// =============================================================================

DefaultImageProcessingEngine::DefaultImageProcessingEngine()
    : cv_helper_(std::make_unique<internal::OpenCVHelper>()),
      fftw_helper_(std::make_unique<internal::FFTWHelper>()),
      calib_cache_(std::make_unique<internal::CalibrationCache>()) {
}

DefaultImageProcessingEngine::~DefaultImageProcessingEngine() {
    Shutdown();
}

bool DefaultImageProcessingEngine::Initialize(const EngineConfig& config) {
    if (initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine already initialized", "Initialize");
        return false;
    }

    config_ = config;

    // Validate configuration
    if (config.max_frame_width == 0 || config.max_frame_height == 0) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Invalid frame dimensions in configuration", "Initialize");
        return false;
    }

    // Initialize helpers
    // (OpenCV and FFTW are initialized lazily on first use)

    initialized_ = true;
    ClearError();
    return true;
}

void DefaultImageProcessingEngine::Shutdown() {
    if (!initialized_) {
        return;
    }

    // Clear cached calibration data
    if (calib_cache_) {
        calib_cache_->Clear();
    }

    initialized_ = false;
}

bool DefaultImageProcessingEngine::ApplyOffsetCorrection(
    ImageBuffer& frame, const CalibrationData& dark) {

    auto start = std::chrono::high_resolution_clock::now();

    if (!initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine not initialized", "OffsetCorrection");
        return false;
    }

    if (!ValidateFrame(frame)) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Invalid frame buffer", "OffsetCorrection");
        return false;
    }

    if (!ValidateCalibration(dark, frame)) {
        SetError(ImagingError::IMAGING_ERR_CALIBRATION,
                 "Invalid dark calibration data", "OffsetCorrection");
        return false;
    }

    if (dark.type != CalibrationDataType::DARK_FRAME) {
        SetError(ImagingError::IMAGING_ERR_CALIBRATION,
                 "Calibration data is not a dark frame", "OffsetCorrection");
        return false;
    }

    cv::Mat mat = internal::OpenCVHelper::WrapBuffer(frame);
    cv::Mat dark_mat(frame.height, frame.width, CV_32FC1,
                     dark.data_f32, cv::Mat::AUTO_STEP);

    // Perform subtraction with clamping to zero
    // Convert to float first for accurate subtraction
    cv::Mat float_mat;
    mat.convertTo(float_mat, CV_32F);
    float_mat -= dark_mat;

    // Clamp to zero and convert back to 16-bit
    cv::max(float_mat, 0.0f, float_mat);
    float_mat.convertTo(mat, CV_16U);

    auto end = std::chrono::high_resolution_clock::now();
    std::lock_guard<std::mutex> lock(timing_mutex_);
    last_timing_.offset_correction_us =
        std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();

    ClearError();
    return true;
}

bool DefaultImageProcessingEngine::ApplyGainCorrection(
    ImageBuffer& frame, const CalibrationData& gain) {

    auto start = std::chrono::high_resolution_clock::now();

    if (!initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine not initialized", "GainCorrection");
        return false;
    }

    if (!ValidateFrame(frame)) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Invalid frame buffer", "GainCorrection");
        return false;
    }

    if (!ValidateCalibration(gain, frame)) {
        SetError(ImagingError::IMAGING_ERR_CALIBRATION,
                 "Invalid gain calibration data", "GainCorrection");
        return false;
    }

    if (gain.type != CalibrationDataType::GAIN_MAP) {
        SetError(ImagingError::IMAGING_ERR_CALIBRATION,
                 "Calibration data is not a gain map", "GainCorrection");
        return false;
    }

    cv::Mat mat = internal::OpenCVHelper::WrapBuffer(frame);
    cv::Mat gain_mat(frame.height, frame.width, CV_32FC1,
                     gain.data_f32, cv::Mat::AUTO_STEP);

    // Perform multiplication
    cv::Mat float_mat;
    mat.convertTo(float_mat, CV_32F);
    float_mat = float_mat.mul(gain_mat);

    // Clamp to 16-bit range and convert back
    cv::max(float_mat, 0.0f, float_mat);
    cv::min(float_mat, 65535.0f, float_mat);
    float_mat.convertTo(mat, CV_16U);

    auto end = std::chrono::high_resolution_clock::now();
    std::lock_guard<std::mutex> lock(timing_mutex_);
    last_timing_.gain_correction_us =
        std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();

    ClearError();
    return true;
}

bool DefaultImageProcessingEngine::ApplyDefectPixelMap(
    ImageBuffer& frame, const DefectMap& map) {

    auto start = std::chrono::high_resolution_clock::now();

    if (!initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine not initialized", "DefectPixelMap");
        return false;
    }

    if (!ValidateFrame(frame)) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Invalid frame buffer", "DefectPixelMap");
        return false;
    }

    if (!map.valid || map.pixels == nullptr || map.count == 0) {
        // No defect pixels to correct - this is OK
        ClearError();
        return true;
    }

    cv::Mat mat = internal::OpenCVHelper::WrapBuffer(frame);

    // Correct each defective pixel
    for (uint32_t i = 0; i < map.count; ++i) {
        const DefectPixelEntry& defect = map.pixels[i];

        if (defect.x >= frame.width || defect.y >= frame.height) {
            continue;  // Skip out-of-bounds pixels
        }

        // Apply interpolation based on method
        switch (defect.interpolation) {
            case InterpolationMethod::NEAREST_NEIGHBOR:
                ApplyNearestNeighbor(mat, defect.x, defect.y);
                break;
            case InterpolationMethod::BILINEAR:
                ApplyBilinear(mat, defect.x, defect.y);
                break;
            case InterpolationMethod::MEDIAN_3X3:
                ApplyMedian3x3(mat, defect.x, defect.y);
                break;
            default:
                // Default to bilinear
                ApplyBilinear(mat, defect.x, defect.y);
                break;
        }
    }

    auto end = std::chrono::high_resolution_clock::now();
    std::lock_guard<std::mutex> lock(timing_mutex_);
    last_timing_.defect_pixel_map_us =
        std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();

    ClearError();
    return true;
}

bool DefaultImageProcessingEngine::ApplyScatterCorrection(
    ImageBuffer& frame, const ScatterParams& params) {

    auto start = std::chrono::high_resolution_clock::now();

    if (!initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine not initialized", "ScatterCorrection");
        return false;
    }

    // Pass-through if disabled
    if (!params.enabled) {
        std::lock_guard<std::mutex> lock(timing_mutex_);
        last_timing_.scatter_correction_us = 0;
        ClearError();
        return true;
    }

    if (!ValidateFrame(frame)) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Invalid frame buffer", "ScatterCorrection");
        return false;
    }

    if (!fftw_helper_->ApplyScatterCorrection(frame,
                                               params.cutoff_frequency,
                                               params.suppression_ratio)) {
        SetError(ImagingError::IMAGING_ERR_ENGINE,
                 "Scatter correction failed", "ScatterCorrection");
        return false;
    }

    auto end = std::chrono::high_resolution_clock::now();
    std::lock_guard<std::mutex> lock(timing_mutex_);
    last_timing_.scatter_correction_us =
        std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();

    ClearError();
    return true;
}

bool DefaultImageProcessingEngine::ApplyWindowLevel(
    ImageBuffer& frame, float window, float level) {

    auto start = std::chrono::high_resolution_clock::now();

    if (!initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine not initialized", "WindowLevel");
        return false;
    }

    if (!ValidateFrame(frame)) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Invalid frame buffer", "WindowLevel");
        return false;
    }

    if (window <= 0.0f) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Window width must be positive", "WindowLevel");
        return false;
    }

    cv::Mat mat = internal::OpenCVHelper::WrapBuffer(frame);

    // Apply window/level mapping
    // This is a non-destructive operation that can be reapplied
    float win_min = level - window / 2.0f;
    float win_max = level + window / 2.0f;
    float scale = 65535.0f / window;

    mat.forEach<uint16_t>(
        [&](uint16_t& pixel, const int*) {
            float value = static_cast<float>(pixel);
            value = (value - win_min) * scale;
            pixel = static_cast<uint16_t>(
                std::max(0.0f, std::min(65535.0f, value)));
        }
    );

    auto end = std::chrono::high_resolution_clock::now();
    std::lock_guard<std::mutex> lock(timing_mutex_);
    last_timing_.window_level_us =
        std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();

    ClearError();
    return true;
}

bool DefaultImageProcessingEngine::ApplyNoiseReduction(
    ImageBuffer& frame, const NoiseReductionConfig& config) {

    auto start = std::chrono::high_resolution_clock::now();

    if (!initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine not initialized", "NoiseReduction");
        return false;
    }

    // Pass-through if disabled
    if (!config.enabled) {
        std::lock_guard<std::mutex> lock(timing_mutex_);
        last_timing_.noise_reduction_us = 0;
        ClearError();
        return true;
    }

    if (!ValidateFrame(frame)) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Invalid frame buffer", "NoiseReduction");
        return false;
    }

    cv::Mat mat = internal::OpenCVHelper::WrapBuffer(frame);
    int kernel_size = static_cast<int>(config.kernel_size);

    // Ensure kernel size is odd and at least 3
    if (kernel_size < 3) kernel_size = 3;
    if (kernel_size % 2 == 0) kernel_size += 1;

    switch (config.filter_type) {
        case NoiseFilterType::GAUSSIAN:
            cv::GaussianBlur(mat, mat, cv::Size(kernel_size, kernel_size),
                             config.sigma);
            break;

        case NoiseFilterType::MEDIAN:
            cv::medianBlur(mat, mat, kernel_size);
            break;

        case NoiseFilterType::BILATERAL:
            cv::bilateralFilter(mat, mat, kernel_size,
                                config.sigma, config.sigma);
            break;

        default:
            SetError(ImagingError::IMAGING_ERR_PARAM,
                     "Unknown noise filter type", "NoiseReduction");
            return false;
    }

    auto end = std::chrono::high_resolution_clock::now();
    std::lock_guard<std::mutex> lock(timing_mutex_);
    last_timing_.noise_reduction_us =
        std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();

    ClearError();
    return true;
}

bool DefaultImageProcessingEngine::ApplyFlattening(
    ImageBuffer& frame, const FlatteningConfig& config) {

    auto start = std::chrono::high_resolution_clock::now();

    if (!initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine not initialized", "Flattening");
        return false;
    }

    // Pass-through if disabled
    if (!config.enabled) {
        std::lock_guard<std::mutex> lock(timing_mutex_);
        last_timing_.flattening_us = 0;
        ClearError();
        return true;
    }

    if (!ValidateFrame(frame)) {
        SetError(ImagingError::IMAGING_ERR_PARAM,
                 "Invalid frame buffer", "Flattening");
        return false;
    }

    cv::Mat mat = internal::OpenCVHelper::WrapBuffer(frame);

    // Simple background subtraction using morphological opening
    // This is a simplified implementation
    int kernel_size = static_cast<int>(config.sigma_background);
    if (kernel_size < 3) kernel_size = 3;
    if (kernel_size % 2 == 0) kernel_size += 1;

    cv::Mat background;
    cv::morphologyEx(mat, background, cv::MORPH_OPEN,
                     cv::getStructuringElement(cv::MORPH_ELLIPSE,
                                               cv::Size(kernel_size, kernel_size)));

    // Normalize the image by dividing by the background and scaling
    cv::Mat float_mat, float_bg;
    mat.convertTo(float_mat, CV_32F);
    background.convertTo(float_bg, CV_32F);

    // Avoid division by zero
    cv::Mat mask = (float_bg > 1.0f);
    float_bg.setTo(1.0f, ~mask);

    cv::Mat result = float_mat * 65535.0f / float_bg;
    cv::max(result, 0.0f, result);
    cv::min(result, 65535.0f, result);
    result.convertTo(mat, CV_16U);

    auto end = std::chrono::high_resolution_clock::now();
    std::lock_guard<std::mutex> lock(timing_mutex_);
    last_timing_.flattening_us =
        std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();

    ClearError();
    return true;
}

bool DefaultImageProcessingEngine::ProcessFrame(
    ImageBuffer& frame, const ProcessingConfig& config) {

    if (!initialized_) {
        SetError(ImagingError::IMAGING_ERR_INIT,
                 "Engine not initialized", "ProcessFrame");
        return false;
    }

    // Clear timing for new frame
    {
        std::lock_guard<std::mutex> lock(timing_mutex_);
        last_timing_ = StageTiming{};
    }

    bool success = true;
    uint64_t stages = 0;

    // Apply pipeline based on mode
    if (config.mode == ProcessingMode::PREVIEW) {
        // Preview mode: Offset -> Gain -> Window/Level
        if (!ApplyOffsetCorrection(frame, *config.calibration_dark)) {
            return false;
        }
        stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_OFFSET_CORRECTION);

        if (!ApplyGainCorrection(frame, *config.calibration_gain)) {
            return false;
        }
        stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_GAIN_CORRECTION);

        if (!ApplyWindowLevel(frame, config.window, config.level)) {
            return false;
        }
        stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_WINDOW_LEVEL);

    } else {
        // Full pipeline mode
        if (config.calibration_dark == nullptr ||
            config.calibration_gain == nullptr ||
            config.defect_map == nullptr) {
            SetError(ImagingError::IMAGING_ERR_PARAM,
                     "Required calibration data is null", "ProcessFrame");
            return false;
        }

        // Stage 1: Offset Correction
        if (!ApplyOffsetCorrection(frame, *config.calibration_dark)) {
            return false;
        }
        stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_OFFSET_CORRECTION);

        // Stage 2: Gain Correction
        if (!ApplyGainCorrection(frame, *config.calibration_gain)) {
            return false;
        }
        stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_GAIN_CORRECTION);

        // Stage 3: Defect Pixel Mapping
        if (!ApplyDefectPixelMap(frame, *config.defect_map)) {
            return false;
        }
        stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_DEFECT_PIXEL_MAP);

        // Stage 4: Scatter Correction (conditional)
        if (!ApplyScatterCorrection(frame, config.scatter)) {
            return false;
        }
        if (config.scatter.enabled) {
            stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_SCATTER_CORRECTION);
        }

        // Stage 5: Noise Reduction (conditional)
        if (!ApplyNoiseReduction(frame, config.noise_reduction)) {
            return false;
        }
        if (config.noise_reduction.enabled) {
            stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_NOISE_REDUCTION);
        }

        // Stage 6: Flattening (conditional)
        if (!ApplyFlattening(frame, config.flattening)) {
            return false;
        }
        if (config.flattening.enabled) {
            stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_FLATTENING);
        }

        // Stage 7: Window/Level
        if (!ApplyWindowLevel(frame, config.window, config.level)) {
            return false;
        }
        stages |= static_cast<uint64_t>(PipelineStageFlags::STAGE_WINDOW_LEVEL);
    }

    ClearError();
    return true;
}

EngineInfo DefaultImageProcessingEngine::GetEngineInfo() const {
    EngineInfo info;
    info.engine_name = "DefaultImageProcessingEngine";
    info.engine_version = "0.1.0";
    info.vendor = "HnVue";
    info.capabilities =
        static_cast<uint64_t>(EngineCapabilityFlags::CAP_OFFSET_CORRECTION) |
        static_cast<uint64_t>(EngineCapabilityFlags::CAP_GAIN_CORRECTION) |
        static_cast<uint64_t>(EngineCapabilityFlags::CAP_DEFECT_PIXEL_MAP) |
        static_cast<uint64_t>(EngineCapabilityFlags::CAP_SCATTER_CORRECTION) |
        static_cast<uint64_t>(EngineCapabilityFlags::CAP_WINDOW_LEVEL) |
        static_cast<uint64_t>(EngineCapabilityFlags::CAP_NOISE_REDUCTION) |
        static_cast<uint64_t>(EngineCapabilityFlags::CAP_FLATTENING) |
        static_cast<uint64_t>(EngineCapabilityFlags::CAP_PREVIEW_MODE);
    info.api_version = 0x01000000;  // v1.0.0
    return info;
}

EngineError DefaultImageProcessingEngine::GetLastError() const {
    std::lock_guard<std::mutex> lock(error_mutex_);
    return last_error_;
}

StageTiming DefaultImageProcessingEngine::GetLastTiming() const {
    std::lock_guard<std::mutex> lock(timing_mutex_);
    return last_timing_;
}

// =============================================================================
// Private Helper Methods
// =============================================================================

void DefaultImageProcessingEngine::SetError(ImagingError code,
                                             const std::string& message,
                                             const std::string& stage) {
    std::lock_guard<std::mutex> lock(error_mutex_);
    last_error_.error_code = code;
    last_error_.error_message = message;
    last_error_.failed_stage = stage;
}

void DefaultImageProcessingEngine::ClearError() {
    std::lock_guard<std::mutex> lock(error_mutex_);
    last_error_.error_code = ImagingError::IMAGING_OK;
    last_error_.error_message.clear();
    last_error_.failed_stage.clear();
}

bool DefaultImageProcessingEngine::ValidateFrame(const ImageBuffer& frame) {
    return frame.data != nullptr &&
           frame.width > 0 &&
           frame.height > 0 &&
           frame.pixel_depth == 16 &&
           frame.stride >= frame.width * 2;
}

bool DefaultImageProcessingEngine::ValidateCalibration(
    const CalibrationData& calib, const ImageBuffer& frame) {

    return calib.valid &&
           calib.data_f32 != nullptr &&
           calib.width == frame.width &&
           calib.height == frame.height;
}

void DefaultImageProcessingEngine::ApplyNearestNeighbor(
    cv::Mat& mat, uint32_t x, uint32_t y) {

    // Find nearest valid pixel
    const int search_radius = 2;
    uint16_t neighbor_value = 0;
    bool found = false;

    for (int dy = -search_radius; dy <= search_radius && !found; ++dy) {
        for (int dx = -search_radius; dx <= search_radius && !found; ++dx) {
            int nx = static_cast<int>(x) + dx;
            int ny = static_cast<int>(y) + dy;
            if (nx >= 0 && nx < mat.cols && ny >= 0 && ny < mat.rows) {
                if (dx != 0 || dy != 0) {  // Not the defective pixel itself
                    neighbor_value = mat.at<uint16_t>(ny, nx);
                    found = true;
                }
            }
        }
    }

    if (found) {
        mat.at<uint16_t>(y, x) = neighbor_value;
    }
}

void DefaultImageProcessingEngine::ApplyBilinear(
    cv::Mat& mat, uint32_t x, uint32_t y) {

    // Simple bilinear interpolation from 4 neighbors
    int x0 = std::max(0, static_cast<int>(x) - 1);
    int x1 = std::min(mat.cols - 1, static_cast<int>(x) + 1);
    int y0 = std::max(0, static_cast<int>(y) - 1);
    int y1 = std::min(mat.rows - 1, static_cast<int>(y) + 1);

    // Avoid using the defective pixel itself
    uint16_t tl = mat.at<uint16_t>(y0, x0);
    uint16_t tr = mat.at<uint16_t>(y0, x1);
    uint16_t bl = mat.at<uint16_t>(y1, x0);
    uint16_t br = mat.at<uint16_t>(y1, x1);

    // Average of neighbors
    uint32_t sum = static_cast<uint32_t>(tl) + tr + bl + br;
    mat.at<uint16_t>(y, x) = static_cast<uint16_t>(sum / 4);
}

void DefaultImageProcessingEngine::ApplyMedian3x3(
    cv::Mat& mat, uint32_t x, uint32_t y) {

    // Collect 3x3 neighborhood values
    std::vector<uint16_t> values;
    values.reserve(9);

    for (int dy = -1; dy <= 1; ++dy) {
        for (int dx = -1; dx <= 1; ++dx) {
            int nx = static_cast<int>(x) + dx;
            int ny = static_cast<int>(y) + dy;
            if (nx >= 0 && nx < mat.cols && ny >= 0 && ny < mat.rows) {
                if (dx != 0 || dy != 0) {  // Exclude the defective pixel
                    values.push_back(mat.at<uint16_t>(ny, nx));
                }
            }
        }
    }

    if (!values.empty()) {
        std::sort(values.begin(), values.end());
        mat.at<uint16_t>(y, x) = values[values.size() / 2];
    }
}

} // namespace hnvue::imaging
