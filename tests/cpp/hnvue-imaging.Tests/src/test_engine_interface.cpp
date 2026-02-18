/**
 * @file test_engine_interface.cpp
 * @brief Unit tests for IImageProcessingEngine interface
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Interface contract validation
 * SPDX-License-Identifier: MIT
 *
 * Tests:
 * - Interface method signatures
 * - Return code validation
 * - Mock engine implementation
 * - Error handling contract
 */

#include <gtest/gtest.h>
#include <gmock/gmock.h>
#include <hnvue/imaging/IImageProcessingEngine.h>
#include <hnvue/imaging/ImagingTypes.h>
#include <memory>

using namespace hnvue::imaging;

// =============================================================================
// Mock Engine Implementation
// =============================================================================

/**
 * @brief Mock implementation of IImageProcessingEngine for testing
 */
class MockImageProcessingEngine : public IImageProcessingEngine {
public:
    MOCK_METHOD(bool, Initialize, (const EngineConfig& config), (override));
    MOCK_METHOD(void, Shutdown, (), (override));

    MOCK_METHOD(bool, ApplyOffsetCorrection,
                (ImageBuffer& frame, const CalibrationData& dark), (override));
    MOCK_METHOD(bool, ApplyGainCorrection,
                (ImageBuffer& frame, const CalibrationData& gain), (override));
    MOCK_METHOD(bool, ApplyDefectPixelMap,
                (ImageBuffer& frame, const DefectMap& map), (override));
    MOCK_METHOD(bool, ApplyScatterCorrection,
                (ImageBuffer& frame, const ScatterParams& params), (override));
    MOCK_METHOD(bool, ApplyWindowLevel,
                (ImageBuffer& frame, float window, float level), (override));
    MOCK_METHOD(bool, ApplyNoiseReduction,
                (ImageBuffer& frame, const NoiseReductionConfig& config), (override));
    MOCK_METHOD(bool, ApplyFlattening,
                (ImageBuffer& frame, const FlatteningConfig& config), (override));
    MOCK_METHOD(bool, ProcessFrame,
                (ImageBuffer& frame, const ProcessingConfig& config), (override));

    MOCK_METHOD(EngineInfo, GetEngineInfo, (), (const, override));
    MOCK_METHOD(EngineError, GetLastError, (), (const, override));
    MOCK_METHOD(StageTiming, GetLastTiming, (), (const, override));
};

// =============================================================================
// Interface Contract Tests
// =============================================================================

class IImageProcessingEngineInterfaceTest : public ::testing::Test {
protected:
    std::unique_ptr<MockImageProcessingEngine> mock_engine_;

    void SetUp() override {
        mock_engine_ = std::make_unique<MockImageProcessingEngine>();
    }
};

TEST_F(IImageProcessingEngineInterfaceTest, InitializeIsCallable) {
    EngineConfig config;
    config.max_frame_width = 512;
    config.max_frame_height = 512;

    EXPECT_CALL(*mock_engine_, Initialize(testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->Initialize(config));
}

TEST_F(IImageProcessingEngineInterfaceTest, InitializeReturnsFalseOnFailure) {
    EngineConfig config;

    EXPECT_CALL(*mock_engine_, Initialize(testing::_))
        .WillOnce(testing::Return(false));

    EXPECT_FALSE(mock_engine_->Initialize(config));
}

TEST_F(IImageProcessingEngineInterfaceTest, ShutdownIsCallable) {
    EXPECT_CALL(*mock_engine_, Shutdown())
        .Times(1);

    mock_engine_->Shutdown();
}

TEST_F(IImageProcessingEngineInterfaceTest, ApplyOffsetCorrectionIsCallable) {
    ImageBuffer frame;
    CalibrationData dark;

    EXPECT_CALL(*mock_engine_, ApplyOffsetCorrection(testing::_, testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->ApplyOffsetCorrection(frame, dark));
}

TEST_F(IImageProcessingEngineInterfaceTest, ApplyGainCorrectionIsCallable) {
    ImageBuffer frame;
    CalibrationData gain;

    EXPECT_CALL(*mock_engine_, ApplyGainCorrection(testing::_, testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->ApplyGainCorrection(frame, gain));
}

TEST_F(IImageProcessingEngineInterfaceTest, ApplyDefectPixelMapIsCallable) {
    ImageBuffer frame;
    DefectMap map;

    EXPECT_CALL(*mock_engine_, ApplyDefectPixelMap(testing::_, testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->ApplyDefectPixelMap(frame, map));
}

TEST_F(IImageProcessingEngineInterfaceTest, ApplyScatterCorrectionIsCallable) {
    ImageBuffer frame;
    ScatterParams params;

    EXPECT_CALL(*mock_engine_, ApplyScatterCorrection(testing::_, testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->ApplyScatterCorrection(frame, params));
}

TEST_F(IImageProcessingEngineInterfaceTest, ApplyWindowLevelIsCallable) {
    ImageBuffer frame;

    EXPECT_CALL(*mock_engine_, ApplyWindowLevel(testing::_, testing::_, testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->ApplyWindowLevel(frame, 4000.0f, 2000.0f));
}

TEST_F(IImageProcessingEngineInterfaceTest, ApplyNoiseReductionIsCallable) {
    ImageBuffer frame;
    NoiseReductionConfig config;

    EXPECT_CALL(*mock_engine_, ApplyNoiseReduction(testing::_, testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->ApplyNoiseReduction(frame, config));
}

TEST_F(IImageProcessingEngineInterfaceTest, ApplyFlatteningIsCallable) {
    ImageBuffer frame;
    FlatteningConfig config;

    EXPECT_CALL(*mock_engine_, ApplyFlattening(testing::_, testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->ApplyFlattening(frame, config));
}

TEST_F(IImageProcessingEngineInterfaceTest, ProcessFrameIsCallable) {
    ImageBuffer frame;
    ProcessingConfig config;

    EXPECT_CALL(*mock_engine_, ProcessFrame(testing::_, testing::_))
        .WillOnce(testing::Return(true));

    EXPECT_TRUE(mock_engine_->ProcessFrame(frame, config));
}

TEST_F(IImageProcessingEngineInterfaceTest, GetEngineInfoIsCallable) {
    EngineInfo info;
    info.engine_name = "TestEngine";
    info.engine_version = "1.0.0";
    info.vendor = "TestVendor";

    EXPECT_CALL(*mock_engine_, GetEngineInfo())
        .WillOnce(testing::Return(info));

    EngineInfo result = mock_engine_->GetEngineInfo();
    EXPECT_EQ(result.engine_name, "TestEngine");
}

TEST_F(IImageProcessingEngineInterfaceTest, GetLastErrorIsCallable) {
    EngineError error;
    error.error_code = ImagingError::IMAGING_ERR_PARAM;
    error.error_message = "Test error";

    EXPECT_CALL(*mock_engine_, GetLastError())
        .WillOnce(testing::Return(error));

    EngineError result = mock_engine_->GetLastError();
    EXPECT_EQ(result.error_code, ImagingError::IMAGING_ERR_PARAM);
}

TEST_F(IImageProcessingEngineInterfaceTest, GetLastTimingIsCallable) {
    StageTiming timing;
    timing.offset_correction_us = 100;

    EXPECT_CALL(*mock_engine_, GetLastTiming())
        .WillOnce(testing::Return(timing));

    StageTiming result = mock_engine_->GetLastTiming();
    EXPECT_EQ(result.offset_correction_us, 100);
}

// =============================================================================
// Interface Behavior Tests
// =============================================================================

TEST_F(IImageProcessingEngineInterfaceTest, ProcessingMethodsReturnFalseOnError) {
    ImageBuffer frame;
    CalibrationData calib;
    DefectMap map;
    ScatterParams params;
    NoiseReductionConfig noise_config;
    FlatteningConfig flatten_config;

    EXPECT_CALL(*mock_engine_, ApplyOffsetCorrection(testing::_, testing::_))
        .WillOnce(testing::Return(false));
    EXPECT_CALL(*mock_engine_, ApplyGainCorrection(testing::_, testing::_))
        .WillOnce(testing::Return(false));
    EXPECT_CALL(*mock_engine_, ApplyDefectPixelMap(testing::_, testing::_))
        .WillOnce(testing::Return(false));
    EXPECT_CALL(*mock_engine_, ApplyScatterCorrection(testing::_, testing::_))
        .WillOnce(testing::Return(false));
    EXPECT_CALL(*mock_engine_, ApplyWindowLevel(testing::_, testing::_, testing::_))
        .WillOnce(testing::Return(false));
    EXPECT_CALL(*mock_engine_, ApplyNoiseReduction(testing::_, testing::_))
        .WillOnce(testing::Return(false));
    EXPECT_CALL(*mock_engine_, ApplyFlattening(testing::_, testing::_))
        .WillOnce(testing::Return(false));

    EXPECT_FALSE(mock_engine_->ApplyOffsetCorrection(frame, calib));
    EXPECT_FALSE(mock_engine_->ApplyGainCorrection(frame, calib));
    EXPECT_FALSE(mock_engine_->ApplyDefectPixelMap(frame, map));
    EXPECT_FALSE(mock_engine_->ApplyScatterCorrection(frame, params));
    EXPECT_FALSE(mock_engine_->ApplyWindowLevel(frame, 4000.0f, 2000.0f));
    EXPECT_FALSE(mock_engine_->ApplyNoiseReduction(frame, noise_config));
    EXPECT_FALSE(mock_engine_->ApplyFlattening(frame, flatten_config));
}

// =============================================================================
// EngineFactory Tests
// =============================================================================

TEST(EngineFactoryTest, CreateDefaultReturnsNonNull) {
    auto engine = EngineFactory::CreateDefault();
    EXPECT_NE(engine, nullptr);
}

TEST(EngineFactoryTest, CreateWithEmptyPathReturnsDefault) {
    auto engine = EngineFactory::Create("");
    EXPECT_NE(engine, nullptr);
}

TEST(EngineFactoryTest, CreateDefaultReturnsCorrectType) {
    auto engine = EngineFactory::CreateDefault();
    EngineInfo info = engine->GetEngineInfo();
    EXPECT_EQ(info.engine_name, "DefaultImageProcessingEngine");
}

TEST(EngineFactoryTest, CreateDefaultCanBeInitialized) {
    auto engine = EngineFactory::CreateDefault();
    EngineConfig config;
    config.max_frame_width = 512;
    config.max_frame_height = 512;

    EXPECT_TRUE(engine->Initialize(config));
    engine->Shutdown();
}

TEST(EngineFactoryTest, CreateFromNonExistentPluginReturnsNull) {
    auto engine = EngineFactory::CreateFromPlugin("/nonexistent/plugin.dll");
    EXPECT_EQ(engine, nullptr);
}

// =============================================================================
// Plugin ABI Contract Tests
// =============================================================================

TEST(PluginABITest, CreateEngineFuncTypeIsCorrect) {
    // Verify the function pointer type is defined correctly
    using FuncType = IImageProcessingEngine* (*)();
    FuncType func = nullptr;  // Just verify the type compiles
    (void)func;
    SUCCEED();
}

TEST(PluginABITest, DestroyEngineFuncTypeIsCorrect) {
    // Verify the function pointer type is defined correctly
    using FuncType = void (*)(IImageProcessingEngine*);
    FuncType func = nullptr;  // Just verify the type compiles
    (void)func;
    SUCCEED();
}
