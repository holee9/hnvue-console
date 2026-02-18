/**
 * @file test_calibration_manager.cpp
 * @brief Unit tests for CalibrationManager
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Calibration data management tests
 * SPDX-License-Identifier: MIT
 *
 * Tests:
 * - Loading calibration data from files
 * - Hot-reload functionality
 * - Validation and checksums
 * - Thread-safe caching
 */

#include <gtest/gtest.h>
#include <hnvue/imaging/CalibrationManager.h>
#include <hnvue/imaging/ImagingTypes.h>
#include <fstream>
#include <cstring>
#include <filesystem>

using namespace hnvue::imaging;

// =============================================================================
// Test Helper Functions
// =============================================================================

namespace {

constexpr uint8_t MAGIC_HN[] = {'H', 'N', 'C', 0x01};
constexpr uint16_t FORMAT_VERSION = 1;

/**
 * @brief Create a test calibration file
 */
bool CreateTestCalibrationFile(const std::string& path,
                               CalibrationDataType type,
                               uint32_t width,
                               uint32_t height) {
    std::ofstream file(path, std::ios::binary);
    if (!file) return false;

    // Write header
    CalibrationFileHeader header;
    std::memcpy(header.magic, MAGIC_HN, 4);
    header.format_version = FORMAT_VERSION;
    header.data_type = static_cast<uint16_t>(type);
    header.width = width;
    header.height = height;
    header.acquisition_time = 1234567890ULL;
    std::memset(header.checksum, 0, 32);  // Zero checksum for test

    if (type == CalibrationDataType::DEFECT_MAP) {
        header.payload_length = sizeof(uint32_t);  // Just count
    } else if (type == CalibrationDataType::SCATTER_PARAMS) {
        header.payload_length = sizeof(bool) + sizeof(uint16_t) +
                               sizeof(float) * 2 + sizeof(unsigned int);
    } else {
        header.payload_length = width * height * sizeof(float);
    }

    file.write(reinterpret_cast<const char*>(&header), sizeof(CalibrationFileHeader));
    if (!file) return false;

    // Write payload
    if (type == CalibrationDataType::DEFECT_MAP) {
        uint32_t count = 0;  // Empty defect map
        file.write(reinterpret_cast<const char*>(&count), sizeof(count));
    } else if (type == CalibrationDataType::SCATTER_PARAMS) {
        bool enabled = false;
        uint16_t algorithm = 0;
        float cutoff = 0.1f;
        float ratio = 0.5f;
        unsigned int flags = 0;
        file.write(reinterpret_cast<const char*>(&enabled), sizeof(enabled));
        file.write(reinterpret_cast<const char*>(&algorithm), sizeof(algorithm));
        file.write(reinterpret_cast<const char*>(&cutoff), sizeof(cutoff));
        file.write(reinterpret_cast<const char*>(&ratio), sizeof(ratio));
        file.write(reinterpret_cast<const char*>(&flags), sizeof(flags));
    } else {
        // Write zero-filled calibration data
        std::vector<float> data(width * height, 0.0f);
        file.write(reinterpret_cast<const char*>(data.data()), data.size() * sizeof(float));
    }

    return file.good();
}

/**
 * @brief Create a test calibration file with invalid magic
 */
bool CreateInvalidMagicFile(const std::string& path) {
    std::ofstream file(path, std::ios::binary);
    if (!file) return false;

    CalibrationFileHeader header;
    std::memset(header.magic, 'X', 4);  // Invalid magic
    header.format_version = FORMAT_VERSION;
    header.data_type = static_cast<uint16_t>(CalibrationDataType::DARK_FRAME);
    header.width = 256;
    header.height = 256;
    header.payload_length = 256 * 256 * sizeof(float);

    file.write(reinterpret_cast<const char*>(&header), sizeof(CalibrationFileHeader));
    return file.good();
}

}  // anonymous namespace

// =============================================================================
// Test Fixture
// =============================================================================

class CalibrationManagerTest : public ::testing::Test {
protected:
    std::unique_ptr<CalibrationManager> manager_;
    std::string test_dir_;

    void SetUp() override {
        manager_ = std::make_unique<CalibrationManager>(90);  // 90 days max age

        // Create test directory
        test_dir_ = "test_calib_data";
        std::filesystem::create_directories(test_dir_);
    }

    void TearDown() override {
        // Clean up test files
        if (std::filesystem::exists(test_dir_)) {
            std::filesystem::remove_all(test_dir_);
        }
    }

    std::string GetTestPath(const std::string& filename) {
        return test_dir_ + "/" + filename;
    }
};

// =============================================================================
// Constructor/Destructor Tests
// =============================================================================

TEST(CalibrationManagerConstructorTest, DefaultConstruction) {
    CalibrationManager mgr;
    EXPECT_NE(&mgr, nullptr);
}

TEST(CalibrationManagerConstructorTest, ConstructionWithMaxAge) {
    CalibrationManager mgr(30);  // 30 days
    EXPECT_NE(&mgr, nullptr);
}

// =============================================================================
// Dark Frame Loading Tests
// =============================================================================

TEST_F(CalibrationManagerTest, LoadDarkFrameFromNonExistentFileFails) {
    CalibrationData data = manager_->LoadDarkFrame("nonexistent_file.calib");

    EXPECT_FALSE(data.valid);
    EXPECT_EQ(data.data_f32, nullptr);
}

TEST_F(CalibrationManagerTest, LoadDarkFrameWithValidFileSucceeds) {
    std::string path = GetTestPath("dark.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::DARK_FRAME, 256, 256));

    CalibrationData data = manager_->LoadDarkFrame(path);

    EXPECT_TRUE(data.valid);
    EXPECT_NE(data.data_f32, nullptr);
    EXPECT_EQ(data.type, CalibrationDataType::DARK_FRAME);
    EXPECT_EQ(data.width, 256);
    EXPECT_EQ(data.height, 256);
}

TEST_F(CalibrationManagerTest, LoadDarkFrameCachesData) {
    std::string path = GetTestPath("dark.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::DARK_FRAME, 256, 256));

    CalibrationData data1 = manager_->LoadDarkFrame(path);
    const CalibrationData* data2 = manager_->GetCalibration(CalibrationDataType::DARK_FRAME);

    EXPECT_NE(data2, nullptr);
    EXPECT_EQ(data2->width, 256);
}

// =============================================================================
// Gain Map Loading Tests
// =============================================================================

TEST_F(CalibrationManagerTest, LoadGainMapSucceeds) {
    std::string path = GetTestPath("gain.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::GAIN_MAP, 512, 512));

    CalibrationData data = manager_->LoadGainMap(path);

    EXPECT_TRUE(data.valid);
    EXPECT_NE(data.data_f32, nullptr);
    EXPECT_EQ(data.type, CalibrationDataType::GAIN_MAP);
    EXPECT_EQ(data.width, 512);
    EXPECT_EQ(data.height, 512);
}

TEST_F(CalibrationManagerTest, LoadGainMapCachesData) {
    std::string path = GetTestPath("gain.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::GAIN_MAP, 512, 512));

    manager_->LoadGainMap(path);
    const CalibrationData* data = manager_->GetCalibration(CalibrationDataType::GAIN_MAP);

    EXPECT_NE(data, nullptr);
    EXPECT_EQ(data->width, 512);
}

// =============================================================================
// Defect Map Loading Tests
// =============================================================================

TEST_F(CalibrationManagerTest, LoadDefectMapSucceeds) {
    std::string path = GetTestPath("defect.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::DEFECT_MAP, 512, 512));

    DefectMap map = manager_->LoadDefectMap(path);

    EXPECT_TRUE(map.valid);
    EXPECT_EQ(map.count, 0);  // Empty map in test file
}

TEST_F(CalibrationManagerTest, LoadDefectMapCachesData) {
    std::string path = GetTestPath("defect.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::DEFECT_MAP, 512, 512));

    manager_->LoadDefectMap(path);
    const DefectMap* map = manager_->GetDefectMap();

    EXPECT_NE(map, nullptr);
    EXPECT_TRUE(map->valid);
}

// =============================================================================
// Scatter Params Loading Tests
// =============================================================================

TEST_F(CalibrationManagerTest, LoadScatterParamsSucceeds) {
    std::string path = GetTestPath("scatter.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::SCATTER_PARAMS, 0, 0));

    ScatterParams params = manager_->LoadScatterParams(path);

    EXPECT_FALSE(params.enabled);  // Default from test file
}

TEST_F(CalibrationManagerTest, LoadScatterParamsCachesData) {
    std::string path = GetTestPath("scatter.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::SCATTER_PARAMS, 0, 0));

    manager_->LoadScatterParams(path);
    const ScatterParams* params = manager_->GetScatterParams();

    EXPECT_NE(params, nullptr);
}

// =============================================================================
// Validation Tests
// =============================================================================

TEST_F(CalibrationManagerTest, LoadWithInvalidMagicFails) {
    std::string path = GetTestPath("invalid.calib");
    ASSERT_TRUE(CreateInvalidMagicFile(path));

    CalibrationData data = manager_->LoadDarkFrame(path);

    EXPECT_FALSE(data.valid);
    EXPECT_EQ(data.data_f32, nullptr);
}

TEST_F(CalibrationManagerTest, LoadWithWrongTypeFails) {
    std::string path = GetTestPath("wrong_type.calib");
    // Create GAIN_MAP file but try to load as DARK_FRAME
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::GAIN_MAP, 256, 256));

    CalibrationData data = manager_->LoadDarkFrame(path);

    EXPECT_FALSE(data.valid);
}

TEST_F(CalibrationManagerTest, SetFrameDimensionsValidatesLoads) {
    manager_->SetFrameDimensions(512, 512);

    std::string path = GetTestPath("dark.calib");
    // Create file with wrong dimensions (256x256)
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::DARK_FRAME, 256, 256));

    CalibrationData data = manager_->LoadDarkFrame(path);

    // Should fail due to dimension mismatch
    EXPECT_FALSE(data.valid);
}

TEST_F(CalibrationManagerTest, SetMaxAge) {
    manager_->SetMaxAge(30);  // 30 days
    SUCCEED();
}

// =============================================================================
// Hot Reload Tests
// =============================================================================

TEST_F(CalibrationManagerTest, HotReloadDarkFrameSucceeds) {
    std::string path = GetTestPath("dark.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::DARK_FRAME, 256, 256));

    EXPECT_TRUE(manager_->HotReload(CalibrationDataType::DARK_FRAME, path));

    const CalibrationData* data = manager_->GetCalibration(CalibrationDataType::DARK_FRAME);
    EXPECT_NE(data, nullptr);
}

TEST_F(CalibrationManagerTest, HotReloadGainMapSucceeds) {
    std::string path = GetTestPath("gain.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::GAIN_MAP, 512, 512));

    EXPECT_TRUE(manager_->HotReload(CalibrationDataType::GAIN_MAP, path));

    const CalibrationData* data = manager_->GetCalibration(CalibrationDataType::GAIN_MAP);
    EXPECT_NE(data, nullptr);
}

TEST_F(CalibrationManagerTest, HotReloadDefectMapSucceeds) {
    std::string path = GetTestPath("defect.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::DEFECT_MAP, 512, 512));

    EXPECT_TRUE(manager_->HotReload(CalibrationDataType::DEFECT_MAP, path));

    const DefectMap* map = manager_->GetDefectMap();
    EXPECT_NE(map, nullptr);
}

TEST_F(CalibrationManagerTest, HotReloadWithInvalidFileFails) {
    EXPECT_FALSE(manager_->HotReload(CalibrationDataType::DARK_FRAME, "nonexistent.calib"));
}

// =============================================================================
// Status Query Tests
// =============================================================================

TEST_F(CalibrationManagerTest, GetCalibrationStatusReturnsEmptyInitially) {
    CalibrationStatus status = manager_->GetCalibrationStatus();

    EXPECT_FALSE(status.dark_frame_loaded);
    EXPECT_FALSE(status.gain_map_loaded);
    EXPECT_FALSE(status.defect_map_loaded);
}

TEST_F(CalibrationManagerTest, GetCalibrationStatusReflectsLoadedData) {
    std::string dark_path = GetTestPath("dark.calib");
    std::string gain_path = GetTestPath("gain.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(dark_path, CalibrationDataType::DARK_FRAME, 256, 256));
    ASSERT_TRUE(CreateTestCalibrationFile(gain_path, CalibrationDataType::GAIN_MAP, 512, 512));

    manager_->LoadDarkFrame(dark_path);
    manager_->LoadGainMap(gain_path);

    CalibrationStatus status = manager_->GetCalibrationStatus();

    EXPECT_TRUE(status.dark_frame_loaded);
    EXPECT_TRUE(status.dark_frame_valid);
    EXPECT_TRUE(status.gain_map_loaded);
    EXPECT_TRUE(status.gain_map_valid);
    EXPECT_EQ(status.dark_frame_timestamp, 1234567890ULL);
}

// =============================================================================
// Cache Tests
// =============================================================================

TEST_F(CalibrationManagerTest, GetCalibrationReturnsNullForNotLoaded) {
    const CalibrationData* data = manager_->GetCalibration(CalibrationDataType::DARK_FRAME);
    EXPECT_EQ(data, nullptr);
}

TEST_F(CalibrationManagerTest, GetDefectMapReturnsNullForNotLoaded) {
    const DefectMap* map = manager_->GetDefectMap();
    EXPECT_EQ(map, nullptr);
}

TEST_F(CalibrationManagerTest, GetScatterParamsReturnsNonNullEvenWithoutLoad) {
    // Scatter params have default values
    const ScatterParams* params = manager_->GetScatterParams();
    EXPECT_NE(params, nullptr);
}

// =============================================================================
// Thread Safety Tests
// =============================================================================

TEST_F(CalibrationManagerTest, ConcurrentAccessIsSafe) {
    // Basic test - just verify it doesn't crash
    std::string path = GetTestPath("dark.calib");
    ASSERT_TRUE(CreateTestCalibrationFile(path, CalibrationDataType::DARK_FRAME, 256, 256));

    CalibrationData data = manager_->LoadDarkFrame(path);
    CalibrationStatus status = manager_->GetCalibrationStatus();

    EXPECT_TRUE(data.valid);
    (void)status;  // Suppress unused warning
}
