/**
 * @file DetectorPluginLoader.h
 * @brief Dynamic loader for detector plugin DLLs
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Plugin loader with ABI validation
 * SPDX-License-Identifier: MIT
 *
 * Implements FR-HAL-01 (vendor detector integration) and FR-HAL-03
 * (plugin architecture) by providing dynamic loading of vendor detector
 * adapter DLLs with ABI compatibility checking.
 */

#ifndef HNUE_HAL_DETECTOR_PLUGIN_LOADER_H
#define HNUE_HAL_DETECTOR_PLUGIN_LOADER_H

#include "hnvue/hal/PluginAbi.h"
#include "hnvue/hal/HalTypes.h"

#include <memory>
#include <string>
#include <vector>
#include <unordered_map>
#include <mutex>

// Platform-specific dynamic library loading
#ifdef _WIN32
    #include <windows.h>
    using HLibrary = HMODULE;
#else
    #include <dlfcn.h>
    using HLibrary = void*;
#endif

namespace hnvue::hal {

// =============================================================================
// Forward Declarations
// =============================================================================

class IDetector;

// =============================================================================
// Plugin Handle
// =============================================================================

/**
 * @brief Plugin state enumeration
 */
enum class PluginState : int32_t {
    UNLOADED = 0,    ///< Plugin not loaded
    LOADED = 1,      ///< DLL loaded, detector instance created
    INITIALIZED = 2, ///< Detector ready for use
    ERROR = 3        ///< Plugin in error state
};

/**
 * @brief Plugin information structure
 */
struct PluginInfo {
    std::string plugin_path;        ///< Path to plugin DLL
    PluginManifest manifest;        ///< Plugin manifest from GetPluginManifest()
    PluginState state = PluginState::UNLOADED;
    std::string error_message;      ///< Error description if in ERROR state
};

/**
 * @brief Opaque handle to loaded plugin
 *
 * Internally contains:
 * - Loaded DLL handle (HMODULE on Windows, void* on Linux)
 * - IDetector instance pointer
 * - PluginInfo metadata
 * - Factory function pointers
 */
class PluginHandle {
public:
    PluginHandle(HLibrary lib, IDetector* detector,
                 CreateDetectorFn create_fn, DestroyDetectorFn destroy_fn,
                 GetLastErrorFn error_fn, const PluginInfo& info);

    ~PluginHandle();

    // Non-copyable
    PluginHandle(const PluginHandle&) = delete;
    PluginHandle& operator=(const PluginHandle&) = delete;

    // Movable
    PluginHandle(PluginHandle&& other) noexcept;
    PluginHandle& operator=(PluginHandle&& other) noexcept;

    // Getters
    HLibrary GetLibraryHandle() const { return library_; }
    IDetector* GetDetector() const { return detector_; }
    const PluginInfo& GetInfo() const { return info_; }
    bool IsValid() const { return library_ != nullptr; }

private:
    HLibrary library_;
    IDetector* detector_;
    CreateDetectorFn create_fn_;
    DestroyDetectorFn destroy_fn_;
    GetLastErrorFn error_fn_;
    PluginInfo info_;
};

// =============================================================================
// Plugin Loader
// =============================================================================

/**
 * @brief Dynamic loader for detector plugin DLLs
 *
 * Loads vendor detector adapter DLLs, validates ABI compatibility,
 * and manages plugin lifecycle (load, unload, reload).
 *
 * Thread Safety:
 * - All public methods are thread-safe
 * - Internal state protected by mutex
 * - Plugin loading/unloading is serialized
 *
 * Exception Safety:
 * - All methods are noexcept or provide exception boundary
 * - Plugin crashes are isolated and reported via PluginLoadError
 *
 * Usage Example:
 * @code
 *   DetectorPluginLoader loader;
 *
 *   // Load plugin
 *   auto handle = loader.LoadPlugin("/path/to/vendor_detector.dll");
 *   if (handle && handle->GetDetector()) {
 *       IDetector* detector = handle->GetDetector();
 *       DetectorInfo info = detector->GetDetectorInfo();
 *       // Use detector...
 *   }
 *
 *   // Unload when done
 *   loader.UnloadPlugin(handle.get());
 * @endcode
 */
class DetectorPluginLoader {
public:
    /**
     * @brief Constructor
     */
    DetectorPluginLoader();

    /**
     * @brief Destructor - unloads all remaining plugins
     */
    ~DetectorPluginLoader();

    // Non-copyable, non-movable
    DetectorPluginLoader(const DetectorPluginLoader&) = delete;
    DetectorPluginLoader& operator=(const DetectorPluginLoader&) = delete;
    DetectorPluginLoader(DetectorPluginLoader&&) = delete;
    DetectorPluginLoader& operator=(DetectorPluginLoader&&) = delete;

    // =========================================================================
    // Plugin Loading
    // =========================================================================

    /**
     * @brief Load plugin from file path
     * @param plugin_path Path to plugin DLL file
     * @return Shared pointer to PluginHandle, or nullptr on failure
     *
     * Process:
     * 1. Load DLL using OS-specific API (LoadLibrary/dlopen)
     * 2. Resolve required symbols: CreateDetector, DestroyDetector, GetPluginManifest
     * 3. Validate API version compatibility
     * 4. Call CreateDetector to instantiate IDetector
     * 5. Store handle in loaded plugins map
     *
     * Error Handling:
     * - Returns nullptr on any failure
     * - Logs detailed error information
     * - Does not throw exceptions
     *
     * Thread Safety:
     * - Thread-safe, internally synchronized
     * - Multiple threads can load plugins concurrently
     *
     * Ownership:
     * - Returns shared_ptr for shared ownership between loader and caller
     * - Plugin remains loaded as long as any shared_ptr reference exists
     * - Loader also maintains a weak reference for management
     */
    std::shared_ptr<PluginHandle> LoadPlugin(const std::string& plugin_path);

    /**
     * @brief Unload plugin by handle pointer
     * @param handle Pointer to plugin handle (obtained from LoadPlugin)
     * @return true if plugin unloaded successfully
     *
     * Process:
     * 1. Remove from loader's internal tracking
     * 2. Plugin will be unloaded when all shared_ptr references are released
     *
     * Thread Safety:
     * - Thread-safe, internally synchronized
     * - Safe to call from any thread
     *
     * Note:
     * - Plugin may not be immediately unloaded if caller holds a reference
     * - Returns true if plugin was found in loader's tracking
     */
    bool UnloadPlugin(PluginHandle* handle);

    /**
     * @brief Reload plugin (unload then load again)
     * @param plugin_path Path to plugin DLL file
     * @return Shared pointer to new PluginHandle, or nullptr on failure
     *
     * Process:
     * 1. Find existing handle by plugin path
     * 2. Unload existing plugin if found
     * 3. Load plugin again
     * 4. Return new handle
     *
     * Use Cases:
     * - Plugin update without restart
     * - Error recovery
     * - Configuration reload
     *
     * Thread Safety:
     * - Thread-safe, internally synchronized
     */
    std::shared_ptr<PluginHandle> ReloadPlugin(const std::string& plugin_path);

    // =========================================================================
    // Plugin Discovery
    // =========================================================================

    /**
     * @brief Get list of all loaded plugins
     * @return Vector of PluginInfo for all loaded plugins
     *
     * Thread Safety:
     * - Thread-safe, returns copy of internal state
     */
    std::vector<PluginInfo> GetLoadedPlugins() const;

    /**
     * @brief Find plugin by vendor name
     * @param vendor_name Vendor name to search for
     * @return Pointer to plugin handle, or nullptr if not found
     *
     * Searches loaded plugins for matching vendor name.
     * Returns first match if multiple plugins from same vendor exist.
     *
     * Thread Safety:
     * - Thread-safe, returns pointer to const handle
     * - Caller should not store pointer across unload operations
     */
    PluginHandle* FindPlugin(const std::string& vendor_name);

    // =========================================================================
    // Error Reporting
    // =========================================================================

    /**
     * @brief Get last load error
     * @return PluginLoadError structure with details of last failure
     *
     * Thread Safety:
     * - Thread-safe, returns copy of last error
     */
    PluginLoadError GetLastError() const;

    /**
     * @brief Clear last error
     *
     * Thread Safety:
     * - Thread-safe
     */
    void ClearLastError();

private:
    // =========================================================================
    // Internal Helper Methods
    // =========================================================================

    /**
     * @brief Load DLL using platform-specific API
     * @param path Path to DLL file
     * @return Library handle, or nullptr on failure
     */
    HLibrary LoadLibrary(const std::string& path);

    /**
     * @brief Unload DLL using platform-specific API
     * @param lib Library handle to unload
     * @return true if unloaded successfully
     */
    bool UnloadLibrary(HLibrary lib);

    /**
     * @brief Resolve symbol from loaded library
     * @param lib Library handle
     * @param symbol_name Name of symbol to resolve
     * @return Pointer to symbol, or nullptr if not found
     */
    void* GetSymbol(HLibrary lib, const std::string& symbol_name);

    /**
     * @brief Validate plugin exports all required symbols
     * @param lib Library handle
     * @param error Output error details on failure
     * @return true if all required symbols found
     */
    bool ValidateSymbols(HLibrary lib, PluginLoadError& error);

    /**
     * @brief Validate plugin API version compatibility
     * @param manifest Plugin manifest to validate
     * @param error Output error details on incompatibility
     * @return true if plugin is compatible
     */
    bool ValidateVersion(const PluginManifest& manifest, PluginLoadError& error);

    /**
     * @brief Create detector instance using factory function
     * @param create_fn CreateDetector function pointer
     * @param config Plugin configuration
     * @return IDetector instance, or nullptr on failure
     */
    IDetector* CreateDetectorInstance(CreateDetectorFn create_fn,
                                      const PluginConfig* config);

    /**
     * @brief Destroy detector instance using factory function
     * @param detector IDetector instance to destroy
     * @param destroy_fn DestroyDetector function pointer
     */
    void DestroyDetectorInstance(IDetector* detector, DestroyDetectorFn destroy_fn);

    /**
     * @brief Set last error with details
     * @param code Error result code
     * @param message Human-readable error description
     * @param plugin_path Path to plugin that failed
     * @param lib Library handle for GetLastError() call
     */
    void SetLastError(PluginLoadResult code, const std::string& message,
                     const std::string& plugin_path, HLibrary lib = nullptr);

    // =========================================================================
    // Member Variables
    // =========================================================================

    mutable std::mutex mutex_;                              ///< Protects all state
    std::unordered_map<std::string, std::weak_ptr<PluginHandle>> loaded_plugins_;
    PluginLoadError last_error_;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_DETECTOR_PLUGIN_LOADER_H
