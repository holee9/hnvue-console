/**
 * @file DetectorPluginLoader.cpp
 * @brief Dynamic loader for detector plugin DLLs implementation
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Plugin loader implementation
 * SPDX-License-Identifier: MIT
 */

#include "plugin/DetectorPluginLoader.h"
#include "hnvue/hal/IDetector.h"

#include <spdlog/spdlog.h>
#include <spdlog/fmt/fmt.h>

#include <filesystem>

namespace fs = std::filesystem;
namespace hal = hnvue::hal;

// =============================================================================
// PluginHandle Implementation
// =============================================================================

namespace hnvue::hal {

PluginHandle::PluginHandle(HLibrary lib, IDetector* detector,
                           CreateDetectorFn create_fn, DestroyDetectorFn destroy_fn,
                           GetLastErrorFn error_fn, const PluginInfo& info)
    : library_(lib)
    , detector_(detector)
    , create_fn_(create_fn)
    , destroy_fn_(destroy_fn)
    , error_fn_(error_fn)
    , info_(info)
{
    if (detector_) {
        info_.state = PluginState::INITIALIZED;
    } else {
        info_.state = PluginState::ERROR;
        info_.error_message = "Detector instance is null";
    }
}

PluginHandle::~PluginHandle() {
    // Destroy detector instance if still valid
    if (detector_ && destroy_fn_) {
        try {
            destroy_fn_(detector_);
            spdlog::debug("Destroyed detector instance for plugin: {}",
                         info_.plugin_path);
        } catch (const std::exception& e) {
            spdlog::error("Exception destroying detector: {}", e.what());
        } catch (...) {
            spdlog::error("Unknown exception destroying detector");
        }
        detector_ = nullptr;
    }

    // Unload library if still loaded
    if (library_ != nullptr) {
#ifdef _WIN32
        FreeLibrary(library_);
#else
        dlclose(library_);
#endif
        spdlog::debug("Unloaded library: {}", info_.plugin_path);
        library_ = nullptr;
    }

    info_.state = PluginState::UNLOADED;
}

PluginHandle::PluginHandle(PluginHandle&& other) noexcept
    : library_(other.library_)
    , detector_(other.detector_)
    , create_fn_(other.create_fn_)
    , destroy_fn_(other.destroy_fn_)
    , error_fn_(other.error_fn_)
    , info_(std::move(other.info_))
{
    // Clear source object
    other.library_ = nullptr;
    other.detector_ = nullptr;
    other.create_fn_ = nullptr;
    other.destroy_fn_ = nullptr;
    other.error_fn_ = nullptr;
    other.info_ = PluginInfo{};
}

PluginHandle& PluginHandle::operator=(PluginHandle&& other) noexcept {
    if (this != &other) {
        // Clean up existing resources
        this->~PluginHandle();

        // Move from other
        library_ = other.library_;
        detector_ = other.detector_;
        create_fn_ = other.create_fn_;
        destroy_fn_ = other.destroy_fn_;
        error_fn_ = other.error_fn_;
        info_ = std::move(other.info_);

        // Clear source object
        other.library_ = nullptr;
        other.detector_ = nullptr;
        other.create_fn_ = nullptr;
        other.destroy_fn_ = nullptr;
        other.error_fn_ = nullptr;
        other.info_ = PluginInfo{};
    }
    return *this;
}

// =============================================================================
// DetectorPluginLoader Implementation
// =============================================================================

DetectorPluginLoader::DetectorPluginLoader() {
    spdlog::debug("DetectorPluginLoader created");
}

DetectorPluginLoader::~DetectorPluginLoader() {
    spdlog::debug("DetectorPluginLoader destroying, clearing plugin references");

    std::lock_guard<std::mutex> lock(mutex_);

    // Clear all weak references
    // Plugins will be unloaded when all shared_ptr references are released
    loaded_plugins_.clear();
}

std::shared_ptr<PluginHandle> DetectorPluginLoader::LoadPlugin(
    const std::string& plugin_path)
{
    std::lock_guard<std::mutex> lock(mutex_);

    spdlog::info("Loading plugin: {}", plugin_path);

    // Clear previous error
    last_error_ = PluginLoadError{};

    // Check if file exists
    if (!fs::exists(plugin_path)) {
        SetLastError(PluginLoadResult::ERR_FILE_NOT_FOUND,
                    "Plugin file not found", plugin_path);
        return nullptr;
    }

    // Load library
    HLibrary lib = LoadLibrary(plugin_path);
    if (lib == nullptr) {
        SetLastError(PluginLoadResult::ERR_FILE_NOT_FOUND,
                    "Failed to load DLL", plugin_path);
        return nullptr;
    }

    // Validate required symbols
    if (!ValidateSymbols(lib, last_error_)) {
        UnloadLibrary(lib);
        last_error_.plugin_path = plugin_path;
        return nullptr;
    }

    // Get symbol pointers
    auto create_fn = reinterpret_cast<CreateDetectorFn>(
        GetSymbol(lib, "CreateDetector"));
    auto destroy_fn = reinterpret_cast<DestroyDetectorFn>(
        GetSymbol(lib, "DestroyDetector"));
    auto manifest_fn = reinterpret_cast<GetPluginManifestFn>(
        GetSymbol(lib, "GetPluginManifest"));
    auto error_fn = reinterpret_cast<GetLastErrorFn>(
        GetSymbol(lib, "GetLastError"));

    // Get and validate manifest
    const PluginManifest* manifest = manifest_fn();
    if (!manifest) {
        UnloadLibrary(lib);
        SetLastError(PluginLoadResult::ERR_VALIDATION_FAILED,
                    "GetPluginManifest returned null", plugin_path);
        return nullptr;
    }

    if (!ValidateVersion(*manifest, last_error_)) {
        UnloadLibrary(lib);
        last_error_.plugin_path = plugin_path;
        return nullptr;
    }

    // Create detector instance
    PluginConfig config;
    config.plugin_path = plugin_path;

    IDetector* detector = CreateDetectorInstance(create_fn, &config);
    if (!detector) {
        std::string error_msg;
        if (error_fn) {
            const char* err = error_fn();
            error_msg = err ? err : "Unknown error";
        } else {
            error_msg = "CreateDetector returned null";
        }

        UnloadLibrary(lib);
        SetLastError(PluginLoadResult::ERR_INIT_FAILED,
                    error_msg, plugin_path);
        return nullptr;
    }

    // Create plugin info
    PluginInfo info;
    info.plugin_path = plugin_path;
    info.manifest = *manifest;
    info.state = PluginState::INITIALIZED;

    // Create shared handle
    auto handle = std::make_shared<PluginHandle>(
        lib, detector, create_fn, destroy_fn, error_fn, info);

    // Store weak reference in loaded plugins map
    loaded_plugins_[plugin_path] = handle;

    spdlog::info("Plugin loaded successfully: {} ({})",
                 manifest->plugin_name, plugin_path);

    return handle;
}

bool DetectorPluginLoader::UnloadPlugin(PluginHandle* handle) {
    if (!handle) {
        spdlog::warn("UnloadPlugin called with null handle");
        return false;
    }

    std::lock_guard<std::mutex> lock(mutex_);

    const std::string& plugin_path = handle->GetInfo().plugin_path;

    spdlog::info("Unloading plugin: {}", plugin_path);

    auto it = loaded_plugins_.find(plugin_path);
    if (it == loaded_plugins_.end()) {
        spdlog::warn("Plugin not found in loaded plugins: {}", plugin_path);
        return false;
    }

    // Remove weak reference from map
    // Plugin will be unloaded when all shared_ptr references are released
    loaded_plugins_.erase(it);

    spdlog::info("Plugin unloaded from loader: {}", plugin_path);
    return true;
}

std::shared_ptr<PluginHandle> DetectorPluginLoader::ReloadPlugin(
    const std::string& plugin_path)
{
    spdlog::info("Reloading plugin: {}", plugin_path);

    // Remove existing weak reference if found
    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto it = loaded_plugins_.find(plugin_path);
        if (it != loaded_plugins_.end()) {
            spdlog::debug("Removing existing plugin reference before reload: {}", plugin_path);
            loaded_plugins_.erase(it);
        }
    }

    // Load again
    return LoadPlugin(plugin_path);
}

std::vector<PluginInfo> DetectorPluginLoader::GetLoadedPlugins() const {
    std::lock_guard<std::mutex> lock(mutex_);

    std::vector<PluginInfo> plugins;
    plugins.reserve(loaded_plugins_.size());

    for (const auto& [path, weak_handle] : loaded_plugins_) {
        // Lock weak_ptr to get shared_ptr
        if (auto handle = weak_handle.lock()) {
            plugins.push_back(handle->GetInfo());
        }
    }

    return plugins;
}

PluginHandle* DetectorPluginLoader::FindPlugin(const std::string& vendor_name) {
    std::lock_guard<std::mutex> lock(mutex_);

    for (auto& [path, weak_handle] : loaded_plugins_) {
        // Lock weak_ptr to get shared_ptr
        if (auto handle = weak_handle.lock()) {
            if (handle->GetInfo().manifest.vendor_name == vendor_name) {
                return handle.get();
            }
        }
    }

    return nullptr;
}

PluginLoadError DetectorPluginLoader::GetLastError() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return last_error_;
}

void DetectorPluginLoader::ClearLastError() {
    std::lock_guard<std::mutex> lock(mutex_);
    last_error_ = PluginLoadError{};
}

// =============================================================================
// Private Helper Methods
// =============================================================================

HLibrary DetectorPluginLoader::LoadLibrary(const std::string& path) {
#ifdef _WIN32
    HMODULE lib = LoadLibraryA(path.c_str());
    if (!lib) {
        DWORD error = GetLastError();
        spdlog::error("LoadLibrary failed for {}: error code {}", path, error);
    }
    return lib;
#else
    void* lib = dlopen(path.c_str(), RTLD_LAZY);
    if (!lib) {
        spdlog::error("dlopen failed for {}: {}", path, dlerror());
    }
    return lib;
#endif
}

bool DetectorPluginLoader::UnloadLibrary(HLibrary lib) {
    if (!lib) {
        return false;
    }

#ifdef _WIN32
    BOOL result = FreeLibrary(lib);
    if (!result) {
        DWORD error = GetLastError();
        spdlog::error("FreeLibrary failed: error code {}", error);
    }
    return result != FALSE;
#else
    int result = dlclose(lib);
    if (result != 0) {
        spdlog::error("dlclose failed: {}", dlerror());
    }
    return result == 0;
#endif
}

void* DetectorPluginLoader::GetSymbol(HLibrary lib, const std::string& symbol_name) {
#ifdef _WIN32
    FARPROC sym = GetProcAddress(lib, symbol_name.c_str());
    if (!sym) {
        spdlog::error("GetProcAddress failed for symbol: {}", symbol_name);
    }
    return reinterpret_cast<void*>(sym);
#else
    void* sym = dlsym(lib, symbol_name.c_str());
    if (!sym) {
        spdlog::error("dlsym failed for symbol: {}", dlerror());
    }
    return sym;
#endif
}

bool DetectorPluginLoader::ValidateSymbols(HLibrary lib, PluginLoadError& error) {
    // Check CreateDetector
    if (!GetSymbol(lib, "CreateDetector")) {
        error.code = PluginLoadResult::ERR_MISSING_SYMBOL;
        error.message = "Required symbol 'CreateDetector' not found";
        return false;
    }

    // Check DestroyDetector
    if (!GetSymbol(lib, "DestroyDetector")) {
        error.code = PluginLoadResult::ERR_MISSING_SYMBOL;
        error.message = "Required symbol 'DestroyDetector' not found";
        return false;
    }

    // Check GetPluginManifest
    if (!GetSymbol(lib, "GetPluginManifest")) {
        error.code = PluginLoadResult::ERR_MISSING_SYMBOL;
        error.message = "Required symbol 'GetPluginManifest' not found";
        return false;
    }

    return true;
}

bool DetectorPluginLoader::ValidateVersion(const PluginManifest& manifest,
                                          PluginLoadError& error) {
    if (!IsPluginVersionCompatible(manifest.api_version)) {
        error.code = PluginLoadResult::ERR_VERSION_MISMATCH;
        error.message = fmt::format(
            "API version mismatch: plugin requires 0x{:08X}, HAL provides 0x{:08X}",
            manifest.api_version, HNUE_HAL_API_VERSION);
        return false;
    }

    return true;
}

IDetector* DetectorPluginLoader::CreateDetectorInstance(CreateDetectorFn create_fn,
                                                        const PluginConfig* config) {
    if (!create_fn) {
        return nullptr;
    }

    try {
        IDetector* detector = create_fn(config);
        if (!detector) {
            spdlog::error("CreateDetector returned null");
        }
        return detector;
    } catch (const std::exception& e) {
        spdlog::error("Exception in CreateDetector: {}", e.what());
        return nullptr;
    } catch (...) {
        spdlog::error("Unknown exception in CreateDetector");
        return nullptr;
    }
}

void DetectorPluginLoader::DestroyDetectorInstance(IDetector* detector,
                                                   DestroyDetectorFn destroy_fn) {
    if (!detector || !destroy_fn) {
        return;
    }

    try {
        destroy_fn(detector);
    } catch (const std::exception& e) {
        spdlog::error("Exception in DestroyDetector: {}", e.what());
    } catch (...) {
        spdlog::error("Unknown exception in DestroyDetector");
    }
}

void DetectorPluginLoader::SetLastError(PluginLoadResult code,
                                        const std::string& message,
                                        const std::string& plugin_path,
                                        HLibrary lib) {
    last_error_.code = code;
    last_error_.message = message;
    last_error_.plugin_path = plugin_path;

    // Try to get additional error from plugin
    if (lib) {
        auto error_fn = reinterpret_cast<GetLastErrorFn>(
            GetSymbol(lib, "GetLastError"));
        if (error_fn) {
            const char* err = error_fn();
            if (err) {
                last_error_.last_error = err;
            }
        }
    }

    spdlog::error("Plugin load error [{}]: {} - {}",
                 static_cast<int>(code), plugin_path, message);
}

} // namespace hnvue::hal
