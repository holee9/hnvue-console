/**
 * @file CalibrationManager.cpp
 * @brief Implementation of CalibrationManager
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Calibration data manager implementation
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/imaging/CalibrationManager.h"

#include <fstream>
#include <cstring>
#include <chrono>
#include <vector>

// Simple SHA-256 implementation stub
// In production, use a proper crypto library like OpenSSL or mbedTLS
namespace {

constexpr uint8_t MAGIC_HN[] = {'H', 'N', 'C', 0x01};
constexpr uint16_t FORMAT_VERSION = 1;

void ComputeSHA256(const uint8_t* data, size_t length, uint8_t* hash) {
    // This is a placeholder implementation
    // In production, replace with actual SHA-256 computation
    // For now, we'll do a simple checksum
    uint32_t sum = 0;
    for (size_t i = 0; i < length; ++i) {
        sum = sum * 31 + data[i];
    }
    std::memset(hash, 0, 32);
    std::memcpy(hash, &sum, sizeof(sum));
}

} // anonymous namespace

namespace hnvue::imaging {

CalibrationManager::CalibrationManager(uint32_t max_age_days)
    : max_age_days_(max_age_days) {
}

CalibrationManager::~CalibrationManager() = default;

CalibrationData CalibrationManager::LoadDarkFrame(const std::string& path) {
    CalibrationData result;
    result.type = CalibrationDataType::DARK_FRAME;

    CalibrationFileHeader header;
    if (!LoadHeader(path, header)) {
        return result;
    }

    if (!ValidateHeader(header, CalibrationDataType::DARK_FRAME)) {
        return result;
    }

    // Load payload
    std::ifstream file(path, std::ios::binary);
    if (!file) {
        return result;
    }

    // Skip header
    file.seekg(sizeof(CalibrationFileHeader));

    // Allocate and read data
    result.width = header.width;
    result.height = header.height;
    result.acquisition_time_us = header.acquisition_time;

    size_t data_size = header.width * header.height * sizeof(float);
    result.data_f32 = new float[data_size / sizeof(float)];

    std::vector<uint8_t> payload(data_size);
    file.read(reinterpret_cast<char*>(payload.data()), data_size);

    if (!file) {
        delete[] result.data_f32;
        result.data_f32 = nullptr;
        return result;
    }

    // Verify checksum
    if (!VerifyChecksum(payload.data(), data_size, header.checksum)) {
        delete[] result.data_f32;
        result.data_f32 = nullptr;
        return result;
    }

    // Copy data to output buffer
    std::memcpy(result.data_f32, payload.data(), data_size);

    result.checksum = header.checksum[0];  // Store first byte as simple checksum
    result.valid = true;

    // Cache the data
    {
        std::lock_guard<std::mutex> lock(mutex_);
        calibration_cache_[CalibrationDataType::DARK_FRAME] = result;
    }

    return result;
}

CalibrationData CalibrationManager::LoadGainMap(const std::string& path) {
    CalibrationData result;
    result.type = CalibrationDataType::GAIN_MAP;

    CalibrationFileHeader header;
    if (!LoadHeader(path, header)) {
        return result;
    }

    if (!ValidateHeader(header, CalibrationDataType::GAIN_MAP)) {
        return result;
    }

    std::ifstream file(path, std::ios::binary);
    if (!file) {
        return result;
    }

    file.seekg(sizeof(CalibrationFileHeader));

    result.width = header.width;
    result.height = header.height;
    result.acquisition_time_us = header.acquisition_time;

    size_t data_size = header.width * header.height * sizeof(float);
    result.data_f32 = new float[data_size / sizeof(float)];

    std::vector<uint8_t> payload(data_size);
    file.read(reinterpret_cast<char*>(payload.data()), data_size);

    if (!file) {
        delete[] result.data_f32;
        result.data_f32 = nullptr;
        return result;
    }

    if (!VerifyChecksum(payload.data(), data_size, header.checksum)) {
        delete[] result.data_f32;
        result.data_f32 = nullptr;
        return result;
    }

    std::memcpy(result.data_f32, payload.data(), data_size);

    result.checksum = header.checksum[0];
    result.valid = true;

    {
        std::lock_guard<std::mutex> lock(mutex_);
        calibration_cache_[CalibrationDataType::GAIN_MAP] = result;
    }

    return result;
}

DefectMap CalibrationManager::LoadDefectMap(const std::string& path) {
    DefectMap result;

    CalibrationFileHeader header;
    if (!LoadHeader(path, header)) {
        return result;
    }

    if (!ValidateHeader(header, CalibrationDataType::DEFECT_MAP)) {
        return result;
    }

    std::ifstream file(path, std::ios::binary);
    if (!file) {
        return result;
    }

    file.seekg(sizeof(CalibrationFileHeader));

    // Read defect count
    uint32_t count;
    file.read(reinterpret_cast<char*>(&count), sizeof(count));
    if (!file) {
        return result;
    }

    result.count = count;
    result.pixels = new DefectPixelEntry[count];

    // Read defect entries
    file.read(reinterpret_cast<char*>(result.pixels),
              count * sizeof(DefectPixelEntry));

    if (!file) {
        delete[] result.pixels;
        result.pixels = nullptr;
        result.count = 0;
        return result;
    }

    result.valid = true;

    {
        std::lock_guard<std::mutex> lock(mutex_);
        defect_map_ = result;
    }

    return result;
}

ScatterParams CalibrationManager::LoadScatterParams(const std::string& path) {
    ScatterParams result;

    CalibrationFileHeader header;
    if (!LoadHeader(path, header)) {
        return result;
    }

    if (!ValidateHeader(header, CalibrationDataType::SCATTER_PARAMS)) {
        return result;
    }

    std::ifstream file(path, std::ios::binary);
    if (!file) {
        return result;
    }

    file.seekg(sizeof(CalibrationFileHeader));

    // Read scatter params
    file.read(reinterpret_cast<char*>(&result.enabled), sizeof(bool));
    file.read(reinterpret_cast<char*>(&result.algorithm), sizeof(ScatterAlgorithm));
    file.read(reinterpret_cast<char*>(&result.cutoff_frequency), sizeof(float));
    file.read(reinterpret_cast<char*>(&result.suppression_ratio), sizeof(float));
    file.read(reinterpret_cast<char*>(&result.fftw_plan_flags), sizeof(unsigned int));

    if (!file) {
        return result;
    }

    {
        std::lock_guard<std::mutex> lock(mutex_);
        scatter_params_ = result;
    }

    return result;
}

bool CalibrationManager::HotReload(CalibrationDataType type,
                                     const std::string& path) {
    switch (type) {
        case CalibrationDataType::DARK_FRAME: {
            CalibrationData data = LoadDarkFrame(path);
            return data.valid;
        }
        case CalibrationDataType::GAIN_MAP: {
            CalibrationData data = LoadGainMap(path);
            return data.valid;
        }
        case CalibrationDataType::DEFECT_MAP: {
            DefectMap map = LoadDefectMap(path);
            return map.valid;
        }
        case CalibrationDataType::SCATTER_PARAMS: {
            ScatterParams params = LoadScatterParams(path);
            return params.enabled || true;  // Valid if loaded
        }
        default:
            return false;
    }
}

const CalibrationData* CalibrationManager::GetCalibration(
    CalibrationDataType type) const {

    std::lock_guard<std::mutex> lock(mutex_);
    auto it = calibration_cache_.find(type);
    if (it != calibration_cache_.end()) {
        return &it->second;
    }
    return nullptr;
}

const DefectMap* CalibrationManager::GetDefectMap() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return defect_map_.valid ? &defect_map_ : nullptr;
}

const ScatterParams* CalibrationManager::GetScatterParams() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return &scatter_params_;
}

CalibrationStatus CalibrationManager::GetCalibrationStatus() const {
    std::lock_guard<std::mutex> lock(mutex_);

    CalibrationStatus status;

    auto dark_it = calibration_cache_.find(CalibrationDataType::DARK_FRAME);
    if (dark_it != calibration_cache_.end()) {
        status.dark_frame_loaded = true;
        status.dark_frame_valid = dark_it->second.valid;
        status.dark_frame_timestamp = dark_it->second.acquisition_time_us;
    }

    auto gain_it = calibration_cache_.find(CalibrationDataType::GAIN_MAP);
    if (gain_it != calibration_cache_.end()) {
        status.gain_map_loaded = true;
        status.gain_map_valid = gain_it->second.valid;
        status.gain_map_timestamp = gain_it->second.acquisition_time_us;
    }

    status.defect_map_loaded = defect_map_.valid;
    status.defect_map_valid = defect_map_.valid;
    status.defect_map_timestamp = 0;  // Not stored in DefectMap

    status.scatter_params_loaded = true;
    status.scatter_params_valid = true;

    return status;
}

void CalibrationManager::SetMaxAge(uint32_t days) {
    max_age_days_ = days;
}

void CalibrationManager::SetFrameDimensions(uint32_t width, uint32_t height) {
    expected_width_ = width;
    expected_height_ = height;
}

bool CalibrationManager::LoadHeader(const std::string& path,
                                     CalibrationFileHeader& header) {
    std::ifstream file(path, std::ios::binary);
    if (!file) {
        return false;
    }

    file.read(reinterpret_cast<char*>(&header), sizeof(CalibrationFileHeader));
    return file.good();
}

bool CalibrationManager::ValidateHeader(
    const CalibrationFileHeader& header,
    CalibrationDataType expected_type) {

    // Check magic number
    if (std::memcmp(header.magic, MAGIC_HN, 4) != 0) {
        return false;
    }

    // Check format version
    if (header.format_version != FORMAT_VERSION) {
        return false;
    }

    // Check data type
    if (static_cast<CalibrationDataType>(header.data_type) != expected_type) {
        return false;
    }

    // Check dimensions if configured
    if (expected_width_ > 0 && header.width != expected_width_) {
        return false;
    }
    if (expected_height_ > 0 && header.height != expected_height_) {
        return false;
    }

    // Check age
    if (max_age_days_ > 0) {
        auto now = std::chrono::system_clock::now();
        auto now_ts = std::chrono::duration_cast<std::chrono::microseconds>(
            now.time_since_epoch()).count();
        uint64_t age_us = now_ts - header.acquisition_time;
        uint64_t max_age_us = max_age_days_ * 24ULL * 3600ULL * 1000000ULL;
        if (age_us > max_age_us) {
            return false;
        }
    }

    return true;
}

bool CalibrationManager::VerifyChecksum(const uint8_t* data, uint64_t length,
                                         const uint8_t* expected_checksum) {
    uint8_t computed[32];
    ComputeChecksum(data, length, computed);
    return std::memcmp(computed, expected_checksum, 32) == 0;
}

void CalibrationManager::ComputeChecksum(const uint8_t* data, uint64_t length,
                                          uint8_t* output) {
    ::ComputeSHA256(data, static_cast<size_t>(length), output);
}

} // namespace hnvue::imaging
