/**
 * @file IGenerator.h
 * @brief Pure abstract interface for High Voltage Generator (HVG) control
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: Generator control
 * SPDX-License-Identifier: MIT
 *
 * All implementations must be reviewed and tested under IEC 62304 §5.5–5.7.
 * This interface is pure abstract with no non-virtual methods to satisfy
 * NFR-HAL-03 mockability requirements for SOUP-isolated unit testing.
 */

#ifndef HNUE_HAL_IGENERATOR_H
#define HNUE_HAL_IGENERATOR_H

#include "HalTypes.h"

#include <memory>
#include <system_error>

namespace hnvue::hal {

/**
 * @brief Pure abstract interface for High Voltage Generator control
 *
 * This interface mirrors the HvgControl protobuf service semantics
 * for in-process use. Implementations include:
 * - GeneratorRS232Impl: Serial communication
 * - GeneratorEthernetImpl: TCP/IP communication
 * - GeneratorSimulator: Development/testing simulator
 *
 * Safety Classification: IEC 62304 Class C
 * Software failure can cause serious injury via radiation overdose.
 *
 * Thread Safety:
 * - GetStatus() is thread-safe for concurrent calls
 * - SetExposureParams(), StartExposure(), AbortExposure() are internally synchronized
 * - Alarm callbacks are invoked on a background thread
 */
class IGenerator {
public:
    /**
     * @brief Virtual destructor for proper cleanup
     */
    virtual ~IGenerator() = default;

    // =========================================================================
    // Query Methods
    // =========================================================================

    /**
     * @brief Query current generator status
     * @return HvgStatus structure with current kVp, mA, state, interlock
     *
     * Thread-safe, non-blocking. Returns cached status from last
     * status update from the physical device.
     */
    virtual HvgStatus GetStatus() = 0;

    // =========================================================================
    // Exposure Control (SAFETY CRITICAL)
    // =========================================================================

    /**
     * @brief Configure exposure parameters before arming
     * @param params Exposure parameters (kVp, mA, ms, AEC mode, focus)
     * @return true if parameters accepted and configured
     *
     * Must be called before StartExposure(). Parameters are validated
     against device capabilities (HvgCapabilities).
     *
     * Thread-safe, internally synchronized.
     */
    virtual bool SetExposureParams(const ExposureParams& params) = 0;

    /**
     * @brief Initiate X-ray exposure
     * @return ExposureResult with actual exposure values or error
     *
     * Blocks until exposure begins or fails to arm.
     * Pre-conditions:
     * - SetExposureParams() must have been called successfully
     * - All safety interlocks must be pass (CheckAllInterlocks())
     * - Generator must be in READY state
     *
     * Thread-safe, internally synchronized.
     */
    virtual ExposureResult StartExposure() = 0;

    /**
     * @brief Abort in-progress exposure immediately
     *
     * SAFETY CRITICAL: Must return within 10 ms to ensure rapid
     * termination of X-ray emission on emergency stop.
     *
     * Safe to call when no exposure is active (no-op).
     *
     * Thread-safe, internally synchronized.
     */
    virtual void AbortExposure() = 0;

    // =========================================================================
    // Callback Registration
    // =========================================================================

    /**
     * @brief Register callback for asynchronous alarm delivery
     * @param callback Function to call for each alarm event
     *
     * Alarm delivery latency must be <= 50 ms from hardware alarm
     * detection to callback invocation (FR-HAL-06).
     *
     * Thread-safe: Multiple callbacks can be registered; all will be
     * invoked for each alarm. Exception thrown by one callback does not
     * prevent invocation of remaining callbacks.
     */
    virtual void RegisterAlarmCallback(AlarmCallback callback) = 0;

    /**
     * @brief Register callback for status updates
     * @param callback Function to call for each status update
     *
     * Status stream rate must be >= 10 Hz during active exposure
     * (FR-HAL-06).
     *
     * Thread-safe: Multiple callbacks can be registered.
     */
    virtual void RegisterStatusCallback(StatusCallback callback) = 0;

    // =========================================================================
    // Capabilities
    // =========================================================================

    /**
     * @brief Query static generator device capabilities
     * @return HvgCapabilities structure with limits and features
     *
     * Thread-safe, non-blocking. Returns cached information available
     * immediately after initialization.
     */
    virtual HvgCapabilities GetCapabilities() = 0;
};

/**
 * @brief Factory for creating IGenerator implementations
 *
 * Loads vendor HVG SDK implementations from plugin path.
 * Returns nullptr on failure; error details available via GetLastError().
 *
 * @param vendor_plugin_path Path to vendor plugin DLL
 * @return Unique pointer to IGenerator implementation, or nullptr on failure
 */
using GeneratorFactory = std::unique_ptr<IGenerator>(*)(const std::string& vendor_plugin_path);

} // namespace hnvue::hal

#endif // HNUE_HAL_IGENERATOR_H
