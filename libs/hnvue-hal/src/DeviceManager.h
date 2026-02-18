/**
 * @file DeviceManager.h
 * @brief Single entry point for all hardware interfaces
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: Device lifecycle management
 * SPDX-License-Identifier: MIT
 *
 * DeviceManager provides unified access to all hardware interfaces,
 * manages initialization order, handles graceful shutdown, and propagates
 * errors to the application layer.
 */

#ifndef HNUE_HAL_DEVICE_MANAGER_H
#define HNUE_HAL_DEVICE_MANAGER_H

#include "hnvue/hal/IDetector.h"
#include "hnvue/hal/IGenerator.h"
#include "hnvue/hal/ICollimator.h"
#include "hnvue/hal/IPatientTable.h"
#include "hnvue/hal/IAEC.h"
#include "hnvue/hal/IDoseMonitor.h"
#include "hnvue/hal/ISafetyInterlock.h"
#include "hnvue/hal/HalTypes.h"

#include <functional>
#include <memory>
#include <string>
#include <vector>

namespace hnvue::hal {

/**
 * @brief Error handler callback type
 *
 * Called when critical hardware errors occur that require
 * application-level intervention.
 */
using ErrorHandler = std::function<void(HalError, const std::string&)>;

/**
 * @brief Device lifecycle manager
 *
 * Single entry point for the application to access all hardware interfaces.
 * Responsible for:
 * - Loading device configuration from JSON
 * - Initializing all devices in correct order
 * - Managing device lifecycle
 * - Providing typed accessors to interfaces
 * - Graceful shutdown (reverse order)
 * - Propagating errors to application
 *
 * Safety Classification: IEC 62304 Class C
 * Device lifecycle management affects system safety.
 *
 * Thread Safety:
 * - Initialization and shutdown are NOT thread-safe
 * - Typed accessors are thread-safe after initialization
 * - Error handler may be invoked from any thread
 */
class DeviceManager {
public:
    /**
     * @brief Construct DeviceManager
     */
    DeviceManager();

    /**
     * @brief Destructor - ensures shutdown
     */
    ~DeviceManager();

    // Disable copy construction and assignment
    DeviceManager(const DeviceManager&) = delete;
    DeviceManager& operator=(const DeviceManager&) = delete;

    // =========================================================================
    // Initialization and Configuration
    // =========================================================================

    /**
     * @brief Load configuration and initialize all devices
     * @param config_path Path to JSON configuration file
     * @return true if initialization successful
     *
     * Configuration JSON structure:
     * {
     *   "generator": {
     *     "type": "simulator",
     *     "port": "COM1",
     *     "baud_rate": 115200
     *   },
     *   "detector": {
     *     "plugin_path": "plugins/hnvue-hal-detector-vendor.dll",
     *     "enabled": false
     *   },
     *   "aec": {
     *     "mode": "AEC_AUTO",
     *     "threshold_percent": 50.0
     *   }
     * }
     *
     * Initialization order (dependency-respecting):
     * 1. Generator (base device)
     * 2. Detector plugin (if enabled)
     * 3. AEC Controller (depends on generator)
     * 4. Dose Monitor
     * 5. Safety Interlock (aggregates all devices)
     * 6. Collimator (peripheral)
     * 7. Patient Table (peripheral)
     */
    bool Initialize(const std::string& config_path);

    /**
     * @brief Check if manager is initialized
     * @return true if Initialize() completed successfully
     */
    bool IsInitialized() const;

    // =========================================================================
    // Typed Device Accessors
    // =========================================================================

    /**
     * @brief Get generator interface
     * @return IGenerator pointer or nullptr if not initialized
     */
    IGenerator* GetGenerator();

    /**
     * @brief Get detector interface
     * @return IDetector pointer or nullptr if not loaded
     */
    IDetector* GetDetector();

    /**
     * @brief Get collimator interface
     * @return ICollimator pointer or nullptr if not initialized
     */
    ICollimator* GetCollimator();

    /**
     * @brief Get patient table interface
     * @return IPatientTable pointer or nullptr if not initialized
     */
    IPatientTable* GetPatientTable();

    /**
     * @brief Get AEC interface
     * @return IAEC pointer or nullptr if not initialized
     */
    IAEC* GetAEC();

    /**
     * @brief Get dose monitor interface
     * @return IDoseMonitor pointer or nullptr if not initialized
     */
    IDoseMonitor* GetDoseMonitor();

    /**
     * @brief Get safety interlock interface
     * @return ISafetyInterlock pointer or nullptr if not initialized
     */
    ISafetyInterlock* GetSafetyInterlock();

    // =========================================================================
    // Shutdown and Error Handling
    // =========================================================================

    /**
     * @brief Graceful shutdown of all devices
     *
     * Shutdown order (reverse of initialization):
     * 1. Patient Table
     * 2. Collimator
     * 3. Safety Interlock
     * 4. Dose Monitor
     * 5. AEC Controller
     * 6. Detector
     * 7. Generator
     *
     * Safe to call multiple times (idempotent).
     */
    void Shutdown();

    /**
     * @brief Register error handler for critical hardware errors
     * @param handler Function to call on hardware errors
     *
     * Error handler may be invoked from any thread when device
     * errors occur. Handler should be thread-safe.
     */
    void RegisterErrorHandler(ErrorHandler handler);

private:
    // Device instances (owned pointers)
    std::unique_ptr<IGenerator> generator_;
    std::unique_ptr<IDetector> detector_;
    std::unique_ptr<ICollimator> collimator_;
    std::unique_ptr<IPatientTable> patient_table_;
    std::unique_ptr<IAEC> aec_;
    std::unique_ptr<IDoseMonitor> dose_monitor_;
    std::unique_ptr<ISafetyInterlock> safety_interlock_;

    // State
    bool initialized_;

    // Error handling
    ErrorHandler error_handler_;

    // Helper methods
    bool LoadConfiguration(const std::string& config_path);
    bool InitializeGenerator(const std::string& type, const std::string& port, int baud_rate);
    bool InitializeDetector(const std::string& plugin_path, bool enabled);
    bool InitializeAEC(AecMode mode, float threshold);
    void ReportError(HalError error, const std::string& message);

    // Simple JSON parsing helpers (for GREEN phase)
    static std::string ExtractJsonString(const std::string& json, const std::string& key, const std::string& default_value);
    static int ExtractJsonInt(const std::string& json, const std::string& key, int default_value);
    static float ExtractJsonFloat(const std::string& json, const std::string& key, float default_value);
    static bool ExtractJsonBool(const std::string& json, const std::string& key, bool default_value);
};

} // namespace hnvue::hal

#endif // HNUE_HAL_DEVICE_MANAGER_H
