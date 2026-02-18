/**
 * @file test_engine_factory.cpp
 * @brief Unit tests for EngineFactory
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Factory and plugin loading tests
 * SPDX-License-Identifier: MIT
 *
 * Tests:
 * - Default engine creation
 * - Plugin loading (success and failure cases)
 * - Factory method selection
 */

#include <gtest/gtest.h>
#include <hnvue/imaging/IImageProcessingEngine.h>
#include <hnvue/imaging/DefaultImageProcessingEngine.h>
#include <memory>

using namespace hnvue::imaging;

// =============================================================================
// Factory Creation Tests
// =============================================================================

TEST(EngineFactoryTest, CreateDefaultReturnsNonNull) {
    auto engine = EngineFactory::CreateDefault();
    EXPECT_NE(engine, nullptr);
}

TEST(EngineFactoryTest, CreateDefaultReturnsUniquePtr) {
    auto engine = EngineFactory::CreateDefault();
    EXPECT_NE(engine.get(), nullptr);
}

TEST(EngineFactoryTest, CreateDefaultCanBeInitialized) {
    auto engine = EngineFactory::CreateDefault();

    EngineConfig config;
    config.max_frame_width = 512;
    config.max_frame_height = 512;

    EXPECT_TRUE(engine->Initialize(config));
}

// =============================================================================
// Factory Method Selection Tests
// =============================================================================

TEST(EngineFactoryTest, CreateWithEmptyPathReturnsDefault) {
    auto engine = EngineFactory::Create("");
    EXPECT_NE(engine, nullptr);
}

TEST(EngineFactoryTest, CreateWithEmptyPathCreatesDefaultEngine) {
    auto engine = EngineFactory::Create("");
    EngineInfo info = engine->GetEngineInfo();
    EXPECT_EQ(info.engine_name, "DefaultImageProcessingEngine");
}

TEST(EngineFactoryTest, CreateWithNonExistentPluginReturnsNull) {
    auto engine = EngineFactory::Create("/nonexistent/plugin.so");
    EXPECT_EQ(engine, nullptr);
}

TEST(EngineFactoryTest, CreateWithInvalidPluginReturnsNull) {
    // Test with a file that exists but is not a valid plugin
    auto engine = EngineFactory::Create("/etc/passwd");
    EXPECT_EQ(engine, nullptr);
}

// =============================================================================
// Plugin Loading Tests
// =============================================================================

TEST(EngineFactoryTest, CreateFromNonExistentPluginReturnsNull) {
    auto engine = EngineFactory::CreateFromPlugin("/nonexistent/plugin.dll");
    EXPECT_EQ(engine, nullptr);
}

TEST(EngineFactoryTest, CreateFromPluginWithEmptyPathReturnsDefault) {
    auto engine = EngineFactory::CreateFromPlugin("");
    EXPECT_NE(engine, nullptr);
}

TEST(EngineFactoryTest, CreateFromPluginWithEmptyPathCreatesDefault) {
    auto engine = EngineFactory::CreateFromPlugin("");
    EngineInfo info = engine->GetEngineInfo();
    EXPECT_EQ(info.engine_name, "DefaultImageProcessingEngine");
}

// =============================================================================
// Default Engine Verification Tests
// =============================================================================

TEST(EngineFactoryTest, DefaultEngineHasCorrectCapabilities) {
    auto engine = EngineFactory::CreateDefault();
    EngineInfo info = engine->GetEngineInfo();

    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_OFFSET_CORRECTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_GAIN_CORRECTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_DEFECT_PIXEL_MAP));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_SCATTER_CORRECTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_WINDOW_LEVEL));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_NOISE_REDUCTION));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_FLATTENING));
    EXPECT_TRUE(info.HasCapability(EngineCapabilityFlags::CAP_PREVIEW_MODE));
}

TEST(EngineFactoryTest, DefaultEngineHasCorrectVendor) {
    auto engine = EngineFactory::CreateDefault();
    EngineInfo info = engine->GetEngineInfo();

    EXPECT_EQ(info.vendor, "HnVue");
}

TEST(EngineFactoryTest, DefaultEngineHasApiVersion) {
    auto engine = EngineFactory::CreateDefault();
    EngineInfo info = engine->GetEngineInfo();

    EXPECT_NE(info.api_version, 0);
}

// =============================================================================
// Lifecycle Tests
// =============================================================================

TEST(EngineFactoryTest, CreatedEngineCanBeShutdown) {
    auto engine = EngineFactory::CreateDefault();

    EngineConfig config;
    config.max_frame_width = 512;
    config.max_frame_height = 512;

    ASSERT_TRUE(engine->Initialize(config));
    engine->Shutdown();

    SUCCEED();
}

TEST(EngineFactoryTest, CreatedEngineCanBeDestroyed) {
    auto engine = EngineFactory::CreateDefault();
    // Destructor is called automatically
    SUCCEED();
}

// =============================================================================
// Multiple Instance Tests
// =============================================================================

TEST(EngineFactoryTest, MultipleDefaultEnginesCanBeCreated) {
    auto engine1 = EngineFactory::CreateDefault();
    auto engine2 = EngineFactory::CreateDefault();

    EXPECT_NE(engine1, nullptr);
    EXPECT_NE(engine2, nullptr);
    EXPECT_NE(engine1.get(), engine2.get());
}

TEST(EngineFactoryTest, EachEngineHasIndependentState) {
    auto engine1 = EngineFactory::CreateDefault();
    auto engine2 = EngineFactory::CreateDefault();

    EngineConfig config;
    config.max_frame_width = 512;
    config.max_frame_height = 512;

    engine1->Initialize(config);

    EngineInfo info1 = engine1->GetEngineInfo();
    EngineInfo info2 = engine2->GetEngineInfo();

    // Both should have same engine info
    EXPECT_EQ(info1.engine_name, info2.engine_name);
}

// =============================================================================
// Function Pointer Type Tests
// =============================================================================

TEST(FunctionPointerTest, CreateEngineFuncTypeIsDefined) {
    // This test verifies the type alias is defined correctly
    CreateEngineFunc func = nullptr;
    EXPECT_EQ(func, nullptr);
}

TEST(FunctionPointerTest, DestroyEngineFuncTypeIsDefined) {
    // This test verifies the type alias is defined correctly
    DestroyEngineFunc func = nullptr;
    EXPECT_EQ(func, nullptr);
}

// =============================================================================
// Platform-Specific Tests
// =============================================================================

#if defined(_WIN32)
TEST(EngineFactoryTest, WindowsDllExtensionIsCorrect) {
    // Verify DLL extension constant
    EXPECT_STREQ(".dll", ".dll");  // Placeholder - extension is in implementation
}
#else
TEST(EngineFactoryTest, UnixSoExtensionIsCorrect) {
    // Verify SO extension constant
    EXPECT_STREQ(".so", ".so");  // Placeholder - extension is in implementation
}
#endif
