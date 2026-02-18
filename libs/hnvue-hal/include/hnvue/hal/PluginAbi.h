/**
 * @file PluginAbi.h
 * @brief C-linkage ABI contract for binary-compatible plugin loading
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Plugin interface
 * SPDX-License-Identifier: MIT
 *
 * Defines the C-linkage factory functions and data structures for
 * loading vendor detector adapter DLLs compiled with different toolchains.
 *
 * The C++ interface (IDetector) is hidden behind the C ABI boundary
 * to eliminate C++ ABI compatibility issues between toolchain versions.
 */

#ifndef HNUE_HAL_PLUGINABI_H
#define HNUE_HAL_PLUGINABI_H

#include "HalTypes.h"

#include <cstdint>

// All plugin ABI functions use C linkage to ensure binary compatibility
extern "C" {

// =============================================================================
// Version Information
// =============================================================================

/**
 * @brief HAL API version number
 *
 * Encoded as 0xMMmmpppp (Major, Minor, Patch)
 * Current version: 0x01000000 = v1.0.0
 */
#define HNUE_HAL_API_VERSION 0x01000000

/**
 * @brief Extract major version from version number
 */
#define HNUE_HAL_VERSION_MAJOR(v) ((v) >> 24)

/**
 * @brief Extract minor version from version number
 */
#define HNUE_HAL_VERSION_MINOR(v) (((v) >> 16) & 0xFF)

/**
 * @brief Extract patch version from version number
 */
#define HNUE_HAL_VERSION_PATCH(v) ((v) & 0xFFFF)

// =============================================================================
// Plugin Factory Functions (Required Exports)
// =============================================================================

/**
 * @brief Create detector plugin instance
 *
 * This function MUST be exported by all detector plugin DLLs.
 * Called by DetectorPluginLoader to instantiate the detector adapter.
 *
 * @param config Pointer to plugin configuration (may be nullptr)
 * @return Pointer to IDetector interface, or nullptr on failure
 *
 * Error Handling:
 * - Returns nullptr if initialization fails
 * - Caller can call GetLastError() for error details (if provided by plugin)
 *
 * Thread Safety:
 * - This function is called during plugin loading, not concurrently
 * - The returned IDetector instance must support concurrent method calls
 *
 * Memory Management:
 * - Caller does NOT own the returned pointer
 * - Plugin must destroy the instance when DestroyDetector() is called
 */
struct IDetector;  // Forward declaration (actual definition in IDetector.h)
using CreateDetectorFn = IDetector*(*)(const PluginConfig* config);

/**
 * @brief Destroy detector plugin instance
 *
 * This function MUST be exported by all detector plugin DLLs.
 * Called by DetectorPluginLoader during plugin unloading.
 *
 * @param detector Pointer to IDetector instance to destroy
 *
 * Thread Safety:
 * - This function is called during plugin unloading, not concurrently
 * - No method calls on the detector pointer may follow this call
 *
 * Memory Management:
 * - Plugin must release all resources associated with the detector
 * - Caller must not use the detector pointer after this call
 */
using DestroyDetectorFn = void(*)(IDetector* detector);

/**
 * @brief Get plugin manifest
 *
 * This function MUST be exported by all detector plugin DLLs.
 * Called by DetectorPluginLoader to validate plugin compatibility.
 *
 * @return Pointer to static PluginManifest structure
 *
 * Thread Safety:
 * - This function may be called multiple times
 * - Returned pointer must remain valid for plugin lifetime
 *
 * Memory Management:
 * - Returns pointer to static storage (do not free)
 */
using GetPluginManifestFn = const PluginManifest*(*)();

// =============================================================================
// Optional Error Reporting
// =============================================================================

/**
 * @brief Get last error message from plugin
 *
 * Optional export for improved error reporting.
 * If provided, called after CreateDetector() returns nullptr.
 *
 * @return Null-terminated error message string, or nullptr if no error
 *
 * Thread Safety:
 * - Returns pointer to thread-local or static storage
 * - Caller should copy the string if needed for later use
 *
 * Memory Management:
 * - Returns pointer to internal storage (do not free)
 * - Contents valid until next plugin call
 */
using GetLastErrorFn = const char*(*)();

// =============================================================================
// ABI Validation Helpers
// =============================================================================

/**
 * @brief Check if plugin API version is compatible
 * @param plugin_version Plugin's reported API version
 * @return true if plugin is compatible with current HAL API
 *
 * Compatibility rules:
 * - Major version must match exactly
 * - Minor version may be greater (plugin is newer)
 * - Patch version is ignored for compatibility
 */
inline bool IsPluginVersionCompatible(uint32_t plugin_version) {
    return HNUE_HAL_VERSION_MAJOR(plugin_version) ==
           HNUE_HAL_VERSION_MAJOR(HNUE_HAL_API_VERSION);
}

} // extern "C"

// =============================================================================
// Plugin Loader Integration
// =============================================================================

namespace hnvue::hal {

/**
 * @brief Plugin loader result codes
 */
enum class PluginLoadResult : int32_t {
    SUCCESS = 0,              ///< Plugin loaded successfully
    ERR_FILE_NOT_FOUND = 1,   ///< Plugin DLL not found
    ERR_MISSING_SYMBOL = 2,   ///< Required export not found
    ERR_VERSION_MISMATCH = 3, ///< API version incompatible
    ERR_INIT_FAILED = 4,      ///< CreateDetector() returned nullptr
    ERR_VALIDATION_FAILED = 5 ///< Plugin manifest validation failed
};

/**
 * @brief Plugin load error details
 */
struct PluginLoadError {
    PluginLoadResult code = PluginLoadResult::SUCCESS;
    std::string message;       ///< Human-readable error description
    std::string plugin_path;   ///< Path to plugin that failed to load
    std::string last_error;    ///< Error from GetLastError() if available
};

} // namespace hnvue::hal

#endif // HNUE_HAL_PLUGINABI_H
