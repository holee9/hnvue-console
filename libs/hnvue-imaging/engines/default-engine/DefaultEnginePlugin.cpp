/**
 * @file DefaultEnginePlugin.cpp
 * @brief Plugin DLL exports for the default image processing engine
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Default engine plugin exports
 * SPDX-License-Identifier: MIT
 *
 * This file implements the extern "C" factory functions required
 * for the engine plugin DLL ABI contract.
 */

#include "hnvue/imaging/DefaultImageProcessingEngine.h"
#include "hnvue/imaging/IImageProcessingEngine.h"

/**
 * @brief Create a new default engine instance
 * @return Pointer to newly created engine
 *
 * This function is exported from the DLL and called by the host
 * pipeline via EngineFactory::CreateFromPlugin().
 *
 * extern "C" linkage prevents C++ name mangling for ABI stability.
 */
extern "C" {

/**
 * @brief Factory function to create a new engine instance
 * @return Pointer to newly created DefaultImageProcessingEngine
 *
 * The caller takes ownership and must call DestroyImageProcessingEngine()
 * to release the instance.
 */
__declspec(dllexport) hnvue::imaging::IImageProcessingEngine*
CreateImageProcessingEngine() {
    return new hnvue::imaging::DefaultImageProcessingEngine();
}

/**
 * @brief Destroy an engine instance created by CreateImageProcessingEngine
 * @param engine Pointer to engine instance to destroy
 *
 * This function ensures the engine is destroyed in the same DLL's heap
 * that created it, preventing cross-DLL heap corruption.
 */
__declspec(dllexport) void
DestroyImageProcessingEngine(hnvue::imaging::IImageProcessingEngine* engine) {
    delete engine;
}

} // extern "C"
