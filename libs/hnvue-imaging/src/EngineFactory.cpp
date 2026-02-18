/**
 * @file EngineFactory.cpp
 * @brief Implementation of EngineFactory for plugin loading
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Engine factory implementation
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/imaging/IImageProcessingEngine.h"
#include "hnvue/imaging/DefaultImageProcessingEngine.h"

#include <stdexcept>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
using LibraryHandle = HMODULE;
constexpr const char* LIBRARY_EXTENSION = ".dll";
#else
#include <dlfcn.h>
using LibraryHandle = void*;
constexpr const char* LIBRARY_EXTENSION = ".so";
#endif

namespace hnvue::imaging {

namespace {

/**
 * @brief RAII wrapper for dynamic library handle
 */
class LibraryWrapper {
public:
    explicit LibraryWrapper(const std::string& path) : handle_(nullptr) {
#ifdef _WIN32
        handle_ = LoadLibraryA(path.c_str());
#else
        handle_ = dlopen(path.c_str(), RTLD_LAZY);
#endif
    }

    ~LibraryWrapper() {
        if (handle_) {
#ifdef _WIN32
            FreeLibrary(handle_);
#else
            dlclose(handle_);
#endif
        }
    }

    bool IsLoaded() const { return handle_ != nullptr; }

    template<typename FuncType>
    FuncType GetSymbol(const char* name) const {
#ifdef _WIN32
        return reinterpret_cast<FuncType>(GetProcAddress(handle_, name));
#else
        return reinterpret_cast<FuncType>(dlsym(handle_, name));
#endif
    }

    std::string GetLastErrorMsg() const {
#ifdef _WIN32
        DWORD error = GetLastError();
        if (error) {
            LPSTR msg = nullptr;
            FormatMessageA(
                FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM,
                nullptr, error, 0,
                reinterpret_cast<LPSTR>(&msg), 0, nullptr);
            std::string result(msg);
            LocalFree(msg);
            return result;
        }
        return "Unknown error";
#else
        const char* msg = dlerror();
        return msg ? msg : "Unknown error";
#endif
    }

private:
    LibraryHandle handle_;
};

} // anonymous namespace

std::unique_ptr<IImageProcessingEngine> EngineFactory::CreateFromPlugin(
    const std::string& plugin_path) {

    if (plugin_path.empty()) {
        return CreateDefault();
    }

    LibraryWrapper lib(plugin_path);
    if (!lib.IsLoaded()) {
        // Failed to load library
        return nullptr;
    }

    // Get factory function
    auto create_func = lib.GetSymbol<CreateEngineFunc>("CreateImageProcessingEngine");
    if (!create_func) {
        // Missing required export
        return nullptr;
    }

    // Create engine instance
    IImageProcessingEngine* engine = create_func();
    if (!engine) {
        // Factory returned null
        return nullptr;
    }

    return std::unique_ptr<IImageProcessingEngine>(engine);
}

std::unique_ptr<IImageProcessingEngine> EngineFactory::CreateDefault() {
    return std::make_unique<DefaultImageProcessingEngine>();
}

std::unique_ptr<IImageProcessingEngine> EngineFactory::Create(
    const std::string& engine_plugin_path) {

    if (engine_plugin_path.empty()) {
        return CreateDefault();
    }
    return CreateFromPlugin(engine_plugin_path);
}

} // namespace hnvue::imaging
