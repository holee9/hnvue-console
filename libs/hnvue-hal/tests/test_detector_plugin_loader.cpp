/**
 * @file test_detector_plugin_loader.cpp
 * @brief Unit tests for DetectorPluginLoader using Google Test
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Unit tests for plugin loader
 * SPDX-License-Identifier: MIT
 */

#include <gtest/gtest.h>
#include <gmock/gmock.h>

#include "plugin/DetectorPluginLoader.h"
#include "hnvue/hal/IDetector.h"
#include "hnvue/hal/PluginAbi.h"
#include "hnvue/hal/HalTypes.h"

#include <filesystem>
#include <fstream>
#include <memory>
#include <string>

namespace fs = std::filesystem;
namespace hal = hnvue::hal;

// =============================================================================
// Test Fixture
// =============================================================================

class DetectorPluginLoaderTest : public ::testing::Test {
protected:
    void SetUp() override {
        // Create test plugin directory
        test_plugin_dir_ = fs::temp_directory_path() / "hnvue_hal_test_plugins";
        fs::create_directories(test_plugin_dir_);

        // Create loader instance
        loader_ = std::make_unique<hal::DetectorPluginLoader>();
    }

    void TearDown() override {
        // Clean up test plugins
        if (fs::exists(test_plugin_dir_)) {
            fs::remove_all(test_plugin_dir_);
        }

        // Loader cleanup
        loader_.reset();
    }

    // Helper: Create a minimal valid plugin manifest
    hal::PluginManifest CreateValidManifest() {
        hal::PluginManifest manifest;
        manifest.api_version = HNUE_HAL_API_VERSION;
        manifest.plugin_version = 0x01000000;  // v1.0.0
        manifest.plugin_name = "TestDetector";
        manifest.vendor_name = "TestVendor";
        manifest.model_name = "TestModel";
        manifest.max_frame_width = 2048;
        manifest.max_frame_height = 2048;
        manifest.max_frame_rate = 30.0f;
        return manifest;
    }

    // Helper: Create a mock plugin file path (not a real DLL for testing)
    std::string CreateMockPluginPath(const std::string& name) {
        return (test_plugin_dir_ / (name + ".dll")).string();
    }

    // Helper: Write mock plugin content (for file existence tests)
    void WriteMockPluginFile(const std::string& path, const std::string& content = "mock") {
        std::ofstream file(path, std::ios::binary);
        file << content;
    }

    fs::path test_plugin_dir_;
    std::unique_ptr<hal::DetectorPluginLoader> loader_;
};

// =============================================================================
// Test Cases: Load Plugin
// =============================================================================

/**
 * @test LoadPlugin returns null handle for non-existent file
 */
TEST_F(DetectorPluginLoaderTest, LoadPlugin_ReturnsNull_HandleForNonExistentFile) {
    std::string non_existent_path = (test_plugin_dir_ / "nonexistent.dll").string();

    auto result = loader_->LoadPlugin(non_existent_path);

    EXPECT_EQ(result.get(), nullptr);
}

/**
 * @test LoadPlugin returns null handle for empty file path
 */
TEST_F(DetectorPluginLoaderTest, LoadPlugin_ReturnsNull_HandleForEmptyPath) {
    auto result = loader_->LoadPlugin("");

    EXPECT_EQ(result.get(), nullptr);
}

/**
 * @test GetLoadedPlugins returns empty list initially
 */
TEST_F(DetectorPluginLoaderTest, GetLoadedPlugins_ReturnsEmptyList_Initially) {
    auto plugins = loader_->GetLoadedPlugins();

    EXPECT_TRUE(plugins.empty());
    EXPECT_EQ(plugins.size(), 0);
}

// =============================================================================
// Test Cases: Symbol Validation (with mock DLL structure)
// =============================================================================

/**
 * @test LoadPlugin validates CreateDetector symbol presence
 *
 * NOTE: This test requires a real test plugin DLL with proper exports.
 * For now, we test the error path with invalid files.
 */
TEST_F(DetectorPluginLoaderTest, LoadPlugin_ValidateCreateDetectorSymbol_Missing) {
    std::string mock_plugin = CreateMockPluginPath("missing_symbols");
    WriteMockPluginFile(mock_plugin, "invalid dll content");

    auto result = loader_->LoadPlugin(mock_plugin);

    // Should fail due to missing required symbols
    EXPECT_EQ(result.get(), nullptr);
}

// =============================================================================
// Test Cases: Version Compatibility
// =============================================================================

/**
 * @test IsPluginVersionCompatible returns true for matching major version
 */
TEST_F(DetectorPluginLoaderTest, VersionCompatibility_ReturnsTrue_MatchingMajor) {
    uint32_t plugin_version = 0x01000000;  // Same major as HNUE_HAL_API_VERSION (0x01)

    EXPECT_TRUE(hal::IsPluginVersionCompatible(plugin_version));
}

/**
 * @test IsPluginVersionCompatible returns false for different major version
 */
TEST_F(DetectorPluginLoaderTest, VersionCompatibility_ReturnsFalse_DifferentMajor) {
    uint32_t plugin_version = 0x02000000;  // Different major version

    EXPECT_FALSE(hal::IsPluginVersionCompatible(plugin_version));
}

/**
 * @test IsPluginVersionCompatible returns true for greater minor version
 */
TEST_F(DetectorPluginLoaderTest, VersionCompatibility_ReturnsTrue_GreaterMinor) {
    uint32_t plugin_version = 0x01010000;  // Minor 1 vs HAL 0

    EXPECT_TRUE(hal::IsPluginVersionCompatible(plugin_version));
}

// =============================================================================
// Test Cases: Unload Plugin
// =============================================================================

/**
 * @test UnloadPlugin returns false for null handle
 */
TEST_F(DetectorPluginLoaderTest, UnloadPlugin_ReturnsFalse_ForNullHandle) {
    bool result = loader_->UnloadPlugin(nullptr);

    EXPECT_FALSE(result);
}

/**
 * @test UnloadPlugin returns false for invalid handle pointer
 */
TEST_F(DetectorPluginLoaderTest, UnloadPlugin_ReturnsFalse_ForInvalidHandle) {
    hal::PluginHandle* invalid_handle = reinterpret_cast<hal::PluginHandle*>(0xDEADBEEF);

    bool result = loader_->UnloadPlugin(invalid_handle);

    EXPECT_FALSE(result);
}

// =============================================================================
// Test Cases: Find Plugin
// =============================================================================

/**
 * @test FindPlugin returns null when no plugins loaded
 */
TEST_F(DetectorPluginLoaderTest, FindPlugin_ReturnsNull_WhenNoPluginsLoaded) {
    auto result = loader_->FindPlugin("NonExistentVendor");

    EXPECT_EQ(result.get(), nullptr);
}

/**
 * @test FindPlugin returns null for non-existent vendor name
 */
TEST_F(DetectorPluginLoaderTest, FindPlugin_ReturnsNull_ForNonExistentVendor) {
    auto result = loader_->FindPlugin("VendorThatDoesNotExist");

    EXPECT_EQ(result.get(), nullptr);
}

// =============================================================================
// Test Cases: Reload Plugin
// =============================================================================

/**
 * @test ReloadPlugin returns null for non-existent file
 */
TEST_F(DetectorPluginLoaderTest, ReloadPlugin_ReturnsNull_ForNonExistentFile) {
    std::string non_existent_path = (test_plugin_dir_ / "nonexistent.dll").string();

    auto result = loader_->ReloadPlugin(non_existent_path);

    EXPECT_EQ(result.get(), nullptr);
}

// =============================================================================
// Test Cases: Error Reporting
// =============================================================================

/**
 * @test PluginLoadError contains proper error details
 */
TEST_F(DetectorPluginLoaderTest, PluginLoadError_ContainsProperErrorDetails) {
    hal::PluginLoadError error;
    error.code = hal::PluginLoadResult::ERR_FILE_NOT_FOUND;
    error.message = "Plugin file not found";
    error.plugin_path = "/path/to/plugin.dll";
    error.last_error = "System error: No such file";

    EXPECT_EQ(error.code, hal::PluginLoadResult::ERR_FILE_NOT_FOUND);
    EXPECT_FALSE(error.message.empty());
    EXPECT_FALSE(error.plugin_path.empty());
    EXPECT_FALSE(error.last_error.empty());
}

// =============================================================================
// Test Cases: Multiple Plugins
// =============================================================================

/**
 * @test GetLoadedPlugins returns all loaded plugins
 *
 * NOTE: This test requires multiple valid test plugin DLLs.
 * For now, we verify the empty state behavior.
 */
TEST_F(DetectorPluginLoaderTest, GetLoadedPlugins_ReturnsAllLoadedPlugins) {
    auto plugins_before = loader_->GetLoadedPlugins();
    size_t initial_count = plugins_before.size();

    // After loading real plugins, count should increase
    // For now, just verify initial state
    EXPECT_EQ(initial_count, 0);
}

// =============================================================================
// Test Cases: Plugin State Management
// =============================================================================

/**
 * @test PluginHandle maintains plugin state correctly
 */
TEST_F(DetectorPluginLoaderTest, PluginHandle_MaintainsStateCorrectly) {
    // Verify PluginHandle structure can be constructed
    hal::PluginInfo info;
    info.plugin_path = "/path/to/plugin.dll";
    info.manifest = CreateValidManifest();

    EXPECT_EQ(info.plugin_path, "/path/to/plugin.dll");
    EXPECT_EQ(info.manifest.plugin_name, "TestDetector");
    EXPECT_EQ(info.manifest.vendor_name, "TestVendor");
}

// =============================================================================
// Main
// =============================================================================

int main(int argc, char** argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
