/**
 * @file DefaultImageProcessingEngine.h
 * @brief Default implementation of IImageProcessingEngine using OpenCV and FFTW
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Default image processing engine
 * SPDX-License-Identifier: MIT
 *
 * This is the reference implementation of the image processing pipeline.
 * It uses OpenCV for image operations and FFTW for frequency-domain scatter
 * correction.
 */

#ifndef HNUE_IMAGING_DEFAULT_IMAGE_PROCESSING_ENGINE_H
#define HNUE_IMAGING_DEFAULT_IMAGE_PROCESSING_ENGINE_H

#include "IImageProcessingEngine.h"

#include <mutex>
#include <memory>
#include <vector>
#include <algorithm>

// Forward declaration for cv::Mat (avoid including OpenCV headers in public interface)
namespace cv {
    class Mat;
}

namespace hnvue::imaging {

// Forward declarations for implementation details
namespace internal {
    class OpenCVHelper;
    class FFTWHelper;
    class CalibrationCache;
} // namespace internal

/**
 * @brief Default implementation of IImageProcessingEngine
 *
 * Provides a complete, production-ready image processing pipeline using:
 * - OpenCV 4.x for core image operations
 * - FFTW 3.x for frequency-domain scatter correction
 *
 * This implementation is designed to be safe, predictable, and suitable
 * for IEC 62304 Class B medical device software.
 *
 * Performance Characteristics:
 * - Full pipeline: < 2000ms for typical medical detector frames (NFR-IMG-01)
 * - Preview mode: < 500ms for typical frames (NFR-IMG-02)
 */
class DefaultImageProcessingEngine : public IImageProcessingEngine {
public:
    /**
     * @brief Constructor
     */
    DefaultImageProcessingEngine();

    /**
     * @brief Destructor
     */
    ~DefaultImageProcessingEngine() override;

    // Disable copy and move
    DefaultImageProcessingEngine(const DefaultImageProcessingEngine&) = delete;
    DefaultImageProcessingEngine& operator=(const DefaultImageProcessingEngine&) = delete;
    DefaultImageProcessingEngine(DefaultImageProcessingEngine&&) = delete;
    DefaultImageProcessingEngine& operator=(DefaultImageProcessingEngine&&) = delete;

    // =========================================================================
    // IImageProcessingEngine Implementation
    // =========================================================================

    bool Initialize(const EngineConfig& config) override;
    void Shutdown() override;

    bool ApplyOffsetCorrection(ImageBuffer& frame,
                                const CalibrationData& dark) override;
    bool ApplyGainCorrection(ImageBuffer& frame,
                              const CalibrationData& gain) override;
    bool ApplyDefectPixelMap(ImageBuffer& frame,
                              const DefectMap& map) override;
    bool ApplyScatterCorrection(ImageBuffer& frame,
                                 const ScatterParams& params) override;
    bool ApplyWindowLevel(ImageBuffer& frame,
                           float window, float level) override;
    bool ApplyNoiseReduction(ImageBuffer& frame,
                              const NoiseReductionConfig& config) override;
    bool ApplyFlattening(ImageBuffer& frame,
                          const FlatteningConfig& config) override;
    bool ProcessFrame(ImageBuffer& frame,
                      const ProcessingConfig& config) override;

    EngineInfo GetEngineInfo() const override;
    EngineError GetLastError() const override;
    StageTiming GetLastTiming() const override;

private:
    // =========================================================================
    // Internal Helper Methods
    // =========================================================================

    /**
     * @brief Set the last error information
     * @param code Error code
     * @param message Human-readable error message
     * @param stage Pipeline stage that failed
     */
    void SetError(ImagingError code, const std::string& message,
                  const std::string& stage);

    /**
     * @brief Clear the last error
     */
    void ClearError();

    /**
     * @brief Validate frame buffer dimensions
     * @param frame Frame to validate
     * @return true if dimensions are valid and match configured maximum
     */
    bool ValidateFrame(const ImageBuffer& frame);

    /**
     * @brief Validate calibration data
     * @param calib Calibration data to validate
     * @param frame Reference frame for dimension matching
     * @return true if calibration data is valid
     */
    bool ValidateCalibration(const CalibrationData& calib,
                             const ImageBuffer& frame);

    /**
     * @brief Apply window/level LUT mapping
     * @param pixel Input pixel value
     * @param window Window width
     * @param level Window center
     * @return Mapped pixel value
     */
    static inline uint16_t ApplyWL(uint16_t pixel, float window, float level);

    /**
     * @brief Apply nearest-neighbor interpolation for defective pixel
     * @param mat OpenCV matrix (modified in-place)
     * @param x X coordinate of defective pixel
     * @param y Y coordinate of defective pixel
     */
    void ApplyNearestNeighbor(cv::Mat& mat, uint32_t x, uint32_t y);

    /**
     * @brief Apply bilinear interpolation for defective pixel
     * @param mat OpenCV matrix (modified in-place)
     * @param x X coordinate of defective pixel
     * @param y Y coordinate of defective pixel
     */
    void ApplyBilinear(cv::Mat& mat, uint32_t x, uint32_t y);

    /**
     * @brief Apply 3x3 median interpolation for defective pixel
     * @param mat OpenCV matrix (modified in-place)
     * @param x X coordinate of defective pixel
     * @param y Y coordinate of defective pixel
     */
    void ApplyMedian3x3(cv::Mat& mat, uint32_t x, uint32_t y);

    // =========================================================================
    // Member Variables
    // =========================================================================

    bool initialized_ = false;
    EngineConfig config_;

    // Error state
    mutable std::mutex error_mutex_;
    EngineError last_error_;

    // Timing information
    mutable std::mutex timing_mutex_;
    StageTiming last_timing_;

    // Internal helpers (PIMPL for ABI stability)
    std::unique_ptr<internal::OpenCVHelper> cv_helper_;
    std::unique_ptr<internal::FFTWHelper> fftw_helper_;
    std::unique_ptr<internal::CalibrationCache> calib_cache_;
};

} // namespace hnvue::imaging

#endif // HNUE_IMAGING_DEFAULT_IMAGE_PROCESSING_ENGINE_H
