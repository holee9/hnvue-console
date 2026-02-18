/**
 * @file IImageProcessingEngine.h
 * @brief Pure abstract interface for pluggable image processing engines
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Image processing engine interface
 * SPDX-License-Identifier: MIT
 *
 * This interface defines the complete contract for image processing engine
 * plugins. All implementations must be pure abstract (no non-virtual methods)
 * to satisfy mockability requirements for unit testing.
 *
 * The interface is designed to be fully mockable and ABI-stable across
 * DLL boundaries. No exceptions are thrown across the interface boundary.
 */

#ifndef HNUE_IMAGING_IIMAGE_PROCESSING_ENGINE_H
#define HNUE_IMAGING_IIMAGE_PROCESSING_ENGINE_H

#include "ImagingTypes.h"

#include <memory>
#include <string>

namespace hnvue::imaging {

/**
 * @brief Pure abstract interface for image processing engines
 *
 * This interface encapsulates all image correction and processing operations.
 * The host pipeline holds a std::unique_ptr<IImageProcessingEngine> and has
 * no knowledge of the concrete implementation.
 *
 * Processing Pipeline Sequence:
 * 1. Offset Correction (FR-IMG-01) - Always applied
 * 2. Gain Correction (FR-IMG-02) - Always applied
 * 3. Defect Pixel Mapping (FR-IMG-03) - Always applied
 * 4. Scatter Correction (FR-IMG-04) - Conditional (pass-through if disabled)
 * 5. Noise Reduction (FR-IMG-06) - Conditional
 * 6. Image Flattening (FR-IMG-07) - Conditional
 * 7. Window/Level (FR-IMG-05) - Always applied
 *
 * Thread Safety:
 * - Instances are NOT thread-safe for concurrent processing calls
 * - The host pipeline must serialize calls to processing methods
 * - Initialize() and Shutdown() are internally synchronized
 */
class IImageProcessingEngine {
public:
    /**
     * @brief Virtual destructor for proper cleanup
     */
    virtual ~IImageProcessingEngine() = default;

    // =========================================================================
    // Initialization and Shutdown
    // =========================================================================

    /**
     * @brief Initialize the engine with resource allocation and validation
     * @param config Engine configuration including dimensions and paths
     * @return true if initialization succeeded
     *
     * Must be called once before any processing method.
     * Allocates internal buffers and validates configuration.
     */
    virtual bool Initialize(const EngineConfig& config) = 0;

    /**
     * @brief Release all resources held by the engine
     *
     * Called by the host before destroying the engine instance.
     * After Shutdown(), no other methods may be called except Initialize().
     */
    virtual void Shutdown() = 0;

    // =========================================================================
    // Individual Processing Stages
    // =========================================================================

    /**
     * @brief Apply offset correction (dark frame subtraction)
     * @param frame Frame buffer (in/out) - modified in-place
     * @param dark Dark calibration data
     * @return true if correction succeeded
     *
     * Subtracts dark frame from raw frame pixel-by-pixel.
     * Results are clamped to zero to prevent underflow.
     * (FR-IMG-01)
     */
    virtual bool ApplyOffsetCorrection(ImageBuffer& frame,
                                        const CalibrationData& dark) = 0;

    /**
     * @brief Apply gain correction (flat-field normalization)
     * @param frame Frame buffer (in/out) - modified in-place
     * @param gain Gain calibration data
     * @return true if correction succeeded
     *
     * Multiplies each pixel by the corresponding gain coefficient.
     * (FR-IMG-02)
     */
    virtual bool ApplyGainCorrection(ImageBuffer& frame,
                                      const CalibrationData& gain) = 0;

    /**
     * @brief Apply defect pixel mapping
     * @param frame Frame buffer (in/out) - modified in-place
     * @param map Defect pixel map
     * @return true if correction succeeded
     *
     * Replaces defective pixel values using neighbor interpolation.
     * (FR-IMG-03)
     */
    virtual bool ApplyDefectPixelMap(ImageBuffer& frame,
                                      const DefectMap& map) = 0;

    /**
     * @brief Apply scatter correction (virtual anti-scatter grid)
     * @param frame Frame buffer (in/out) - modified in-place
     * @param params Scatter correction parameters
     * @return true if correction succeeded
     *
     * Applies frequency-domain scatter correction if enabled.
     * Pass-through if params.enabled is false.
     * (FR-IMG-04)
     */
    virtual bool ApplyScatterCorrection(ImageBuffer& frame,
                                         const ScatterParams& params) = 0;

    /**
     * @brief Apply window/level adjustment with display LUT
     * @param frame Frame buffer (in/out) - modified in-place
     * @param window Window width for display LUT
     * @param level Window center for display LUT
     * @return true if adjustment succeeded
     *
     * Applies window/level mapping for display.
     * This operation is reentrant and non-destructive to upstream data.
     * (FR-IMG-05)
     */
    virtual bool ApplyWindowLevel(ImageBuffer& frame,
                                   float window, float level) = 0;

    /**
     * @brief Apply noise reduction filtering
     * @param frame Frame buffer (in/out) - modified in-place
     * @param config Noise reduction configuration
     * @return true if filtering succeeded
     *
     * Applies the configured noise reduction filter (Gaussian/Median/Bilateral).
     * (FR-IMG-06)
     */
    virtual bool ApplyNoiseReduction(ImageBuffer& frame,
                                      const NoiseReductionConfig& config) = 0;

    /**
     * @brief Apply image flattening (background normalization)
     * @param frame Frame buffer (in/out) - modified in-place
     * @param config Flattening configuration
     * @return true if flattening succeeded
     *
     * Applies large-area background normalization.
     * (FR-IMG-07)
     */
    virtual bool ApplyFlattening(ImageBuffer& frame,
                                  const FlatteningConfig& config) = 0;

    // =========================================================================
    // Full Pipeline Processing
    // =========================================================================

    /**
     * @brief Execute the complete correction pipeline
     * @param frame Frame buffer (in/out) - modified in-place
     * @param config Complete processing configuration
     * @return true if all configured stages completed successfully
     *
     * Executes the pipeline stages in sequence based on config.mode:
     *
     * FULL_PIPELINE mode:
     *   Offset -> Gain -> Defect -> Scatter -> Noise -> Flatten -> Window/Level
     *
     * PREVIEW mode:
     *   Offset -> Gain -> Window/Level (only)
     *
     * If any stage fails, subsequent stages are not executed.
     * The original raw frame is preserved in the result.
     */
    virtual bool ProcessFrame(ImageBuffer& frame,
                              const ProcessingConfig& config) = 0;

    // =========================================================================
    // Query Methods
    // =========================================================================

    /**
     * @brief Get engine identification and capability information
     * @return EngineInfo structure
     *
     * Returns engine name, version, vendor, and capability flags.
     */
    virtual EngineInfo GetEngineInfo() const = 0;

    /**
     * @brief Get information about the last error that occurred
     * @return EngineError structure with error details
     *
     * Provides error code, message, and the stage that failed.
     * Called when a processing method returns false.
     */
    virtual EngineError GetLastError() const = 0;

    /**
     * @brief Get detailed timing information for the last processed frame
     * @return StageTiming structure with per-stage timing
     *
     * Provides execution time in microseconds for each pipeline stage.
     * Useful for performance profiling and bottleneck identification.
     */
    virtual StageTiming GetLastTiming() const = 0;
};

// =============================================================================
// Factory Method
// =============================================================================

/**
 * @brief Factory function type for creating engine instances
 *
 * Plugin DLLs must export a function of this type with the name
 * "CreateImageProcessingEngine" using extern "C" linkage.
 *
 * @return Pointer to newly created engine instance (caller owns)
 */
using CreateEngineFunc = IImageProcessingEngine* (*)();

/**
 * @brief Destroy function type for destroying engine instances
 *
 * Plugin DLLs must export a function of this type with the name
 * "DestroyImageProcessingEngine" using extern "C" linkage.
 *
 * @param engine Pointer to engine instance to destroy
 */
using DestroyEngineFunc = void (*)(IImageProcessingEngine*);

/**
 * @brief Factory class for creating engine instances
 *
 * Provides static methods to create engines from plugin DLLs or
 * instantiate the built-in default engine.
 */
class EngineFactory {
public:
    /**
     * @brief Create an engine from a plugin DLL
     * @param plugin_path Path to the engine plugin DLL
     * @return unique_ptr to the engine, or nullptr on failure
     *
     * Loads the DLL and calls the exported CreateImageProcessingEngine function.
     * Returns nullptr if the DLL cannot be loaded or the factory fails.
     */
    static std::unique_ptr<IImageProcessingEngine> CreateFromPlugin(
        const std::string& plugin_path);

    /**
     * @brief Create the built-in default engine
     * @return unique_ptr to the default engine
     *
     * Creates an instance of the DefaultImageProcessingEngine.
     */
    static std::unique_ptr<IImageProcessingEngine> CreateDefault();

    /**
     * @brief Create an engine (automatic selection)
     * @param engine_plugin_path Path to plugin DLL, or empty for default
     * @return unique_ptr to the engine, or nullptr on failure
     *
     * If engine_plugin_path is empty, creates the default engine.
     * Otherwise, loads from the specified plugin.
     */
    static std::unique_ptr<IImageProcessingEngine> Create(
        const std::string& engine_plugin_path = "");
};

// =============================================================================
// Plugin ABI Contract
// =============================================================================

/**
 * @brief Plugin DLL ABI contract
 *
 * Each engine plugin DLL must export the following C-linkage functions:
 *
 * extern "C" {
 *     IImageProcessingEngine* CreateImageProcessingEngine();
 *     void DestroyImageProcessingEngine(IImageProcessingEngine* engine);
 * }
 *
 * The extern "C" linkage prevents C++ name-mangling and ensures ABI
 * compatibility across different compilers.
 *
 * The DestroyImageProcessingEngine export is required to ensure that
 * the engine object is destroyed in the same DLL's heap, preventing
 * cross-DLL heap corruption.
 */

} // namespace hnvue::imaging

#endif // HNUE_IMAGING_IIMAGE_PROCESSING_ENGINE_H
